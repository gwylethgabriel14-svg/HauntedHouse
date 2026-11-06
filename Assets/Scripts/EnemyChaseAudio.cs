
using UnityEngine;
using UnityEngine.AI;

// ========================================================
// THE LAST LULLABY - ELEANOR CHASE AUDIO
//
// Add to the same GameObject as EnemyAI.
//
// Plays:
// 1. Horror sting when Eleanor spots the player
// 2. Heartbeat/chase audio while Eleanor is chasing
// 3. Optional footsteps while running
//
// If no clips are assigned, original procedural
// alert and heartbeat sounds are generated.
//
// Compatible with EnemyAI.ChaseStarted and IsChasing.
// Stops when chase ends or EnemyAI is disabled/stunned.
// ========================================================

[DisallowMultipleComponent]
public class EnemyChaseAudio : MonoBehaviour
{
    [Header("References")]
    public EnemyAI enemyAI;
    public NavMeshAgent agent;

    [Header("Audio Clips")]
    public AudioClip spottedClip;
    public AudioClip chaseLoopClip;
    public AudioClip runningFootstepsClip;

    [Header("Automatic Test Audio")]
    public bool generateSoundsIfMissing = true;

    [Header("Spotted Sound")]
    [Range(0f, 1f)]
    public float spottedVolume = 0.8f;

    [Range(0f, 1f)]
    public float spottedSpatialBlend = 0f;

    [Header("Chase Sound")]
    [Range(0f, 1f)]
    public float chaseVolume = 0.55f;

    [Range(0f, 1f)]
    public float chaseSpatialBlend = 0.2f;

    public float chaseFadeInSpeed = 1.5f;
    public float chaseFadeOutSpeed = 2f;

    [Header("Running Footsteps")]
    [Range(0f, 1f)]
    public float footstepsVolume = 0.6f;

    public float minimumRunningSpeed = 1.5f;

    [Header("3D Audio Range")]
    public float minimumDistance = 2f;
    public float maximumDistance = 25f;

    [Header("Debug")]
    public bool showDebugLogs = true;

    private AudioSource spottedSource;
    private AudioSource chaseSource;
    private AudioSource footstepsSource;

    private AudioClip generatedSpotted;
    private AudioClip generatedHeartbeat;

    private bool wasChasing;
    private bool hooked;

    // ========================================================
    // INITIALIZATION
    // ========================================================

    private void Awake()
    {
        FindReferences();

        spottedSource = CreateSource("SpottedAudio");
        chaseSource = CreateSource("ChaseAudio");
        footstepsSource = CreateSource("RunningFootstepsAudio");

        spottedSource.loop = false;
        chaseSource.loop = true;
        footstepsSource.loop = true;

        spottedSource.volume = spottedVolume;
        chaseSource.volume = 0f;
        footstepsSource.volume = 0f;

        if (generateSoundsIfMissing)
        {
            if (spottedClip == null)
            {
                generatedSpotted =
                    GenerateSpottedSound();

                spottedClip = generatedSpotted;
            }

            if (chaseLoopClip == null)
            {
                generatedHeartbeat =
                    GenerateHeartbeat();

                chaseLoopClip = generatedHeartbeat;
            }
        }

        ApplyAudioSettings();
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void Start()
    {
        FindReferences();
        Subscribe();
        ApplyAudioSettings();

        if (enemyAI == null)
        {
            Debug.LogWarning(
                "[CHASE AUDIO] EnemyAI not found. " +
                "Attach this to Eleanor's EnemyAI object.",
                this);
        }
    }

    private void FindReferences()
    {
        if (enemyAI == null)
        {
            enemyAI = GetComponent<EnemyAI>();

            if (enemyAI == null)
                enemyAI =
                    GetComponentInParent<EnemyAI>();
        }

        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();

            if (agent == null)
                agent =
                    GetComponentInParent<NavMeshAgent>();
        }
    }

    private AudioSource CreateSource(string sourceName)
    {
        Transform existing = transform.Find(sourceName);

        GameObject sourceObject;

        if (existing != null)
        {
            sourceObject = existing.gameObject;
        }
        else
        {
            sourceObject = new GameObject(sourceName);
            sourceObject.transform.SetParent(transform, false);
            sourceObject.transform.localPosition = Vector3.zero;
        }

        AudioSource source =
            sourceObject.GetComponent<AudioSource>();

        if (source == null)
            source = sourceObject.AddComponent<AudioSource>();

        source.playOnAwake = false;
        source.loop = false;
        source.dopplerLevel = 0f;
        source.rolloffMode = AudioRolloffMode.Linear;

        return source;
    }

    private void ApplyAudioSettings()
    {
        ConfigureSource(
            spottedSource,
            spottedSpatialBlend);

        ConfigureSource(
            chaseSource,
            chaseSpatialBlend);

        ConfigureSource(
            footstepsSource,
            1f);

        if (spottedSource != null)
            spottedSource.clip = spottedClip;

        if (chaseSource != null)
            chaseSource.clip = chaseLoopClip;

        if (footstepsSource != null)
        {
            footstepsSource.clip =
                runningFootstepsClip;
        }
    }

    private void ConfigureSource(
        AudioSource source,
        float spatialBlend)
    {
        if (source == null)
            return;

        source.spatialBlend =
            Mathf.Clamp01(spatialBlend);

        source.minDistance =
            Mathf.Max(0.1f, minimumDistance);

        source.maxDistance =
            Mathf.Max(
                source.minDistance + 0.1f,
                maximumDistance);
    }

    // ========================================================
    // ENEMYAI CHASE EVENT
    // ========================================================

    private void Subscribe()
    {
        FindReferences();

        if (enemyAI == null || hooked)
            return;

        enemyAI.ChaseStarted += OnChaseStarted;
        hooked = true;
    }

    private void Unsubscribe()
    {
        if (enemyAI != null && hooked)
        {
            enemyAI.ChaseStarted -= OnChaseStarted;
        }

        hooked = false;
    }

    private bool CanPlayChase()
    {
        return enemyAI != null &&
               enemyAI.isActiveAndEnabled &&
               enemyAI.IsChasing;
    }

    private void OnChaseStarted()
    {
        if (!CanPlayChase())
            return;

        if (wasChasing)
            return;

        BeginChaseAudio();
    }

    private void BeginChaseAudio()
    {
        wasChasing = true;

        if (spottedSource != null &&
            spottedClip != null)
        {
            spottedSource.Stop();
            spottedSource.volume = spottedVolume;
            spottedSource.Play();
        }

        if (chaseSource != null &&
            chaseLoopClip != null &&
            !chaseSource.isPlaying)
        {
            chaseSource.volume = 0f;
            chaseSource.Play();
        }

        if (showDebugLogs)
        {
            Debug.Log(
                "[CHASE AUDIO] Eleanor spotted the player! " +
                "Alert and chase sounds started.",
                this);
        }
    }

    // ========================================================
    // CHASE STATE AUDIO
    // ========================================================

    private void Update()
    {
        // Pause the sounds with the game.
        if (Time.timeScale <= 0f)
        {
            SetPaused(true);
            return;
        }

        SetPaused(false);

        bool chasing = CanPlayChase();

        // Fallback if the chase event happened
        // before this component subscribed.
        if (chasing && !wasChasing)
        {
            BeginChaseAudio();
        }
        else if (!chasing && wasChasing)
        {
            EndChaseAudio();
        }

        UpdateChaseLoop(chasing);
        UpdateFootsteps(chasing);
    }

    private void SetPaused(bool paused)
    {
        if (spottedSource != null)
        {
            if (paused)
                spottedSource.Pause();
            else
                spottedSource.UnPause();
        }

        if (chaseSource != null)
        {
            if (paused)
                chaseSource.Pause();
            else
                chaseSource.UnPause();
        }

        if (footstepsSource != null)
        {
            if (paused)
                footstepsSource.Pause();
            else
                footstepsSource.UnPause();
        }
    }

    private void EndChaseAudio()
    {
        wasChasing = false;

        if (spottedSource != null)
            spottedSource.Stop();

        if (showDebugLogs)
        {
            Debug.Log(
                "[CHASE AUDIO] Eleanor stopped chasing.",
                this);
        }
    }

    private void UpdateChaseLoop(bool chasing)
    {
        if (chaseSource == null)
            return;

        if (chasing && chaseLoopClip != null &&
            !chaseSource.isPlaying)
        {
            chaseSource.clip = chaseLoopClip;
            chaseSource.Play();
        }

        float desiredVolume =
            chasing ? chaseVolume : 0f;

        float fadeSpeed =
            chasing
                ? chaseFadeInSpeed
                : chaseFadeOutSpeed;

        chaseSource.volume =
            Mathf.MoveTowards(
                chaseSource.volume,
                desiredVolume,
                Mathf.Max(0.01f, fadeSpeed) *
                Time.deltaTime);

        if (!chasing &&
            chaseSource.volume <= 0.001f &&
            chaseSource.isPlaying)
        {
            chaseSource.Stop();
        }
    }

    private void UpdateFootsteps(bool chasing)
    {
        if (footstepsSource == null)
            return;

        if (runningFootstepsClip == null)
        {
            if (footstepsSource.isPlaying)
                footstepsSource.Stop();

            return;
        }

        bool moving = false;

        if (chasing &&
            agent != null &&
            agent.enabled &&
            agent.isOnNavMesh &&
            !agent.isStopped)
        {
            moving =
                agent.velocity.magnitude >=
                minimumRunningSpeed;
        }

        if (moving)
        {
            if (!footstepsSource.isPlaying)
            {
                footstepsSource.clip =
                    runningFootstepsClip;

                footstepsSource.volume =
                    footstepsVolume;

                footstepsSource.Play();
            }

            // Make footstep pacing slightly quicker
            // as Eleanor runs faster.
            footstepsSource.pitch =
                Mathf.Clamp(
                    agent.velocity.magnitude / 4f,
                    0.9f,
                    1.3f);
        }
        else
        {
            if (footstepsSource.isPlaying)
                footstepsSource.Stop();
        }
    }

    // ========================================================
    // GENERATE ORIGINAL TEST HORROR STING
    // ========================================================

    private AudioClip GenerateSpottedSound()
    {
        const int rate = 44100;
        const float duration = 1.5f;

        int count =
            Mathf.CeilToInt(rate * duration);

        float[] data = new float[count];

        System.Random random =
            new System.Random(1987);

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)rate;

            float envelope =
                Mathf.Pow(
                    Mathf.Clamp01(1f - t / duration),
                    1.7f);

            float attack =
                Mathf.Clamp01(t / 0.025f);

            // Low cinematic rumble.
            float rumble =
                Mathf.Sin(
                    2f * Mathf.PI * 66f * t);

            // A rising pair of dissonant notes.
            float frequencyA =
                330f + 130f * t;

            float frequencyB =
                466f + 180f * t;

            float toneA =
                Mathf.Sin(
                    2f * Mathf.PI *
                    (330f * t + 65f * t * t));

            float toneB =
                Mathf.Sin(
                    2f * Mathf.PI *
                    (466f * t + 90f * t * t));

            float noise =
                (float)(random.NextDouble() * 2.0 - 1.0);

            float value =
                rumble * 0.25f +
                toneA * 0.20f +
                toneB * 0.17f +
                noise * 0.075f;

            data[i] = Mathf.Clamp(
                value * envelope * attack,
                -1f,
                1f);
        }

        AudioClip clip =
            AudioClip.Create(
                "Eleanor_Spotted_Test",
                count,
                1,
                rate,
                false);

        clip.SetData(data, 0);

        return clip;
    }

    // ========================================================
    // GENERATE ORIGINAL HEARTBEAT CHASE LOOP
    // ========================================================

    private AudioClip GenerateHeartbeat()
    {
        const int rate = 44100;
        const float duration = 2f;

        int count =
            Mathf.RoundToInt(rate * duration);

        float[] data = new float[count];

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)rate;

            // Four pairs of heartbeat pulses
            // repeat seamlessly over two seconds.
            float cycle = t % 0.5f;

            float pulse1 =
                Pulse(cycle, 0.02f, 0.11f);

            float pulse2 =
                Pulse(cycle, 0.19f, 0.075f);

            float bass =
                Mathf.Sin(
                    2f * Mathf.PI * 54f * t);

            float low =
                Mathf.Sin(
                    2f * Mathf.PI * 83f * t);

            float ominousDrone =
                Mathf.Sin(
                    2f * Mathf.PI * 41f * t) *
                0.035f;

            float beat =
                pulse1 * 0.62f +
                pulse2 * 0.39f;

            float value =
                beat *
                (bass * 0.75f + low * 0.25f) +
                ominousDrone;

            data[i] =
                Mathf.Clamp(value, -1f, 1f);
        }

        AudioClip clip =
            AudioClip.Create(
                "Eleanor_Heartbeat_Test",
                count,
                1,
                rate,
                false);

        clip.SetData(data, 0);

        return clip;
    }

    private float Pulse(
        float time,
        float start,
        float length)
    {
        float elapsed = time - start;

        if (elapsed < 0f || elapsed > length)
            return 0f;

        float fade =
            Mathf.Exp(-elapsed * 42f);

        float attack =
            Mathf.Clamp01(elapsed / 0.009f);

        return fade * attack;
    }

    // ========================================================
    // DISABLE / CLEANUP
    // ========================================================

    private void OnDisable()
    {
        Unsubscribe();

        wasChasing = false;

        if (spottedSource != null)
            spottedSource.Stop();

        if (chaseSource != null)
            chaseSource.Stop();

        if (footstepsSource != null)
            footstepsSource.Stop();
    }

    private void OnDestroy()
    {
        if (generatedSpotted != null)
            Destroy(generatedSpotted);

        if (generatedHeartbeat != null)
            Destroy(generatedHeartbeat);
    }
}
