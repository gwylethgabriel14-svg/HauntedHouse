using UnityEngine;

// The player's lives (5 by default) AND a health bar (HP). Put it on the Player.
//
// How it works:
//   - Every hit takes HP off the health bar.
//   - When the bar hits 0, you lose ONE life and the bar refills (if you still have lives).
//   - When the last life is gone, it is game over.
//
// Other scripts hurt the player with:   playerHealth.TakeDamage();       (1 hit = hpPerHit HP)
//                                       playerHealth.TakeDamageHP(30f);  (exact HP)
public class PlayerHealth : MonoBehaviour
{
    [Header("Lives")]
    public int maxLives = 5;
    public float invulnerableSeconds = 1.5f;     // can't be hurt again right after a hit

    [Header("Health bar (HP)")]
    public float maxHealth = 100f;
    public float hpPerHit = 25f;                 // HP lost by one TakeDamage() hit (4 hits = 1 life)
    public float refillDelay = 0.6f;             // pause at empty before the bar refills

    [Header("Hit effect")]
    public float shakeTime = 0.3f;
    public float shakeAmount = 0.08f;

    [Header("Heartbeat when badly hurt")]
    public int criticalLives = 2;                // heartbeat starts at this many lives or fewer

    [Header("Audio (optional)")]
    public AudioClip hurtClip;
    public AudioClip heartbeatClip;
    [Range(0f, 1f)] public float hurtVolume = 0.8f;
    [Range(0f, 1f)] public float heartbeatVolume = 0.9f;

    // lives carry over when the scene restarts ("Try again"), so every capture really costs one
    public static int PersistedLives = -1;
    public static void ResetPersisted() { PersistedLives = -1; }

    public int Lives { get; private set; }
    public float Health { get; private set; }
    public bool IsDead { get { return Lives <= 0; } }

    public event System.Action<int, int> LivesChanged;       // (lives, max)
    public event System.Action<float, float> HealthChanged;  // (health, max)
    public event System.Action Damaged;

    float invulnerableUntil;
    bool refillPending;
    float refillAt;
    AudioSource sfx, beat;

    void Awake()
    {
        Lives = PersistedLives > 0 ? Mathf.Clamp(PersistedLives, 1, maxLives) : maxLives;
        PersistedLives = Lives;
        Health = maxHealth;
    }

    void Start()
    {
        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        sfx.spatialBlend = 0f;

        beat = gameObject.AddComponent<AudioSource>();
        beat.clip = heartbeatClip;
        beat.loop = true;
        beat.playOnAwake = false;
        beat.spatialBlend = 0f;
        beat.volume = 0f;
    }

    void Update()
    {
        if (refillPending && Time.time >= refillAt)
        {
            refillPending = false;
            if (!IsDead)
            {
                Health = maxHealth;
                if (HealthChanged != null) HealthChanged(Health, maxHealth);
            }
        }

        UpdateHeartbeat();
    }

    // returns true if the hit counted. amount = number of hits (each one is hpPerHit HP)
    public bool TakeDamage(int amount = 1)
    {
        if (amount <= 0) return false;
        return TakeDamageHP(amount * hpPerHit);
    }

    // returns true if the hit counted. hp = exact HP to remove
    public bool TakeDamageHP(float hp)
    {
        if (IsDead || hp <= 0f) return false;
        if (refillPending) return false;
        if (Time.time < invulnerableUntil) return false;

        invulnerableUntil = Time.time + invulnerableSeconds;
        Health = Mathf.Max(0f, Health - hp);

        if (hurtClip != null && sfx != null) sfx.PlayOneShot(hurtClip, hurtVolume);
        CameraShake.Shake(shakeTime, shakeAmount);

        if (Damaged != null) Damaged();
        if (HealthChanged != null) HealthChanged(Health, maxHealth);

        // the bar is empty: that costs one life
        if (Health <= 0f)
        {
            Lives = Mathf.Max(0, Lives - 1);
            PersistedLives = Lives;
            if (LivesChanged != null) LivesChanged(Lives, maxLives);

            if (Lives <= 0)
            {
                if (GameManager.Instance != null) GameManager.Instance.PlayerDied();
            }
            else
            {
                refillPending = true;
                refillAt = Time.time + refillDelay;
            }
        }

        return true;
    }

    // heal lives (hearts)
    public void Heal(int amount = 1)
    {
        if (IsDead) return;
        SetLives(Lives + amount);
    }

    // heal the health bar
    public void HealHP(float hp)
    {
        if (IsDead || hp <= 0f) return;
        Health = Mathf.Min(maxHealth, Health + hp);
        if (HealthChanged != null) HealthChanged(Health, maxHealth);
    }

    // the main enemy caught you: costs one life (the capture sequence handles the rest)
    public int LoseLifeForCapture()
    {
        Lives = Mathf.Max(0, Lives - 1);
        PersistedLives = Lives;
        refillPending = false;
        Health = maxHealth;
        if (LivesChanged != null) LivesChanged(Lives, maxLives);
        if (HealthChanged != null) HealthChanged(Health, maxHealth);
        return Lives;
    }

    public void SetLives(int value)
    {
        Lives = Mathf.Clamp(value, 0, maxLives);
        PersistedLives = Lives;
        if (LivesChanged != null) LivesChanged(Lives, maxLives);
    }

    void UpdateHeartbeat()
    {
        if (beat == null || heartbeatClip == null) return;

        bool critical = !IsDead && Lives <= criticalLives;
        float targetVolume = 0f, targetPitch = 1f;
        if (critical)
        {
            bool last = Lives <= 1;
            targetVolume = last ? heartbeatVolume : heartbeatVolume * 0.5f;
            targetPitch = last ? 1.25f : 1f;
            if (!beat.isPlaying) beat.Play();
        }

        beat.volume = Mathf.MoveTowards(beat.volume, targetVolume, Time.unscaledDeltaTime * 0.6f);
        beat.pitch = Mathf.MoveTowards(beat.pitch, targetPitch, Time.unscaledDeltaTime * 0.5f);

        if (!critical && beat.isPlaying && beat.volume <= 0.01f) beat.Stop();
    }
}