using UnityEngine;

// Spread the charts out for editing, then fold them into their spherical poses at runtime.
[DefaultExecutionOrder(-150)]
public class FarmChart : MonoBehaviour
{
    public Vector3 editorOffset;

    void Awake()
    {
        transform.localPosition -= editorOffset;
    }
}
