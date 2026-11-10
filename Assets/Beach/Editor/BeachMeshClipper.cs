using System;
using System.Collections.Generic;
using UnityEngine;

public static class BeachMeshClipper
{
    struct Vertex
    {
        public Vector3 position,normal;
        public Vector2 uv,uv2;
        public static Vertex Lerp(Vertex a,Vertex b,float t)
        {
            return new Vertex{position=Vector3.LerpUnclamped(a.position,b.position,t),normal=Vector3.LerpUnclamped(a.normal,b.normal,t).normalized,
                uv=Vector2.LerpUnclamped(a.uv,b.uv,t),uv2=Vector2.LerpUnclamped(a.uv2,b.uv2,t)};
        }
    }

    public static Mesh Clip(Mesh source)
    {
        Vector3[] positions=source.vertices,normals=source.normals;
        Vector2[] uv=source.uv,uv2=source.uv2;
        var vertices=new Vertex[positions.Length];
        for(int i=0;i<vertices.Length;i++)
        {
            Vector3 position=positions[i];
            vertices[i]=new Vertex{position=position,normal=normals[i],uv=uv[i],uv2=uv2.Length==positions.Length?uv2[i]:Vector2.zero};
        }
        var outlineTriangles=BeachFootprint.Triangulate(); var sourceTriangles=source.triangles;
        var output=new List<Vertex>(); var indices=new List<int>();
        for(int i=0;i<sourceTriangles.Length;i+=3)
        {
            var original=new List<Vertex>{vertices[sourceTriangles[i]],vertices[sourceTriangles[i+1]],vertices[sourceTriangles[i+2]]};
            AddClipped(original,outlineTriangles,output,indices);
        }
        // Add new sand outside the original tile; keep the hole, props, and their baked textures intact.
        int corner=0;float nearest=float.MaxValue;
        for(int i=0;i<vertices.Length;i++)
        {
            float distance=(vertices[i].position-new Vector3(-1,0,-1)).sqrMagnitude;
            if(distance<nearest){nearest=distance;corner=i;}
        }
        Vertex sand=vertices[corner];sand.normal=Vector3.up;
        AddSand(-1,BeachFootprint.Min.y,1,-1,sand,outlineTriangles,output,indices);
        AddSand(-1,1,1,BeachFootprint.Max.y,sand,outlineTriangles,output,indices);
        AddSand(BeachFootprint.Min.x,BeachFootprint.Min.y,-1,BeachFootprint.Max.y,sand,outlineTriangles,output,indices);
        AddSand(1,BeachFootprint.Min.y,BeachFootprint.Max.x,BeachFootprint.Max.y,sand,outlineTriangles,output,indices);
        var mesh=new Mesh{name="Beach — tapered sketch outline",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
        var resultPositions=new List<Vector3>();var resultNormals=new List<Vector3>();var resultUV=new List<Vector2>();var resultUV2=new List<Vector2>();
        foreach(var v in output) {resultPositions.Add(v.position);resultNormals.Add(v.normal);resultUV.Add(v.uv);resultUV2.Add(v.uv2);}
        mesh.SetVertices(resultPositions);mesh.SetNormals(resultNormals);mesh.SetUVs(0,resultUV);mesh.SetUVs(1,resultUV2);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();
        return mesh;
    }

    static void AddSand(float x0,float z0,float x1,float z1,Vertex sand,List<int> outline,List<Vertex> output,List<int> indices)
    {
        if(x1<=x0 || z1<=z0)return;
        Vertex a=sand,b=sand,c=sand,d=sand;
        a.position=new Vector3(x0,0,z0);b.position=new Vector3(x0,0,z1);
        c.position=new Vector3(x1,0,z1);d.position=new Vector3(x1,0,z0);
        AddClipped(new List<Vertex>{a,b,c},outline,output,indices);
        AddClipped(new List<Vertex>{a,c,d},outline,output,indices);
    }

    static void AddClipped(List<Vertex> original,List<int> outlineTriangles,List<Vertex> output,List<int> indices)
    {
        for(int j=0;j<outlineTriangles.Count;j+=3)
        {
            var polygon=original;
            for(int k=0;k<3 && polygon.Count>0;k++)
            {
                Vector2 a=BeachFootprint.Points[outlineTriangles[j+k]],b=BeachFootprint.Points[outlineTriangles[j+(k+1)%3]];
                polygon=ClipEdge(polygon,a,b);
            }
            for(int k=1;k+1<polygon.Count;k++)
            {
                if(Vector3.Cross(polygon[k].position-polygon[0].position,polygon[k+1].position-polygon[0].position).sqrMagnitude<1e-15f)continue;
                AddTriangle(polygon[0],polygon[k],polygon[k+1],output,indices);
            }
        }
    }

    static void AddTriangle(Vertex a,Vertex b,Vertex c,List<Vertex> output,List<int> indices)
    {
        float ab=(a.position-b.position).sqrMagnitude,bc=(b.position-c.position).sqrMagnitude,ca=(c.position-a.position).sqrMagnitude;
        if(Mathf.Max(ab,Mathf.Max(bc,ca))>.22f*.22f)
        {
            if(ab>=bc && ab>=ca){var m=Vertex.Lerp(a,b,.5f);AddTriangle(a,m,c,output,indices);AddTriangle(m,b,c,output,indices);}
            else if(bc>=ca){var m=Vertex.Lerp(b,c,.5f);AddTriangle(a,b,m,output,indices);AddTriangle(a,m,c,output,indices);}
            else{var m=Vertex.Lerp(c,a,.5f);AddTriangle(a,b,m,output,indices);AddTriangle(m,b,c,output,indices);}
            return;
        }
        int n=output.Count;output.Add(a);output.Add(b);output.Add(c);indices.AddRange(new[]{n,n+1,n+2});
    }

    static List<Vertex> ClipEdge(List<Vertex> input,Vector2 a,Vector2 b)
    {
        var result=new List<Vertex>(); var previous=input[input.Count-1];
        float before=Distance(previous,a,b);
        foreach(var current in input)
        {
            float after=Distance(current,a,b);
            if((before>=-1e-7f)!=(after>=-1e-7f)) result.Add(Vertex.Lerp(previous,current,before/(before-after)));
            if(after>=-1e-7f) result.Add(current);
            previous=current;before=after;
        }
        return result;
    }
    static float Distance(Vertex v,Vector2 a,Vector2 b)
    {return BeachFootprint.Cross(b-a,new Vector2(v.position.x,v.position.z)-a);}
}
