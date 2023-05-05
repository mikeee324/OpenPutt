# OpenPutt

This is a very early test version of OpenPutt.  
Most of the systems are working, the main issues right now are physics based. If anybody has ideas on how to solve these please let me know!

# Dependencies (Install these first!!!)
- UdonSharp 1.1.7 (Use VCC!)
- CyanLasers Player Object Pool - https://cyanlaser.github.io/CyanPlayerObjectPool/
- Varneons Array Extensions - https://github.com/Varneon/VUdon-ArrayExtensions/releases/tag/0.3.0

Also wanted to mention the Udon profiler from here - https://gist.github.com/MerlinVR/2da80b29361588ddb556fd8d3f3f47b5  
This is included in the project for ease of use on my part  

# Installation
> Add OpenPutt to VCC by visiting [this page](https://mikeee324.github.io/OpenPutt/)
> Click 'Add to VCC' and follow the instructions to add the repository
> You can then add OpenPutt to your project inside VCC

# Scene Setup
1. Drag in the OpenPutt prefab from Packages/OpenPutt/Prefabs

# Adding Courses
1. Drag in a new OpenPuttCourse prefab into the Holes object of the OpenPutt prefab
2. Move the start pad to where you want players to start the course
3. Move the Hole box collider to the hole in your course
4. Assign any floor meshes for that course to the CourseManager script at the root of the Course prefab (Allows OpenPutt to know if the ball is on the correct course or not)
6. Add a par/max score to the same CourseManager script
5. Add the Course prefab to the list of courses in the main OpenPutt script

```
Note - Courses should have at least the floor as it's own mesh.
When setting up colliders make sure to use the physics materials included with the prefab.
Floor meshes for your courses need to use the FloorPhysics material so OpenPutt knows if the ball is on top of the course floor or not.
Anything that the ball needs to bounce off (walls, obstacles etc) please use the WallPhysics material so OpenPutt can handle the bouncing properly (default unity physics doesn't do this so well)
```

# Adding scoreboards to the world
1. Drag in a new ScoreboardPositioner prefab into the scene and position it to where you would like it to be
2. You can adjust rules on when a scoreboard will be shown at that position in the inspector window
3. Assign the reference to the ScoreboardManager and add the ScoreboardPositioner to the list of 'Scoreboard Positions' on the ScoreboardManager itself

The scoreboards themselves are in a 'pool' and will be moved around to the closest avaiable position. There are 3 scoreboards in the pool by default but you can add/remove them as you like. Just remember to set up the references between the scoreboards and ScoreboardManager correctly!

# Notes
There will be more docs on everything at some point (like how to set up the meshes for courses etc), this is just a first go at releasing something so people can have a play with it

# Worlds
A list of worlds where you can see this in action (If you make one let me know!)
- ![Junkyard Golf - mikeee324](https://vrchat.com/home/launch?worldId=wrld_d62918a1-9172-40cd-93a9-5d8546dad6cf)

# Patreon Thing
If you feel the need to support me you can do so here: https://patreon.com/mikeee324  
Thanks
