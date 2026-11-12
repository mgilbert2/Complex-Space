using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class GopherSceneBuilder
{
    public const string ScenePath = "Assets/Gopher/Scenes/Chopped De Sitter farms.unity";
    const string Recovered = "Assets/Gopher/Recovered/";
    const string Generated = "Assets/Gopher/Generated/";

    [MenuItem("Tools/Hyperbolica Gophers/Open Gopher Scene")]
    public static void Open()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(ScenePath);
        Shader.SetGlobalFloat("_Enable", 0);
        var view = SceneView.lastActiveSceneView;
        if (view) view.LookAt(new Vector3(4.5f, 0.05f, 1.5f), Quaternion.Euler(65, 0, 0), 7, false);
    }

    [MenuItem("Tools/Hyperbolica Gophers/Preview Spherical Farm")]
    public static void Preview()
    {
        Open();
        EditorApplication.isPlaying = true;
    }

    public static void Build()
    {
        if (File.Exists(ScenePath)) throw new InvalidOperationException("Gopher scene already exists. Open it to edit it.");
        Directory.CreateDirectory("Assets/Gopher/Scenes");
        Directory.CreateDirectory(Generated);
        AssetDatabase.Refresh();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GopherStage.ConfigureGeometry();
        Shader.SetGlobalFloat("_Enable", 0);
        var stage = new GameObject("Gopher stage settings").AddComponent<GopherStage>();

        var ground = Spawn("Farm_Crops.prefab", "Farm clearing");
        ApplyMaterials(ground);
        var gophers = Spawn("Gophers_Recovered.prefab", "Gophers — move these to edit the layout");
        var actors = gophers.transform.Cast<Transform>().OrderBy(t => t.name).ToArray();
        var idle = AssetDatabase.FindAssets("t:AnimationClip", new[] { Recovered })
            .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<AnimationClip>)
            .First(clip => clip.name == "Idle_01");
        for (int i = 0; i < actors.Length; i++)
        {
            var actor = actors[i];
            float angle = i * Mathf.PI * 2 / actors.Length;
            actor.localPosition = new Vector3(Mathf.Sin(angle) * 0.55f, 0.013f, Mathf.Cos(angle) * 0.55f);
            actor.localRotation = Quaternion.Euler(0, angle * Mathf.Rad2Deg + 180, 0);
            foreach (var ho in actor.GetComponentsInChildren<HyperObject>(true))
            {
                ho.localGV = GyroVector.identity;
                ho.allowCulling = false;
            }
            foreach (var child in actor.GetComponentsInChildren<Transform>(true))
                if (child.name.Contains("Stars") || child.name.Contains("Stun")) child.gameObject.SetActive(false);
            idle.SampleAnimation(actor.gameObject, 0);
        }
        ApplyMaterials(gophers);
        stage.gophers = actors.Select(t => t.GetComponent<Animator>()).ToArray();

        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.transform.rotation = Quaternion.Euler(50, -30, 0);
        RenderSettings.ambientLight = new Color(0.8f, 0.85f, 0.8f);
        RenderSettings.skybox = null;

        var playerObject = new GameObject("Player");
        var cameraObject = new GameObject("Main Camera");
        cameraObject.transform.SetParent(playerObject.transform);
        cameraObject.transform.localPosition = new Vector3(0, 1.5f, 0);
        cameraObject.tag = "MainCamera";
        var camera = cameraObject.AddComponent<Camera>();
        camera.nearClipPlane = 0.001f;
        camera.farClipPlane = 100;
        camera.fieldOfView = 75;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.68f, 0.85f, 0.95f);
        cameraObject.AddComponent<AudioListener>();
        cameraObject.AddComponent<DisableCameraCull>();
        stage.player = playerObject.AddComponent<Player>();
        stage.player.mainCamera = cameraObject;
        stage.player.startHyper = HM.UnitToPoincare(new Vector3(0, 0, -0.72f), true);
        stage.player.height = 0.06f;
        stage.player.walkingSpeed = 2;
        stage.player.jumpSpeed = 0;
        EditorSceneManager.SaveScene(scene, ScenePath);
        var scenes = EditorBuildSettings.scenes.ToList();
        if (!scenes.Any(s => s.path == ScenePath)) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log("[Gopher] Editable scene created at " + ScenePath);
    }

    static GameObject Spawn(string prefabName, string name)
    {
        var path = Recovered + "GameObject/" + prefabName;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (!prefab) throw new FileNotFoundException(path);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        instance.name = name;
        return instance;
    }

    static void ApplyMaterials(GameObject root)
    {
        foreach (var textures in root.GetComponentsInChildren<SetTextures>(true))
        {
            var renderer = textures.GetComponent<Renderer>();
            if (!renderer) continue;
            var material = new Material(Shader.Find("Custom/SphericalShader"));
            material.SetTexture("_MainTex", textures.texture ? textures.texture : Texture2D.whiteTexture);
            material.SetTexture("_AOTex", textures.aomap ? textures.aomap : Texture2D.whiteTexture);
            material.SetColor("_Color", textures.colorize);
            material.SetFloat("_Ambient", 0.8f);
            material.SetFloat("_BoundaryAO", 1);
            var path = AssetDatabase.GenerateUniqueAssetPath(Generated + renderer.name + ".mat");
            AssetDatabase.CreateAsset(material, path);
            renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
        }
    }

    public static void BuildAndCheck()
    {
        Build();
        GopherSceneCheck.Run();
    }

    public static void RestoreFarmAndCheck()
    {
        EditorSceneManager.OpenScene(ScenePath);
        GopherStage.ConfigureGeometry();
        Shader.SetGlobalFloat("_Enable", 0);
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == "Farm clearing" || root.name.StartsWith("Gophers —") || root.name == "Spherical farm charts")
                UnityEngine.Object.DestroyImmediate(root);
        }

        // The game generated these cells at runtime. Its tile-selection method was
        // not recovered, so this assigns the recovered scenery to the original cell map.
        var assignments = new Dictionary<string, string>
        {
            { "", "Farm_Crops" }, { "R", "Farm_Windmill" },
            { "L", "Farm_Tractor" }, { "U", "Farm_Enclosure" },
            { "D", "Farm_Well" }, { "RR", "Farm_Barn" }
        };
        var chartRoot = new GameObject("Spherical farm charts");
        var poses = new List<GyroVector>();
        var offsets = new List<Vector3>();
        using (var reader = new BinaryReader(File.OpenRead(Recovered + "Resources/Map3.bytes")))
        {
            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                string coordinate = reader.ReadString();
                var vector = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                var rotation = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                var pose = new GyroVector(vector, rotation);
                int index = poses.Count;
                var offset = new Vector3((index % 4) * 3, 0, (index / 4) * 3);
                var tile = Spawn(assignments[coordinate] + ".prefab", assignments[coordinate] + " — chart " + (coordinate == "" ? "center" : coordinate));
                tile.transform.SetParent(chartRoot.transform);
                tile.transform.localPosition = offset;
                tile.AddComponent<FarmChart>().editorOffset = offset;
                tile.GetComponent<HyperObject>().localGV = pose;
                ApplyMaterials(tile);
                poses.Add(pose);
                offsets.Add(offset);
            }
        }

        var stage = UnityEngine.Object.FindFirstObjectByType<GopherStage>();
        var gophers = Spawn("Gophers_Recovered.prefab", "Gophers — original recovered positions");
        var actors = gophers.transform.Cast<Transform>().OrderBy(t => t.name).ToArray();
        var idle = AssetDatabase.FindAssets("t:AnimationClip", new[] { Recovered })
            .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<AnimationClip>)
            .First(clip => clip.name == "Idle_01");
        foreach (var actor in actors)
        {
            var ho = actor.GetComponent<HyperObject>();
            int nearest = 0;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < poses.Count; i++)
            {
                float distance = (ho.localGV - poses[i]).Point().sqrMagnitude;
                if (distance < bestDistance)
                {
                    nearest = i;
                    bestDistance = distance;
                }
            }
            actor.localPosition += offsets[nearest];
            actor.gameObject.AddComponent<FarmChart>().editorOffset = offsets[nearest];
            ho.allowCulling = false;
            foreach (var child in actor.GetComponentsInChildren<Transform>(true))
                if (child.name.Contains("Stars") || child.name.Contains("Stun")) child.gameObject.SetActive(false);
            idle.SampleAnimation(actor.gameObject, 0);
        }
        ApplyMaterials(gophers);
        stage.gophers = actors.Select(t => t.GetComponent<Animator>()).ToArray();
        stage.player.startHyper = new Vector3(-0.13f, 0, -0.09f);
        stage.player.height = 0.036f;
        EditorUtility.SetDirty(stage);
        EditorUtility.SetDirty(stage.player);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        GopherSceneCheck.Run();
    }
}
