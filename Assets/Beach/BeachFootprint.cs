using System;
using System.Collections.Generic;
using UnityEngine;

// Coordinates are in the recovered mesh's local XZ plane, viewed from above.
public static class BeachFootprint
{
    public const float LayoutScale = 3f;
    public static readonly Vector2[] Points = MakeOutline();
    public const float ChestClearance = 0.18f;
    public static readonly Vector2 Min = new Vector2(-1.38f, -0.745f) * LayoutScale;
    public static readonly Vector2 Max = new Vector2(1.4f, 0.75f) * LayoutScale;

    static Vector2[] MakeOutline()
    {
        var p = new[] {
            new Vector2(-1.38f,.70f),new Vector2(-1.22f,.74f),new Vector2(-.92f,.745f),
            new Vector2(-.50f,.75f),new Vector2(-.12f,.745f),new Vector2(.18f,.69f),
            new Vector2(.40f,.665f),new Vector2(.65f,.605f),new Vector2(.91f,.56f),
            new Vector2(1.10f,.44f),new Vector2(1.25f,.325f),new Vector2(1.40f,.255f),
            new Vector2(1.16f,.145f),new Vector2(.99f,.10f),new Vector2(.83f,.015f),
            new Vector2(.71f,-.105f),new Vector2(.60f,-.185f),new Vector2(.51f,-.32f),
            new Vector2(.38f,-.52f),new Vector2(.32f,-.745f),new Vector2(.15f,-.70f),
            new Vector2(-.08f,-.675f),new Vector2(-.285f,-.625f),new Vector2(-.28f,-.55f),
            new Vector2(-.35f,-.325f),new Vector2(-.39f,-.18f),new Vector2(-.48f,-.04f),
            new Vector2(-.595f,.08f),new Vector2(-.72f,.20f),new Vector2(-.815f,.395f),
            new Vector2(-.90f,.525f),new Vector2(-1.08f,.62f)
        };
        float area=0;
        for(int i=0;i<p.Length;i++) area+=Cross(p[i],p[(i+1)%p.Length]);
        if(area<0) Array.Reverse(p);
        for(int i=0;i<p.Length;i++)p[i]*=LayoutScale;
        return p;
    }

    public static float Cross(Vector2 a,Vector2 b) {return a.x*b.y-a.y*b.x;}

    public static bool Contains(Vector3 point,float clearance=0)
    {return Contains(new Vector2(point.x,point.z),clearance);}

    public static bool Contains(Vector2 point,float clearance=0)
    {
        bool inside=false;
        for(int i=0,j=Points.Length-1;i<Points.Length;j=i++)
        {
            var a=Points[i]; var b=Points[j];
            if((a.y>point.y)!=(b.y>point.y) && point.x<(b.x-a.x)*(point.y-a.y)/(b.y-a.y)+a.x) inside=!inside;
            if(clearance>0)
            {
                Vector2 d=b-a;
                Vector2 nearest=a+d*Mathf.Clamp01(Vector2.Dot(point-a,d)/d.sqrMagnitude);
                if((point-nearest).sqrMagnitude<clearance*clearance) return false;
            }
        }
        return inside;
    }

    public static Vector2[] Offset(float distance)
    {
        var result=new Vector2[Points.Length];
        for(int i=0;i<Points.Length;i++)
        {
            Vector2 before=(Points[i]-Points[(i+Points.Length-1)%Points.Length]).normalized;
            Vector2 after=(Points[(i+1)%Points.Length]-Points[i]).normalized;
            Vector2 a=new Vector2(before.y,-before.x),b=new Vector2(after.y,-after.x);
            Vector2 bisector=(a+b).normalized;
            float shift=distance/Mathf.Max(0.25f,Vector2.Dot(bisector,a));
            if(distance>0)shift=Mathf.Min(shift,distance*1.35f);
            result[i]=Points[i]+bisector*shift;
        }
        return result;
    }

    public static List<int> Triangulate()
    {
        var remaining=new List<int>(); var result=new List<int>();
        for(int i=0;i<Points.Length;i++) remaining.Add(i);
        while(remaining.Count>3)
        {
            bool clipped=false;
            for(int i=0;i<remaining.Count;i++)
            {
                int a=remaining[(i+remaining.Count-1)%remaining.Count],b=remaining[i],c=remaining[(i+1)%remaining.Count];
                if(Cross(Points[b]-Points[a],Points[c]-Points[b])<=1e-7f) continue;
                bool occupied=false;
                foreach(int v in remaining)
                {
                    if(v==a || v==b || v==c) continue;
                    if(Cross(Points[b]-Points[a],Points[v]-Points[a])>=-1e-7f &&
                       Cross(Points[c]-Points[b],Points[v]-Points[b])>=-1e-7f &&
                       Cross(Points[a]-Points[c],Points[v]-Points[c])>=-1e-7f) {occupied=true;break;}
                }
                if(occupied) continue;
                result.AddRange(new[]{a,b,c}); remaining.RemoveAt(i); clipped=true; break;
            }
            if(!clipped) throw new InvalidOperationException("Beach outline could not be triangulated.");
        }
        result.AddRange(remaining); return result;
    }
}
