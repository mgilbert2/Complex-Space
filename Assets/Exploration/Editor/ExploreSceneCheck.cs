using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class ExploreSceneCheck
{
    const string Key="Explore.Check";
    static string ReportDir=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Logs/ExplorationChecks"));
    static int frames;
    static bool checkedScene;
    static ExploreSceneCheck()
    {
        EditorApplication.update+=Tick;
        EditorApplication.playModeStateChanged+=Changed;
        Application.logMessageReceived+=(message,trace,type)=>
        {
            if(SessionState.GetBool(Key,false)&&(type==LogType.Error||type==LogType.Exception||type==LogType.Assert))
                File.AppendAllText(Path.Combine(ReportDir,"errors.txt"),message+"\n"+trace+"\n");
        };
    }
    [MenuItem("Tools/Complex Space/Check Exploration Scenes")]
    public static void Run()
    {
        Directory.CreateDirectory(ReportDir);
        File.WriteAllText(Path.Combine(ReportDir,"results.txt"),"");
        File.WriteAllText(Path.Combine(ReportDir,"errors.txt"),"");
        SessionState.SetBool(Key+"Failed",false);
        SessionState.SetInt(Key+"Index",0);SessionState.SetBool(Key,true);
        Enter();
    }
    public static void BuildAndRun(){ExploreSceneBuilder.BuildBoth();Run();}
    static void Enter()
    {
        frames=0;checkedScene=false;
        EditorSceneManager.OpenScene(ExploreSceneBuilder.PathFor(SessionState.GetInt(Key+"Index",0)==0));
        EditorApplication.isPlaying=true;
    }
    static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||checkedScene)return;
        if(++frames<80)return;
        checkedScene=true;
        try
        {
            var stage=UnityEngine.Object.FindFirstObjectByType<ExploreSpace>();
            Require(stage!=null,"Missing stage");
            Require(HM.K==(stage.elliptic?1:-1),"Wrong curvature");
            Require(stage.terrain.Length>=(stage.elliptic?6:80),"Incomplete surface coverage");
            Require(UnityEngine.Object.FindObjectsByType<WarpCollider>(FindObjectsSortMode.None).Length==6,"Expected six collidable columns");
            Require(stage.trampoline.GetComponent<MeshFilter>().sharedMesh!=null,"Missing recovered trampoline");
            Require(!float.IsNaN(stage.player.height),"Invalid player");
            Capture(stage,stage.elliptic?"Elliptic":"Hyperbolic");
            InputManager.pauseInput=false;
            var ball=stage.ball;
            HyperObject.worldGV=GyroVector.identity;
            stage.player.mainCamera.transform.rotation=Quaternion.Euler(0,30,0);
            ball.Initialize(new GyroVector(.04f,0,.12f));
            Require(ball.TryKick(stage.player),"Nearby ball cannot be kicked");
            for(int i=0;i<120;i++)ball.Advance(1f/120);
            Require(Mathf.Abs(ball.hyper.localGV.vec.y)<1e-6f,"Ball left ground surface");
            Require(ball.speed>0&&ball.speed<.65f,"Ball friction failed");
            Require((ball.hyper.localGV.Point()-new Vector3(0,0,.12f)).magnitude>.1f,"Ball did not advance");
            var beforePause=ball.hyper.localGV.Point();
            stage.SendMessage("Pause",true);
            ball.Advance(.5f);
            Require((ball.hyper.localGV.Point()-beforePause).sqrMagnitude<1e-10f,"Ball moved while paused");
            stage.SendMessage("Pause",false);
            ball.Initialize(new GyroVector(HM.HyperTranslate(0,0,1.2f)));
            Require(!ball.TryKick(stage.player),"Distant ball can be kicked");
            var column=UnityEngine.Object.FindObjectsByType<HyperObject>(FindObjectsSortMode.None).First(h=>h.name=="Stone column 1");
            var columnPose=column.localGV;
            var playerPose=new GyroVector(HM.HyperTranslate(0,0,-.28f))+columnPose;
            HyperObject.worldGV=-playerPose;
            stage.player.mainCamera.transform.rotation=Quaternion.identity;
            ball.Initialize(new GyroVector(HM.HyperTranslate(0,0,-.16f))+columnPose);
            Require(ball.TryKick(stage.player),"Cannot kick ball toward column");
            for(int i=0;i<100;i++)ball.Advance(.01f);
            Require(ball.CollisionCount>0,"Ball passed through a stone column");
            Require(Mathf.Abs(ball.hyper.localGV.vec.y)<1e-6f,"Impact lifted ball off ground");
            // Descending above the central mat should trigger a launch.
            HyperObject.worldGV=-stage.trampoline.localGV;
            stage.player.velocity=Vector3.down*.01f;
            stage.SendMessage("Update");
            Require(stage.player.velocity.y>0,"Trampoline did not bounce player");
            if(stage.elliptic)
            {
                // tan has period pi: a complete great circle returns to the starting point.
                var pose=GyroVector.identity;
                for(int i=0;i<1000;i++)pose=new GyroVector(HM.HyperTranslate(0,0,Mathf.PI/1000))+pose;
                Require(pose.Point().magnitude<.002f,"Closed-space geodesic did not return");
            }
            else
            {
                var ballBefore=ball.hyper.localGV.Point();
                var trampolineBefore=stage.trampoline.localGV.Point();
                ball.speed=0;
                for(int i=0;i<200;i++)
                {
                    HyperObject.worldGV=new GyroVector(-Vector3.forward*(float)HM.CELL_WIDTH);
                    stage.SendMessage("LateUpdate");
                }
                Require(stage.streamCount>=200,"Terrain does not continue streaming");
                Require(HyperObject.worldGV.vec.sqrMagnitude<.5f,"Streaming did not rebase player");
                Require(!stage.trampoline.gameObject.activeSelf,"Distant central landmark was not parked");
                for(int i=0;i<200;i++)
                {
                    HyperObject.worldGV=new GyroVector(Vector3.forward*(float)HM.CELL_WIDTH);
                    stage.SendMessage("LateUpdate");
                }
                Require(stage.trampoline.gameObject.activeSelf&&(stage.trampoline.localGV.Point()-trampolineBefore).magnitude<.0002f,"Central trampoline did not survive long out-and-back travel");
                Require((ball.hyper.localGV.Point()-ballBefore).magnitude<.001f,"Stationary ball did not survive long out-and-back travel");
                for(int i=0;i<200;i++)
                {
                    HyperObject.worldGV=new GyroVector(-Vector3.forward*(float)HM.CELL_WIDTH);
                    stage.SendMessage("LateUpdate");
                }
                stage.RecallBall();
                Require(ball.TryKick(stage.player),"Cannot recall and kick ball in a distant streamed region");
                foreach(var ho in UnityEngine.Object.FindObjectsByType<HyperObject>(FindObjectsSortMode.None))
                {
                    ho.UpdateComposedGV();
                    Require(!float.IsNaN(ho.composedGV.vec.x)&&!float.IsNaN(ho.composedGV.gyr.w),"Non-finite rebased object: "+ho.name);
                }
            }
            File.AppendAllText(Path.Combine(ReportDir,"results.txt"),(stage.elliptic?"Elliptic":"Hyperbolic")+": PASS; cells="+stage.terrain.Length+"; rolling, ground constraint, kick distance, pause, column impact, trampoline and "+(stage.elliptic?"closed geodesic":"200-cell out/return, continued streaming, distant recall")+" passed.\n");
        }
        catch(Exception e)
        {
            File.AppendAllText(Path.Combine(ReportDir,"results.txt"),"FAIL: "+e+"\n");
            SessionState.SetBool(Key+"Failed",true);
        }
        EditorApplication.isPlaying=false;
    }
    static void Capture(ExploreSpace stage,string name)
    {
        var camera=stage.player.mainCamera.GetComponent<Camera>();
        var rt=new RenderTexture(1280,720,24);camera.targetTexture=rt;
        camera.Render();RenderTexture.active=rt;
        var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
        File.WriteAllBytes(Path.Combine(ReportDir,name+".png"),image.EncodeToPNG());
        camera.targetTexture=null;RenderTexture.active=null;
        UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(rt);
    }
    static void Changed(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode){frames=0;checkedScene=false;}
        if(state!=PlayModeStateChange.EnteredEditMode)return;
        int index=SessionState.GetInt(Key+"Index",0)+1;SessionState.SetInt(Key+"Index",index);
        if(index<2){EditorApplication.delayCall+=Enter;return;}
        SessionState.SetBool(Key,false);
        bool fail=SessionState.GetBool(Key+"Failed",false)||new FileInfo(Path.Combine(ReportDir,"errors.txt")).Length>0;
        Debug.Log("[Explore] Checks complete: "+(fail?"FAIL":"PASS"));
        if(Application.isBatchMode)EditorApplication.Exit(fail?1:0);
    }
}
