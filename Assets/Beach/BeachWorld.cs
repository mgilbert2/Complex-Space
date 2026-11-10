using UnityEngine;

[DefaultExecutionOrder(-200)]
public class BeachWorld : MonoBehaviour
{
    // HyperEngine applies the chart conversion to this source height in Player.Awake.
    public const float PlayerHeight=0.1f;
    [HideInInspector] public float appliedLayoutScale=1;
    [HideInInspector] public bool appliedRadialChart=false;
    [Tooltip("The recovered game uses small source coordinates: 0.1 is approximately one player height.")]
    public Vector3 umbrellaOffset = new Vector3(-0.28f, 0, 0.46f);
    public Transform umbrella;
    public HyperObject chest;
    public Vector3 chestStart = new Vector3(0.10f, 0, 0.06f);
    public float chestStartAngle = -31;

    void Awake()
    {
        ConfigureGeometry();
        WCollider.AllColliders.Clear();
        HyperObject.worldGV = GyroVector.identity;
        HyperObject.isShaking = false;
        HyperObject.updateColliders = true;
        WorldBuilder.globalBounryAO = 0.9f;
        WorldBuilder.SetFog(new Color(0.59f, 0.79f, 0.88f, 0.38f), 0.65f);
        Shader.SetGlobalFloat("_Enable", 1);
        Shader.SetGlobalFloat("_DualLight", 1);
        Shader.SetGlobalFloat("_Proj", 0);
        Shader.SetGlobalVector("_WarpParams", Vector4.zero);
        chest.transform.localPosition = Vector3.zero;
        chest.transform.localRotation = Quaternion.identity;
        chest.localGV = PoseAt(chestStart, Quaternion.Euler(0, chestStartAngle, 0));
        if (umbrella) umbrella.localPosition = umbrellaOffset;
    }

    public static void ConfigureGeometry()
    {
        HM.SetTileType(5);
        HM.UseRadialChart=true;
        Shader.SetGlobalFloat("_BeachRadialChart",1);
        Shader.SetGlobalFloat("_KleinV",HM.KLEIN_V);
    }

    public static GyroVector PoseAt(Vector3 unitPoint, Quaternion rotation)
    {
        Vector3 point = HM.UnitToPoincare(unitPoint, true);
        return new GyroVector(Quaternion.Inverse(rotation) * point, rotation);
    }

    void OnDestroy()
    {
        Shader.SetGlobalFloat("_Enable", 0);
        Shader.SetGlobalFloat("_BeachRadialChart",0);
        HM.UseRadialChart=false;
        WCollider.AllColliders.Clear();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 0.9f);
        var p=BeachFootprint.Points;
        for(int i=0;i<p.Length;i++)
            Gizmos.DrawLine(new Vector3(p[i].x,0.02f,p[i].y),new Vector3(p[(i+1)%p.Length].x,0.02f,p[(i+1)%p.Length].y));
    }
}
