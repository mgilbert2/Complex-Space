# Exploration scenes

Saved November 16, 2026. Project: `Complex-Space`, Unity 6000.1.14f1.

Scenes:
- `Assets/Exploration/Scenes/Elliptic Exploration.unity`
- `Assets/Exploration/Scenes/Hyperbolic Exploration.unity`

Both contain textured grass and an orientation grid, six collidable stone columns with colored landmarks, the recovered Hyperbolica trampoline with bounce sound, and a kickable surface ball. Sizes match between geometries. The ball follows geodesics, remains on the surface, slows with friction and reflects off columns. The trampoline launches the player automatically when stepped on.

Controls: WASD, mouse, Space, left click near the ball, B recall ball, R reset, Esc pause/switch. Open scenes through **Tools > Complex Space**, then press Play.

Validation: both scenes generated and compiled in Unity. Automated Play Mode checks and rendered screenshots passed for rolling, ground constraint, kick distance, pause, column impacts, trampoline launch, spherical geodesic closure, and hyperbolic streaming. Long travel checks cover 200 cell changes outward, 200 back restoring the trampoline and stationary ball, another 200 outward, and recalling/kicking the ball there. Logs/screenshots are in ignored `Logs/ExplorationChecks`; rerun with `ExploreSceneCheck.Run` or the Tools menu.

Implementation: `ExploreSpace.cs`, `SurfaceBall.cs`, `ExploreAnchorHistory.cs`, and editor scene builder/checks under `Assets/Exploration`. Hyperbolic terrain uses 109 reusable nearby cells and rebases at existing tile centers. Chart-step history preserves distant landmarks on retraced routes and simplifies short loops.

Limits: the scene called Elliptic uses closed spherical positive curvature, without antipodal identification. The origin-history reducer handles retracing and short loops; very large loops returning by a different route still need more general tiling-word reduction for guaranteed landmark restoration. Offscreen rolling uses friction but skips obstacle collisions while its chart is parked. These are exploration prototypes with reconstructed trampoline/ball behavior, not recovered original game logic.

Preserve the beach and farm scenes and the unrelated `My project (5)` directory. The initial checkpoint commit was `a956e55`; later fixes supersede its failed first Play Mode checks.
