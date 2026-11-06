
using UnityEngine;

// =====================================================
// THE LAST LULLABY - ELEANOR CHASE CAMERA SHAKE
//
// Attach to PLAYER'S MAIN CAMERA.
//
// Shake gets stronger when Eleanor is close
// and is actively chasing.
//
// Uses the existing EnemyAI.IsChasing state.
// Compatible with EnemyChaseAudio.
//
// Does not permanently change camera position.
// =====================================================

[DefaultExecutionOrder(500)]
[DisallowMultipleComponent]
public class EnemyChaseCameraShake : MonoBehaviour
{
    [Header("References")]
    public EnemyAI enemyAI;
    public Transform enemy;
    public Transform player;

    [Header("Chase Screen Shake")]
    public bool enableChaseShake = true;

    [Tooltip("Maximum distance where shake can begin.")]
    public float shakeStartDistance = 20f;

    [Tooltip("Shake becomes strongest within this distance.")]
    public float strongestDistance = 3f;

    [Range(0f, 0.2f)]
    public float maximumPositionShake = 0.055f;

    [Range(0f, 5f)]
    public float maximumRotationShake = 1.2f;

    public float shakeFrequency = 16f;

    [Header("Smoothness")]
    public float fadeInSpeed = 3f;
    public float fadeOutSpeed = 5f;

    [Header("Extra Spotted Impact")]
    public bool shakeWhenSpotted = true;
    public float spottedShakeDuration = 0.5f;
    public float spottedShakeStrength = 0.08f;

    [Header("Debug")]
    public bool showDebugLogs = false;

    private Vector3 lastPositionOffset;
    private Quaternion lastRotationOffset =
        Quaternion.identity;

    private float currentIntensity;
    private float spottedUntil;

    private bool subscribed;
    private bool lastChasing;

    // =====================================================
    // INITIALIZATION
    // =====================================================

    private void Awake()
    {
        FindReferences();
    }

    private void OnEnable()
    {
        FindReferences();
        Subscribe();
    }

    private void Start()
    {
        FindReferences();
        Subscribe();
    }

    private void FindReferences()
    {
        if (enemyAI == null)
        {
            enemyAI =
                FindFirstObjectByType<EnemyAI>();
        }

        if (enemy == null && enemyAI != null)
        {
            enemy = enemyAI.transform;
        }

        if (player == null)
        {
            PlayerController controller =
                GetComponentInParent<PlayerController>();

            if (controller != null)
                player = controller.transform;
        }
    }

    private void Subscribe()
    {
        if (enemyAI == null || subscribed)
            return;

        enemyAI.ChaseStarted += OnChaseStarted;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (enemyAI != null && subscribed)
        {
            enemyAI.ChaseStarted -= OnChaseStarted;
        }

        subscribed = false;
    }

    private void OnChaseStarted()
    {
        if (!shakeWhenSpotted)
            return;

        spottedUntil =
            Time.time +
            Mathf.Max(0.01f, spottedShakeDuration);

        if (showDebugLogs)
        {
            Debug.Log(
                "[CAMERA SHAKE] Eleanor spotted player!",
                this);
        }
    }

    // =====================================================
    // SHAKE AFTER CAMERA MOVEMENT
    // =====================================================

    private void LateUpdate()
    {
        // Remove our own offset from last frame.
        // This avoids continuously moving the camera
        // away from its original position.

        transform.localPosition -= lastPositionOffset;

        transform.localRotation =
            transform.localRotation *
            Quaternion.Inverse(lastRotationOffset);

        lastPositionOffset = Vector3.zero;
        lastRotationOffset = Quaternion.identity;

        if (Time.timeScale <= 0f)
        {
            currentIntensity = 0f;
            return;
        }

        if (enemyAI == null || enemy == null)
        {
            FindReferences();
            Subscribe();
            return;
        }

        bool chasing =
            enemyAI.isActiveAndEnabled &&
            enemyAI.IsChasing;

        if (chasing && !lastChasing)
        {
            OnChaseStarted();
        }

        lastChasing = chasing;

        float targetIntensity = 0f;

        if (enableChaseShake && chasing)
        {
            Vector3 center =
                player != null
                    ? player.position
                    : transform.position;

            float distance =
                Vector3.Distance(
                    center,
                    enemy.position);

            float range = Mathf.Max(
                strongestDistance + 0.1f,
                shakeStartDistance);

            targetIntensity =
                1f -
                Mathf.InverseLerp(
                    strongestDistance,
                    range,
                    distance);

            targetIntensity =
                Mathf.Clamp01(targetIntensity);

            // Smoother and stronger when close.
            targetIntensity *= targetIntensity;
        }

        float fadeSpeed =
            targetIntensity > currentIntensity
                ? fadeInSpeed
                : fadeOutSpeed;

        currentIntensity =
            Mathf.MoveTowards(
                currentIntensity,
                targetIntensity,
                Mathf.Max(0.01f, fadeSpeed) *
                Time.deltaTime);

        // One-time impact when spotted.
        float spottedIntensity = 0f;

        if (shakeWhenSpotted &&
            Time.time < spottedUntil)
        {
            float remaining =
                spottedUntil - Time.time;

            spottedIntensity =
                Mathf.Clamp01(
                    remaining /
                    Mathf.Max(
                        0.01f,
                        spottedShakeDuration));
        }

        if (currentIntensity <= 0.001f &&
            spottedIntensity <= 0.001f)
        {
            return;
        }

        float t = Time.time * shakeFrequency;

        float noiseX =
            Mathf.PerlinNoise(t, 11.4f) * 2f - 1f;

        float noiseY =
            Mathf.PerlinNoise(21.7f, t) * 2f - 1f;

        float noiseZ =
            Mathf.PerlinNoise(t * 0.8f, 39.3f) *
            2f - 1f;

        float positionStrength =
            maximumPositionShake * currentIntensity +
            spottedShakeStrength * spottedIntensity;

        float rotationStrength =
            maximumRotationShake *
            currentIntensity +
            spottedIntensity * 1.2f;

        lastPositionOffset =
            new Vector3(
                noiseX,
                noiseY,
                noiseZ * 0.25f) *
            positionStrength;

        lastRotationOffset =
            Quaternion.Euler(
                noiseY * rotationStrength,
                noiseX * rotationStrength * 0.5f,
                noiseZ * rotationStrength);

        transform.localPosition +=
            lastPositionOffset;

        transform.localRotation =
            transform.localRotation *
            lastRotationOffset;
    }

    // =====================================================
    // CLEANUP
    // =====================================================

    private void OnDisable()
    {
        Unsubscribe();
        RestoreCamera();
    }

    private void OnDestroy()
    {
        Unsubscribe();
        RestoreCamera();
    }

    private void RestoreCamera()
    {
        transform.localPosition -=
            lastPositionOffset;

        transform.localRotation =
            transform.localRotation *
            Quaternion.Inverse(lastRotationOffset);

        lastPositionOffset = Vector3.zero;

        lastRotationOffset =
            Quaternion.identity;

        currentIntensity = 0f;
        lastChasing = false;
    }
}
