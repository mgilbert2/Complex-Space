using UnityEngine;

[DefaultExecutionOrder(200)]
public class GopherBolt : MonoBehaviour
{
    const float Speed = 2.1f;
    const float Radius = 0.004f;
    GopherTask task;
    HyperObject hyperObject;
    float remaining = 1.5f;
    bool spent;

    public void Initialize(GopherTask owner, GyroVector pose)
    {
        task = owner;
        hyperObject = GetComponent<HyperObject>();
        hyperObject.localGV = pose;
    }

    void LateUpdate()
    {
        if (!task || !hyperObject) { Destroy(gameObject); return; }
        if (InputManager.pauseInput || task.Complete) return;
        float deltaTime = Mathf.Min(Time.deltaTime, 0.1f);
        remaining -= deltaTime;
        if (remaining <= 0) { Destroy(gameObject); return; }
        Advance(deltaTime);
    }

    public void Advance(float deltaTime)
    {
        if (spent) return;
        int steps = Mathf.Max(1, Mathf.CeilToInt(Speed * deltaTime / 0.003f));
        Vector3 step = HM.HyperTranslate(Vector3.forward * (Speed * deltaTime / steps));
        for (int i = 0; i < steps; i++)
        {
            hyperObject.localGV = new GyroVector(step) + hyperObject.localGV;
            if (Blocked(hyperObject.localGV)) { Stop(); return; }
            foreach (var target in task.Targets)
            {
                if (!target.CanBeHit || !WithinRadius(hyperObject.localGV.Point(), target.Center, target.hitRadius)) continue;
                target.TryShock();
                Stop();
                return;
            }
        }
    }

    void Stop()
    {
        spent = true;
        Destroy(gameObject);
    }

    static bool Blocked(GyroVector pose)
    {
        // Evaluate colliders in the bolt's local chart, avoiding the projection pole.
        for (int i = 0; i < WCollider.AllColliders.Count; i++)
        {
            var group = WCollider.AllColliders[i];
            if (group.skip || !group.ho || (group.ho.localGV - pose).vec.sqrMagnitude > 2) continue;
            var hit = WCollider.Collide(Vector3.zero, Radius, i, i + 1, false, pose);
            if (hit.displacement.sqrMagnitude > 1e-10f) return true;
        }
        return false;
    }

    public static bool WithinRadius(Vector3 a, Vector3 b, float angularRadius)
    {
        float chord = 2f * Mathf.Sin(angularRadius * 0.5f);
        return (OnSphere(a) - OnSphere(b)).sqrMagnitude <= chord * chord;
    }

    static Vector4 OnSphere(Vector3 point)
    {
        float squared = point.sqrMagnitude;
        float denominator = 1 + squared;
        return new Vector4(2 * point.x / denominator, 2 * point.y / denominator,
            2 * point.z / denominator, (1 - squared) / denominator);
    }
}
