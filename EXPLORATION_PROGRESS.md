# Exploration scenes checkpoint

Saved November 16, 2026. Work in progress.

Project: `Complex-Space` (Unity 6000.1.14f1).

- Scenes: `Assets/Exploration/Scenes/Elliptic Exploration.unity` and `Hyperbolic Exploration.unity`.
- Both contain grassy ground with an orientation grid, six colored stone columns, the recovered Hyperbolica trampoline mesh, and a kickable ball.
- New runtime scripts: `ExploreSpace.cs` (geometry, streaming, trampoline, input) and `SurfaceBall.cs` (surface-constrained geodesic rolling).
- Unity menu: **Tools > Complex Space** opens either scene or rebuilds both.
- Controls: WASD, mouse, Space, left click near the ball, R reset, Esc pause.

Validation: runtime and editor scripts compile. Scene generation succeeded. Initial Unity Play Mode checks FAILED; repeated NaN quaternion errors occurred during the hyperbolic run. Streaming/rebasing, interactions and visuals still need debugging and verification. Ball obstacle collisions are not implemented. The scene named Elliptic currently uses the engine's closed spherical positive-curvature geometry, without antipodal identification.

Resume with `ExploreSceneCheck.Run` and inspect Unity logs. Its current diagnostics use `Temp/ExplorationChecks`, which may be removed when Unity quits; move diagnostics to a persistent ignored location before the next run. Preserve the existing beach and farm scenes and the unrelated `My project (5)` directory.
