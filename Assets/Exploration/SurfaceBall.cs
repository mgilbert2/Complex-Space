using UnityEngine;

public class SurfaceBall : MonoBehaviour
{
    public HyperObject hyper;
    public Transform visual;
    public float speed;
    public const float Radius=.025f;
    GyroVector groundPose;
    bool initialized;
    public void Initialize(GyroVector pose)
    {
        groundPose=pose; initialized=true; speed=0; ApplyPose();
    }
    void Start() { if(!initialized) Initialize(hyper.localGV); }
    public bool TryKick(Player player)
    {
        var relative=groundPose+HyperObject.worldGV;
        Vector3 p=relative.Point();
        if(new Vector2(p.x,p.z).magnitude>.22f) return false;
        var forward=player.mainCamera.transform.forward; forward.y=0;
        if(forward.sqrMagnitude<.01f) return false;
        if(Vector3.Dot(forward.normalized,new Vector3(p.x,0,p.z).normalized)<.4f) return false;
        // Set the tangent direction at the player's frame, then parallel transport to the ball.
        var rotation=Quaternion.LookRotation(forward.normalized,Vector3.up);
        var viewPose=new GyroVector(Quaternion.Inverse(rotation)*p,rotation);
        groundPose=viewPose-HyperObject.worldGV;
        speed=.65f; ApplyPose(); return true;
    }
    public void Advance(float dt)
    {
        if(!initialized || InputManager.pauseInput) return;
        float distance=speed*dt;
        // Left composition transports the local forward direction along the geodesic.
        groundPose=new GyroVector(HM.HyperTranslate(0,0,distance))+groundPose;
        speed=Mathf.Max(0,speed-.035f*dt);
        visual.Rotate(Vector3.right,distance/Radius*Mathf.Rad2Deg,Space.Self);
        ApplyPose();
    }
    void Update(){Advance(Mathf.Min(Time.deltaTime,.05f));}
    void ApplyPose(){hyper.localGV=groundPose;}
    public void Rebase(GyroVector anchor){groundPose=groundPose-anchor;ApplyPose();}
}
