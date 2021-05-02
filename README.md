# fish_AI
I implemented steering behaviours in 3D to simulate fish movement and schooling.

## Background
My thesis was about utilizing the <i>data-oriented</i> programming paradigm in video games. After graduating I decided to revisit the paradigm and try it as a form of optimization. Steering behaviours are usually quite expensive when dealing with a great amount of agents, and so I figured it would be a good benchmark.

I use Unity DOTS (<i>Data-Oriented Technology Stack</i>) and the central steering behaviours that I implement are <i>wandering</i> and <i>flocking</i> (cohesion, separation and alignment).

### Wandering
Wander-behaviours are usually implemented when agents need to move <i>randomly</i> and explore their surroundings. Usually these characters are just waiting for something to happen.

### Flocking
The three sub-behaviours of flocking handle different parts and therefore update independently. <i>Cohesion</i> attracts fish to the average position of other fish within their field of view. <i>Separation</i> separates each fish from others if they're too close to eachother. <i>Alignment</i> aligns each fish's direction to the average direction of other fish within their field of vision. 

Fish can normally turn both vertically and horizontally, which means that these behaviours were a little bit more complex to implement. 

## The result
https://user-images.githubusercontent.com/45757491/116811973-7f81a880-ab4c-11eb-81ff-4f5c1232ea0e.mp4

https://user-images.githubusercontent.com/45757491/116811974-83152f80-ab4c-11eb-87a9-b00849a25a0a.mp4
