using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class BeachPlayCheck
{
    const string Key="Beach.PlayCheck";
    static int stage, frames;
    static readonly List<string> errors=new List<string>();
    static readonly List<string> results=new List<string>();
    static double deadline;
    static BeachPuzzle puzzle;
    static GyroVector initial;

    static BeachPlayCheck()
    {
        if(SessionState.GetBool(Key,false)) deadline=EditorApplication.timeSinceStartup+55;
        EditorApplication.update+=Tick;
        EditorApplication.playModeStateChanged+=state=>{
            if(state==PlayModeStateChange.EnteredEditMode && SessionState.GetBool("Beach.ReopenAfterCheck",false))
            {SessionState.SetBool("Beach.ReopenAfterCheck",false);BeachSceneBuilder.Open();}
        };
        Application.logMessageReceived+=(message,trace,type)=>{
            if(SessionState.GetBool(Key,false) && (type==LogType.Error || type==LogType.Exception || type==LogType.Assert)) errors.Add(message);
        };
    }

    public static void Run()
    {
        stage=0;frames=0;errors.Clear();results.Clear();deadline=EditorApplication.timeSinceStartup+55;
        EditorSceneManager.OpenScene(BeachSceneBuilder.ScenePath);
        SessionState.SetBool(Key,true);
        EditorApplication.isPlaying=true;
    }

    public static void RunInOpenEditor()
    {
        SessionState.SetBool("Beach.CheckKeepOpen",true);Run();
    }

    static void Check(bool success,string detail)
    {
        if(!success) throw new Exception(detail);
        results.Add("PASS: "+detail);
    }

    static void Tick()
    {
        if(!SessionState.GetBool(Key,false)) return;
        if(deadline>0 && EditorApplication.timeSinceStartup>deadline) {Finish("Play check timed out");return;}
        if(!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        frames++;
        try
        {
            if(stage==0 && frames>=35)
            {
                puzzle=UnityEngine.Object.FindFirstObjectByType<BeachPuzzle>();
                Check(puzzle!=null,"Puzzle loaded in Play Mode");
                Check(!puzzle.solved,"Starting orientation is unsolved");
                int missing=0,npcs=0;
                foreach(var go in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                {
                    missing+=GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                    if(go.name.ToLowerInvariant().Contains("npc")) npcs++;
                }
                Check(missing==0,"No missing scripts"); Check(npcs==0,"No NPCs");
                var sand=GameObject.Find("Beach — treasure area only").GetComponent<MeshFilter>().sharedMesh;
                Check(sand.bounds.size.x>8f && sand.bounds.size.z>4.3f,"Beach expanded to three times its width and depth");
                Check(puzzle.chest.GetComponent<MeshFilter>().sharedMesh.bounds.size.x<.3f,"Chest retains its original mesh size");
                Check(Mathf.Abs(puzzle.player.height-BeachWorld.PlayerHeight*HM.KLEIN_V/.5774f)<1e-6f,"Player proportions match the expanded curved-space chart");
                Check(HM.K==-1 && HM.UseRadialChart && Shader.GetGlobalFloat("_Enable")>.5f && Shader.GetGlobalFloat("_BeachRadialChart")>.5f,"Hyperbolic movement and rendering are enabled in Play Mode");
                var start=BeachWorld.PoseAt(new Vector3(-.5f,0,-.5f),Quaternion.identity);var loop=start;
                foreach(var point in new[]{new Vector3(.5f,0,-.5f),new Vector3(.5f,0,.5f),new Vector3(-.5f,0,.5f),new Vector3(-.5f,0,-.5f)})
                    loop=BeachPuzzle.TransportTo(loop,HM.UnitToPoincare(point,true));
                float loopAngle=Quaternion.Angle(start.gyr,loop.gyr);
                Check(Vector3.Distance(start.Point(),loop.Point())<.0001f && loopAngle>8,"Restored local curvature: closed loop rotates the frame by "+loopAngle+" degrees");
                Check(GameObject.Find("Red and white lighthouse") && GameObject.Find("Red sailboat offshore") && GameObject.Find("Turquoise flag at treasure hole"),"Lighthouse, sailboat, and treasure marker are present");
                Check(RenderSettings.skybox && Camera.main.fieldOfView>=78,"Coastal sky and wide game view are configured");
                Check(!ShaderUtil.ShaderHasError(Shader.Find("Beach/Hyperbolic Ocean")),"Animated hyperbolic ocean shader compiles");
                foreach(var unit in new[]{new Vector3(4,0,.7f),new Vector3(-3.8f,0,1.9f),new Vector3(.4f,.3f,.2f)})
                    Check(Vector3.Distance(unit,HM.PoincareToUnit(HM.UnitToPoincare(unit,true),true))<.003f,"Radial chart round trip at "+unit);
                foreach(var filter in UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
                {
                    if(!filter.GetComponentInParent<HyperObject>())continue;
                    foreach(var vertex in filter.sharedMesh.vertices)
                    {
                        Vector3 point=HM.UnitToPoincare(filter.transform.TransformPoint(vertex),true);
                        if(float.IsNaN(point.sqrMagnitude) || float.IsInfinity(point.sqrMagnitude) || point.sqrMagnitude>=1)
                            throw new Exception("A mesh vertex exceeds the curved-space chart: "+filter.name);
                    }
                }
                Check(true,"Expanded meshes stay finite and inside the curved-space chart");
                Check(BeachFootprint.Contains(puzzle.world.chestStart,BeachFootprint.ChestClearance),"Chest starts inside the tapered beach");
                Check(BeachFootprint.Contains(puzzle.hole.position,BeachFootprint.ChestClearance),"Treasure hole stays inside the tapered beach");
                Check(BeachFootprint.Contains(HM.PoincareToUnit(puzzle.player.startHyper,true),.12f),"Player starts inside the tapered beach");
                initial=puzzle.chest.localGV;
                HyperObject.worldGV=new GyroVector(-HM.UnitToPoincare(new Vector3(0.1f,0,-0.18f),true));
                foreach(var ho in UnityEngine.Object.FindObjectsByType<HyperObject>(FindObjectsSortMode.None)) ho.UpdateCollisions();
                puzzle.player.inputDelta=new Vector3(0,0,0.001f);
                for(int i=0;i<15;i++) puzzle.OnPreCollide(puzzle.player);
                Check(puzzle.pushCount>0 && Vector3.Distance(initial.Point(),puzzle.chest.localGV.Point())>0.0005f,"Walking into the chest pushes it");
                puzzle.ResetPuzzle();
                Check(Vector3.Distance(initial.Point(),puzzle.chest.localGV.Point())<1e-6f,"Reset restores the chest position");
                var goal=BeachWorld.PoseAt(puzzle.hole.position,puzzle.hole.rotation);
                var best=initial; float bestError=360; int bestLoops=0; float bestInset=0;
                for(int n=0;n<600;n++)
                {
                    float inset=.19f+n*.0003f*BeachFootprint.LayoutScale;
                    var corners=new List<Vector3>();
                    foreach(var p in BeachFootprint.Offset(-inset))
                        if(BeachFootprint.Contains(p,BeachFootprint.ChestClearance+.005f)) corners.Add(new Vector3(p.x,0,p.y));
                    if(corners.Count<4)continue;
                    bool valid=true;
                    for(int j=0;j<corners.Count && valid;j++)for(int sample=0;sample<=12;sample++)
                        if(!BeachFootprint.Contains(Vector3.Lerp(corners[j],corners[(j+1)%corners.Count],sample/12f),BeachFootprint.ChestClearance)) {valid=false;break;}
                    if(!valid)continue;
                    var dense=new List<Vector3>();
                    for(int j=0;j<corners.Count;j++)
                    {
                        var a=corners[j];var b=corners[(j+1)%corners.Count];int segments=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(a,b)/.16f));
                        for(int k=0;k<segments;k++)dense.Add(Vector3.Lerp(a,b,(float)k/segments));
                    }
                    corners=dense;
                    for(int direction=0;direction<2;direction++)
                    {
                        if(direction==1)corners.Reverse();
                        var candidate=BeachPuzzle.TransportTo(initial,HM.UnitToPoincare(corners[0],true));
                        for(int loops=1;loops<=8;loops++)
                        {
                            for(int j=1;j<=corners.Count;j++)candidate=BeachPuzzle.TransportTo(candidate,HM.UnitToPoincare(corners[j%corners.Count],true));
                            var atGoal=BeachPuzzle.TransportTo(candidate,goal.Point());
                            float angle=Quaternion.Angle((atGoal-goal).gyr,Quaternion.identity);
                            if(angle<bestError){bestError=angle;best=atGoal;bestLoops=loops;bestInset=inset;}
                        }
                    }
                }
                Check(bestError<1,"Puzzle solvable inside the sketched shoreline: "+bestLoops+" loop(s), clearance "+bestInset+", error "+bestError+" degrees");
                puzzle.chest.localGV=best;
                stage=1;frames=0;
            }
            else if(stage==1 && frames>=8)
            {
                Check(puzzle.solved,"Position and orientation alignment completes the puzzle");
                Check(puzzle.chest.transform.localPosition.y<0,"Completed chest sinks into its hole");
                puzzle.ResetPuzzle();
                HyperObject.worldGV=new GyroVector(-puzzle.player.startHyper);
                puzzle.player.velocity=Vector3.zero;
                stage=2; frames=0;
            }
            else if(stage==2 && frames>=8)
            {
                Check(!puzzle.solved && puzzle.pushCount==0,"Reset clears completion and allows another attempt");
                Check(errors.Count==0,"No runtime errors");
                BeachSceneBuilder.Render(Camera.main,"../BeachGameplay.png");
                Finish(null);
            }
        }
        catch(Exception ex){Finish(ex.ToString());}
    }

    static void Finish(string failure)
    {
        SessionState.SetBool(Key,false);
        string report=string.Join("\n",results)+"\n"+(failure==null?"PLAY CHECK PASSED":"FAILED: "+failure)+"\n"+string.Join("\n",errors);
        File.WriteAllText("../PlayCheck.txt",report); Debug.Log("[BeachPlayCheck] "+report);
        if(SessionState.GetBool("Beach.CheckKeepOpen",false))
        {
            SessionState.SetBool("Beach.CheckKeepOpen",false);SessionState.SetBool("Beach.ReopenAfterCheck",true);
            EditorApplication.isPlaying=false;
        }
        else EditorApplication.Exit(failure==null?0:1);
    }
}
