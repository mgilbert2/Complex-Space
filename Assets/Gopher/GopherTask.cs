using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class GopherTask : MonoBehaviour
{
    public IReadOnlyList<GopherTarget> Targets => targets;
    public bool Complete { get; private set; }
    public int StunnedCount { get; private set; }
    public int Shots { get; private set; }
    public AudioClip shotSound, hitSound, emergeSound, hideSound, chirpSound;
    public Texture2D hitTexture;

    readonly List<GopherTarget> targets = new List<GopherTarget>();
    GopherStage stage;
    Material boltMaterial;
    AudioSource audioSource;
    AudioClip completeSound;
    Transform weapon;
    Mesh weaponMesh;
    float cooldown;
    float elapsed;
    float finishedTime;
    GUIStyle titleStyle, textStyle;

    public void Initialize(GopherStage source)
    {
        stage = source;
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0;
        shotSound = Resources.Load<AudioClip>("Gopher/plasma_shot");
        hitSound = Resources.Load<AudioClip>("Gopher/gopher_hit");
        emergeSound = Resources.Load<AudioClip>("Gopher/gopher_emerge");
        hideSound = Resources.Load<AudioClip>("Gopher/gopher_hide");
        chirpSound = Resources.Load<AudioClip>("Gopher/gopher_chirp1");
        completeSound = Resources.Load<AudioClip>("Gopher/complete");
        hitTexture = Resources.Load<Texture2D>("Gopher/Gopher_Hit");
        boltMaterial = new Material(Shader.Find("Custom/SphericalShader"));
        boltMaterial.SetColor("_Color", new Color(0.25f, 0.95f, 1));
        boltMaterial.SetFloat("_Ambient", 1);
        boltMaterial.EnableKeyword("GLOW");
        CreateWeapon();
        foreach (var actor in stage.gophers)
        {
            var target = actor.gameObject.AddComponent<GopherTarget>();
            target.Initialize(this, actor);
            targets.Add(target);
        }
        ResetLevel();
    }

    void Update()
    {
        if (!stage) return;
        if (weapon) weapon.localPosition = new Vector3(0.25f, -0.21f, 0.55f - cooldown * 0.08f);
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) ResetLevel();
        if (InputManager.pauseInput) return;
        if (!Complete)
        {
            float deltaTime = Time.deltaTime;
            elapsed += deltaTime;
            cooldown = Mathf.Max(0, cooldown - deltaTime);
            int outCount = 0;
            foreach (var target in targets)
            {
                target.Tick(deltaTime);
                if (target.IsOut) outCount++;
            }
            foreach (var target in targets)
            {
                if (outCount >= 2 || !target.ReadyToEmerge) continue;
                target.Emerge();
                outCount++;
            }
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && Cursor.lockState == CursorLockMode.Locked)
                TryFire();
            CheckCompletion();
        }
    }

    void CreateWeapon()
    {
        var model = Resources.Load<Mesh>("Gopher/Object_Plasma_Gun");
        var material = Resources.Load<Material>("Gopher/PlasmaBlaster");
        if (!model || !material) return;
        weapon = new GameObject("Plasma blaster").transform;
        weapon.SetParent(stage.player.mainCamera.transform, false);
        weapon.localPosition = new Vector3(0.25f, -0.21f, 0.55f);
        weapon.localRotation = Quaternion.Euler(90, 0, 0);
        weapon.localScale = Vector3.one * 2.8f;
        weaponMesh = Instantiate(model);
        var colors = new Color[weaponMesh.vertexCount];
        for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;
        weaponMesh.colors = colors;
        weapon.gameObject.AddComponent<MeshFilter>().sharedMesh = weaponMesh;
        var renderer = weapon.gameObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        var properties = new MaterialPropertyBlock();
        properties.SetMatrix("_HyperRot", Matrix4x4.identity);
        renderer.SetPropertyBlock(properties);
    }

    public void CheckCompletion()
    {
        StunnedCount = 0;
        foreach (var target in targets)
            if (target.State == GopherTarget.TargetState.Stunned) StunnedCount++;
        if (Complete || targets.Count == 0 || StunnedCount != targets.Count) return;
        Complete = true;
        finishedTime = elapsed;
        ClearBolts();
        stage.SetPaused(true);
        PlaySound(completeSound, 0.8f);
    }

    public bool TryFire()
    {
        if (Complete || cooldown > 0 || InputManager.pauseInput) return false;
        var rotation = stage.player.mainCamera.transform.rotation;
        var viewPose = new GyroVector(Quaternion.Inverse(rotation) * stage.player.HeadCollisionPoint(), rotation);
        viewPose = new GyroVector(Vector3.forward * 0.025f) + viewPose;
        SpawnBolt(viewPose - HyperObject.worldGV);
        cooldown = 0.5f;
        Shots++;
        PlaySound(shotSound, 0.35f);
        return true;
    }

    public GopherBolt SpawnBolt(GyroVector pose)
    {
        var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ball.name = "Plasma bolt";
        Destroy(ball.GetComponent<Collider>());
        ball.transform.localScale = Vector3.one * 0.014f;
        ball.GetComponent<Renderer>().sharedMaterial = boltMaterial;
        var ho = ball.AddComponent<HyperObject>();
        ho.useTanKHeight = false;
        ho.allowCulling = false;
        var bolt = ball.AddComponent<GopherBolt>();
        bolt.Initialize(this, pose);
        return bolt;
    }

    public void ResetLevel()
    {
        ClearBolts();
        Complete = false;
        StunnedCount = 0;
        Shots = 0;
        elapsed = 0;
        cooldown = 0;
        for (int i = 0; i < targets.Count; i++) targets[i].ResetTarget(0.5f + i * 0.8f);
        for (int i = 0; i < Mathf.Min(2, targets.Count); i++) targets[i].Emerge();
        HyperObject.worldGV = new GyroVector(-stage.player.startHyper);
        stage.player.velocity = Vector3.zero;
        stage.SetPaused(false);
    }

    void ClearBolts()
    {
        foreach (var bolt in FindObjectsByType<GopherBolt>(FindObjectsSortMode.None))
        {
            bolt.gameObject.SetActive(false);
            Destroy(bolt.gameObject);
        }
    }

    public void PlaySound(AudioClip clip, float volume)
    {
        if (clip) audioSource.PlayOneShot(clip, volume);
    }

    void OnDestroy()
    {
        if (boltMaterial) Destroy(boltMaterial);
        if (weaponMesh) Destroy(weaponMesh);
    }

    void OnGUI()
    {
        if (!stage) return;
        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            textStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
        }
        GUI.Box(new Rect(20, Screen.height - 136, 485, 116), GUIContent.none);
        GUI.Label(new Rect(34, Screen.height - 128, 460, 30), "Stun all four gophers — " + StunnedCount + "/" + targets.Count, textStyle);
        GUI.Label(new Rect(34, Screen.height - 96, 455, 76),
            "Aim and click to fire plasma when a gopher emerges.\nWASD move · Mouse aim · Left click fire\nR restart · Esc release/resume cursor", textStyle);
        if (!InputManager.pauseInput && !Complete)
        {
            float x = Screen.width * 0.5f, y = Screen.height * 0.5f;
            GUI.color = cooldown > 0 ? Color.gray : Color.cyan;
            GUI.DrawTexture(new Rect(x - 7, y - 1, 14, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x - 1, y - 7, 2, 14), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
        if (Complete)
        {
            var box = new Rect(Screen.width / 2f - 230, Screen.height / 2f - 100, 460, 185);
            GUI.Box(box, GUIContent.none);
            GUI.Label(new Rect(box.x, box.y + 14, box.width, 36), "Farm complete!", titleStyle);
            GUI.Label(new Rect(box.x + 30, box.y + 60, 400, 55),
                "All four gophers stunned.\nTime " + finishedTime.ToString("F1") + "s · Shots " + Shots, textStyle);
            if (GUI.Button(new Rect(box.x + 130, box.y + 128, 200, 38), "Play again (R)")) ResetLevel();
        }
        else if (InputManager.pauseInput)
            GUI.Label(new Rect(Screen.width / 2f - 200, Screen.height / 2f - 25, 400, 50), "Press Esc to resume", titleStyle);
    }
}
