using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Reuses the original farm's dirt opening at each retained gopher.</summary>
public static class GopherVisualRepair
{
    const string Generated = "Assets/Gopher/Generated/";
    const int RimSegments = 20;

    struct Vertex
    {
        public Vector3 position;
        public Vector2 uv, uv2;
    }

    [Serializable] class Selection { public int[] vertices; }

    public static void ApplyAndCheck()
    {
        Apply();
        GopherSceneCheck.Run();
    }

    [MenuItem("Tools/Hyperbolica Gophers/Restore Original Burrows")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        var scene = EditorSceneManager.OpenScene(GopherSceneBuilder.ScenePath);
        Directory.CreateDirectory("Logs/GopherVisualBackups");
        File.Copy(GopherSceneBuilder.ScenePath,
            "Logs/GopherVisualBackups/Farm-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".unity");
        GopherStage.ConfigureGeometry();
        Shader.SetGlobalFloat("_Enable", 0);

        var stage = UnityEngine.Object.FindFirstObjectByType<GopherStage>();
        // This disconnected soil rim is part of the original enclosure mesh.
        Vector3 sourceCenter = new Vector3(-0.31295f, 0, -0.32045f);

        var original = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Gopher/Recovered/Mesh/Farm_Enclosure.asset");
        var selected = new HashSet<int>(JsonUtility.FromJson<Selection>(
            File.ReadAllText("Assets/Gopher/Editor/OriginalBurrowVertices.json")).vertices);
        var sourceVertices = ReadVertices(original);
        var patch = new List<Vertex>();
        var indices = original.triangles;
        for (int i = 0; i < indices.Length; i += 3)
        {
            if (!selected.Contains(indices[i]) || !selected.Contains(indices[i + 1]) || !selected.Contains(indices[i + 2])) continue;
            patch.Add(sourceVertices[indices[i]]);
            patch.Add(sourceVertices[indices[i + 1]]);
            patch.Add(sourceVertices[indices[i + 2]]);
        }
        for (int i = 0; i < patch.Count; i++)
        {
            var vertex = patch[i];
            vertex.position -= sourceCenter;
            patch[i] = vertex;
        }
        if (patch.Count < 30) throw new InvalidOperationException("Original burrow geometry was not recovered.");
        var burrowMesh = SaveMesh(patch, "RecoveredGopherBurrow");
        var material = AssetDatabase.LoadAssetAtPath<Material>(Generated + "GopherBurrow.mat");
        if (!material)
        {
            material = new Material(Shader.Find("Custom/SphericalShader"));
            AssetDatabase.CreateAsset(material, Generated + "GopherBurrow.mat");
        }
        material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Gopher/Recovered/Texture2D/Farm_Pallette.png");
        material.SetTexture("_AOTex", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Gopher/Recovered/Texture2D/Farm_Enclosure.png"));
        material.SetColor("_Color", Color.white);
        material.SetFloat("_Ambient", 0.8f);
        material.SetFloat("_BoundaryAO", 1);
        EditorUtility.SetDirty(material);

        // A recessed dark floor closes the opening against the background sky.
        var interiorVertices = new List<Vertex>();
        for (int i = 0; i < RimSegments; i++)
        {
            float a = i * 2 * Mathf.PI / RimSegments;
            float b = (i + 1) * 2 * Mathf.PI / RimSegments;
            interiorVertices.Add(new Vertex { position = new Vector3(0, 0.035f, 0) });
            interiorVertices.Add(new Vertex { position = new Vector3(Mathf.Cos(b) * 0.027f, 0.035f, Mathf.Sin(b) * 0.027f) });
            interiorVertices.Add(new Vertex { position = new Vector3(Mathf.Cos(a) * 0.027f, 0.035f, Mathf.Sin(a) * 0.027f) });
        }
        var interiorMesh = SaveMesh(interiorVertices, "GopherBurrowInterior");
        var interiorMaterial = AssetDatabase.LoadAssetAtPath<Material>(Generated + "GopherBurrowInterior.mat");
        if (!interiorMaterial)
        {
            interiorMaterial = new Material(Shader.Find("Custom/SphericalShader"));
            interiorMaterial.SetColor("_Color", new Color(0.07f, 0.035f, 0.015f));
            interiorMaterial.SetFloat("_Ambient", 1);
            AssetDatabase.CreateAsset(interiorMaterial, Generated + "GopherBurrowInterior.mat");
        }

        foreach (var actor in stage.gophers)
        {
            var previous = actor.transform.Find("Recovered dirt burrow");
            if (previous) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            var burrow = new GameObject("Recovered dirt burrow");
            burrow.transform.SetParent(actor.transform, false);
            // FarmChart removes the editor offsets in Awake; cancel only the
            // remaining source Transform translation, not the curved-space pose.
            var chart = actor.GetComponent<FarmChart>();
            float floorHeight = FindGroundHeight(actor.GetComponent<HyperObject>().localGV);
            float verticalShift = floorHeight - burrowMesh.bounds.min.y + 0.0003f;
            burrow.transform.localPosition = -(actor.transform.position - chart.editorOffset) + Vector3.up * verticalShift;
            burrow.AddComponent<MeshFilter>().sharedMesh = burrowMesh;
            burrow.AddComponent<MeshRenderer>().sharedMaterial = material;
            var interior = new GameObject("Recessed burrow interior");
            interior.transform.SetParent(burrow.transform, false);
            interior.AddComponent<MeshFilter>().sharedMesh = interiorMesh;
            interior.AddComponent<MeshRenderer>().sharedMaterial = interiorMaterial;
        }

        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[Gopher] Restored four original textured dirt openings with rims seated on the existing ground.");
    }

    static float Cross(Vector2 a, Vector2 b) { return a.x * b.y - a.y * b.x; }

    static float FindGroundHeight(GyroVector actorPose)
    {
        float bestHeight = 0;
        float bestDistance = float.MaxValue;
        foreach (var chart in UnityEngine.Object.FindObjectsByType<FarmChart>(FindObjectsSortMode.None))
        {
            if (!chart.name.StartsWith("Farm_")) continue;
            var filter = chart.GetComponent<MeshFilter>();
            if (!filter) continue;
            var tilePose = chart.GetComponent<HyperObject>().localGV;
            var relativePose = actorPose - tilePose;
            if (relativePose.vec.sqrMagnitude >= 1) continue;
            var center = HM.PoincareToUnit(relativePose.Point(), true);
            var point = new Vector2(center.x, center.z);
            var vertices = filter.sharedMesh.vertices;
            var triangles = filter.sharedMesh.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                var a = vertices[triangles[i]];
                var b = vertices[triangles[i + 1]];
                var c = vertices[triangles[i + 2]];
                var ab = new Vector2(b.x - a.x, b.z - a.z);
                var ac = new Vector2(c.x - a.x, c.z - a.z);
                var ap = point - new Vector2(a.x, a.z);
                float area = Cross(ab, ac);
                if (Mathf.Abs(area) < 1e-9f) continue;
                float wb = Cross(ap, ac) / area;
                float wc = Cross(ab, ap) / area;
                if (wb < -1e-5f || wc < -1e-5f || wb + wc > 1.00001f) continue;
                float height = a.y + wb * (b.y - a.y) + wc * (c.y - a.y);
                var groundPoint = new Vector3(center.x, height, center.z);
                var local = HM.PoincareToUnit((-actorPose) * (tilePose * HM.UnitToPoincare(groundPoint, true)), true);
                if (Mathf.Abs(local.y) < bestDistance)
                {
                    bestDistance = Mathf.Abs(local.y);
                    bestHeight = local.y;
                }
            }
        }
        return bestHeight;
    }

    static Vertex[] ReadVertices(Mesh mesh)
    {
        var vertices = mesh.vertices; var uv = mesh.uv; var uv2 = mesh.uv2;
        return vertices.Select((position, i) => new Vertex { position = position,
            uv = uv.Length > i ? uv[i] : Vector2.zero, uv2 = uv2.Length > i ? uv2[i] : Vector2.zero }).ToArray();
    }

    static Mesh SaveMesh(List<Vertex> triangles, string name)
    {
        var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.vertices = triangles.Select(v => v.position).ToArray();
        mesh.uv = triangles.Select(v => v.uv).ToArray();
        mesh.uv2 = triangles.Select(v => v.uv2).ToArray();
        mesh.triangles = Enumerable.Range(0, triangles.Count).ToArray();
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        string path = Generated + name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing) { EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh); return existing; }
        AssetDatabase.CreateAsset(mesh, path); return mesh;
    }
}
