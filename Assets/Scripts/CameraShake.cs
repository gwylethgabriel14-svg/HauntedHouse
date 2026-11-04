using UnityEngine;
using UnityEngine.Rendering;

// Shakes the camera without fighting the player's mouse look.
// You never add this yourself:  CameraShake.Shake(0.3f, 0.1f);  creates it on the main camera.
[RequireComponent(typeof(Camera))]
public class CameraShake : MonoBehaviour
{
    Camera cam;
    float duration, magnitude, elapsed;
    float seed;

    Vector3 offsetPos;
    Vector3 offsetRot;

    Vector3 savedPos;
    Quaternion savedRot;
    bool applied;

    public static void Shake(float seconds, float amount)
    {
        var c = Camera.main;
        if (c == null) return;
        var s = c.GetComponent<CameraShake>();
        if (s == null) s = c.gameObject.AddComponent<CameraShake>();
        s.Begin(seconds, amount);
    }

    void Awake()
    {
        cam = GetComponent<Camera>();
    }

    void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += OnBegin;
        RenderPipelineManager.endCameraRendering += OnEnd;
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBegin;
        RenderPipelineManager.endCameraRendering -= OnEnd;
    }

    void Begin(float seconds, float amount)
    {
        // a stronger shake replaces a weaker one that is still running
        float remaining = Mathf.Max(0f, duration - elapsed);
        if (amount * seconds < magnitude * remaining) return;

        duration = seconds;
        magnitude = amount;
        elapsed = 0f;
        seed = Random.value * 100f;
    }

    void Update()
    {
        if (elapsed >= duration) { offsetPos = Vector3.zero; offsetRot = Vector3.zero; return; }

        elapsed += Time.unscaledDeltaTime;
        float fade = 1f - Mathf.Clamp01(elapsed / duration);
        float m = magnitude * fade;
        float t = Time.unscaledTime * 28f;

        offsetPos = new Vector3(N(t, 0f), N(t, 10f), 0f) * m;
        offsetRot = new Vector3(N(t, 20f), N(t, 30f), N(t, 40f) * 2f) * (m * 40f);
    }

    float N(float t, float o)
    {
        return (Mathf.PerlinNoise(seed + o, t) - 0.5f) * 2f;
    }

    // applied just before the camera draws and removed right after, so nothing else is disturbed
    void OnBegin(ScriptableRenderContext context, Camera c)
    {
        if (c != cam || (offsetPos == Vector3.zero && offsetRot == Vector3.zero)) return;
        savedPos = transform.localPosition;
        savedRot = transform.localRotation;
        transform.localPosition = savedPos + offsetPos;
        transform.localRotation = savedRot * Quaternion.Euler(offsetRot);
        applied = true;
    }

    void OnEnd(ScriptableRenderContext context, Camera c)
    {
        if (!applied || c != cam) return;
        transform.localPosition = savedPos;
        transform.localRotation = savedRot;
        applied = false;
    }
}