using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-200)]
public class GopherStage : MonoBehaviour
{
    public Player player;
    public Animator[] gophers;
    bool paused;

    public static void ConfigureGeometry()
    {
        HM.SetTileType(3);
        HM.UseRadialChart = false;
        Shader.SetGlobalFloat("_BeachRadialChart", 0);
        Shader.SetGlobalFloat("_KleinV", HM.KLEIN_V);
    }

    void Awake()
    {
        ConfigureGeometry();
        WCollider.AllColliders.Clear();
        HyperObject.worldGV = GyroVector.identity;
        HyperObject.isShaking = false;
        HyperObject.updateColliders = true;
        WorldBuilder.globalBounryAO = 1;
        WorldBuilder.SetFog(new Color(0.72f, 0.86f, 0.92f, 0.15f), 0.45f);
        Shader.SetGlobalFloat("_Enable", 1);
        Shader.SetGlobalFloat("_DualLight", 1);
        Shader.SetGlobalFloat("_Proj", 0);
        Shader.SetGlobalVector("_WarpParams", Vector4.zero);
        InputManager.pauseInput = false;
    }

    void Start()
    {
        // The recovered controller defaults to Hidden. Show the models for editing.
        for (int i = 0; i < gophers.Length; i++)
        {
            gophers[i].Play("Idle_01", 0, i / (float)gophers.Length);
            gophers[i].Update(0);
        }
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            paused = !paused;
            InputManager.pauseInput = paused;
            Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = paused;
        }
    }

    void OnDestroy()
    {
        Shader.SetGlobalFloat("_Enable", 0);
        WCollider.AllColliders.Clear();
        InputManager.pauseInput = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void OnGUI()
    {
        GUI.Box(new Rect(20, Screen.height - 100, 410, 80), "Gopher farm — spherical world");
        GUI.Label(new Rect(32, Screen.height - 74, 390, 50),
            "WASD move · Mouse look · Esc release cursor\nExplore the wrapped farm. Task rules pending.");
    }
}
