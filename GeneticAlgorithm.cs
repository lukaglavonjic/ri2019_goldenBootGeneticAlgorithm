using System;
using System.Collections.Generic;
using System.IO;
using Nordeus.GoldenBoot;
using UnityEngine;
using Random = UnityEngine.Random;

public class GeneticAlgorithm : MonoBehaviour
{
	// These are the limits for target. Goal is 7.32m wide so I set right most position to be 3.4m right of the center, and top is just below the top bar
	private const float RightMost = 3.4f;
	private const float TopMost = 2.15f;

	[SerializeField]
	private SinglePlayerMatchController matchContoller;

	[SerializeField]
	private BallController ballController;

	[SerializeField]
	private GoalkeeperController goalkeeperController;

	private static int NumberOfBots = 24;
	private static int NumberOfGenerations = 50;
	private static float MutationRatio = 0.5f;

	// ReSharper disable once InconsistentNaming
	private class DNA
	{
		public float DeltaPosX;
		public float DeltaPosY;
		public float DeltaTime;

		public DNA(float deltaPosX, float deltaPosY, float deltaTime)
		{
			DeltaPosX = deltaPosX;
			DeltaPosY = deltaPosY;
			DeltaTime = deltaTime;
		}

		public override string ToString()
		{
			return DeltaPosX + " " + DeltaPosY + " " + DeltaTime;
		}
	}

	private List<DNA> bots = new List<DNA>();

	private const float FixedTimeStep = 0.04f;

	private float deltaX;
	private float deltaY;
	private float deltaT;

	private float targetPointXNormalized;
	private float targetPointYNormalized;

	private Vector3 savedBallPosition;
	private bool ballHitPostThisScenario;

	public void Start()
	{
		// First we parse config file
		string path = Application.streamingAssetsPath + "/config.txt";

		// Config file should have numbers in this order:
		// NumberOfBots
		// NumberOfGenerations
		// MutationRatio
		// targetPointXNormalized
		// targetPointYNormalized

		StreamReader reader = new StreamReader(path);

		try
		{
			string nextLine = reader.ReadLine();
			NumberOfBots = int.Parse(nextLine);
			nextLine = reader.ReadLine();
			NumberOfGenerations = int.Parse(nextLine);
			nextLine = reader.ReadLine();
			MutationRatio = float.Parse(nextLine);

			nextLine = reader.ReadLine();
			targetPointXNormalized = float.Parse(nextLine);
			nextLine = reader.ReadLine();
			targetPointYNormalized = float.Parse(nextLine);
		}
		catch (Exception e)
		{
			Debug.LogError("Config file is not in the correct format! " + e.StackTrace);
		}

		reader.Close();
	}

	public void Update()
	{
		// We can press R until we get a scenario we are satisfied with
		if (Input.GetKeyDown(KeyCode.R))
		{
			matchContoller.StartNextScenario();
		}

		// When T is pressed we calculate kick parameters which would get the ball to the target
		if (Input.GetKeyDown(KeyCode.T))
		{
			// Start ball position is saved so after simulation we can restart it
			savedBallPosition = ballController.transform.position;

			// Automatic physics are disabled so this script can take control of it
			Physics.autoSimulation = false;

			GenerateRandomBots();

			// Goalkeeper is disabled before simulation because he would interfere with results
			goalkeeperController.gameObject.SetActive(false);
			for (int i = 0; i < NumberOfGenerations; i++)
			{
				bool success = Simulate();
				if (success) break;
			}

			// After simulation we enable goalkeeper, reset ball position and enable physics
			goalkeeperController.gameObject.SetActive(true);
			matchContoller.StartNextScenarioWithGivenBallPosition(savedBallPosition);
			Physics.autoSimulation = true;
		}

		// When Y is clicked, we will just shoot the ball with calculated parameters
		if (Input.GetKeyDown(KeyCode.Y))
		{
			matchContoller.KickBall(deltaX, deltaY, deltaT);
		}
	}

	private void GenerateRandomBots()
	{
		for (int i = 0; i < NumberOfBots; i++)
		{
			// These limits are experimentally calculated
			float randomXForce = Random.Range(-0.2f, 0.2f);
			float randomYForce = Random.Range(0f, 0.4f);
			float randomTime = Random.Range(0.05f, 0.2f);
			bots.Add(new DNA(randomXForce, randomYForce, randomTime));
		}
	}

	private bool Simulate()
	{
		List<float> scores = new List<float>();

		ballController.OnPostHit += OnPostHit;
		// Calculate scores
		for (int i = 0; i < NumberOfBots; i++)
		{
			scores.Add(SimulateShot(bots[i]));
		}
		ballController.OnPostHit -= OnPostHit;

		// Sort by score
		for (int i = 1; i < scores.Count; i++)
		{
			for (int j = 0; j < i; j++)
			{
				if (scores[i] < scores[j])
				{
					DNA tempBot = bots[i];
					bots[i] = bots[j];
					bots[j] = tempBot;

					float tempScore = scores[i];
					scores[i] = scores[j];
					scores[j] = tempScore;
				}
			}
		}

		// We update kick parameters to the currently best bot
		deltaX = bots[0].DeltaPosX;
		deltaY = bots[0].DeltaPosY;
		deltaT = bots[0].DeltaTime;

		// If best score (distance to the target) is below 0.05 meters, we can say we achieved the result
		if (scores[0] < 0.05f)
		{
			return true;
		}
		else
		{
			bots = ExchangeDna(bots);
			bots = Mutate(bots);
			return false;
		}
	}

	// Returns distance to the target
	private float SimulateShot(DNA dna)
	{
		// Before shot simulation we reset ball position
		matchContoller.StartNextScenarioWithGivenBallPosition(savedBallPosition);

		matchContoller.KickBall(dna.DeltaPosX, dna.DeltaPosY, dna.DeltaTime);
		ballHitPostThisScenario = false;

		for (int i = 0; i < 75; i++)
		{
			// This just simulates physics for some time interval
			Physics.Simulate(FixedTimeStep);

			// TickUpdate is called on ball so it moves its shadow
			ballController.TickUpdate(SingleplayerMatchModel.Instance.ScenarioOutcome);

			// If scenario outcome is decided (goal or miss) or if we hit the post, we calculate distance to the target
			if (SingleplayerMatchModel.Instance.ScenarioOutcome != ScenarioOutcomes.NotDecided || ballHitPostThisScenario)
			{
				var distanceToTarget = Vector3.Distance(ballController.transform.position, new Vector3(targetPointXNormalized * RightMost, targetPointYNormalized * TopMost, 0f));
				if (SingleplayerMatchModel.Instance.ScenarioOutcome == ScenarioOutcomes.Goal)
				{
					return distanceToTarget;
				}
				else if (ballHitPostThisScenario)
				{
					// If we hit the post, we don't want to punish the score as much as for the miss, since it was closer
					return distanceToTarget * 5f;
				}
				else
				{
					return distanceToTarget * 20f;
				}
			}
		}

		return 1000f;
	}

	private void OnPostHit()
	{
		ballHitPostThisScenario = true;
	}

	private static List<DNA> ExchangeDna(List<DNA> tempBots)
	{
		// We just want to merge one third of all bots
		List<DNA> botsToUse = tempBots.GetRange(0, tempBots.Count / 3);
		List<DNA> newBots = new List<DNA>();

		for (int i = 0; i < botsToUse.Count; i += 2)
		{
			DNA bot1 = new DNA(botsToUse[i].DeltaPosX, botsToUse[i].DeltaPosY, botsToUse[i].DeltaTime);
			DNA bot2 = new DNA(botsToUse[i + 1].DeltaPosX, botsToUse[i + 1].DeltaPosY, botsToUse[i + 1].DeltaTime);

			// There are now strict rules why I chose to generate new bots in this way. It helped a lot when I favoritized the first bot since he's always better than the second one
			newBots.Add(new DNA(bot1.DeltaPosX, bot2.DeltaPosY, Random.value < 0.5f ? bot1.DeltaTime : bot2.DeltaTime));
			newBots.Add(new DNA(bot2.DeltaPosX, bot1.DeltaPosY, Random.value < 0.5f ? bot1.DeltaTime : bot2.DeltaTime));
			newBots.Add(new DNA(-bot1.DeltaPosX, bot1.DeltaPosY, Random.value < 0.5f ? bot1.DeltaTime : bot2.DeltaTime));
			newBots.Add(new DNA(-bot1.DeltaPosX, bot2.DeltaPosY, Random.value < 0.5f ? bot1.DeltaTime : bot2.DeltaTime));
			newBots.Add(new DNA((bot1.DeltaPosX + bot2.DeltaPosX) / 2f, (bot1.DeltaPosY + bot2.DeltaPosY) / 2f, (bot1.DeltaTime + bot2.DeltaTime) / 2f));
			newBots.Add(new DNA(bot1.DeltaPosX, bot1.DeltaPosY, bot1.DeltaTime));
		}

		return newBots;
	}

	private static List<DNA> Mutate(List<DNA> tempBots)
	{
		// For mutation I just decided that for random bots I want to either increase or decrease one of their parameters
		for (int i = 0; i < tempBots.Count * MutationRatio; i++)
		{
			int randomBotIndex = Random.Range(0, tempBots.Count);
			int randomGenToChange = Random.Range(0, 3);
			int increaseOrDecrease = Random.value < 0.5f ? 1 : -1;

			switch (randomGenToChange) {
				case 0:
					tempBots[randomBotIndex].DeltaPosX += increaseOrDecrease * 0.02f;
					break;
				case 1:
					tempBots[randomBotIndex].DeltaPosY += increaseOrDecrease * 0.02f;
					break;
				case 2:
					tempBots[randomBotIndex].DeltaTime += increaseOrDecrease * 0.003f;
					break;
			}
		}

		return tempBots;
	}
}