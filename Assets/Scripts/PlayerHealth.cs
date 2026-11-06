
using UnityEngine;

// THE LAST LULLABY - PLAYER HEALTH
//
// HP reaches zero:
// 1. Lose ONE life.
// 2. Play death / falling animation.
// 3. Show YOU DIED with remaining lives.
// 4. TRY AGAIN restores full HP,
//    but retains the remaining lives.
//
// Mommy capture uses LoseLifeForCapture()
// and has its own YOU GOT CAUGHT sequence.

public class PlayerHealth : MonoBehaviour
{
    [Header("Lives")]
    public int maxLives = 5;

    public float invulnerableSeconds = 1.5f;

    [Header("Health Bar (HP)")]
    public float maxHealth = 100f;

    public float hpPerHit = 25f;

    [Tooltip("Legacy setting. HP now refills on retry.")]
    public float refillDelay = 0.6f;

    [Header("Death Behavior")]
    [Tooltip(
        "When HP becomes zero, show YOU DIED " +
        "even if the player still has lives."
    )]
    public bool showDeathOnEveryLostLife = true;

    [Header("Hit Effect")]
    public float shakeTime = 0.3f;
    public float shakeAmount = 0.08f;

    [Header("Heartbeat")]
    public int criticalLives = 2;

    [Header("Audio")]
    public AudioClip hurtClip;
    public AudioClip heartbeatClip;

    [Range(0f, 1f)]
    public float hurtVolume = 0.8f;

    [Range(0f, 1f)]
    public float heartbeatVolume = 0.9f;

    // Lives persist between retries.
    public static int PersistedLives = -1;

    public static void ResetPersisted()
    {
        PersistedLives = -1;
    }

    public int Lives { get; private set; }

    public float Health { get; private set; }

    public bool IsDead
    {
        get { return Lives <= 0; }
    }

    public event System.Action<int, int> LivesChanged;
    public event System.Action<float, float> HealthChanged;
    public event System.Action Damaged;

    private float invulnerableUntil;

    private bool deathSequenceTriggered;

    // Used only if the new death behavior
    // is disabled.
    private bool refillPending;
    private float refillAt;

    private AudioSource sfx;
    private AudioSource beat;

    // ==========================================
    // INITIALIZATION
    // ==========================================

    private void Awake()
    {
        maxLives = Mathf.Max(1, maxLives);
        maxHealth = Mathf.Max(1f, maxHealth);

        if (PersistedLives > 0)
        {
            Lives = Mathf.Clamp(
                PersistedLives,
                1,
                maxLives);
        }
        else
        {
            Lives = maxLives;
        }

        PersistedLives = Lives;

        Health = maxHealth;

        deathSequenceTriggered = false;
        refillPending = false;
        invulnerableUntil = 0f;
    }

    private void Start()
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

        Debug.Log(
            "[PlayerHealth] Started with " +
            Lives + "/" + maxLives +
            " lives and full HP.");
    }

    // ==========================================
    // UPDATE
    // ==========================================

    private void Update()
    {
        // Original refill behavior when the
        // new death setting is disabled.
        if (!showDeathOnEveryLostLife &&
            refillPending &&
            Time.time >= refillAt)
        {
            refillPending = false;

            if (!IsDead)
            {
                Health = maxHealth;

                HealthChanged?.Invoke(
                    Health,
                    maxHealth);
            }
        }

        UpdateHeartbeat();
    }

    // ==========================================
    // DAMAGE
    // ==========================================

    // Standard damage:
    // One hit = hpPerHit HP.
    public bool TakeDamage(int amount = 1)
    {
        if (amount <= 0)
            return false;

        return TakeDamageHP(
            amount * hpPerHit);
    }

    // Exact HP damage.
    public bool TakeDamageHP(float hp)
    {
        if (hp <= 0f)
            return false;

        if (IsDead || deathSequenceTriggered)
            return false;

        if (refillPending)
            return false;

        if (GameManager.Instance != null &&
            GameManager.Instance.IsEnded)
        {
            return false;
        }

        if (Time.time < invulnerableUntil)
            return false;

        invulnerableUntil =
            Time.time + invulnerableSeconds;

        // Reduce HP.
        Health = Mathf.Max(
            0f,
            Health - hp);

        // Hurt sound.
        if (hurtClip != null && sfx != null)
        {
            sfx.PlayOneShot(
                hurtClip,
                hurtVolume);
        }

        // Camera shake.
        CameraShake.Shake(
            shakeTime,
            shakeAmount);

        Damaged?.Invoke();

        HealthChanged?.Invoke(
            Health,
            maxHealth);

        Debug.Log(
            "[PlayerHealth] Damage: " + hp +
            " | HP: " + Health +
            "/" + maxHealth);

        // HP still available:
        // Player continues playing.
        if (Health > 0f)
        {
            return true;
        }

        // HP reached zero.
        LoseLifeFromDamage();

        return true;
    }

    // ==========================================
    // LOSE LIFE FROM DAMAGE
    // ==========================================

    private void LoseLifeFromDamage()
    {
        if (deathSequenceTriggered)
            return;

        // Deduct exactly ONE heart.
        Lives = Mathf.Max(
            0,
            Lives - 1);

        PersistedLives = Lives;

        LivesChanged?.Invoke(
            Lives,
            maxLives);

        Debug.Log(
            "[PlayerHealth] One life lost! " +
            "Remaining lives: " + Lives);

        // Final life:
        // Always play the death sequence.
        if (Lives <= 0)
        {
            TriggerDeathSequence();
            return;
        }

        // NEW BEHAVIOR:
        // Play falling animation and YOU DIED
        // even when lives remain.
        if (showDeathOnEveryLostLife)
        {
            TriggerDeathSequence();
        }
        else
        {
            // Legacy behavior:
            // refill HP and continue playing.
            refillPending = true;

            refillAt =
                Time.time + refillDelay;
        }
    }

    // ==========================================
    // TRIGGER DEATH SEQUENCE
    // ==========================================

    private void TriggerDeathSequence()
    {
        if (deathSequenceTriggered)
            return;

        deathSequenceTriggered = true;

        // Prevent further damage.
        invulnerableUntil =
            float.PositiveInfinity;

        Debug.Log(
            "[PlayerHealth] YOU DIED sequence! " +
            "Lives left: " + Lives);

        GameManager gm =
            GameManager.Instance;

        if (gm != null)
        {
            // GameManager calls
            // GameOverSequence.PlayDeath().
            gm.PlayerDied();
        }
        else
        {
            Debug.LogError(
                "[PlayerHealth] GameManager missing! " +
                "Cannot start death animation.");
        }
    }

    // ==========================================
    // MOMMY CAPTURE
    // ==========================================

    // This method is called by the existing
    // GameOverSequence after Mommy catches
    // the player.
    //
    // Do not trigger another death sequence here.
    // GameOverSequence is already running.

    public int LoseLifeForCapture()
    {
        Lives = Mathf.Max(
            0,
            Lives - 1);

        PersistedLives = Lives;

        refillPending = false;

        Health = maxHealth;

        LivesChanged?.Invoke(
            Lives,
            maxLives);

        HealthChanged?.Invoke(
            Health,
            maxHealth);

        Debug.Log(
            "[PlayerHealth] Mommy capture! " +
            "Lives left: " + Lives);

        return Lives;
    }

    // ==========================================
    // HEAL LIVES
    // ==========================================

    public void Heal(int amount = 1)
    {
        if (IsDead || deathSequenceTriggered)
            return;

        if (amount <= 0)
            return;

        SetLives(Lives + amount);
    }

    // ==========================================
    // HEAL HP
    // ==========================================

    public void HealHP(float hp)
    {
        if (IsDead ||
            deathSequenceTriggered ||
            hp <= 0f)
        {
            return;
        }

        Health = Mathf.Min(
            maxHealth,
            Health + hp);

        HealthChanged?.Invoke(
            Health,
            maxHealth);
    }

    // ==========================================
    // SET LIVES
    // ==========================================

    public void SetLives(int value)
    {
        Lives = Mathf.Clamp(
            value,
            0,
            maxLives);

        PersistedLives = Lives;

        LivesChanged?.Invoke(
            Lives,
            maxLives);

        Debug.Log(
            "[PlayerHealth] Lives set to " +
            Lives);
    }

    // ==========================================
    // HEARTBEAT AUDIO
    // ==========================================

    private void UpdateHeartbeat()
    {
        if (beat == null ||
            heartbeatClip == null)
        {
            return;
        }

        bool critical =
            !IsDead &&
            !deathSequenceTriggered &&
            Lives <= criticalLives;

        float targetVolume = 0f;
        float targetPitch = 1f;

        if (critical)
        {
            bool lastLife = Lives <= 1;

            targetVolume = lastLife
                ? heartbeatVolume
                : heartbeatVolume * 0.5f;

            targetPitch = lastLife
                ? 1.25f
                : 1f;

            if (!beat.isPlaying)
                beat.Play();
        }

        beat.volume = Mathf.MoveTowards(
            beat.volume,
            targetVolume,
            Time.unscaledDeltaTime * 0.6f);

        beat.pitch = Mathf.MoveTowards(
            beat.pitch,
            targetPitch,
            Time.unscaledDeltaTime * 0.5f);

        if (!critical &&
            beat.isPlaying &&
            beat.volume <= 0.01f)
        {
            beat.Stop();
        }
    }
}
