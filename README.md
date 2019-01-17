# Football shooting genetic algorithm

Contents:
- Source code for the genetic algorithm
- Windows build of the game

Inside the windows build at \GoldenBootWindows\Golden Boot_Data\StreamingAssets\config.txt there is a configuration file which can be used to set these parameters:
1) Amount of bots inside a generation
2) Number of generations
3) Mutation rate [0.0-1.0]
4) Normalized point on x axis where to aim (-1.0 means to aim at far left, 1.0 at far right and 0.0 at center)
5) Normalized point on y axis where to aim (0.0 means to aim at the bottom and 1.0 means to aim just under the crossbar)

Instructions:
- When the game is started 'R' key can be pressed until we are satisfied with the ball position
- After that, 'T' key should be pressed to perform calculations and find the shooting parameters which are closest to hitting the target set in the config file
- Game will freeze for a few seconds while physics simulations are being performed
- When game unfeezes, 'Y' key can be pressed to perform the shot

![alt text](https://i.imgur.com/yxUKwvF.png)
