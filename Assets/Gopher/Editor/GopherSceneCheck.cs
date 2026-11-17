using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class GopherSceneCheck
{
    const string Key = "Gopher.Check";
    const string Reports = "Logs/GopherChecks";
    static int frames;
    static readonly List<string> errors = new List<string>();

    static GopherSceneCheck()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool("Gopher.PreviewAfterCheck", false))
            {
                SessionState.SetBool("Gopher.PreviewAfterCheck", false);
                EditorApplication.delayCall += GopherSceneBuilder.Preview;
            }
        };
        EditorApplication.update += Tick;
        Application.logMessageReceived += (message, trace, type) =>
        {
            if (SessionState.GetBool(Key, false) && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
                errors.Add(message);
        };
    }

    public static void Run()
    {
        frames = 0;
        errors.Clear();
        Directory.CreateDirectory(Reports);
        EditorSceneManager.OpenScene(GopherSceneBuilder.ScenePath);
        SessionState.SetBool(Key, true);
        EditorApplication.isPlaying = true;
    }

    [MenuItem("Tools/Hyperbolica Gophers/Check Playable Level")]
    public static void RunInEditor()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        SessionState.SetBool("Gopher.KeepOpen", true);
        Run();
    }

    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        if (++frames < 60) return;
        try
        {
            var stage = UnityEngine.Object.FindFirstObjectByType<GopherStage>();
            if (!stage || stage.gophers.Length != 4) throw new Exception("Expected four gophers.");
            foreach (var go in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go) > 0) throw new Exception("Missing script on " + go.name);
            if (HM.K != 1 || HM.UseRadialChart) throw new Exception("Spherical geometry did not initialize.");
            var charts = UnityEngine.Object.FindObjectsByType<FarmChart>(FindObjectsSortMode.None);
            if (charts.Length != 10) throw new Exception("Expected six ground cells and four gopher placements.");
            foreach (var chart in charts)
                if (Mathf.Abs(chart.GetComponent<HyperObject>().localGV.vec.y) > 0.0001f)
                    throw new Exception("Unexpected vertical farm cell: " + chart.name);
            CheckSeam("Farm_Windmill — chart R", new Vector3(1, 0, 0), new Vector3(-1, 0, 0));
            CheckSeam("Farm_Tractor — chart L", new Vector3(-1, 0, 0), new Vector3(1, 0, 0));
            CheckSeam("Farm_Enclosure — chart U", new Vector3(0, 0, 1), new Vector3(0, 0, -1));
            CheckSeam("Farm_Well — chart D", new Vector3(0, 0, -1), new Vector3(0, 0, 1));
            if (WCollider.AllColliders.Count == 0) throw new Exception("Farm colliders did not initialize.");
            if (ShaderUtil.ShaderHasError(Shader.Find("Custom/SphericalShader"))) throw new Exception("Spherical shader has errors.");
            if (!GameObject.Find("Farm_Enclosure — chart U") || !GameObject.Find("Farm_Well — chart D") || !GameObject.Find("Farm_Windmill — chart R"))
                throw new Exception("A requested landmark is missing.");
            if (!GameObject.Find("Portal")) throw new Exception("The portal is missing.");
            foreach (var transform in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (transform.name.IndexOf("NPC", StringComparison.OrdinalIgnoreCase) >= 0) throw new Exception("Unexpected NPC.");
            CheckRecoveredVisuals(stage);
            CheckGameplay(stage.GetComponent<GopherTask>());
            CaptureBurrow(stage);
            if (errors.Count > 0) throw new Exception(string.Join("\n", errors));
            BeachSceneBuilder.Render(Camera.main, Reports + "/Gameplay.png");
            var camera = Camera.main;
            var rotation = camera.transform.localRotation;
            camera.transform.localRotation = Quaternion.Euler(0, 90, 0);
            BeachSceneBuilder.Render(camera, Reports + "/FarmRight.png");
            camera.transform.localRotation = Quaternion.Euler(0, 180, 0);
            BeachSceneBuilder.Render(camera, Reports + "/FarmBehind.png");
            camera.transform.localRotation = rotation;
            File.WriteAllText(Reports + "/Check.txt", "PASS: Four gophers, no NPCs, requested landmarks, six connected spherical ground cells. Visual checks: four original soil rims, original gun rig and controller, separate color/AO maps. Gameplay checks: emergence limit, hidden targets reject hits, weapon cooldown, blocked plasma, curved plasma hits all four targets, stunned targets reject repeat hits, completion, reset. No runtime errors.\n");
            Debug.Log("[Gopher] Playable level checks passed. Report: " + Path.GetFullPath(Reports + "/Check.txt"));
            Finish(0);
        }
        catch (Exception ex)
        {
            File.WriteAllText(Reports + "/Check.txt", ex.ToString());
            Debug.LogException(ex);
            Finish(1);
        }
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    static void CheckRecoveredVisuals(GopherStage stage)
    {
        foreach (var actor in stage.gophers)
        {
            var burrow = actor.transform.Find("Recovered dirt burrow");
            Require(burrow && burrow.GetComponent<MeshFilter>().sharedMesh.vertexCount > 30,
                "Original dirt opening is missing for " + actor.name);
            Require(burrow.GetComponent<Renderer>().sharedMaterial.GetTexture("_AOTex"),
                "Original burrow shading is missing.");
        }
        var gun = GameObject.Find("Original Hyperbolica plasma gun");
        Require(gun, "Recovered plasma gun is missing.");
        var skin = gun.GetComponentInChildren<SkinnedMeshRenderer>();
        Require(skin && skin.bones.Length == 3, "Original three-bone gun rig is missing.");
        Require(gun.GetComponent<Animator>().runtimeAnimatorController, "Original gun controller is missing.");
        Require(skin.sharedMaterial.mainTexture != skin.sharedMaterial.GetTexture("_AOTex"),
            "Gun color palette and ambient occlusion must use separate textures.");
    }

    static void CaptureBurrow(GopherStage stage)
    {
        var task = stage.GetComponent<GopherTask>();
        var camera = Camera.main;
        var rotation = camera.transform.localRotation;
        for (int i = 0; i < task.Targets.Count; i++)
        {
            var target = task.Targets[i];
            var pose = target.GetComponent<HyperObject>().localGV;
            HyperObject.worldGV = -(new GyroVector(HM.HyperTranslate(0, 0, -0.14f)) + pose);
            camera.transform.localRotation = Quaternion.Euler(18, 0, 0);
            string prefix = i == 0 ? "Burrow" : target.name;
            target.ResetTarget(0);
            RefreshVisuals();
            BeachSceneBuilder.Render(camera, Reports + "/" + prefix + "Hidden.png");
            target.Emerge();
            target.Tick(0.6f);
            target.GetComponent<Animator>().Update(0);
            RefreshVisuals();
            BeachSceneBuilder.Render(camera, Reports + "/" + prefix + "Emerged.png");
        }
        task.ResetLevel();
        camera.transform.localRotation = rotation;
        RefreshVisuals();
    }

    static void RefreshVisuals()
    {
        foreach (var hyperObject in UnityEngine.Object.FindObjectsByType<HyperObject>(FindObjectsSortMode.None))
            hyperObject.SendMessage("LateUpdate");
    }

    static void CheckGameplay(GopherTask task)
    {
        Require(task && task.Targets.Count == 4, "Playable task did not initialize.");
        task.ResetLevel();
        int outCount = 0;
        foreach (var target in task.Targets) if (target.IsOut) outCount++;
        Require(outCount == 2, "Expected two emerging gophers.");
        Require(!task.Targets[3].TryShock(), "Hidden gopher accepted a hit.");
        Require(task.TryFire() && !task.TryFire() && task.Shots == 1, "Weapon cooldown failed.");
        task.ResetLevel();
        InputManager.pauseInput = true;

        var testTarget = task.Targets[0];
        testTarget.Tick(0.6f);
        testTarget.GetComponent<Animator>().Update(0);
        var blockedPose = ShotAbove(testTarget.Center);
        var obstruction = new GameObject("Gameplay check obstruction").AddComponent<HyperObject>();
        obstruction.localGV = blockedPose;
        int groupIndex = WCollider.AllColliders.Count;
        WCollider.AllColliders.Add(new WCollider.Group
        {
            ho = obstruction,
            colliders = new WCollider[] { new SphereWCollider(false, Vector3.zero, 0.06f) }
        });
        try
        {
            var blockedBolt = task.SpawnBolt(blockedPose);
            blockedBolt.Advance(0.001f);
            Require(testTarget.State == GopherTarget.TargetState.Outside, "A blocked shot hit a gopher.");
        }
        finally
        {
            WCollider.AllColliders.RemoveAt(groupIndex);
            UnityEngine.Object.DestroyImmediate(obstruction.gameObject);
        }

        foreach (var target in task.Targets)
        {
            target.ResetTarget(0);
            target.Emerge();
            target.Tick(0.6f);
            target.GetComponent<Animator>().Update(0);
            var bolt = task.SpawnBolt(ShotAbove(target.Center));
            for (int i = 0; i < 50 && target.CanBeHit; i++) bolt.Advance(0.001f);
            Require(target.State == GopherTarget.TargetState.Shocked, "Curved plasma missed " + target.name);
            target.Tick(0.81f);
            Require(target.State == GopherTarget.TargetState.Stunned && !target.TryShock(), "Stun state failed.");
        }
        task.CheckCompletion();
        Require(task.Complete && task.StunnedCount == 4 && !task.TryFire(), "Completion did not stop firing.");
        task.ResetLevel();
        Require(!task.Complete && task.StunnedCount == 0 && task.Shots == 0, "Level reset failed.");
    }

    static GyroVector ShotAbove(Vector3 point)
    {
        var origin = new GyroVector(Vector3.up * 0.05f) + new GyroVector(point);
        Vector3 direction = (-origin) * point;
        return Quaternion.FromToRotation(Vector3.forward, direction) + origin;
    }

    static void CheckSeam(string neighbourName, Vector3 centerEdge, Vector3 neighbourEdge)
    {
        var center = GameObject.Find("Farm_Crops — chart center").GetComponent<HyperObject>();
        var neighbour = GameObject.Find(neighbourName).GetComponent<HyperObject>();
        Vector3 a = center.WorldPositionToLocalHyper(centerEdge);
        Vector3 b = neighbour.WorldPositionToLocalHyper(neighbourEdge);
        if (Vector3.Distance(a, b) > 0.001f) throw new Exception("Spherical chart seam is disconnected at " + neighbourName);
    }

    static void Finish(int exitCode)
    {
        SessionState.SetBool(Key, false);
        bool keepOpen = SessionState.GetBool("Gopher.KeepOpen", false);
        SessionState.SetBool("Gopher.KeepOpen", false);
        if (keepOpen && exitCode == 0) SessionState.SetBool("Gopher.PreviewAfterCheck", true);
        EditorApplication.isPlaying = false;
        if (!keepOpen) EditorApplication.Exit(exitCode);
    }
}
