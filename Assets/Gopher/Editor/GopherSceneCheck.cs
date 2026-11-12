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
        EditorSceneManager.OpenScene(GopherSceneBuilder.ScenePath);
        SessionState.SetBool(Key, true);
        EditorApplication.isPlaying = true;
    }

    public static void RunInEditor()
    {
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
            foreach (var animator in stage.gophers)
            {
                var state = animator.GetCurrentAnimatorStateInfo(0);
                if (!state.IsName("Idle_01") && !state.IsName("Idle_02") && !state.IsName("Idle_03") && !state.IsName("TriggerIdle"))
                    throw new Exception("Gopher idle animation did not start for " + animator.name + ": " + state.shortNameHash);
            }
            if (!GameObject.Find("Farm_Enclosure — chart U") || !GameObject.Find("Farm_Well — chart D") || !GameObject.Find("Farm_Windmill — chart R"))
                throw new Exception("A requested landmark is missing.");
            if (!GameObject.Find("Portal")) throw new Exception("The portal is missing.");
            foreach (var transform in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (transform.name.IndexOf("NPC", StringComparison.OrdinalIgnoreCase) >= 0) throw new Exception("Unexpected NPC.");
            if (errors.Count > 0) throw new Exception(string.Join("\n", errors));
            BeachSceneBuilder.Render(Camera.main, "../HyperbolicaBeach/GopherGameplay.png");
            var camera = Camera.main;
            var rotation = camera.transform.localRotation;
            camera.transform.localRotation = Quaternion.Euler(0, 90, 0);
            BeachSceneBuilder.Render(camera, "../HyperbolicaBeach/GopherFarmRight.png");
            camera.transform.localRotation = Quaternion.Euler(0, 180, 0);
            BeachSceneBuilder.Render(camera, "../HyperbolicaBeach/GopherFarmBehind.png");
            camera.transform.localRotation = rotation;
            File.WriteAllText("../HyperbolicaBeach/GopherCheck.txt", "PASS: Four gophers, no NPCs, requested landmarks retained, six spherical ground cells, no vertical cells, four connected seams, idle animations, farm colliders, no missing scripts or runtime errors.\n");
            Finish(0);
        }
        catch (Exception ex)
        {
            File.WriteAllText("../HyperbolicaBeach/GopherCheck.txt", ex.ToString());
            Debug.LogException(ex);
            Finish(1);
        }
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
