# Complex Space

A Unity project for exploring curved spaces and playing modified tasks reconstructed from CodeParade's *Hyperbolica*. It contains two task scenes and two exploration scenes, using HyperEngine for curved geometry, rendering, movement, and collisions.

## Open and play

1. Install **Unity 6.1, version 6000.1.14f1**, through Unity Hub.
2. In Hub, choose **Add project from disk** and select this repository's root folder: the folder containing `Assets`, `Packages`, and `ProjectSettings`. Open the project itself instead of importing it into another Unity project. The unrelated local folder `My project (5)` is not the project to open.
3. Let Unity finish importing assets, restoring packages, and compiling. The project uses the built-in render pipeline and Input System **1.14.0**, declared in [Packages/manifest.json](Packages/manifest.json).
4. Open a scene below and press **Play**, then click the **Game** view for keyboard and mouse input. Press **Esc** to release the cursor.

The curved layout appears during Play Mode. Scene view shows source geometry and editor placements; the farm deliberately spreads its charts out for editing.

## Scenes and controls

All scenes use **WASD** to move and the **mouse** to look or aim.

| Scene | Geometry and objective | Scene file |
| --- | --- | --- |
| Chopped Beach task | Hyperbolic treasure puzzle: push the chest into its hole with the correct orientation. | [Beach scene](Assets/Beach/Scenes/Chopped%20Beach%20task.unity) |
| Chopped De Sitter farms | Spherical farm: stun all four gophers with plasma as they emerge. | [Farm scene](Assets/Gopher/Scenes/Chopped%20De%20Sitter%20farms.unity) |
| Elliptic Exploration | Finite, closed spherical space with grass, columns, a trampoline, and a ball. | [Elliptic scene](Assets/Exploration/Scenes/Elliptic%20Exploration.unity) |
| Hyperbolic Exploration | Open negatively curved space with terrain streamed as you travel. | [Hyperbolic scene](Assets/Exploration/Scenes/Hyperbolic%20Exploration.unity) |

**Beach:** walk into the chest to push it; **Q** opens the map, **R** resets, and **Esc** pauses/releases the cursor. Returning the chest's position alone is insufficient: moving it around a loop changes its orientation through parallel transport.

**Farm:** **left click** fires plasma, **R** restarts, and **Esc** pauses/resumes. Hidden and already stunned gophers reject hits. Completion displays your time and shot count. The modified scene removes NPCs and retains the fence, well/portal, and wind tower. Its retained name is “Chopped De Sitter farms”; its implemented geometry is spherical.

**Exploration:** **Space** jumps, **left click** kicks a nearby ball in the direction you face, **B** recalls the ball in front of you, **R** resets the scene, and **Esc** opens the pause menu. Step onto the trampoline to bounce automatically. The pause menu switches between the two exploration spaces.

Use **Tools > Complex Space > Open Elliptic Exploration / Open Hyperbolic Exploration** to select an exploration scene. The farm also has **Tools > Hyperbolica Gophers > Open Gopher Scene / Preview Spherical Farm**. These are independent scenes; a shared intro, level selector, and campaign progression are not implemented.

## How the exploration spaces work

The spherical space has a finite total extent and closes around itself, with no boundary wall. The hyperbolic layout has no designed outer edge: it reuses **109 nearby terrain cells** and shifts the local coordinate frame as the player crosses cells. It renders a finite neighborhood rather than allocating infinite terrain. The edge of a Poincaré coordinate chart represents distance, not a physical wall.

Both scenes have matching player and landmark sizes, six stone columns with distinct colored markers, and a grass grid that makes changes in perspective easier to compare. The original Hyperbolica trampoline mesh and sound are reused; its bounce behavior is reconstructed.

The ball uses a custom surface simulation rather than a Unity Rigidbody. Between impacts it follows a **geodesic**—the straightest path in that geometry—and transports its direction along that path. It stays on the ground, slows with friction, visually rolls, and reflects off columns with reduced speed. Jumping or bouncing does not lift the ball when you kick it.

**Geometry distinction:** “Elliptic Exploration” currently uses spherical positive curvature. It does not identify opposite (antipodal) points, so it is not a strict elliptic/projective-space quotient.

## Code and editing guide

| Location | Responsibility |
| --- | --- |
| [ExploreSpace.cs](Assets/Exploration/ExploreSpace.cs) | Geometry setup, input, trampoline interaction, terrain streaming, landmark restoration, and pause UI. |
| [SurfaceBall.cs](Assets/Exploration/SurfaceBall.cs) | Kicking, geodesic motion, friction, collision response, rolling visuals, and ball recovery. |
| [ExploreAnchorHistory.cs](Assets/Exploration/ExploreAnchorHistory.cs) | Retains bounded chart changes so retracing long routes can restore distant landmarks without coordinate saturation. |
| [Exploration/Editor](Assets/Exploration/Editor) | Scene/material generation and Play Mode checks. |
| [Beach](Assets/Beach) | Chest puzzle, shoreline, scenery, and beach editor tools. |
| [Gopher](Assets/Gopher) | Farm layout, four-gopher task, plasma interactions, recovered art, and editor tools. |
| [HyperEngine](Assets/HyperEngine) | Shared curved-space mathematics, shaders, player controller, and collision system. |

Stop Play Mode before editing scripts or saving scene changes. A `HyperObject` has a curved-space pose in addition to its ordinary Unity Transform, so moving source geometry alone does not always change its curved-space placement. Use Play Mode to review the result.

To tune the exploration scenes:

- Change layout and object sizes in `ExploreSceneBuilder`: its named constants and `CreateColumns`, `CreateTrampoline`, `CreateBall`, and `CreatePlayer` methods describe the generated scene.
- Change bounce strength and contact distances in `ExploreSpace`, or kick speed, rolling deceleration, and impact restitution in `SurfaceBall`.
- Builder changes require regenerating the scenes; runtime changes take effect on the next Play session. The comments explain the engine's coordinate and scale conversions. Keep serialized scene-reference field names intact when refactoring.

The exploration code uses four-space indentation, explicit control-flow blocks, named tuning constants, and comments for the geometry operations. [.editorconfig](.editorconfig) records the formatting rules for these scripts without reformatting the recovered engine.

**Build Exploration Scenes** regenerates both exploration scene files and assets under `Assets/Exploration/Generated`. Save and commit manual changes before rebuilding; use the **Open** commands when you only want to inspect or play an existing scene. Extra recovered farm prefabs are in `Assets/Gopher/Recovered/GameObject`.

## Validation and known limits

Run **Tools > Complex Space > Check Exploration Scenes** after saving your work. It opens both scenes and enters Play Mode. Reports and rendered previews are written to `Logs/ExplorationChecks` (`results.txt`, `errors.txt`, and scene PNGs); `Logs` is ignored by Git.

The recorded exploration run passed rolling, ground constraints, kick distance, pause, column impacts, trampoline launch, spherical geodesic closure, and hyperbolic streaming. Its long-travel check covers 200 cell changes outward, 200 back restoring the trampoline and stationary ball, another 200 outward, and recalling/kicking the ball there. These automated checks supplement visual playtesting; they do not establish exact equivalence to the original game's behavior.

For batch execution, close this project in the editor and launch Unity with `-batchmode -projectPath <project-root> -executeMethod ExploreSceneCheck.Run -logFile <log-path>`. The checker exits Unity with a pass/fail exit code; omit `-quit` so its Play Mode checks can finish. `ExploreSceneCheck.BuildAndRun` regenerates the scenes before checking them.

The farm provides **Tools > Hyperbolica Gophers > Check Playable Level**; the beach checker is `BeachPlayCheck.Run`. Their existence is separate from the recorded exploration pass above.

Current limits:

- Landmark history restores retraced routes and simplifies short loops. Very large loops returning by a different route still need broader tiling-path reduction for guaranteed restoration.
- While a ball's distant chart is parked, friction and motion continue but obstacle collisions are skipped. **B** always recalls it.
- Original task/generation method bodies were unavailable. Farm scenery assignments and gameplay, trampoline behavior, and ball mechanics are reconstructions.

## Troubleshooting

- **Empty project or missing scene:** verify Hub opened this repository root, then open a listed `.unity` scene. Copying a scene file alone omits its scripts, shaders, meshes, and textures.
- **Flat, overlapping, or separated geometry in Scene view:** press Play and inspect Game view; chart placement and rendering are initialized at runtime.
- **No movement or mouse look:** focus Game view and resume with Esc. The project is configured for the new Input System.
- **Pink materials or compiler errors:** let importing finish and check the first Console error. Retain the project's built-in render pipeline and custom shaders. After script changes, stop and restart Play Mode to rebuild the shared geometry/collision state.

## Sources and licensing

Game meshes, textures, animations, and audio were recovered from CodeParade's *Hyperbolica*. The modified scenes and reconstructed behavior are this project's adaptations.

The geometry engine is CodeParade/HackerPoet's [HyperEngine](https://github.com/HackerPoet/HyperEngine), with its [MIT license retained here](Assets/HyperEngine/LICENSE.txt). That engine license does not license the recovered Hyperbolica art or audio. See [EXPLORATION_PROGRESS.md](EXPLORATION_PROGRESS.md) for the saved implementation and validation notes.
