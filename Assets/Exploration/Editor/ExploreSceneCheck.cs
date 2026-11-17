using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Runs both saved exploration scenes in Play Mode, writes diagnostic reports,
/// and captures a camera image before the checks change the player's position.
/// SessionState keeps the run alive across Play Mode domain reloads.
/// </summary>
[InitializeOnLoad]
public static class ExploreSceneCheck
{
    const string SessionKey = "Explore.Check";
    const string SceneIndexKey = SessionKey + "Index";
    const string FailedKey = SessionKey + "Failed";
    const int StartupFrames = 80;
    const int LongTripCellCount = 200;

    static string ReportDirectory => Path.GetFullPath(
        Path.Combine(Application.dataPath, "../Logs/ExplorationChecks"));
    static string ResultsPath => Path.Combine(ReportDirectory, "results.txt");
    static string ErrorsPath => Path.Combine(ReportDirectory, "errors.txt");

    static int frames;
    static bool checkedScene;

    static ExploreSceneCheck()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        Application.logMessageReceived += CaptureError;
    }

    /// <summary>Checks the saved scenes without regenerating their contents.</summary>
    [MenuItem("Tools/Complex Space/Check Exploration Scenes")]
    public static void Run()
    {
        Directory.CreateDirectory(ReportDirectory);
        File.WriteAllText(ResultsPath, "");
        File.WriteAllText(ErrorsPath, "");
        SessionState.SetBool(FailedKey, false);
        SessionState.SetInt(SceneIndexKey, 0);
        SessionState.SetBool(SessionKey, true);
        EnterNextScene();
    }

    /// <summary>Regenerates both scenes first; intended for a clean batch run.</summary>
    public static void BuildAndRun()
    {
        ExploreSceneBuilder.BuildBoth();
        Run();
    }

    static void EnterNextScene()
    {
        frames = 0;
        checkedScene = false;
        bool elliptic = SessionState.GetInt(SceneIndexKey, 0) == 0;
        EditorSceneManager.OpenScene(ExploreSceneBuilder.PathFor(elliptic));
        EditorApplication.isPlaying = true;
    }

    static void Tick()
    {
        if (!SessionState.GetBool(SessionKey, false)
            || !EditorApplication.isPlaying
            || checkedScene)
        {
            return;
        }

        // Allow Start, collider registration, and shader setup to finish.
        if (++frames < StartupFrames)
        {
            return;
        }

        checkedScene = true;
        try
        {
            CheckActiveScene();
        }
        catch (Exception exception)
        {
            File.AppendAllText(ResultsPath, "FAIL: " + exception + "\n");
            SessionState.SetBool(FailedKey, true);
        }

        EditorApplication.isPlaying = false;
    }

    static void CheckActiveScene()
    {
        var stage = UnityEngine.Object.FindFirstObjectByType<ExploreSpace>();
        Require(stage != null, "Missing stage");
        CheckSceneSetup(stage);
        CaptureScreenshot(stage, stage.elliptic ? "Elliptic" : "Hyperbolic");

        InputManager.pauseInput = false;
        CheckBallRolling(stage);
        CheckPause(stage);
        CheckKickDistance(stage);
        CheckColumnImpact(stage);
        CheckTrampoline(stage);

        if (stage.elliptic)
        {
            CheckClosedGeodesic();
        }
        else
        {
            CheckTerrainStreaming(stage);
        }

        WriteSuccess(stage);
    }

    static void CheckSceneSetup(ExploreSpace stage)
    {
        Require(HM.K == (stage.elliptic ? 1 : -1), "Wrong curvature");
        Require(stage.terrain.Length >= (stage.elliptic ? 6 : 80), "Incomplete surface coverage");

        int columnCount = UnityEngine.Object.FindObjectsByType<WarpCollider>(
            FindObjectsSortMode.None).Length;
        Require(columnCount == 6, "Expected six collidable columns");
        Require(stage.trampoline.GetComponent<MeshFilter>().sharedMesh != null,
            "Missing recovered trampoline");
        Require(!float.IsNaN(stage.player.height), "Invalid player");
    }

    static void CheckBallRolling(ExploreSpace stage)
    {
        HyperObject.worldGV = GyroVector.identity;
        stage.player.mainCamera.transform.rotation = Quaternion.Euler(0, 30, 0);
        stage.ball.Initialize(new GyroVector(0.04f, 0, 0.12f));

        // Aim between columns so this check measures free rolling and friction.
        Require(stage.ball.TryKick(stage.player), "Nearby ball cannot be kicked");
        for (int step = 0; step < 120; step++)
        {
            stage.ball.Advance(1f / 120);
        }

        Require(Mathf.Abs(stage.ball.hyper.localGV.vec.y) < 1e-6f, "Ball left ground surface");
        Require(stage.ball.speed > 0 && stage.ball.speed < 0.65f, "Ball friction failed");
        Require((stage.ball.hyper.localGV.Point() - new Vector3(0, 0, 0.12f)).magnitude > 0.1f,
            "Ball did not advance");
    }

    static void CheckPause(ExploreSpace stage)
    {
        Vector3 beforePause = stage.ball.hyper.localGV.Point();
        stage.SendMessage("Pause", true);
        stage.ball.Advance(0.5f);
        Require((stage.ball.hyper.localGV.Point() - beforePause).sqrMagnitude < 1e-10f,
            "Ball moved while paused");
        stage.SendMessage("Pause", false);
    }

    static void CheckKickDistance(ExploreSpace stage)
    {
        stage.ball.Initialize(new GyroVector(HM.HyperTranslate(0, 0, 1.2f)));
        Require(!stage.ball.TryKick(stage.player), "Distant ball can be kicked");
    }

    static void CheckColumnImpact(ExploreSpace stage)
    {
        var column = UnityEngine.Object.FindObjectsByType<HyperObject>(FindObjectsSortMode.None)
            .First(hyperObject => hyperObject.name == "Stone column 1");
        GyroVector columnPose = column.localGV;
        var playerPose = new GyroVector(HM.HyperTranslate(0, 0, -0.28f)) + columnPose;
        HyperObject.worldGV = -playerPose;
        stage.player.mainCamera.transform.rotation = Quaternion.identity;
        stage.ball.Initialize(new GyroVector(HM.HyperTranslate(0, 0, -0.16f)) + columnPose);

        Require(stage.ball.TryKick(stage.player), "Cannot kick ball toward column");
        for (int step = 0; step < 100; step++)
        {
            stage.ball.Advance(0.01f);
        }

        Require(stage.ball.CollisionCount > 0, "Ball passed through a stone column");
        Require(Mathf.Abs(stage.ball.hyper.localGV.vec.y) < 1e-6f,
            "Impact lifted ball off ground");
    }

    static void CheckTrampoline(ExploreSpace stage)
    {
        // Place the descending player's feet above the central mat.
        HyperObject.worldGV = -stage.trampoline.localGV;
        stage.player.velocity = Vector3.down * 0.01f;
        stage.SendMessage("Update");
        Require(stage.player.velocity.y > 0, "Trampoline did not bounce player");
    }

    static void CheckClosedGeodesic()
    {
        // tan has period pi: these tangent steps trace a complete great circle.
        var pose = GyroVector.identity;
        for (int step = 0; step < 1000; step++)
        {
            pose = new GyroVector(HM.HyperTranslate(0, 0, Mathf.PI / 1000)) + pose;
        }

        Require(pose.Point().magnitude < 0.002f, "Closed-space geodesic did not return");
    }

    static void CheckTerrainStreaming(ExploreSpace stage)
    {
        Vector3 ballBefore = stage.ball.hyper.localGV.Point();
        Vector3 trampolineBefore = stage.trampoline.localGV.Point();
        stage.ball.speed = 0;

        TravelCells(stage, Vector3.forward, LongTripCellCount);
        Require(stage.streamCount >= LongTripCellCount, "Terrain does not continue streaming");
        Require(HyperObject.worldGV.vec.sqrMagnitude < 0.5f, "Streaming did not rebase player");
        Require(!stage.trampoline.gameObject.activeSelf, "Distant central landmark was not parked");

        TravelCells(stage, Vector3.back, LongTripCellCount);
        Require(stage.trampoline.gameObject.activeSelf
            && (stage.trampoline.localGV.Point() - trampolineBefore).magnitude < 0.0002f,
            "Central trampoline did not survive long out-and-back travel");
        Require((stage.ball.hyper.localGV.Point() - ballBefore).magnitude < 0.001f,
            "Stationary ball did not survive long out-and-back travel");

        TravelCells(stage, Vector3.forward, LongTripCellCount);
        stage.RecallBall();
        Require(stage.ball.TryKick(stage.player),
            "Cannot recall and kick ball in a distant streamed region");
        CheckRebasedPoses();
    }

    static void TravelCells(ExploreSpace stage, Vector3 direction, int count)
    {
        // The engine moves the world opposite to the player's travel direction.
        for (int cell = 0; cell < count; cell++)
        {
            HyperObject.worldGV = new GyroVector(-direction * (float)HM.CELL_WIDTH);
            stage.SendMessage("LateUpdate");
        }
    }

    static void CheckRebasedPoses()
    {
        foreach (var hyperObject in UnityEngine.Object.FindObjectsByType<HyperObject>(FindObjectsSortMode.None))
        {
            hyperObject.UpdateComposedGV();
            Require(!float.IsNaN(hyperObject.composedGV.vec.x)
                && !float.IsNaN(hyperObject.composedGV.gyr.w),
                "Non-finite rebased object: " + hyperObject.name);
        }
    }

    static void WriteSuccess(ExploreSpace stage)
    {
        string geometryChecks = stage.elliptic
            ? "closed geodesic"
            : "200-cell out/return, continued streaming, distant recall";
        string sceneName = stage.elliptic ? "Elliptic" : "Hyperbolic";
        File.AppendAllText(ResultsPath,
            sceneName + ": PASS; cells=" + stage.terrain.Length
            + "; rolling, ground constraint, kick distance, pause, column impact, trampoline and "
            + geometryChecks + " passed.\n");
    }

    static void CaptureScreenshot(ExploreSpace stage, string name)
    {
        var camera = stage.player.mainCamera.GetComponent<Camera>();
        var previousTarget = camera.targetTexture;
        var previousActive = RenderTexture.active;
        var renderTexture = new RenderTexture(1280, 720, 24);
        Texture2D image = null;

        try
        {
            camera.targetTexture = renderTexture;
            camera.Render();
            RenderTexture.active = renderTexture;
            image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(ReportDirectory, name + ".png"), image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            if (image)
            {
                UnityEngine.Object.DestroyImmediate(image);
            }
            UnityEngine.Object.DestroyImmediate(renderTexture);
        }
    }

    static void CaptureError(string message, string stackTrace, LogType type)
    {
        bool isError = type == LogType.Error || type == LogType.Exception || type == LogType.Assert;
        if (SessionState.GetBool(SessionKey, false) && isError)
        {
            File.AppendAllText(ErrorsPath, message + "\n" + stackTrace + "\n");
        }
    }

    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(SessionKey, false))
        {
            return;
        }

        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            frames = 0;
            checkedScene = false;
        }

        if (state != PlayModeStateChange.EnteredEditMode)
        {
            return;
        }

        int nextIndex = SessionState.GetInt(SceneIndexKey, 0) + 1;
        SessionState.SetInt(SceneIndexKey, nextIndex);
        if (nextIndex < 2)
        {
            EditorApplication.delayCall += EnterNextScene;
            return;
        }

        SessionState.SetBool(SessionKey, false);
        bool failed = SessionState.GetBool(FailedKey, false) || new FileInfo(ErrorsPath).Length > 0;
        Debug.Log("[Explore] Checks complete: " + (failed ? "FAIL" : "PASS"));
        if (Application.isBatchMode)
        {
            EditorApplication.Exit(failed ? 1 : 0);
        }
    }

    static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }
}
