using UnityEngine;
using UnityEngine.InputSystem;

public class BeachPuzzle : MonoBehaviour
{
    public Player player;
    public HyperObject chest;
    public Transform hole;
    public BeachWorld world;
    public float chestRadius = 0.05f;
    public float positionToleranceSquared = 0.0003f;
    public float angleTolerance = 6;
    public bool solved;
    public float angleError;
    public float distanceSquared;
    public int pushCount;
    GyroVector startPose, goalPose;
    WarpCollider chestCollider;
    bool mapVisible, paused;
    float sink;
    GUIStyle title, body;
    Texture2D mapTexture;

    void Start()
    {
        startPose = chest.localGV;
        goalPose = BeachWorld.PoseAt(hole.position, hole.rotation);
        chestCollider = chest.GetComponent<WarpCollider>();
        player.AddPreCollision(this);
        Player.ENABLE_HEAD_BOB = false;
        Player.CAM_SMOOTHING_PREF = 1;
        SetPaused(false);
    }

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.escapeKey.wasPressedThisFrame) SetPaused(!paused);
            if (keyboard.rKey.wasPressedThisFrame) ResetPuzzle();
            if (keyboard.qKey.wasPressedThisFrame) mapVisible = !mapVisible;
        }
        if (paused && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) SetPaused(false);
        GyroVector relative = chest.localGV - goalPose;
        distanceSquared = relative.Point().sqrMagnitude;
        angleError = Quaternion.Angle(relative.gyr, Quaternion.identity);
        if (!solved && distanceSquared < positionToleranceSquared && angleError < angleTolerance)
        {
            solved = true;
            Debug.Log("[BeachPuzzle] Chest returned to its hole with the correct orientation.");
        }
        if (solved)
        {
            sink = Mathf.Min(sink + Time.deltaTime * 0.07f, 0.2f);
            chest.transform.localPosition = Vector3.down * sink;
            if (chestCollider.groupIx >= 0 && chestCollider.groupIx < WCollider.AllColliders.Count)
                WCollider.AllColliders[chestCollider.groupIx].skip = true;
        }
    }

    public void OnPreCollide(Player movingPlayer)
    {
        if (paused) return;
        var proposedWorld=HyperObject.worldGV-movingPlayer.inputDeltaXZ;
        var proposedUnit=HM.PoincareToUnit((-proposedWorld).Point(),true);
        if(!BeachFootprint.Contains(proposedUnit,.08f))
        {
            movingPlayer.inputDelta=new Vector3(0,movingPlayer.inputDelta.y,0);
            movingPlayer.velocity=new Vector3(0,movingPlayer.velocity.y,0);
            return;
        }
        if(solved)return;
        GyroVector viewPose = chest.localGV + HyperObject.worldGV;
        Vector3 center = viewPose.Point();
        Vector3 flat = new Vector3(center.x, 0, center.z);
        Vector3 delta = movingPlayer.inputDeltaXZ;
        float reach = chestRadius + movingPlayer.CollisionRadius() + 0.016f;
        if (flat.magnitude > reach || Vector3.Dot(delta, flat) <= 0 || delta.sqrMagnitude < 1e-12f) return;
        // Translation follows a geodesic; the frame is parallel transported rather than manually rotated.
        Vector3 targetViewPoint = HM.MobiusAdd(delta * 0.9f, center);
        GyroVector nextView = TransportTo(viewPose, targetViewPoint);
        GyroVector nextLocal = nextView - HyperObject.worldGV;
        Vector3 nextUnit = HM.PoincareToUnit(nextLocal.Point(), true);
        if (!BeachFootprint.Contains(nextUnit,BeachFootprint.ChestClearance)) return;
        if (chestCollider.groupIx < 0) return;
        var ownGroup = WCollider.AllColliders[chestCollider.groupIx];
        ownGroup.skip = true;
        var obstruction = WCollider.Collide(targetViewPoint + Vector3.up * chestRadius, chestRadius);
        ownGroup.skip = false;
        if (obstruction.displacement.sqrMagnitude > 1e-7f) return;
        chest.localGV = nextLocal;
        chest.UpdateCollisions();
        movingPlayer.isPushing = true;
        pushCount++;
    }

    public static GyroVector TransportTo(GyroVector pose, Vector3 point)
    {
        Vector3 localStep = (-pose) * point;
        return new GyroVector(localStep) + pose;
    }

    public void ResetPuzzle()
    {
        solved = false; sink = 0; pushCount = 0;
        chest.transform.localPosition = Vector3.zero;
        chest.localGV = startPose;
        if (chestCollider != null && chestCollider.groupIx >= 0)
            WCollider.AllColliders[chestCollider.groupIx].skip = false;
        chest.UpdateCollisions();
    }

    void SetPaused(bool value)
    {
        paused = value;
        InputManager.pauseInput = value;
        Cursor.lockState = value ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = value;
    }

    void OnDisable()
    {
        if (player) player.RemovePreCollision(this);
        InputManager.pauseInput = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if(mapTexture) Destroy(mapTexture);
    }

    void OnGUI()
    {
        if (title == null)
        {
            title = new GUIStyle(GUI.skin.label) {fontSize = 21, fontStyle = FontStyle.Bold, wordWrap = true};
            body = new GUIStyle(GUI.skin.label) {fontSize = 15, wordWrap = true};
        }
        float scale = Mathf.Clamp(Screen.height / 850f, 0.7f, 1.5f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale,scale,1));
        float h=Screen.height/scale, w=Screen.width/scale;
        GUI.Box(new Rect(20,h-144,470,124),GUIContent.none);
        GUI.Label(new Rect(36,h-135,440,32),solved ? "The chest is home. Curse lifted!" : "Return the chest to its hole",title);
        GUI.Label(new Rect(36,h-101,440,46),solved ? "Press R to try again." : "Push it by walking into it. A loop through curved space changes its angle.",body);
        GUI.Label(new Rect(36,h-53,440,26),"WASD Move  ·  Mouse Look  ·  Q Map  ·  R Reset  ·  Esc Cursor",body);
        if (paused) GUI.Label(new Rect(w/2-140,h/2-20,280,60),"Click to continue",title);
        if (mapVisible) DrawMap(new Rect(w-280,20,260,295));
    }

    void DrawMap(Rect box)
    {
        GUI.Box(box,GUIContent.none);
        GUI.Label(new Rect(box.x+14,box.y+8,230,28),"Beach map",title);
        float aspect=(BeachFootprint.Max.y-BeachFootprint.Min.y)/(BeachFootprint.Max.x-BeachFootprint.Min.x);
        Rect map=new Rect(box.x+20,box.y+65,220,220*aspect);
        if(!mapTexture)
        {
            mapTexture=new Texture2D(256,138,TextureFormat.RGBA32,false);
            var pixels=new Color[256*138];
            for(int y=0;y<138;y++) for(int x=0;x<256;x++)
            {
                var point=new Vector2(Mathf.Lerp(BeachFootprint.Min.x,BeachFootprint.Max.x,(x+.5f)/256),Mathf.Lerp(BeachFootprint.Min.y,BeachFootprint.Max.y,(y+.5f)/138));
                pixels[y*256+x]=BeachFootprint.Contains(point)?new Color(.78f,.68f,.42f):new Color(.12f,.56f,.7f);
            }
            mapTexture.SetPixels(pixels);mapTexture.Apply();
        }
        GUI.DrawTexture(map,mapTexture);
        Vector3 unitChest=HM.PoincareToUnit(chest.localGV.Point(),true);
        Vector3 unitPlayer=HM.PoincareToUnit((-HyperObject.worldGV).Point(),true);
        Mark(map,hole.position,new Color(0.23f,0.16f,0.08f),12);
        Mark(map,unitChest,new Color(1f,0.8f,0.12f),9);
        Mark(map,unitPlayer,new Color(0.12f,0.65f,1f),8);
        GUI.Label(new Rect(box.x+14,box.y+270,235,24),"Blue: you   Gold: chest   Brown: hole",new GUIStyle(body){fontSize=12});
    }
    static void Mark(Rect map,Vector3 p,Color c,float size)
    {
        Vector2 pixel=new Vector2(map.x+(p.x-BeachFootprint.Min.x)/(BeachFootprint.Max.x-BeachFootprint.Min.x)*map.width,
            map.yMax-(p.z-BeachFootprint.Min.y)/(BeachFootprint.Max.y-BeachFootprint.Min.y)*map.height);
        Fill(new Rect(pixel.x-size/2,pixel.y-size/2,size,size),c);
    }
    static void Fill(Rect r,Color c)
    {
        Color old=GUI.color; GUI.color=c; GUI.DrawTexture(r,Texture2D.whiteTexture); GUI.color=old;
    }
}
