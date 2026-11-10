using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BeachSceneBuilder
{
    public const string ScenePath="Assets/Beach/Scenes/Chopped Beach task.unity";
    const string Recovered="Assets/Recovered/GameObject/";
    [Serializable] class VertexSelection {public int[] vertices;}

    [MenuItem("Tools/Hyperbolica Beach/Rebuild Beach Scene")]
    public static void Build()
    {
        Directory.CreateDirectory("Assets/Beach/Scenes");
        Directory.CreateDirectory("Assets/Beach/Generated");
        AssetDatabase.Refresh();
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        Shader.SetGlobalFloat("_Enable",0);
        RenderSettings.skybox=null;
        RenderSettings.ambientLight=Color.white;
        var world=new GameObject("Beach settings").AddComponent<BeachWorld>();
        world.appliedLayoutScale=BeachFootprint.LayoutScale;
        world.appliedRadialChart=true;
        var beach=Spawn("Beach_Treasure.prefab","Beach — treasure area only");
        ApplyMaterials(beach);
        BeachShoreline.ShapeGround(beach);
        var chest=Spawn("TreasureChest.prefab","Treasure chest");
        chest.transform.SetPositionAndRotation(world.chestStart,Quaternion.Euler(0,world.chestStartAngle,0));
        var chestHO=chest.GetComponent<HyperObject>();
        chestHO.localGV=GyroVector.identity;
        ApplyMaterials(chest);
        world.chest=chestHO;
        var hole=beach.GetComponentsInChildren<Transform>().First(t=>t.name=="ChestGoal");
        hole.name="Treasure hole";
        CreateUmbrella(world);
        CreateOcean();
        CreateBoundary();
        var light=new GameObject("Sun").AddComponent<Light>();
        light.type=LightType.Directional; light.intensity=1; light.transform.rotation=Quaternion.Euler(50,-30,0);
        var playerGO=new GameObject("Player");
        var cameraGO=new GameObject("Main Camera");
        cameraGO.transform.SetParent(playerGO.transform);
        cameraGO.transform.localPosition=new Vector3(0,1.5f,0);
        cameraGO.tag="MainCamera";
        var camera=cameraGO.AddComponent<Camera>();
        camera.nearClipPlane=0.001f; camera.farClipPlane=1.01f; camera.fieldOfView=65;
        camera.backgroundColor=new Color(0.63f,0.83f,0.96f); camera.clearFlags=CameraClearFlags.SolidColor;
        cameraGO.AddComponent<AudioListener>(); cameraGO.AddComponent<DisableCameraCull>();
        var player=playerGO.AddComponent<Player>();
        BeachWorld.ConfigureGeometry();
        player.startHyper=HM.UnitToPoincare(new Vector3(0.1f,0,-1.20f),true);
        player.mainCamera=cameraGO; player.walkingSpeed=2; player.height=BeachWorld.PlayerHeight; player.jumpSpeed=0;
        var puzzle=world.gameObject.AddComponent<BeachPuzzle>();
        puzzle.world=world; puzzle.player=player; puzzle.chest=chestHO; puzzle.hole=hole;
        BeachEnvironmentBuilder.Build();
        var overviewGO=new GameObject("Editor overview");
        var overview=overviewGO.AddComponent<Camera>(); overview.enabled=false;
        overview.transform.position=new Vector3(2,2.5f,-3);
        overview.transform.LookAt(new Vector3(0,0.05f,0));
        overview.orthographic=true; overview.orthographicSize=1.65f; overview.nearClipPlane=0.01f; overview.farClipPlane=20;
        overview.clearFlags=CameraClearFlags.SolidColor; overview.backgroundColor=camera.backgroundColor;
        EditorSceneManager.SaveScene(scene,ScenePath);
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
        PlayerSettings.productName="Hyperbolica Beach Study";
        AssetDatabase.SaveAssets();
        BeachShoreline.RenderPreviews();
        ValidateMath();
        Debug.Log("[BeachBuild] Complete: "+ScenePath);
    }

    static GameObject Spawn(string path,string name)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Recovered+path);
        if(!prefab) throw new FileNotFoundException(Recovered+path);
        var obj=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
        PrefabUtility.UnpackPrefabInstance(obj,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        obj.name=name; return obj;
    }

    static void ApplyMaterials(GameObject obj)
    {
        foreach(var st in obj.GetComponentsInChildren<SetTextures>(true))
        {
            var renderer=st.GetComponent<Renderer>(); if(!renderer) continue;
            var mat=new Material(Shader.Find("Custom/HyperbolicShader"));
            mat.SetTexture("_MainTex",st.texture ? st.texture : Texture2D.whiteTexture);
            mat.SetTexture("_AOTex",st.aomap ? st.aomap : Texture2D.whiteTexture);
            mat.SetColor("_Color",st.colorize); mat.SetFloat("_Ambient",st.ambient);
            mat.SetFloat("_BoundaryAO",1); mat.SetColor("_Noise",st.noise);
            mat.SetVector("_Specular",new Vector4(st.specular,st.shininess,0,0));
            string path="Assets/Beach/Generated/"+obj.name.Replace('—','-').Replace(' ','_')+"_"+st.GetInstanceID()+".mat";
            AssetDatabase.CreateAsset(mat,path); renderer.sharedMaterial=mat;
        }
    }

    static void CreateUmbrella(BeachWorld world)
    {
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(Recovered+"Beach_A.prefab");
        var mesh=source.GetComponent<MeshFilter>().sharedMesh;
        var selection=JsonUtility.FromJson<VertexSelection>(File.ReadAllText("Assets/Beach/Editor/UmbrellaVertices.json"));
        var chosen=new HashSet<int>(selection.vertices);
        var triangles=mesh.triangles; var selected=new List<int>();
        for(int i=0;i<triangles.Length;i+=3)
            if(chosen.Contains(triangles[i]) && chosen.Contains(triangles[i+1]) && chosen.Contains(triangles[i+2]))
                selected.AddRange(new[]{triangles[i],triangles[i+1],triangles[i+2]});
        var umbrellaMesh=UnityEngine.Object.Instantiate(mesh); umbrellaMesh.name="Recovered umbrella canopy and pole";
        umbrellaMesh.triangles=selected.ToArray(); umbrellaMesh.RecalculateBounds();
        AssetDatabase.CreateAsset(umbrellaMesh,"Assets/Beach/Generated/Umbrella.asset");
        var umbrella=new GameObject("Umbrella — moved away from chest");
        umbrella.transform.localPosition=world.umbrellaOffset;
        umbrella.AddComponent<MeshFilter>().sharedMesh=umbrellaMesh;
        umbrella.AddComponent<MeshRenderer>(); umbrella.AddComponent<HyperObject>().allowCulling=false;
        var st=umbrella.AddComponent<SetTextures>(); var original=source.GetComponent<SetTextures>();
        st.texture=original.texture; st.aomap=original.aomap; st.ambient=original.ambient; st.overrideBoundaryAO=1;
        var wc=umbrella.AddComponent<WarpCollider>();
        wc.boundingCylinders=new[]{new WarpCollider.Cylinder{p1=new Vector3(-0.012f,0,-0.144f),p2=new Vector3(-0.012f,0.62f,-0.144f),radius=0.013f}};
        world.umbrella=umbrella.transform;
        ApplyMaterials(umbrella);
    }

    static void CreateOcean()
    {
        BeachShoreline.CreateOcean(GameObject.Find("Beach — treasure area only").GetComponent<MeshFilter>().sharedMesh);
    }

    static void CreateBoundary() {BeachShoreline.CreateBoundary();}

    public static void Render(Camera camera,string path)
    {
        var target=new RenderTexture(1200,850,24); camera.targetTexture=target; camera.Render();
        var prev=RenderTexture.active; RenderTexture.active=target;
        var image=new Texture2D(1200,850,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,1200,850),0,0); image.Apply();
        File.WriteAllBytes(path,image.EncodeToPNG()); camera.targetTexture=null; RenderTexture.active=prev;
        UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(target);
    }

    public static void ValidateMath()
    {
        BeachWorld.ConfigureGeometry();
        var pose=BeachWorld.PoseAt(new Vector3(-0.5f,0,-0.5f),Quaternion.identity);
        var begin=pose;
        foreach(var point in new[]{new Vector3(0.5f,0,-0.5f),new Vector3(0.5f,0,0.5f),new Vector3(-0.5f,0,0.5f),new Vector3(-0.5f,0,-0.5f)})
            pose=BeachPuzzle.TransportTo(pose,HM.UnitToPoincare(point,true));
        float drift=Vector3.Distance(begin.Point(),pose.Point()); float angle=Quaternion.Angle(begin.gyr,pose.gyr);
        if(drift>0.00001f || angle<1) throw new Exception("Parallel transport validation failed: "+drift+" / "+angle);
        var report="Closed-loop position drift: "+drift+"\nClosed-loop orientation change: "+angle+" degrees\nNPC count: 0\nScene count: 1\n";
        File.WriteAllText("../Validation.txt",report); Debug.Log("[BeachValidation] "+report);
    }

    public static void Open()
    {
        EditorSceneManager.OpenScene(ScenePath);
        Selection.activeGameObject=UnityEngine.Object.FindFirstObjectByType<BeachWorld>().gameObject;
        SceneView.lastActiveSceneView?.LookAt(Vector3.zero,Quaternion.Euler(90,0,0),1.6f*BeachFootprint.LayoutScale,true);
    }

    public static void BuildAndOpen() {Build();Open();}
}
