using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-200)]
public class ExploreSpace : MonoBehaviour
{
    public bool elliptic;
    public Player player;
    public HyperObject[] terrain;
    public HyperObject trampoline;
    public SurfaceBall ball;
    public Material grass;
    public Mesh groundMesh;
    public AudioClip bounceSound;
    public int streamCount;
    bool paused;
    float bounceCooldown;
    Vector3 trampolineScale;
    AudioSource audioSource;
    readonly List<GyroVector> neighborhood = new List<GyroVector>();
    readonly Dictionary<HyperObject,GyroVectorD> fixedLandmarks=new Dictionary<HyperObject,GyroVectorD>();
    readonly ExploreAnchorHistory anchorHistory=new ExploreAnchorHistory();

    public void Configure()
    {
        HM.SetTileType(elliptic ? 3 : 5);
        HM.UseRadialChart = false;
        Shader.SetGlobalFloat("_BeachRadialChart", 0);
        Shader.SetGlobalFloat("_KleinV", HM.KLEIN_V);
    }

    void Awake()
    {
        Configure();
        WCollider.AllColliders.Clear();
        HyperObject.worldGV = GyroVector.identity;
        HyperObject.isShaking = false;
        HyperObject.updateColliders = true;
        WorldBuilder.globalBounryAO = 1;
        Shader.SetGlobalFloat("_Enable", 1);
        Shader.SetGlobalFloat("_DualLight", 1);
        Shader.SetGlobalFloat("_Proj", 0);
        Shader.SetGlobalVector("_WarpParams", Vector4.zero);
        WorldBuilder.SetFog(new Color(.67f,.83f,.94f,.12f), .2f);
        Pause(false);
    }

    void Start()
    {
        trampolineScale=trampoline.transform.localScale;
        audioSource=gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend=0;
        WorldBuilder.UpdateReplacementShaders(true);
        if (!elliptic)
        {
            foreach (var ho in FindObjectsByType<HyperObject>(FindObjectsSortMode.None))
            {
                ho.highPrecision=true;
                ho.localGVD=(GyroVectorD)ho.localGV;
                ho.allowCulling=true;
                if(ho!=ball.hyper&&System.Array.IndexOf(terrain,ho)<0)
                    fixedLandmarks.Add(ho,ho.localGVD);
            }
            neighborhood.Add(GyroVector.identity);
            var depths = new List<int> { 0 };
            for (int i=0; i<neighborhood.Count; i++)
            {
                if (depths[i] == 4) continue;
                foreach (var direction in new[]{Vector3.right,Vector3.left,Vector3.forward,Vector3.back})
                {
                    var candidate = new GyroVector(direction*(float)HM.CELL_WIDTH) + neighborhood[i];
                    bool duplicate = false;
                    foreach (var existing in neighborhood)
                        if ((existing.Point()-candidate.Point()).sqrMagnitude < 1e-8f) { duplicate=true; break; }
                    if (!duplicate) { neighborhood.Add(candidate); depths.Add(depths[i]+1); }
                }
            }
            var tiles = new List<HyperObject>(terrain);
            while (tiles.Count < neighborhood.Count)
            {
                var tile = new GameObject("Streamed grass cell");
                tile.AddComponent<MeshFilter>().sharedMesh = groundMesh;
                tile.AddComponent<MeshRenderer>().sharedMaterial = grass;
                var ho=tile.AddComponent<HyperObject>();
                ho.highPrecision=true;
                tiles.Add(ho);
            }
            terrain = tiles.ToArray();
            for (int i=0;i<terrain.Length;i++) SetPose(terrain[i],neighborhood[i]);
        }
    }

    void Update()
    {
        var keyboard=Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) Pause(!paused);
        if (keyboard != null && keyboard.rKey.wasPressedThisFrame) ResetExploration();
        if (paused) return;
        if(keyboard!=null&&keyboard.bKey.wasPressedThisFrame) RecallBall();
        bounceCooldown -= Time.deltaTime;
        float spring=Mathf.Max(0,bounceCooldown)/.6f;
        trampoline.transform.localScale=new Vector3(trampolineScale.x,trampolineScale.y*(1-.28f*spring*Mathf.Sin(spring*Mathf.PI)),trampolineScale.z);
        var relative=trampoline.localGV+HyperObject.worldGV;
        var p=relative.Point();
        // The trampoline uses the same tangent frame as the player, not a Euclidean trigger.
        if (trampoline.gameObject.activeSelf && new Vector2(p.x,p.z).magnitude < .05f && player.velocity.y <= 0 &&
            -HyperObject.worldGV.vec.y < .025f && bounceCooldown <= 0)
        {
            player.velocity.y=player.height*5.8f;
            bounceCooldown=.6f;
            if(bounceSound)audioSource.PlayOneShot(bounceSound,.65f);
        }
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) ball.TryKick(player);
    }

    void LateUpdate()
    {
        if (elliptic || terrain.Length==0) return;
        int nearest=0;
        float best=float.MaxValue;
        for (int i=0;i<terrain.Length;i++)
        {
            float d=(terrain[i].localGV+HyperObject.worldGV).vec.sqrMagnitude;
            if(d<best){best=d;nearest=i;}
        }
        var anchor=terrain[nearest].localGV;
        if(anchor.vec.sqrMagnitude < .01f) return;
        // Rebase the entire world onto an existing cell, preserving positions and orientation.
        // This prevents approaching the Poincare coordinate boundary during long walks.
        anchorHistory.Advance(anchor);
        HyperObject.worldGVD=(GyroVectorD)anchor+HyperObject.worldGVD;
        bool visible=anchorHistory.TryGetOriginPose(out GyroVectorD originPose);
        foreach(var landmark in fixedLandmarks)
        {
            var ho=landmark.Key;
            ho.gameObject.SetActive(visible);
            if(visible)
            {
                ho.localGVD=landmark.Value+originPose;
                ho.localGV=(GyroVector)ho.localGVD;
            }
            else foreach(var group in WCollider.AllColliders)
                if(group.ho==ho)group.skip=true;
        }
        ball.Rebase(anchor);
        for(int i=0;i<terrain.Length;i++) SetPose(terrain[i],neighborhood[i]);
        streamCount++;
    }

    static void SetPose(HyperObject ho,GyroVector pose)
    {
        ho.localGV=pose;
        ho.localGVD=(GyroVectorD)pose;
    }

    public void ResetExploration()
    {
        // Reloading also restores the fixed landmarks after any streaming rebases.
        UnityEngine.SceneManagement.SceneManager.LoadScene(gameObject.scene.name);
    }
    public void RecallBall()
    {
        var view=HyperObject.worldGV.ProjectToPlane();
        Vector3 forward=player.mainCamera.transform.forward;forward.y=0;
        if(forward.sqrMagnitude<.01f)forward=Vector3.forward;
        var pose=new GyroVector(HM.HyperTranslate(forward.normalized*.12f))-view;
        ball.Initialize(pose);
    }
    void Pause(bool value)
    {
        paused=value; InputManager.pauseInput=value;
        Cursor.lockState=value?CursorLockMode.None:CursorLockMode.Locked;
        Cursor.visible=value;
    }
    void OnGUI()
    {
        GUI.Box(new Rect(15,Screen.height-112,570,96), "");
        GUI.Label(new Rect(28,Screen.height-106,550,27),elliptic?"ELLIPTIC EXPLORATION · closed positive curvature":"HYPERBOLIC EXPLORATION · continuously streamed negative curvature");
        GUI.Label(new Rect(28,Screen.height-80,550,25),"WASD move · Space jump · click to kick · B recall ball · R reset · Esc pause");
        GUI.Label(new Rect(28,Screen.height-55,550,25),"Step onto the central trampoline. Follow the grid and the six colored stone columns.");
        GUI.Label(new Rect(Screen.width/2-5,Screen.height/2-12,20,25),"+");
        if(paused)
        {
            if(GUI.Button(new Rect(Screen.width/2-105,Screen.height/2+30,210,35),"Resume")) Pause(false);
            if(GUI.Button(new Rect(Screen.width/2-105,Screen.height/2+72,210,35),elliptic?"Explore hyperbolic space":"Explore elliptic space"))
                UnityEngine.SceneManagement.SceneManager.LoadScene(elliptic?"Hyperbolic Exploration":"Elliptic Exploration");
        }
    }
    void OnDestroy()
    {
        Shader.SetGlobalFloat("_Enable",0);
        WCollider.AllColliders.Clear();
        InputManager.pauseInput=false;
        Cursor.lockState=CursorLockMode.None; Cursor.visible=true;
    }
}
