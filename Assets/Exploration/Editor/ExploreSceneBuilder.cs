using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ExploreSceneBuilder
{
    public const string Root="Assets/Exploration/";
    [MenuItem("Tools/Complex Space/Build Exploration Scenes")]
    public static void BuildBoth()
    {
        Build(true); Build(false);
        AssetDatabase.SaveAssets();
        Debug.Log("[Explore] Built both exploration scenes.");
    }
    [MenuItem("Tools/Complex Space/Open Elliptic Exploration")]
    public static void OpenElliptic(){Open(true);}
    [MenuItem("Tools/Complex Space/Open Hyperbolic Exploration")]
    public static void OpenHyperbolic(){Open(false);}
    static void Open(bool elliptic)
    {
        if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        EditorSceneManager.OpenScene(PathFor(elliptic));
        Shader.SetGlobalFloat("_Enable",0);
        SceneView.lastActiveSceneView?.LookAt(Vector3.zero,Quaternion.Euler(55,0,0),3);
    }
    public static string PathFor(bool elliptic)=>Root+"Scenes/"+(elliptic?"Elliptic":"Hyperbolic")+" Exploration.unity";
    static void Save(UnityEngine.Object asset,string name)
    {
        string path=Root+"Generated/"+name;
        var existing=AssetDatabase.LoadMainAssetAtPath(path);
        if(existing){EditorUtility.CopySerialized(asset,existing);UnityEngine.Object.DestroyImmediate(asset);}
        else AssetDatabase.CreateAsset(asset,path);
    }
    static Material Material(string name,bool elliptic,Color color,Texture texture=null)
    {
        string path=Root+"Generated/"+name+".mat";
        var material=new Material(Shader.Find(elliptic?"Custom/SphericalShader":"Custom/HyperbolicShader"));
        material.SetColor("_Color",color);material.SetFloat("_Ambient",.8f);
        material.SetFloat("_BoundaryAO",1);
        if(texture)material.mainTexture=texture;
        Save(material,name+".mat");return AssetDatabase.LoadAssetAtPath<Material>(path);
    }
    static GameObject Shape(string name,PrimitiveType type,Transform parent,Vector3 pos,Vector3 scale,Material material)
    {
        var obj=GameObject.CreatePrimitive(type);obj.name=name;obj.transform.SetParent(parent,false);
        obj.transform.localPosition=pos;obj.transform.localScale=scale;
        UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
        obj.GetComponent<Renderer>().sharedMaterial=material;
        return obj;
    }
    static Mesh Ground()
    {
        const int n=40;
        var vertices=new Vector3[(n+1)*(n+1)];var uv=new Vector2[vertices.Length];
        var colors=new Color[vertices.Length];var triangles=new List<int>();
        for(int z=0;z<=n;z++)for(int x=0;x<=n;x++)
        {
            int i=z*(n+1)+x;vertices[i]=new Vector3(-1+2f*x/n,0,-1+2f*z/n);
            uv[i]=new Vector2(x/(float)n,z/(float)n);colors[i]=Color.white;
            if(x<n&&z<n){int a=i,b=i+1,c=i+n+1,d=c+1;triangles.AddRange(new[]{a,c,b,b,c,d});}
        }
        var mesh=new Mesh{name="Subdivided curved-space grass"};mesh.vertices=vertices;mesh.uv=uv;mesh.colors=colors;
        mesh.triangles=triangles.ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();
        Save(mesh,"GrassSurface.asset");return AssetDatabase.LoadAssetAtPath<Mesh>(Root+"Generated/GrassSurface.asset");
    }
    static Texture2D GrassTexture()
    {
        // Grass remains visible between narrow, regular orientation lines.
        var texture=new Texture2D(256,256){name="Grass with orientation grid",wrapMode=TextureWrapMode.Repeat};
        var random=new System.Random(4821);
        for(int y=0;y<256;y++)for(int x=0;x<256;x++)
        {
            float noise=(float)random.NextDouble()*.09f;
            bool line=x%32<1||y%32<1;
            texture.SetPixel(x,y,line?new Color(.48f,.57f,.32f):new Color(.27f+noise,.43f+noise,.16f+noise));
        }
        texture.Apply();Save(texture,"GrassGrid.asset");
        return AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"Generated/GrassGrid.asset");
    }
    static void Build(bool elliptic)
    {
        HM.SetTileType(elliptic?3:5);HM.UseRadialChart=false;
        Shader.SetGlobalFloat("_Enable",0);
        Directory.CreateDirectory(Root+"Scenes");Directory.CreateDirectory(Root+"Generated");
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var stage=new GameObject(elliptic?"Elliptic exploration settings":"Hyperbolic exploration settings").AddComponent<ExploreSpace>();
        stage.elliptic=elliptic;stage.Configure();Shader.SetGlobalFloat("_Enable",0);
        var grass=Material((elliptic?"Elliptic":"Hyperbolic")+" Grass",elliptic,Color.white,GrassTexture());
        stage.grass=grass;stage.groundMesh=Ground();
        var poses=new List<GyroVector>();
        if(elliptic)
        {
            using(var reader=new BinaryReader(File.OpenRead("Assets/Gopher/Recovered/Resources/Map3.bytes")))
                while(reader.BaseStream.Position<reader.BaseStream.Length)
                {
                    reader.ReadString();var p=new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());
                    poses.Add(new GyroVector(p,new Quaternion(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle())));
                }
        }
        else poses.Add(GyroVector.identity);
        stage.terrain=poses.Select((pose,i)=>
        {
            var obj=new GameObject("Grass chart "+i);obj.AddComponent<MeshFilter>().sharedMesh=stage.groundMesh;
            obj.AddComponent<MeshRenderer>().sharedMaterial=grass;
            var ho=obj.AddComponent<HyperObject>();ho.localGV=pose;ho.allowCulling=false;return ho;
        }).ToArray();

        var stone=Material((elliptic?"E":"H")+" Stone",elliptic,new Color(.72f,.73f,.68f));
        Color[] colors={new Color(.85f,.25f,.18f),new Color(1,.67f,.13f),new Color(.21f,.65f,.84f),new Color(.68f,.33f,.8f),new Color(.24f,.7f,.48f),new Color(.95f,.67f,.73f)};
        for(int i=0;i<6;i++)
        {
            float angle=i*Mathf.PI/3;
            var root=new GameObject("Stone column "+(i+1));
            root.transform.localScale=Vector3.one/HM.KLEIN_V;
            var ho=root.AddComponent<HyperObject>();
            ho.localGV=new GyroVector(HM.HyperTranslate(new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle))*.65f));
            ho.allowCulling=false;
            Shape("Stone base",PrimitiveType.Cylinder,root.transform,new Vector3(0,.013f,0),new Vector3(.15f,.013f,.15f),stone);
            Shape("Stone shaft",PrimitiveType.Cylinder,root.transform,new Vector3(0,.19f,0),new Vector3(.085f,.17f,.085f),stone);
            Shape("Stone capital",PrimitiveType.Cube,root.transform,new Vector3(0,.37f,0),new Vector3(.15f,.035f,.15f),stone);
            var accent=Material((elliptic?"E":"H")+" Column "+i,elliptic,colors[i]);
            Shape("Colored landmark",PrimitiveType.Sphere,root.transform,new Vector3(0,.425f,0),Vector3.one*.075f,accent);
            root.AddComponent<WarpCollider>().boundingCylinders=new[]{new WarpCollider.Cylinder{p1=Vector3.zero,p2=Vector3.up*.39f,radius=.065f,capped=true}};
        }
        var tramp=new GameObject("Original Hyperbolica trampoline");
        var trampMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"Recovered/Plain_Trampoline.asset");
        tramp.AddComponent<MeshFilter>().sharedMesh=trampMesh;
        var trampolineMaterial=Material((elliptic?"E":"H")+" Trampoline",elliptic,Color.white,AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"Recovered/Plain_Pallette.png"));
        trampolineMaterial.SetTexture("_AOTex",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"Recovered/Plain_Trampoline.png"));
        tramp.AddComponent<MeshRenderer>().sharedMaterial=trampolineMaterial;
        tramp.transform.localScale=Vector3.one*.18f/HM.KLEIN_V;
        stage.trampoline=tramp.AddComponent<HyperObject>();stage.trampoline.allowCulling=false;
        stage.bounceSound=AssetDatabase.LoadAssetAtPath<AudioClip>(Root+"Recovered/trampoline.ogg");

        var ballRoot=new GameObject("Kickable surface ball");var ball=ballRoot.AddComponent<SurfaceBall>();
        ball.hyper=ballRoot.AddComponent<HyperObject>();ball.hyper.allowCulling=false;
        ball.hyper.localGV=new GyroVector(HM.HyperTranslate(.12f,0,-.22f));
        var ballMat=Material((elliptic?"E":"H")+" Ball",elliptic,new Color(.98f,.81f,.24f));
        var ballVisual=Shape("Rolling ball",PrimitiveType.Sphere,ballRoot.transform,new Vector3(0,SurfaceBall.Radius/HM.KLEIN_V,0),Vector3.one*SurfaceBall.Radius*2/HM.KLEIN_V,ballMat);
        ball.visual=ballVisual.transform;
        var stripe=Material((elliptic?"E":"H")+" Ball stripe",elliptic,new Color(.15f,.2f,.3f));
        Shape("Rotation marker",PrimitiveType.Sphere,ballVisual.transform,new Vector3(0,.43f,0),Vector3.one*.3f,stripe);
        stage.ball=ball;
        var playerObject=new GameObject("Player");var camObj=new GameObject("Main Camera");
        camObj.tag="MainCamera";camObj.transform.SetParent(playerObject.transform);camObj.transform.localPosition=Vector3.up*1.5f;
        var cam=camObj.AddComponent<Camera>();cam.nearClipPlane=.001f;cam.farClipPlane=100;cam.fieldOfView=80;
        cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.67f,.83f,.94f);
        camObj.AddComponent<AudioListener>();camObj.AddComponent<DisableCameraCull>();
        stage.player=playerObject.AddComponent<Player>();stage.player.mainCamera=camObj;
        // Player.Awake applies KleinV/.5774; preserve the same physical size in both spaces.
        stage.player.height=.07f*.5774f/HM.KLEIN_V;stage.player.walkingSpeed=2.5f;stage.player.jumpSpeed=2.2f;
        stage.player.startHyper=HM.HyperTranslate(0,0,-.36f);
        var sun=new GameObject("Sun").AddComponent<Light>();sun.type=LightType.Directional;sun.transform.rotation=Quaternion.Euler(50,-25,0);
        RenderSettings.ambientLight=new Color(.8f,.85f,.8f);RenderSettings.skybox=null;
        EditorSceneManager.SaveScene(scene,PathFor(elliptic));
        var scenes=EditorBuildSettings.scenes.ToList();
        if(!scenes.Any(s=>s.path==PathFor(elliptic)))scenes.Add(new EditorBuildSettingsScene(PathFor(elliptic),true));
        EditorBuildSettings.scenes=scenes.ToArray();
    }
}
