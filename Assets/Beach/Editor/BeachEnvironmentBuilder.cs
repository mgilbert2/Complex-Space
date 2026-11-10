using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class BeachEnvironmentBuilder
{
    const string RootName="Beach landmarks";
    const string Generated="Assets/Beach/Generated/";
    static Material stone,red,ivory,dark,gold,wood,green,greenLight,cyan;

    public static void Build()
    {
        var old=GameObject.Find(RootName);if(old)Object.DestroyImmediate(old);
        var root=new GameObject(RootName);
        stone=Mat("Stone",new Color(.43f,.46f,.46f));red=Mat("Lighthouse red",new Color(.82f,.13f,.08f));
        ivory=Mat("Ivory",new Color(.98f,.91f,.72f));dark=Mat("Dark metal",new Color(.055f,.10f,.14f));
        gold=Mat("Beacon gold",new Color(1,.70f,.20f));gold.EnableKeyword("GLOW");
        wood=Mat("Palm bark",new Color(.38f,.20f,.09f));green=Mat("Palm green",new Color(.09f,.34f,.16f));
        greenLight=Mat("Palm leaf tips",new Color(.24f,.52f,.20f));cyan=Mat("Treasure flag",new Color(.03f,.77f,.86f));
        var lighthouse=HyperRoot("Red and white lighthouse",root.transform,new Vector3(1.70f,0,1.10f));
        Cylinder(lighthouse.transform,"Stone foundation",Vector3.zero,.24f,.11f,stone);
        for(int band=0;band<6;band++)
            Cylinder(lighthouse.transform,"Tower band "+band,new Vector3(0,.10f+band*.22f,0),.155f-band*.006f,.22f,band%2==0?ivory:red);
        Cylinder(lighthouse.transform,"Gallery",new Vector3(0,1.40f,0),.20f,.055f,dark);
        Cylinder(lighthouse.transform,"Lantern glow",new Vector3(0,1.46f,0),.09f,.18f,gold);
        for(int i=0;i<8;i++)
        {
            float a=i*Mathf.PI/4;Cylinder(lighthouse.transform,"Lantern post",new Vector3(Mathf.Cos(a)*.14f,1.45f,Mathf.Sin(a)*.14f),.008f,.21f,dark);
        }
        Cone(lighthouse.transform,"Red roof",new Vector3(0,1.67f,0),.22f,.18f,red);
        var beacon=new GameObject("Rotating beacon");beacon.transform.SetParent(lighthouse.transform,false);beacon.transform.localPosition=Vector3.up*1.53f;
        MeshObject(beacon.transform,"Beacon glint",SphereMesh(.035f),gold,new Vector3(.14f,.015f,0));
        var collision=lighthouse.AddComponent<WarpCollider>();collision.boundingCylinders=new[]{new WarpCollider.Cylinder{p1=Vector3.zero,p2=Vector3.up*1.42f,radius=.17f,capped=true}};
        Palm(root.transform,new Vector3(-1.35f,0,.98f),.94f,8);
        Palm(root.transform,new Vector3(-1.75f,0,.81f),.72f,-24);
        Palm(root.transform,new Vector3(-1.14f,0,1.24f),1.12f,28);
        var boat=HyperRoot("Red sailboat offshore",root.transform,new Vector3(2.30f,-.07f,-.65f));
        boat.transform.localRotation=Quaternion.Euler(0,-25,0);
        Primitive(boat.transform,"Red hull",PrimitiveType.Cube,new Vector3(0,.035f,0),new Vector3(.17f,.075f,.43f),red);
        Cylinder(boat.transform,"Mast",new Vector3(0,.07f,0),.008f,.60f,dark);
        Triangle(boat.transform,"Ivory sail",new Vector3(.012f,.64f,0),new Vector3(.012f,.16f,.21f),new Vector3(.012f,.16f,0),ivory);
        Triangle(boat.transform,"Red jib",new Vector3(-.012f,.59f,0),new Vector3(-.012f,.16f,0),new Vector3(-.012f,.16f,-.16f),red);
        var flag=HyperRoot("Turquoise flag at treasure hole",root.transform,new Vector3(.73f,0,.08f));
        Cylinder(flag.transform,"Marker pole",Vector3.zero,.007f,.43f,wood);
        Triangle(flag.transform,"Turquoise pennant",new Vector3(0,.43f,0),new Vector3(.15f,.37f,0),new Vector3(0,.30f,0),cyan);
        var motion=root.AddComponent<BeachSceneryMotion>();motion.beacon=beacon.transform;motion.boat=boat.transform;
        CreateSky();
    }

    static GameObject HyperRoot(string name,Transform parent,Vector3 position)
    {
        var obj=new GameObject(name);obj.transform.SetParent(parent,false);obj.transform.localPosition=position;
        obj.AddComponent<HyperObject>().allowCulling=false;return obj;
    }
    static Material Mat(string name,Color color)
    {
        var mat=new Material(Shader.Find("Custom/HyperbolicShader"));mat.SetColor("_Color",color);mat.SetFloat("_Ambient",.78f);mat.SetFloat("_BoundaryAO",1);
        AssetDatabase.CreateAsset(mat,Generated+name.Replace(' ','_')+".mat");return mat;
    }
    static GameObject MeshObject(Transform parent,string name,Mesh mesh,Material material,Vector3 position)
    {
        var obj=new GameObject(name);obj.transform.SetParent(parent,false);obj.transform.localPosition=position;
        obj.AddComponent<MeshFilter>().sharedMesh=mesh;obj.AddComponent<MeshRenderer>().sharedMaterial=material;
        AssetDatabase.CreateAsset(mesh,AssetDatabase.GenerateUniqueAssetPath(Generated+name.Replace(' ','_')+".asset"));return obj;
    }
    static void Cylinder(Transform parent,string name,Vector3 position,float radius,float height,Material material)
    {
        var vertices=new List<Vector3>();var triangles=new List<int>();const int sides=16,levels=4;
        for(int y=0;y<=levels;y++)for(int i=0;i<sides;i++)
        {float a=i*Mathf.PI*2/sides;vertices.Add(new Vector3(Mathf.Cos(a)*radius,height*y/levels,Mathf.Sin(a)*radius));}
        for(int y=0;y<levels;y++)for(int i=0;i<sides;i++)
        {int a=y*sides+i,b=y*sides+(i+1)%sides,c=b+sides,d=a+sides;triangles.AddRange(new[]{a,d,c,a,c,b});}
        int bottom=vertices.Count;vertices.Add(Vector3.zero);int top=vertices.Count;vertices.Add(Vector3.up*height);
        for(int i=0;i<sides;i++){int j=(i+1)%sides;triangles.AddRange(new[]{bottom,i,j,top,levels*sides+j,levels*sides+i});}
        var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();MeshObject(parent,name,mesh,material,position);
    }
    static void Cone(Transform parent,string name,Vector3 position,float radius,float height,Material material)
    {
        var v=new List<Vector3>();var t=new List<int>();const int sides=16;
        v.Add(Vector3.up*height);for(int i=0;i<sides;i++){float a=i*Mathf.PI*2/sides;v.Add(new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius));}
        for(int i=0;i<sides;i++)t.AddRange(new[]{0,1+(i+1)%sides,1+i});
        var mesh=new Mesh{name=name};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();MeshObject(parent,name,mesh,material,position);
    }
    static void Primitive(Transform parent,string name,PrimitiveType type,Vector3 position,Vector3 scale,Material material)
    {
        var obj=GameObject.CreatePrimitive(type);obj.name=name;obj.transform.SetParent(parent,false);obj.transform.localPosition=position;obj.transform.localScale=scale;
        Object.DestroyImmediate(obj.GetComponent<Collider>());obj.GetComponent<Renderer>().sharedMaterial=material;
    }
    static Mesh SphereMesh(float radius)
    {
        var temp=GameObject.CreatePrimitive(PrimitiveType.Sphere);var mesh=Object.Instantiate(temp.GetComponent<MeshFilter>().sharedMesh);Object.DestroyImmediate(temp);
        var vertices=mesh.vertices;for(int i=0;i<vertices.Length;i++)vertices[i]*=radius*2;mesh.vertices=vertices;mesh.RecalculateBounds();return mesh;
    }
    static void Triangle(Transform parent,string name,Vector3 a,Vector3 b,Vector3 c,Material material)
    {
        var mesh=new Mesh{name=name};mesh.vertices=new[]{a,b,c,a,b,c};mesh.triangles=new[]{0,1,2,5,4,3};mesh.RecalculateNormals();MeshObject(parent,name,mesh,material,Vector3.zero);
    }
    static void Palm(Transform parent,Vector3 position,float height,float angle)
    {
        var root=HyperRoot("Palm landmark",parent,position);root.transform.localRotation=Quaternion.Euler(0,angle,0);
        var vertices=new List<Vector3>();var triangles=new List<int>();const int sides=8,levels=8;
        for(int y=0;y<=levels;y++)
        {float t=(float)y/levels;for(int i=0;i<sides;i++){float a=i*Mathf.PI*2/sides;vertices.Add(new Vector3(.11f*t*t+Mathf.Cos(a)*.033f*(1-.4f*t),t*height,Mathf.Sin(a)*.033f*(1-.4f*t)));}}
        for(int y=0;y<levels;y++)for(int i=0;i<sides;i++)
        {int a=y*sides+i,b=y*sides+(i+1)%sides,c=b+sides,d=a+sides;triangles.AddRange(new[]{a,d,c,a,c,b});}
        var trunk=new Mesh{name="Palm trunk"};trunk.SetVertices(vertices);trunk.SetTriangles(triangles,0);trunk.RecalculateNormals();MeshObject(root.transform,"Curved palm trunk",trunk,wood,Vector3.zero);
        for(int leaf=0;leaf<7;leaf++)
        {
            vertices.Clear();triangles.Clear();float a=leaf*Mathf.PI*2/7;var direction=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var side=Vector3.Cross(Vector3.up,direction);
            for(int segment=0;segment<=6;segment++)
            {float t=segment/6f;var center=new Vector3(.11f,height,0)+direction*(t*.52f*height)+Vector3.up*(.12f*Mathf.Sin(t*Mathf.PI)-.22f*t*t);float width=.09f*Mathf.Sin(t*Mathf.PI)*height;vertices.Add(center+side*width);vertices.Add(center-side*width);}
            for(int segment=0;segment<6;segment++){int i=segment*2;triangles.AddRange(new[]{i,i+2,i+1,i+1,i+2,i+3});}
            int frontCount=vertices.Count;vertices.AddRange(vertices.ToArray());int frontTriangles=triangles.Count;
            for(int i=0;i<frontTriangles;i+=3)triangles.AddRange(new[]{triangles[i+2]+frontCount,triangles[i+1]+frontCount,triangles[i]+frontCount});
            var mesh=new Mesh{name="Palm frond"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();MeshObject(root.transform,"Palm frond",mesh,leaf%2==0?green:greenLight,Vector3.zero);
        }
        root.AddComponent<WarpCollider>().boundingCylinders=new[]{new WarpCollider.Cylinder{p1=Vector3.zero,p2=new Vector3(.11f,height,0),radius=.04f,capped=true}};
    }
    static void CreateSky()
    {
        var sky=new Material(Shader.Find("Skybox/Procedural"));sky.SetFloat("_SunSize",.035f);sky.SetFloat("_AtmosphereThickness",.85f);sky.SetColor("_SkyTint",new Color(.42f,.57f,.76f));sky.SetColor("_GroundColor",new Color(.45f,.63f,.70f));sky.SetFloat("_Exposure",1.1f);
        AssetDatabase.CreateAsset(sky,Generated+"CoastalSky.mat");RenderSettings.skybox=sky;
        RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.65f,.76f,.9f);RenderSettings.ambientEquatorColor=new Color(.50f,.67f,.72f);
        var camera=Camera.main;if(camera){camera.clearFlags=CameraClearFlags.Skybox;camera.fieldOfView=80;camera.useOcclusionCulling=false;}
    }
}
