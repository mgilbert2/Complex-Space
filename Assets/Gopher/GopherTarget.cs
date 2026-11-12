using UnityEngine;

public class GopherTarget : MonoBehaviour
{
    public enum TargetState { Hidden, Emerging, Outside, Shocked, Stunned }
    public TargetState State { get; private set; }
    public bool CanBeHit => State == TargetState.Outside || State == TargetState.Emerging;
    public bool IsOut => CanBeHit || State == TargetState.Shocked;
    public bool ReadyToEmerge => State == TargetState.Hidden && timer <= 0;
    public float hitRadius = 0.035f;

    GopherTask task;
    Animator animator;
    HyperObject hyperObject;
    Renderer body;
    SetTextures textures;
    Texture2D regularTexture;
    Transform stars;
    Vector3 starScale;
    float timer;
    float chirpTimer;

    public Vector3 Center => hyperObject.WorldPositionToLocalHyper(body.bounds.center);
    public Vector3 GroundPoint => hyperObject.WorldPositionToLocalHyper(transform.position);

    public void Initialize(GopherTask owner, Animator source)
    {
        task = owner;
        animator = source;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        hyperObject = GetComponent<HyperObject>();
        body = GetComponentInChildren<SkinnedMeshRenderer>(true);
        ((SkinnedMeshRenderer)body).updateWhenOffscreen = true;
        textures = body.GetComponent<SetTextures>();
        regularTexture = textures.texture;
        foreach (var child in GetComponentsInChildren<Transform>(true))
        {
            if (!child.name.Contains("Stars")) continue;
            stars = child;
            starScale = child.localScale;
            break;
        }
        ResetTarget(0);
    }

    public void ResetTarget(float delay)
    {
        State = TargetState.Hidden;
        timer = delay;
        animator.Rebind();
        animator.Play("Hidden", 0, 0);
        animator.Update(0);
        textures.texture = regularTexture;
        textures.UpdateTextures();
        if (stars)
        {
            stars.gameObject.SetActive(false);
            stars.localScale = starScale;
        }
    }

    public void Emerge()
    {
        if (State != TargetState.Hidden) return;
        State = TargetState.Emerging;
        timer = 0.55f;
        animator.Play("Get_Out", 0, 0);
        task.PlaySound(task.emergeSound, 0.35f);
    }

    public void Hide()
    {
        if (!CanBeHit) return;
        State = TargetState.Hidden;
        timer = Random.Range(5.5f, 6f);
        animator.Play("Get_In", 0, 0);
        task.PlaySound(task.hideSound, 0.25f);
    }

    public bool TryShock()
    {
        if (!CanBeHit) return false;
        State = TargetState.Shocked;
        timer = 0.8f;
        animator.Play("Hit", 0, 0);
        if (task.hitTexture) textures.texture = task.hitTexture;
        textures.UpdateTextures();
        task.PlaySound(task.hitSound, 0.65f);
        return true;
    }

    public void Tick(float deltaTime)
    {
        timer -= deltaTime;
        if (State == TargetState.Emerging && timer <= 0)
        {
            State = TargetState.Outside;
            timer = Random.Range(7f, 12f);
            chirpTimer = Random.Range(1f, 2.5f);
            animator.Play("Idle_01", 0, Random.value);
        }
        else if (State == TargetState.Outside)
        {
            chirpTimer -= deltaTime;
            if (chirpTimer <= 0)
            {
                task.PlaySound(task.chirpSound, 0.15f);
                chirpTimer = Random.Range(1f, 2.5f);
            }
            if (timer <= 0) Hide();
        }
        else if (State == TargetState.Shocked && timer <= 0)
        {
            State = TargetState.Stunned;
            animator.Play("Stun", 0, 0);
            if (stars) stars.gameObject.SetActive(true);
        }
        if (State == TargetState.Stunned && stars)
            stars.Rotate(0, 110f * deltaTime, 0, Space.Self);
    }
}
