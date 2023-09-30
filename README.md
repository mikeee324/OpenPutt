![OpenPutt Banner](https://github.com/mikeee324/OpenPutt/blob/dev/Website/banner.png?raw=true)  
  
OpenPutt is a golf game prefab for VRChat. This is still in development so things might act weird or be broken.  
If you have suggestions or can help me fix things please let me know!  
  
The aim for this prefab is to make it easier to make golf game worlds so more people make them!  
Lets play some golf!  

# Quick Notes
- Recommended Max Players - **32**
    - The prefab technically supports up to 82 players, but you shouldn't try it with that many players
        - Large amounts of players cause Very Low FPS and/or Network Timeouts (for some people anyway not all)
        - This issue might get fixed later, but it's not a priority of mine as it works for the kind of player counts you see in normal lobbies
   
# Dependencies (Install these first!!!)
- UdonSharp 1.1.8+ (Use VCC!)
- CyanLasers Player Object Pool 1.1.2+ - https://cyanlaser.github.io/CyanPlayerObjectPool/
- Varneons Array Extensions 0.3.0 - [Add To VCC]([vcc://vpm/addRepo?url=https://vpm.varneon.com/repos/vudon.json) [Github](https://github.com/Varneon/VUdon-ArrayExtensions/releases/tag/0.3.0)

Also wanted to mention the Udon profiler from here - https://gist.github.com/MerlinVR/2da80b29361588ddb556fd8d3f3f47b5  
This is included in the project for ease of use on my part  

# Updates from non VCC builds
1. Close Unity
2. Delete the Assets/OpenPutt folder and .meta file
3. Install VCC build of OpenPutt as per below
4. Open your project and hopefully it's not brokey (You may have to drag in a new Openputt prefab and use that if it's broken)

# Installation
1. Add OpenPutt to VCC by visiting [this page](https://mikeee324.github.io/OpenPutt/)
2. Click 'Add to VCC' and follow the instructions to add the repository
3. You can then add OpenPutt to your project inside VCC

# Scene Setup
1. Drag in the OpenPutt prefab from Packages/OpenPutt/Prefabs
   1. There is also a demo scene available in Packages/OpenPutt/Runtime/OpenPuttDemoScene
   2. Make sure to make a copy of this in your Assets folder if you want to make changes!
# Adding Courses
1. Drag in a new OpenPuttCourse prefab into the Holes object of the OpenPutt prefab
2. Move the start pad to where you want players to start the course
3. Move the Hole box collider to the hole in your course
4. Assign any floor meshes for that course to the CourseManager script at the root of the Course prefab (Allows OpenPutt to know if the ball is on the correct course or not)
5. Add a par/max score to the same CourseManager script
6. Add the Course prefab to the list of courses in the main OpenPutt script

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
- [Junkyard Golf - mikeee324](https://vrchat.com/home/world/wrld_d62918a1-9172-40cd-93a9-5d8546dad6cf)

# Patreon Thing
If you feel the need to support me you can do so here: https://patreon.com/mikeee324  
Thanks
