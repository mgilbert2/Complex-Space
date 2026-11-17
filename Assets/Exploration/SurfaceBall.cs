using UnityEngine;

public class SurfaceBall : MonoBehaviour
{
    public HyperObject hyper;
    public Transform visual;
    public float speed;
    public const float Radius=.025f;
    GyroVectorD groundPose;
    GyroVectorD storedGroundPose;
    readonly ExploreAnchorHistory anchorHistory=new ExploreAnchorHistory();
    bool initialized;
    bool visible=true;
    Renderer[] renderers;
    public int CollisionCount { get; private set; }
    public bool IsVisible { get { return visible; } }

    public void Initialize(GyroVector pose)
    {
        groundPose=OnGround((GyroVectorD)pose);
        initialized=true;
        speed=0;
        CollisionCount=0;
        hyper.highPrecision=true;
        storedGroundPose=groundPose;
        anchorHistory.Clear();
        SetVisible(true);
        ApplyPose();
    }
    void Start() { if(!initialized) Initialize(hyper.localGV); }
    public bool TryKick(Player player)
    {
        if(!initialized || !visible || InputManager.pauseInput || !player || !player.mainCamera) return false;
        // Project the observer onto the ground before choosing a tangent direction.
        // A kick while the player is bouncing must never pitch the ball off the surface.
        var groundView=OnGround(HyperObject.worldGVD);
        var relative=groundPose+groundView;
        Vector3 p=(Vector3)relative.Point();
        if(new Vector2(p.x,p.z).magnitude>.22f) return false;
        var forward=player.mainCamera.transform.forward; forward.y=0;
        if(forward.sqrMagnitude<.01f) return false;
        if(Vector3.Dot(forward.normalized,new Vector3(p.x,0,p.z).normalized)<.4f) return false;
        // Turn the local tangent frame without changing the ball's ground point.
        Vector3 localForward=(Vector3)(QuaternionD.Inverse(relative.gyr)*(Vector3D)forward.normalized);
        float yaw=Mathf.Atan2(localForward.x,localForward.z)*Mathf.Rad2Deg;
        groundPose=new GyroVectorD((QuaternionD)Quaternion.Euler(0,yaw,0))+groundPose;
        storedGroundPose=groundPose;
        anchorHistory.Clear();
        speed=.65f; ApplyPose(); return true;
    }
    public void Advance(float dt)
    {
        if(!initialized || InputManager.pauseInput || dt<=0 || speed<=0) return;
        float distance=speed*dt;
        int steps=Mathf.Max(1,Mathf.CeilToInt(distance/.002f));
        float stepDistance=distance/steps;
        var step=new GyroVectorD((Vector3D)HM.HyperTranslate(0,0,stepDistance));
        bool localChart=TryResolvePose();
        var movingPose=localChart?groundPose:storedGroundPose;
        float traveled=0;
        for(int i=0;i<steps;i++)
        {
            // Left composition parallel transports the local forward direction.
            var candidate=step+movingPose;
            if(localChart && ObstacleNormal(candidate,out Vector3 normal))
            {
                Vector3 reflected=Vector3.Reflect(Vector3.forward,normal);
                float yaw=Mathf.Atan2(reflected.x,reflected.z)*Mathf.Rad2Deg;
                movingPose=new GyroVectorD((QuaternionD)Quaternion.Euler(0,yaw,0))+movingPose;
                speed*=.55f;
                CollisionCount++;
                break;
            }
            movingPose=candidate;
            traveled+=stepDistance;
        }
        speed=Mathf.Max(0,speed-.035f*dt);
        // HyperTranslate(d) advances 2d in the unit-curvature metric.
        visual.Rotate(Vector3.right,2*traveled/Radius*Mathf.Rad2Deg,Space.Self);
        if(localChart)
        {
            // Reanchor active motion in the current local chart before it can
            // accumulate a long path near the Poincare boundary.
            groundPose=movingPose;
            storedGroundPose=groundPose;
            anchorHistory.Clear();
            ApplyPose();
        }
        else storedGroundPose=movingPose;
    }
    void Update(){Advance(Mathf.Min(Time.deltaTime,.05f));}
    void ApplyPose()
    {
        hyper.localGVD=groundPose;
        hyper.localGV=(GyroVector)groundPose;
    }
    public void Rebase(GyroVector anchor)
    {
        if(!initialized) return;
        anchorHistory.Advance(anchor);
        TryResolvePose();
    }

    bool TryResolvePose()
    {
        if(HM.K>=0)
        {
            groundPose=storedGroundPose;
            SetVisible(true);
            return true;
        }
        if(anchorHistory.TryGetOriginPose(out GyroVectorD originPose))
        {
            var resolved=storedGroundPose+originPose;
            double squared=resolved.vec.sqrMagnitude;
            if(!double.IsNaN(squared) && !double.IsInfinity(squared) && squared<.995*.995)
            {
                groundPose=resolved;
                SetVisible(true);
                ApplyPose();
                return true;
            }
        }
        // Keep the simulation and its path history alive while rendering is
        // hidden. Retracing terrain changes restores the stored ground pose.
        SetVisible(false);
        hyper.localGVD=GyroVectorD.identity;
        hyper.localGV=GyroVector.identity;
        return false;
    }

    void SetVisible(bool value)
    {
        visible=value;
        hyper.enabled=value;
        if(renderers==null) renderers=GetComponentsInChildren<Renderer>(true);
        foreach(var renderer in renderers) renderer.enabled=value;
    }

    static GyroVectorD OnGround(GyroVectorD pose)
    {
        return new GyroVectorD(HM.ProjectToPlane(pose.vec),
            new QuaternionD(0,pose.gyr.y,0,pose.gyr.w));
    }

    static bool ObstacleNormal(GyroVectorD pose,out Vector3 normal)
    {
        // Evaluate each obstacle in the ball's own chart. This remains stable
        // across the spherical projection pole and hyperbolic origin rebases.
        Vector3 center=Vector3.up*HM.TanK(Radius*.5f);
        float radius=Radius*.5f;
        foreach(var group in WCollider.AllColliders)
        {
            if(group==null || !group.ho || !group.ho.isActiveAndEnabled) continue;
            var obstacle=group.ho.highPrecision?group.ho.localGVD:(GyroVectorD)group.ho.localGV;
            var relative=(GyroVector)(obstacle-pose);
            if(relative.vec.sqrMagnitude>2) continue;
            foreach(var collider in group.colliders)
            {
                Vector3 delta=collider.ClosestPoint(center,relative)-center;
                float squared=delta.sqrMagnitude;
                if(squared<=1e-12f || squared>=radius*radius || float.IsNaN(squared)) continue;
                normal=-delta;normal.y=0;
                if(normal.sqrMagnitude<1e-10f) continue;
                normal.Normalize();return true;
            }
        }
        normal=Vector3.zero;return false;
    }
}
