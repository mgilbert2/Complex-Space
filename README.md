# Complex Space

Open this project in Unity 6.1 (6000.1.14f1), then open `Assets/Beach/Scenes/Chopped Beach task.unity` and press Play.

The scene is an edited reconstruction of the beach treasure puzzle from CodeParade's *Hyperbolica*, using art recovered from the game. Its hyperbolic geometry and movement use CodeParade's [HyperEngine](https://github.com/HackerPoet/HyperEngine) (MIT).

The gopher scene is `Assets/Gopher/Scenes/Chopped De Sitter farms.unity`, with four gophers and recovered Farm scenery. Six ground cells form a spherical surface. Scenery assignments are reconstructed because the original generation and task code were unavailable. The Scene view spreads cells out for editing; press Play (or Tools > Hyperbolica Gophers > Preview Spherical Farm) to see them joined in spherical space. Extra farm prefabs are in `Assets/Gopher/Recovered/GameObject`.

Stun all four gophers with plasma as they emerge. WASD moves, the mouse aims, left click fires, R restarts, and Esc pauses/releases the cursor. Completion shows your time and shot count. These mechanics are reconstructed from the recovered states and assets. Run Tools > Hyperbolica Gophers > Check Playable Level for gameplay checks.

The two exploration scenes are in `Assets/Exploration/Scenes`: **Elliptic Exploration** uses a finite, closed spherical space; **Hyperbolic Exploration** streams an open negatively curved surface. Both have grassy orientation grids, six colored stone columns, the recovered Hyperbolica trampoline, and a ball that rolls along surface geodesics, slows with friction, and bounces off columns. Player and landmark sizes match between scenes. “Elliptic” currently means spherical positive curvature; antipodal points are not identified.

Use **Tools > Complex Space** to open either scene, then press Play. WASD moves, mouse looks, Space jumps, left click kicks a nearby ball, B recalls it, R resets, and Esc pauses or switches spaces. Step onto the trampoline to bounce. **Check Exploration Scenes** runs the Unity gameplay checks.
