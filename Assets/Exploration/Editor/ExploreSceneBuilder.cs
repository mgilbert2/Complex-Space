using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ExploreSceneBuilder
{
    public const string Root = "Assets/Exploration/";

    const string GeneratedDirectory = Root + "Generated/";
    const string RecoveredDirectory = Root + "Recovered/";
    const string SphericalMapPath = "Assets/Gopher/Recovered/Resources/Map3.bytes";
    const int GroundSubdivisions = 40;
    const int GrassTextureSize = 256;
    const int GrassNoiseSeed = 4821;
    const int GridSpacingPixels = 32;
    const float GrassNoiseAmplitude = 0.09f;
    const int ColumnCount = 6;
    const float ColumnDistance = 0.65f;
    const float TrampolineScale = 0.18f;
    const float PhysicalPlayerHeight = 0.07f;
    const float PlayerReferenceKleinScale = 0.5774f;

    static readonly Color[] ColumnColors =
    {
        new Color(0.85f, 0.25f, 0.18f),
        new Color(1, 0.67f, 0.13f),
        new Color(0.21f, 0.65f, 0.84f),
        new Color(0.68f, 0.33f, 0.8f),
        new Color(0.24f, 0.7f, 0.48f),
        new Color(0.95f, 0.67f, 0.73f)
    };

    [MenuItem("Tools/Complex Space/Build Exploration Scenes")]
    public static void BuildBoth()
    {
        Build(true);
        Build(false);
        AssetDatabase.SaveAssets();
        Debug.Log("[Explore] Built both exploration scenes.");
    }

    [MenuItem("Tools/Complex Space/Open Elliptic Exploration")]
    public static void OpenElliptic()
    {
        Open(true);
    }

    [MenuItem("Tools/Complex Space/Open Hyperbolic Exploration")]
    public static void OpenHyperbolic()
    {
        Open(false);
    }

    public static string PathFor(bool elliptic)
    {
        return Root + "Scenes/" + (elliptic ? "Elliptic" : "Hyperbolic") + " Exploration.unity";
    }

    static void Open(bool elliptic)
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EditorSceneManager.OpenScene(PathFor(elliptic));
        Shader.SetGlobalFloat("_Enable", 0);
        SceneView.lastActiveSceneView?.LookAt(Vector3.zero, Quaternion.Euler(55, 0, 0), 3);
    }

    static void Build(bool elliptic)
    {
        HM.SetTileType(elliptic ? 3 : 5);
        HM.UseRadialChart = false;
        Shader.SetGlobalFloat("_Enable", 0);
        Directory.CreateDirectory(Root + "Scenes");
        Directory.CreateDirectory(GeneratedDirectory);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var settingsName = elliptic ? "Elliptic exploration settings" : "Hyperbolic exploration settings";
        var stage = new GameObject(settingsName).AddComponent<ExploreSpace>();
        stage.elliptic = elliptic;
        stage.Configure();
        Shader.SetGlobalFloat("_Enable", 0);

        CreateGround(stage);
        CreateColumns(elliptic);
        CreateTrampoline(stage);
        CreateBall(stage);
        CreatePlayer(stage);
        CreateLighting();

        EditorSceneManager.SaveScene(scene, PathFor(elliptic));
        RegisterScene(PathFor(elliptic));
    }

    static void CreateGround(ExploreSpace stage)
    {
        string materialName = (stage.elliptic ? "Elliptic" : "Hyperbolic") + " Grass";
        stage.grass = CreateMaterial(materialName, stage.elliptic, Color.white, CreateGrassTexture());
        stage.groundMesh = CreateGroundMesh();

        var poses = GetGroundPoses(stage.elliptic);
        var terrain = new List<HyperObject>();
        for (int i = 0; i < poses.Count; i++)
        {
            var tile = new GameObject("Grass chart " + i);
            tile.AddComponent<MeshFilter>().sharedMesh = stage.groundMesh;
            tile.AddComponent<MeshRenderer>().sharedMaterial = stage.grass;

            var hyper = tile.AddComponent<HyperObject>();
            hyper.localGV = poses[i];
            hyper.allowCulling = false;
            terrain.Add(hyper);
        }
        stage.terrain = terrain.ToArray();
    }

    static List<GyroVector> GetGroundPoses(bool elliptic)
    {
        var poses = new List<GyroVector>();
        if (!elliptic)
        {
            poses.Add(GyroVector.identity);
            return poses;
        }

        // Map3 contains the recovered six ground-chart poses that close the
        // positive-curvature surface. Hyperbolic charts are streamed at runtime.
        using (var reader = new BinaryReader(File.OpenRead(SphericalMapPath)))
        {
            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                reader.ReadString();
                var position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                var rotation = new Quaternion(
                    reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                poses.Add(new GyroVector(position, rotation));
            }
        }
        return poses;
    }

    static void CreateColumns(bool elliptic)
    {
        string prefix = elliptic ? "E" : "H";
        var stone = CreateMaterial(prefix + " Stone", elliptic, new Color(0.72f, 0.73f, 0.68f));

        for (int i = 0; i < ColumnCount; i++)
        {
            float angle = i * Mathf.PI / 3;
            var column = new GameObject("Stone column " + (i + 1));

            // HyperEngine multiplies source mesh coordinates by KleinV. Cancel
            // that chart scale so landmarks have the same size in both scenes.
            column.transform.localScale = Vector3.one / HM.KLEIN_V;
            var hyper = column.AddComponent<HyperObject>();
            var direction = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
            hyper.localGV = new GyroVector(HM.HyperTranslate(direction * ColumnDistance));
            hyper.allowCulling = false;

            CreateShape("Stone base", PrimitiveType.Cylinder, column.transform,
                new Vector3(0, 0.013f, 0), new Vector3(0.15f, 0.013f, 0.15f), stone);
            CreateShape("Stone shaft", PrimitiveType.Cylinder, column.transform,
                new Vector3(0, 0.19f, 0), new Vector3(0.085f, 0.17f, 0.085f), stone);
            CreateShape("Stone capital", PrimitiveType.Cube, column.transform,
                new Vector3(0, 0.37f, 0), new Vector3(0.15f, 0.035f, 0.15f), stone);

            var accent = CreateMaterial(prefix + " Column " + i, elliptic, ColumnColors[i]);
            CreateShape("Colored landmark", PrimitiveType.Sphere, column.transform,
                new Vector3(0, 0.425f, 0), Vector3.one * 0.075f, accent);

            column.AddComponent<WarpCollider>().boundingCylinders = new[]
            {
                new WarpCollider.Cylinder
                {
                    p1 = Vector3.zero,
                    p2 = Vector3.up * 0.39f,
                    radius = 0.065f,
                    capped = true
                }
            };
        }
    }

    static void CreateTrampoline(ExploreSpace stage)
    {
        var trampoline = new GameObject("Original Hyperbolica trampoline");
        trampoline.AddComponent<MeshFilter>().sharedMesh =
            AssetDatabase.LoadAssetAtPath<Mesh>(RecoveredDirectory + "Plain_Trampoline.asset");

        string materialName = (stage.elliptic ? "E" : "H") + " Trampoline";
        var palette = AssetDatabase.LoadAssetAtPath<Texture2D>(RecoveredDirectory + "Plain_Pallette.png");
        var material = CreateMaterial(materialName, stage.elliptic, Color.white, palette);

        // The recovered mesh uses the palette for color and its separate red
        // texture as baked AO; using that AO image as albedo would turn it red.
        material.SetTexture("_AOTex",
            AssetDatabase.LoadAssetAtPath<Texture2D>(RecoveredDirectory + "Plain_Trampoline.png"));
        trampoline.AddComponent<MeshRenderer>().sharedMaterial = material;
        trampoline.transform.localScale = Vector3.one * TrampolineScale / HM.KLEIN_V;

        stage.trampoline = trampoline.AddComponent<HyperObject>();
        stage.trampoline.allowCulling = false;
        stage.bounceSound = AssetDatabase.LoadAssetAtPath<AudioClip>(RecoveredDirectory + "trampoline.ogg");
    }

    static void CreateBall(ExploreSpace stage)
    {
        var ballObject = new GameObject("Kickable surface ball");
        var ball = ballObject.AddComponent<SurfaceBall>();
        ball.hyper = ballObject.AddComponent<HyperObject>();
        ball.hyper.allowCulling = false;
        ball.hyper.localGV = new GyroVector(HM.HyperTranslate(0.12f, 0, -0.22f));

        string prefix = stage.elliptic ? "E" : "H";
        var material = CreateMaterial(prefix + " Ball", stage.elliptic, new Color(0.98f, 0.81f, 0.24f));
        var position = new Vector3(0, SurfaceBall.Radius / HM.KLEIN_V, 0);
        var scale = Vector3.one * SurfaceBall.Radius * 2 / HM.KLEIN_V;
        var visual = CreateShape("Rolling ball", PrimitiveType.Sphere, ballObject.transform,
            position, scale, material);
        ball.visual = visual.transform;

        var stripe = CreateMaterial(prefix + " Ball stripe", stage.elliptic, new Color(0.15f, 0.2f, 0.3f));
        CreateShape("Rotation marker", PrimitiveType.Sphere, visual.transform,
            new Vector3(0, 0.43f, 0), Vector3.one * 0.3f, stripe);
        stage.ball = ball;
    }

    static void CreatePlayer(ExploreSpace stage)
    {
        var playerObject = new GameObject("Player");
        var cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.SetParent(playerObject.transform);
        cameraObject.transform.localPosition = Vector3.up * 1.5f;

        var camera = cameraObject.AddComponent<Camera>();
        camera.nearClipPlane = 0.001f;
        camera.farClipPlane = 100;
        camera.fieldOfView = 80;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.67f, 0.83f, 0.94f);
        cameraObject.AddComponent<AudioListener>();
        cameraObject.AddComponent<DisableCameraCull>();

        stage.player = playerObject.AddComponent<Player>();
        stage.player.mainCamera = cameraObject;
        // Player.Awake applies KleinV/0.5774 to serialized height. Compensate
        // here to preserve the same physical player size in both geometries.
        stage.player.height = PhysicalPlayerHeight * PlayerReferenceKleinScale / HM.KLEIN_V;
        stage.player.walkingSpeed = 2.5f;
        stage.player.jumpSpeed = 2.2f;
        stage.player.startHyper = HM.HyperTranslate(0, 0, -0.36f);
    }

    static void CreateLighting()
    {
        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.transform.rotation = Quaternion.Euler(50, -25, 0);
        RenderSettings.ambientLight = new Color(0.8f, 0.85f, 0.8f);
        RenderSettings.skybox = null;
    }

    static void RegisterScene(string path)
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (!scenes.Any(scene => scene.path == path))
            scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    static T SaveGeneratedAsset<T>(T asset, string name) where T : UnityEngine.Object
    {
        string path = GeneratedDirectory + name;
        var existing = AssetDatabase.LoadMainAssetAtPath(path);
        if (existing)
        {
            // Keep the existing asset and GUID so saved scene references survive a rebuild.
            EditorUtility.CopySerialized(asset, existing);
            UnityEngine.Object.DestroyImmediate(asset);
        }
        else
        {
            AssetDatabase.CreateAsset(asset, path);
        }
        return AssetDatabase.LoadAssetAtPath<T>(path);
    }

    static Material CreateMaterial(string name, bool elliptic, Color color, Texture texture = null)
    {
        string shaderName = elliptic ? "Custom/SphericalShader" : "Custom/HyperbolicShader";
        var material = new Material(Shader.Find(shaderName));
        material.SetColor("_Color", color);
        material.SetFloat("_Ambient", 0.8f);
        material.SetFloat("_BoundaryAO", 1);
        if (texture) material.mainTexture = texture;
        return SaveGeneratedAsset(material, name + ".mat");
    }

    static GameObject CreateShape(string name, PrimitiveType type, Transform parent,
        Vector3 position, Vector3 scale, Material material)
    {
        var shape = GameObject.CreatePrimitive(type);
        shape.name = name;
        shape.transform.SetParent(parent, false);
        shape.transform.localPosition = position;
        shape.transform.localScale = scale;

        // Curved-space collision uses WarpCollider instead of Unity's flat physics shapes.
        UnityEngine.Object.DestroyImmediate(shape.GetComponent<Collider>());
        shape.GetComponent<Renderer>().sharedMaterial = material;
        return shape;
    }

    static Mesh CreateGroundMesh()
    {
        int rowSize = GroundSubdivisions + 1;
        var vertices = new Vector3[rowSize * rowSize];
        var uv = new Vector2[vertices.Length];
        var colors = new Color[vertices.Length];
        var triangles = new List<int>();

        for (int z = 0; z <= GroundSubdivisions; z++)
        {
            for (int x = 0; x <= GroundSubdivisions; x++)
            {
                int index = z * rowSize + x;
                vertices[index] = new Vector3(
                    -1 + 2f * x / GroundSubdivisions, 0, -1 + 2f * z / GroundSubdivisions);
                uv[index] = new Vector2(x / (float)GroundSubdivisions, z / (float)GroundSubdivisions);
                colors[index] = Color.white;

                if (x >= GroundSubdivisions || z >= GroundSubdivisions) continue;
                int a = index;
                int b = index + 1;
                int c = index + rowSize;
                int d = c + 1;
                triangles.AddRange(new[] { a, c, b, b, c, d });
            }
        }

        var mesh = new Mesh { name = "Subdivided curved-space grass" };
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.colors = colors;
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return SaveGeneratedAsset(mesh, "GrassSurface.asset");
    }

    static Texture2D CreateGrassTexture()
    {
        var texture = new Texture2D(GrassTextureSize, GrassTextureSize)
        {
            name = "Grass with orientation grid",
            wrapMode = TextureWrapMode.Repeat
        };
        var random = new System.Random(GrassNoiseSeed);

        // Grass remains visible between narrow, regular orientation lines.
        for (int y = 0; y < GrassTextureSize; y++)
        {
            for (int x = 0; x < GrassTextureSize; x++)
            {
                float noise = (float)random.NextDouble() * GrassNoiseAmplitude;
                bool gridLine = x % GridSpacingPixels < 1 || y % GridSpacingPixels < 1;
                var color = gridLine
                    ? new Color(0.48f, 0.57f, 0.32f)
                    : new Color(0.27f + noise, 0.43f + noise, 0.16f + noise);
                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply();
        return SaveGeneratedAsset(texture, "GrassGrid.asset");
    }
}
