using UnityEngine;

// Goes on the Player. Handles footsteps, heartbeat, chase music + sting and the ambient drone.
// (The enemy's hum is a normal 3D AudioSource on the Enemy object.)
[RequireComponent(typeof(PlayerController))]
public class GameAudio : MonoBehaviour
{
    [Header("Clips (assigned by Haunted House > FINISH GAME)")]
    public AudioClip footstep;
    public AudioClip heartbeat;
    public AudioClip chaseSting;
    public AudioClip chaseMusic;
    public AudioClip ambience;

    [Header("Refs")]
    public EnemyAI enemy;

    [Header("Tuning")]
    public float heartbeatRange = 8f;
    [Range(0f, 1f)] public float masterVolume = 1f;

    PlayerController pc;
    AudioSource stepSrc, stingSrc, heartSrc, musicSrc, ambSrc;
    float stepTimer;

    void Awake()
    {
        pc = GetComponent<PlayerController>();

        stepSrc  = MakeSource(false);
        stingSrc = MakeSource(false);
        heartSrc = MakeLoop(heartbeat, 0f);
        musicSrc = MakeLoop(chaseMusic, 0f);
        ambSrc   = MakeLoop(ambience, 0.35f * masterVolume);
    }

    void Start()
    {
        if (enemy == null) enemy = FindFirstObjectByType<EnemyAI>();
        if (enemy != null) enemy.ChaseStarted += OnChaseStarted;
    }

    void OnDestroy()
    {
        if (enemy != null) enemy.ChaseStarted -= OnChaseStarted;
    }

    AudioSource MakeSource(bool loop)
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = loop;
        s.spatialBlend = 0f;   // 2D: heard the same everywhere
        return s;
    }

    AudioSource MakeLoop(AudioClip clip, float volume)
    {
        var s = MakeSource(true);
        s.clip = clip;
        s.volume = volume;
        if (clip != null) s.Play();
        return s;
    }

    void OnChaseStarted()
    {
        if (chaseSting != null) stingSrc.PlayOneShot(chaseSting, 0.9f * masterVolume);
    }

    void Update()
    {
        // Game over screen: silence the tension loops
        if (Time.timeScale == 0f)
        {
            heartSrc.volume = 0f;
            musicSrc.volume = 0f;
            return;
        }

        // ---- Footsteps: faster and louder the more noise you make ----
        float noise = pc.CurrentNoise;
        if (noise > 0f && footstep != null)
        {
            stepTimer -= Time.deltaTime;
            if (stepTimer <= 0f)
            {
                bool run  = noise >= pc.noiseRun - 0.01f;
                bool walk = !run && noise >= pc.noiseWalk - 0.01f;
                float interval = run ? 0.32f : (walk ? 0.5f : 0.8f);
                float volume   = run ? 0.9f  : (walk ? 0.5f : 0.22f);

                stepSrc.pitch = Random.Range(0.9f, 1.1f);
                stepSrc.PlayOneShot(footstep, volume * masterVolume);
                stepTimer = interval;
            }
        }
        else
        {
            stepTimer = 0f;   // first step plays immediately when you start moving
        }

        // ---- Heartbeat: louder and faster as the enemy gets close ----
        if (enemy != null)
        {
            float d = Vector3.Distance(transform.position, enemy.transform.position);
            float t = Mathf.Clamp01(1f - d / heartbeatRange);
            heartSrc.volume = t * 0.9f * masterVolume;
            heartSrc.pitch = Mathf.Lerp(0.9f, 1.5f, t);

            // ---- Chase music fades in while she is chasing you, out slowly after ----
            float target = enemy.IsChasing ? 0.5f * masterVolume : 0f;
            float speed = target > 0f ? 1.5f : 0.4f;
            musicSrc.volume = Mathf.MoveTowards(musicSrc.volume, target, speed * Time.deltaTime);
        }
    }
}
