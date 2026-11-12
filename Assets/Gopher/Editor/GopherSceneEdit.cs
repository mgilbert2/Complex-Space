using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class GopherSceneEdit
{
    [Serializable] class VertexSelection { public int[] vertices; }
    const string Request = "SimplifyGophers.request";

    static GopherSceneEdit()
    {
        EditorApplication.update += CheckRequest;
    }

    static void CheckRequest()
    {
        if (File.Exists("RepairFarmGround.request") && !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
        {
            File.Delete("RepairFarmGround.request");
            RepairGroundLayout();
            GopherSceneCheck.RunInEditor();
            return;
        }
        if (File.Exists("CheckGophers.request") && !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
        {
            File.Delete("CheckGophers.request");
            GopherSceneCheck.RunInEditor();
            return;
        }
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.isPlaying = false;
            return;
        }
        try { File.Delete(Request); }
        catch (IOException) { return; }
        try
        {
            Simplify();
            GopherSceneCheck.RunInEditor();
        }
        catch (Exception ex)
        {
            EditorSceneManager.OpenScene(GopherSceneBuilder.ScenePath);
            Debug.LogException(ex);
        }
    }

    static void RepairGroundLayout()
    {
        var scene = EditorSceneManager.OpenScene(GopherSceneBuilder.ScenePath);
        File.Copy(GopherSceneBuilder.ScenePath,
            "../HyperbolicaBeach/Backups/Gopher-before-ground-repair-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".unity");
        var barn = GameObject.Find("Farm_Barn — chart B");
        var opposite = GameObject.Find("Farm_Crops — chart RR");
        if (barn && opposite)
        {
            barn.GetComponent<HyperObject>().localGV = opposite.GetComponent<HyperObject>().localGV;
            barn.GetComponent<FarmChart>().editorOffset = opposite.GetComponent<FarmChart>().editorOffset;
            barn.transform.localPosition = opposite.transform.localPosition;
            barn.name = "Farm_Barn — chart RR";
            UnityEngine.Object.DestroyImmediate(opposite);
        }
        var lowerCell = GameObject.Find("Farm_Hole — chart F");
        if (lowerCell) UnityEngine.Object.DestroyImmediate(lowerCell);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }

    public static void Simplify()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Edit the farm outside Play Mode.");
        var scene = EditorSceneManager.OpenScene(GopherSceneBuilder.ScenePath);
        string backup = "../HyperbolicaBeach/Backups/Gopher-before-four-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".unity";
        File.Copy(GopherSceneBuilder.ScenePath, backup);

        var stage = UnityEngine.Object.FindFirstObjectByType<GopherStage>();
        var keep = new HashSet<string> { "Gopher4", "Gopher5", "Gopher7", "Gopher8" };
        foreach (var animator in stage.gophers)
            if (!keep.Contains(animator.name)) UnityEngine.Object.DestroyImmediate(animator.gameObject);
        stage.gophers = UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsSortMode.None)
            .Where(a => keep.Contains(a.name)).OrderBy(a => a.name).ToArray();

        var charts = GameObject.Find("Spherical farm charts");
        foreach (Transform tile in charts.transform)
        {
            if (tile.name.StartsWith("Farm_Crops") || tile.name.StartsWith("Farm_Tractor"))
            {
                foreach (Transform child in tile.Cast<Transform>().ToArray())
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                string source = tile.name.StartsWith("Farm_Crops") ? "Farm_Crops" : "Farm_Tractor";
                KeepGround(tile.gameObject, source);
            }
            if (tile.name.StartsWith("Farm_Hole"))
                foreach (Transform child in tile.Cast<Transform>().ToArray())
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
        }
        foreach (var root in scene.GetRootGameObjects())
            foreach (var transform in root.GetComponentsInChildren<Transform>(true).Reverse())
                if (transform.name.IndexOf("NPC", StringComparison.OrdinalIgnoreCase) >= 0)
                    UnityEngine.Object.DestroyImmediate(transform.gameObject);

        EditorUtility.SetDirty(stage);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[Gopher] Four gophers; crop clutter, scarecrows, tractor, and loose hole props cleared. Saved backup at " + backup);
    }

    static void KeepGround(GameObject tile, string source)
    {
        var selection = JsonUtility.FromJson<VertexSelection>(File.ReadAllText("Assets/Gopher/Editor/" + source + "Ground.json"));
        var indices = new HashSet<int>(selection.vertices);
        var filter = tile.GetComponent<MeshFilter>();
        if (!filter)
        {
            var skinned = tile.GetComponent<SkinnedMeshRenderer>();
            var materials = skinned.sharedMaterials;
            UnityEngine.Object.DestroyImmediate(skinned);
            var animator = tile.GetComponent<Animator>();
            if (animator) UnityEngine.Object.DestroyImmediate(animator);
            filter = tile.AddComponent<MeshFilter>();
            tile.AddComponent<MeshRenderer>().sharedMaterials = materials;
        }
        var original = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Gopher/Recovered/Mesh/" + source + ".asset");
        var triangles = original.triangles;
        var groundTriangles = new List<int>();
        for (int i = 0; i < triangles.Length; i += 3)
        {
            if (!indices.Contains(triangles[i]) || !indices.Contains(triangles[i + 1]) || !indices.Contains(triangles[i + 2])) continue;
            groundTriangles.Add(triangles[i]);
            groundTriangles.Add(triangles[i + 1]);
            groundTriangles.Add(triangles[i + 2]);
        }
        var mesh = UnityEngine.Object.Instantiate(original);
        mesh.name = source + " — cleared terrain";
        mesh.triangles = groundTriangles.ToArray();
        var path = AssetDatabase.GenerateUniqueAssetPath("Assets/Gopher/Generated/" + source + "Ground.asset");
        AssetDatabase.CreateAsset(mesh, path);
        filter.sharedMesh = mesh;
        // The source AO texture contains shadows from the removed props.
        var textures = tile.GetComponent<SetTextures>();
        textures.aomap = null;
        foreach (var material in tile.GetComponent<Renderer>().sharedMaterials)
        {
            material.SetTexture("_AOTex", null);
            EditorUtility.SetDirty(material);
        }
        foreach (var collider in tile.GetComponents<WarpCollider>()) UnityEngine.Object.DestroyImmediate(collider);
        var groundCollider = tile.AddComponent<WarpCollider>();
        groundCollider.boundingMeshes = new[] { mesh };
    }
}
