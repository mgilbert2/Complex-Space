using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BeachShoreline
{
    const string Generated="Assets/Beach/Generated/";
    public static readonly Color WaterColor=new Color(.12f,.56f,.7f);

    public static Mesh ShapeGround(GameObject beach)
    {
        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Recovered/GameObject/Beach_Treasure.prefab").GetComponent<MeshFilter>().sharedMesh;
        Mesh mesh=BeachMeshClipper.Clip(source);
        AssetDatabase.CreateAsset(mesh,Generated+"TaperedBeach.asset");
        beach.GetComponent<MeshFilter>().sharedMesh=mesh;
        return mesh;
    }

    public static void CreateOcean(Mesh ground)
    {
        var p=BeachFootprint.Points; var outer=BeachFootprint.Offset(.22f);
        var groundVertices=ground.vertices;var groundIndices=ground.triangles;
        var waterVertices=new List<Vector3>();var waterTriangles=new List<int>();
        var edgeVertices=new List<Vector3>();var edgeTriangles=new List<int>();
        for(int i=0;i<p.Length;i++)
        {
            int j=(i+1)%p.Length;
            int count=Mathf.Max(2,Mathf.CeilToInt(Vector2.Distance(p[i],p[j])*30));
            for(int segment=0;segment<count;segment++)
            {
                float t=(float)segment/count,u=(float)(segment+1)/count;
                var a=Vector2.Lerp(p[i],p[j],t);var b=Vector2.Lerp(p[i],p[j],u);
                var c=Vector2.Lerp(outer[i],outer[j],t);var d=Vector2.Lerp(outer[i],outer[j],u);
                Quad(waterVertices,waterTriangles,At(a,-.075f),At(b,-.075f),At(d,-.075f),At(c,-.075f));
                Quad(edgeVertices,edgeTriangles,At(a,GroundHeight(a,groundVertices,groundIndices)),At(b,GroundHeight(b,groundVertices,groundIndices)),At(b,-.085f),At(a,-.085f));
            }
        }
        var water=new Material(Shader.Find("Beach/Hyperbolic Ocean"));water.SetColor("_Color",new Color(.025f,.52f,.63f));water.SetColor("_DeepColor",new Color(.012f,.09f,.22f));
        AssetDatabase.CreateAsset(water,Generated+"Ocean.mat");
        Surface("Ocean — tapered shoreline","OceanRing",waterVertices,waterTriangles,water);
        Surface("Cut beach edge","SandEdge",edgeVertices,edgeTriangles,Material("SandEdge",new Color(.58f,.42f,.23f)));
        waterVertices.Clear();waterTriangles.Clear();
        const float step=.18f,radius=9.5f;
        for(float x=-radius;x<radius;x+=step)for(float z=-radius;z<radius;z+=step)
        {
            var a=new Vector2(x,z);var b=new Vector2(x,z+step);var c=new Vector2(x+step,z+step);var d=new Vector2(x+step,z);
            if(a.sqrMagnitude>radius*radius || b.sqrMagnitude>radius*radius || c.sqrMagnitude>radius*radius || d.sqrMagnitude>radius*radius)continue;
            if(BeachFootprint.Contains(a) || BeachFootprint.Contains(b) || BeachFootprint.Contains(c) || BeachFootprint.Contains(d))continue;
            Quad(waterVertices,waterTriangles,At(a,-.075f),At(b,-.075f),At(c,-.075f),At(d,-.075f));
        }
        Surface("Ocean — open water to horizon","OpenOcean",waterVertices,waterTriangles,water);
        waterVertices.Clear();waterTriangles.Clear();var foamInner=BeachFootprint.Offset(.035f);var foamOuter=BeachFootprint.Offset(.075f);
        for(int i=0;i<p.Length;i++)
        {
            int j=(i+1)%p.Length;
            Quad(waterVertices,waterTriangles,At(foamInner[i],-.060f),At(foamInner[j],-.060f),At(foamOuter[j],-.060f),At(foamOuter[i],-.060f));
        }
        Surface("White surf at shoreline","ShoreFoam",waterVertices,waterTriangles,Material("SeaFoam",new Color(.80f,.96f,.92f)));
    }

    public static void CreateBoundary()
    {
        var boundary=new GameObject("Playable beach boundary — sketch outline");
        boundary.AddComponent<HyperObject>().allowCulling=false;
        var wc=boundary.AddComponent<WarpCollider>();wc.removeFloorTriangles=false;
        var p=BeachFootprint.Points;var boxes=new List<WarpCollider.Box>();
        for(int i=0;i<p.Length;i++)
        {
            var from=p[i];var to=p[(i+1)%p.Length];int count=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(from,to)/.12f));
            for(int piece=0;piece<count;piece++)
            {
                var a=Vector2.Lerp(from,to,(float)piece/count);var b=Vector2.Lerp(from,to,(float)(piece+1)/count);var d=b-a;
                var outward=new Vector2(d.y,-d.x).normalized;var center=(a+b)*.5f+outward*.045f;
                boxes.Add(new WarpCollider.Box{pos=At(center,.25f),rot=new Vector3(0,Mathf.Atan2(d.x,d.y)*Mathf.Rad2Deg,0),size=new Vector3(.09f,.7f,d.magnitude+.065f)});
            }
        }
        wc.boundingBoxes=boxes.ToArray();
    }

    static Vector3 At(Vector2 p,float height) {return new Vector3(p.x,height,p.y);}
    static void Quad(List<Vector3> vertices,List<int> triangles,Vector3 a,Vector3 b,Vector3 c,Vector3 d)
    {
        int n=vertices.Count;vertices.AddRange(new[]{a,b,c,d});triangles.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
    }
    static Material Material(string name,Color color)
    {
        var mat=new Material(Shader.Find("Custom/HyperbolicShader"));mat.SetColor("_Color",color);mat.SetFloat("_Ambient",1);mat.SetFloat("_BoundaryAO",1);
        AssetDatabase.CreateAsset(mat,Generated+name+".mat");return mat;
    }
    static void Surface(string name,string file,List<Vector3> vertices,List<int> triangles,Material material)
    {
        var mesh=new Mesh{name=file,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();
        AssetDatabase.CreateAsset(mesh,Generated+file+".asset");
        var obj=new GameObject(name);obj.AddComponent<MeshFilter>().sharedMesh=mesh;obj.AddComponent<MeshRenderer>().sharedMaterial=material;
        obj.AddComponent<HyperObject>().allowCulling=false;
    }

    static float GroundHeight(Vector2 point,Vector3[] positions,int[] triangles)
    {
        float best=1000;
        for(int i=0;i<triangles.Length;i+=3)
        {
            var a=positions[triangles[i]];var b=positions[triangles[i+1]];var c=positions[triangles[i+2]];
            var ab=new Vector2(b.x-a.x,b.z-a.z);var ac=new Vector2(c.x-a.x,c.z-a.z);var ap=point-new Vector2(a.x,a.z);
            float denominator=BeachFootprint.Cross(ab,ac);if(Mathf.Abs(denominator)<1e-10f)continue;
            float u=BeachFootprint.Cross(ap,ac)/denominator,v=BeachFootprint.Cross(ab,ap)/denominator;
            if(u<-.0001f || v<-.0001f || u+v>1.0001f) continue;
            float height=a.y+u*(b.y-a.y)+v*(c.y-a.y);
            if(Mathf.Abs(height)<Mathf.Abs(best))best=height;
            if(Mathf.Abs(best)<1e-7f)return best;
        }
        return best==1000?0:best;
    }

    [MenuItem("Tools/Hyperbolica Beach/Apply Sketched Shoreline")]
    public static void ApplyToOpenScene()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Apply the shoreline in Edit Mode.");
        var scene=EditorSceneManager.GetSceneByPath(BeachSceneBuilder.ScenePath);
        if(!scene.isLoaded)scene=EditorSceneManager.OpenScene(BeachSceneBuilder.ScenePath);
        EditorSceneManager.SaveScene(scene);
        Directory.CreateDirectory("../Backups");
        string backup="../Backups/BeachTreasure-before-outline-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+".unity";
        File.Copy(BeachSceneBuilder.ScenePath,backup);
        var world=UnityEngine.Object.FindFirstObjectByType<BeachWorld>();
        GameObject beach=null;
        foreach(var root in scene.GetRootGameObjects())
        {
            if(root.name=="Beach — treasure area only")beach=root;
            if(root.name.StartsWith("Ocean —") || root.name=="Cut beach edge" || root.name=="White surf at shoreline" || root.name.StartsWith("Playable beach boundary —"))
                UnityEngine.Object.DestroyImmediate(root);
        }
        if(!beach)throw new InvalidOperationException("The recovered beach object was not found.");
        var ground=ShapeGround(beach);CreateOcean(ground);CreateBoundary();
        if(world.umbrella)world.umbrella.localPosition=world.umbrellaOffset;
        var player=UnityEngine.Object.FindFirstObjectByType<Player>();
        HM.SetTileType(5);HM.UseRadialChart=world.appliedRadialChart;
        if(!world.appliedRadialChart)HM.KLEIN_V/=Mathf.Max(1,world.appliedLayoutScale);
        Vector3 spawnUnit=HM.PoincareToUnit(player.startHyper,true);
        BeachWorld.ConfigureGeometry();
        player.startHyper=HM.UnitToPoincare(new Vector3(.10f,0,-1.20f),true);player.height=BeachWorld.PlayerHeight;
        var puzzle=world.GetComponent<BeachPuzzle>();
        puzzle.chestRadius=.05f;puzzle.positionToleranceSquared=.0003f;
        world.appliedLayoutScale=BeachFootprint.LayoutScale;
        world.appliedRadialChart=true;
        BeachEnvironmentBuilder.Build();
        EditorUtility.SetDirty(player);EditorUtility.SetDirty(puzzle);
        var overview=GameObject.Find("Editor overview").GetComponent<Camera>();
        overview.transform.position=new Vector3(2,2.5f,-3)*BeachFootprint.LayoutScale;
        overview.transform.LookAt(new Vector3(0,.05f,0));
        overview.orthographicSize=1.65f*BeachFootprint.LayoutScale;overview.farClipPlane=20*BeachFootprint.LayoutScale;
        EditorUtility.SetDirty(world);Shader.SetGlobalFloat("_Enable",0);
        EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        RenderPreviews();BeachSceneBuilder.Open();
        File.WriteAllText("../ShapeUpdate.txt","Applied tapered sketch outline at "+BeachFootprint.LayoutScale+"x width and depth.\nPrevious scene: "+Path.GetFullPath(backup)+"\n");
        Debug.Log("[BeachShape] Applied the sketched shoreline, matching water edge, boundary, and map.");
    }

    public static void RenderPreviews()
    {
        var overview=GameObject.Find("Editor overview").GetComponent<Camera>();
        BeachSceneBuilder.Render(overview,"../BeachOverview.png");
        var obj=new GameObject("Outline preview"){hideFlags=HideFlags.HideAndDontSave};var camera=obj.AddComponent<Camera>();
        obj.transform.SetPositionAndRotation(new Vector3(0,5,0)*BeachFootprint.LayoutScale,Quaternion.Euler(90,0,0));
        camera.orthographic=true;camera.orthographicSize=1.12f*BeachFootprint.LayoutScale;camera.nearClipPlane=.01f;camera.farClipPlane=20*BeachFootprint.LayoutScale;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.09f,.535f,.65f);
        BeachSceneBuilder.Render(camera,"../BeachShape.png");UnityEngine.Object.DestroyImmediate(obj);
    }

    public static void ApplyAndCheck() {ApplyToOpenScene();BeachPlayCheck.Run();}
}
