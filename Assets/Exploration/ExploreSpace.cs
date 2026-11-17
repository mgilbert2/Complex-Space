using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Configures an exploration scene, handles its interactions, and keeps the
/// hyperbolic terrain near the player as the world changes coordinate charts.
/// </summary>
[DefaultExecutionOrder(-200)]
public class ExploreSpace : MonoBehaviour
{
    const int TerrainExpansionDepth = 4;
    const float DuplicateCellToleranceSquared = 1e-8f;
    const float RebaseThresholdSquared = 0.01f;
    const float BounceCooldownSeconds = 0.6f;
    const float BounceSpeedInPlayerHeights = 5.8f;
    const float TrampolineContactRadius = 0.05f;
    const float TrampolineContactHeight = 0.025f;
    const float BallRecallDistance = 0.12f;

    static readonly Vector3[] NeighborDirections =
    {
        Vector3.right, Vector3.left, Vector3.forward, Vector3.back
    };

    [Header("Geometry")]
    [Tooltip("Uses closed spherical positive curvature. Antipodal points remain distinct.")]
    public bool elliptic;

    [Header("Scene references")]
    public Player player;
    public HyperObject[] terrain;
    public HyperObject trampoline;
    public SurfaceBall ball;

    [Header("Terrain and audio")]
    public Material grass;
    public Mesh groundMesh;
    public AudioClip bounceSound;

    [Header("Runtime diagnostics")]
    [Tooltip("Number of terrain-origin changes during this play session.")]
    public int streamCount;

    bool paused;
    float bounceCooldown;
    Vector3 trampolineRestScale;
    AudioSource bounceAudio;

    readonly List<GyroVector> neighborhood = new List<GyroVector>();
    readonly Dictionary<HyperObject, GyroVectorD> fixedLandmarks =
        new Dictionary<HyperObject, GyroVectorD>();
    readonly ExploreAnchorHistory anchorHistory = new ExploreAnchorHistory();

    /// <summary>Sets the engine's curvature before Player.Awake scales the player.</summary>
    public void Configure()
    {
        HM.SetTileType(elliptic ? 3 : 5);
        HM.UseRadialChart = false;
        Shader.SetGlobalFloat("_BeachRadialChart", 0);
        Shader.SetGlobalFloat("_KleinV", HM.KLEIN_V);
    }

    void Awake()
    {
        Configure();
        ResetSharedEngineState();
        Pause(false);
    }

    void ResetSharedEngineState()
    {
        // HyperEngine stores world and collision state globally. Each scene
        // must initialize it before its objects register their colliders.
        WCollider.AllColliders.Clear();
        HyperObject.worldGV = GyroVector.identity;
        HyperObject.isShaking = false;
        HyperObject.updateColliders = true;
        WorldBuilder.globalBounryAO = 1;

        Shader.SetGlobalFloat("_Enable", 1);
        Shader.SetGlobalFloat("_DualLight", 1);
        Shader.SetGlobalFloat("_Proj", 0);
        Shader.SetGlobalVector("_WarpParams", Vector4.zero);
        WorldBuilder.SetFog(new Color(0.67f, 0.83f, 0.94f, 0.12f), 0.2f);
    }

    void Start()
    {
        trampolineRestScale = trampoline.transform.localScale;
        bounceAudio = gameObject.AddComponent<AudioSource>();
        bounceAudio.spatialBlend = 0;
        WorldBuilder.UpdateReplacementShaders(true);

        if (!elliptic)
        {
            CacheFixedLandmarks();
            BuildTerrainNeighborhood();
            CreateTerrainPool();
            PlaceTerrainNeighborhood();
        }
    }

    void CacheFixedLandmarks()
    {
        var terrainObjects = new HashSet<HyperObject>(terrain);

        foreach (var hyperObject in FindObjectsByType<HyperObject>(FindObjectsSortMode.None))
        {
            hyperObject.highPrecision = true;
            hyperObject.localGVD = (GyroVectorD)hyperObject.localGV;
            hyperObject.allowCulling = true;

            if (hyperObject != ball.hyper && !terrainObjects.Contains(hyperObject))
            {
                fixedLandmarks.Add(hyperObject, hyperObject.localGVD);
            }
        }
    }

    void BuildTerrainNeighborhood()
    {
        // Breadth-first expansion visits nearby square cells in increasing
        // distance. Shared cell centers identify duplicate routes through the tiling.
        neighborhood.Add(GyroVector.identity);
        var depths = new List<int> { 0 };

        for (int cellIndex = 0; cellIndex < neighborhood.Count; cellIndex++)
        {
            if (depths[cellIndex] == TerrainExpansionDepth)
            {
                continue;
            }

            foreach (var direction in NeighborDirections)
            {
                var neighborStep = new GyroVector(direction * (float)HM.CELL_WIDTH);
                var candidate = neighborStep + neighborhood[cellIndex];

                if (!ContainsTerrainCell(candidate))
                {
                    neighborhood.Add(candidate);
                    depths.Add(depths[cellIndex] + 1);
                }
            }
        }
    }

    bool ContainsTerrainCell(GyroVector candidate)
    {
        foreach (var existing in neighborhood)
        {
            if ((existing.Point() - candidate.Point()).sqrMagnitude < DuplicateCellToleranceSquared)
            {
                return true;
            }
        }

        return false;
    }

    void CreateTerrainPool()
    {
        var tiles = new List<HyperObject>(terrain);

        while (tiles.Count < neighborhood.Count)
        {
            var tile = new GameObject("Streamed grass cell");
            tile.AddComponent<MeshFilter>().sharedMesh = groundMesh;
            tile.AddComponent<MeshRenderer>().sharedMaterial = grass;

            var hyperObject = tile.AddComponent<HyperObject>();
            hyperObject.highPrecision = true;
            tiles.Add(hyperObject);
        }

        terrain = tiles.ToArray();
    }

    void PlaceTerrainNeighborhood()
    {
        for (int cellIndex = 0; cellIndex < terrain.Length; cellIndex++)
        {
            SetPose(terrain[cellIndex], neighborhood[cellIndex]);
        }
    }

    void Update()
    {
        var keyboard = Keyboard.current;

        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            Pause(!paused);
        }

        if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
        {
            ResetExploration();
        }

        if (paused)
        {
            return;
        }

        if (keyboard != null && keyboard.bKey.wasPressedThisFrame)
        {
            RecallBall();
        }

        UpdateTrampoline();

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            ball.TryKick(player);
        }
    }

    void UpdateTrampoline()
    {
        bounceCooldown -= Time.deltaTime;
        float spring = Mathf.Max(0, bounceCooldown) / BounceCooldownSeconds;
        float compression = 1 - 0.28f * spring * Mathf.Sin(spring * Mathf.PI);
        trampoline.transform.localScale = new Vector3(
            trampolineRestScale.x,
            trampolineRestScale.y * compression,
            trampolineRestScale.z);

        // Compare positions in the player's curved-space chart, rather than
        // using a Unity trigger whose mesh would remain Euclidean.
        var relativePose = trampoline.localGV + HyperObject.worldGV;
        Vector3 relativePosition = relativePose.Point();
        float horizontalDistance = new Vector2(relativePosition.x, relativePosition.z).magnitude;
        float playerElevation = -HyperObject.worldGV.vec.y;

        bool touchesMat = trampoline.gameObject.activeSelf
            && horizontalDistance < TrampolineContactRadius
            && playerElevation < TrampolineContactHeight;

        if (touchesMat && player.velocity.y <= 0 && bounceCooldown <= 0)
        {
            player.velocity.y = player.height * BounceSpeedInPlayerHeights;
            bounceCooldown = BounceCooldownSeconds;

            if (bounceSound)
            {
                bounceAudio.PlayOneShot(bounceSound, 0.65f);
            }
        }
    }

    void LateUpdate()
    {
        if (elliptic || terrain.Length == 0)
        {
            return;
        }

        var anchor = FindNearestTerrainPose();
        if (anchor.vec.sqrMagnitude < RebaseThresholdSquared)
        {
            return;
        }

        RebaseWorld(anchor);
    }

    GyroVector FindNearestTerrainPose()
    {
        GyroVector nearestPose = terrain[0].localGV;
        float nearestDistanceSquared = float.MaxValue;

        foreach (var tile in terrain)
        {
            float distanceSquared = (tile.localGV + HyperObject.worldGV).vec.sqrMagnitude;
            if (distanceSquared < nearestDistanceSquared)
            {
                nearestDistanceSquared = distanceSquared;
                nearestPose = tile.localGV;
            }
        }

        return nearestPose;
    }

    void RebaseWorld(GyroVector anchor)
    {
        // Objects change from A to A - anchor while the observer changes from
        // W to anchor + W. Their relative transform is unchanged. Reusing a
        // nearby cell keeps the player away from the Poincare coordinate boundary.
        anchorHistory.Advance(anchor);
        HyperObject.worldGVD = (GyroVectorD)anchor + HyperObject.worldGVD;
        RestoreFixedLandmarks();
        ball.Rebase(anchor);
        PlaceTerrainNeighborhood();
        streamCount++;
    }

    void RestoreFixedLandmarks()
    {
        bool visible = anchorHistory.TryGetOriginPose(out GyroVectorD originPose);

        foreach (var landmark in fixedLandmarks)
        {
            var hyperObject = landmark.Key;
            hyperObject.gameObject.SetActive(visible);

            if (visible)
            {
                hyperObject.localGVD = landmark.Value + originPose;
                hyperObject.localGV = (GyroVector)hyperObject.localGVD;
            }
            else
            {
                // Inactive objects no longer update their cached colliders.
                // Explicitly skip those caches until the landmark is restored.
                foreach (var group in WCollider.AllColliders)
                {
                    if (group.ho == hyperObject)
                    {
                        group.skip = true;
                    }
                }
            }
        }
    }

    static void SetPose(HyperObject hyperObject, GyroVector pose)
    {
        hyperObject.localGV = pose;
        hyperObject.localGVD = (GyroVectorD)pose;
    }

    /// <summary>Reloads the scene, restoring its original landmarks and ball.</summary>
    public void ResetExploration()
    {
        SceneManager.LoadScene(gameObject.scene.name);
    }

    /// <summary>Places a stationary ball on the ground just ahead of the player.</summary>
    public void RecallBall()
    {
        var groundView = HyperObject.worldGV.ProjectToPlane();
        Vector3 forward = player.mainCamera.transform.forward;
        forward.y = 0;

        if (forward.sqrMagnitude < 0.01f)
        {
            forward = Vector3.forward;
        }

        var nearbyPose = new GyroVector(HM.HyperTranslate(forward.normalized * BallRecallDistance));
        ball.Initialize(nearbyPose - groundView);
    }

    void Pause(bool value)
    {
        paused = value;
        InputManager.pauseInput = value;
        Cursor.lockState = value ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = value;
    }

    void OnGUI()
    {
        DrawInstructions();
        GUI.Label(new Rect(Screen.width / 2 - 5, Screen.height / 2 - 12, 20, 25), "+");

        if (paused)
        {
            DrawPauseMenu();
        }
    }

    void DrawInstructions()
    {
        string title = elliptic
            ? "ELLIPTIC EXPLORATION · closed positive curvature"
            : "HYPERBOLIC EXPLORATION · continuously streamed negative curvature";

        GUI.Box(new Rect(15, Screen.height - 112, 570, 96), "");
        GUI.Label(new Rect(28, Screen.height - 106, 550, 27), title);
        GUI.Label(new Rect(28, Screen.height - 80, 550, 25),
            "WASD move · Space jump · click to kick · B recall ball · R reset · Esc pause");
        GUI.Label(new Rect(28, Screen.height - 55, 550, 25),
            "Step onto the central trampoline. Follow the grid and the six colored stone columns.");
    }

    void DrawPauseMenu()
    {
        if (GUI.Button(new Rect(Screen.width / 2 - 105, Screen.height / 2 + 30, 210, 35), "Resume"))
        {
            Pause(false);
        }

        string switchLabel = elliptic ? "Explore hyperbolic space" : "Explore elliptic space";
        if (GUI.Button(new Rect(Screen.width / 2 - 105, Screen.height / 2 + 72, 210, 35), switchLabel))
        {
            SceneManager.LoadScene(elliptic ? "Hyperbolic Exploration" : "Elliptic Exploration");
        }
    }

    void OnDestroy()
    {
        Shader.SetGlobalFloat("_Enable", 0);
        WCollider.AllColliders.Clear();
        InputManager.pauseInput = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
