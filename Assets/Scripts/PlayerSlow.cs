
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

// ============================================================
// THE LAST LULLABY - PLAYER MOVEMENT SLOWDOWN
//
// FEATURES:
// 1. Player slows down as remaining lives decrease.
// 2. Slime traps can temporarily slow the player further.
// 3. Walking, sprinting and crouching speeds are supported.
// 4. Smooth speed transitions.
// 5. Automatically restores normal speed when appropriate.
// 6. Compatible with existing PlayerSlow.For(), Hold(),
//    Release(), IsCrouching() and IsMoving() calls.
//
// Requires existing PlayerController and PlayerHealth scripts.
// ============================================================

[DisallowMultipleComponent]
public class PlayerSlow : MonoBehaviour
{
    // Other scripts can read this multiplier.
    public static float Multiplier = 1f;

    [Header("Low Lives Slowdown")]
    public bool enableLowLivesSlowdown = true;

    [Header("Speed By Remaining Lives")]
    [Range(0.1f, 1f)]
    public float speedAt5Lives = 1f;

    [Range(0.1f, 1f)]
    public float speedAt4Lives = 0.95f;

    [Range(0.1f, 1f)]
    public float speedAt3Lives = 0.85f;

    [Range(0.1f, 1f)]
    public float speedAt2Lives = 0.75f;

    [Range(0.1f, 1f)]
    public float speedAt1Life = 0.60f;

    [Header("Slime Trap")]
    public bool enableSlimeSlowdown = true;

    [Range(0.1f, 1f)]
    public float defaultSlimeMultiplier = 0.35f;

    [Header("Smooth Transition")]
    [Min(0.1f)]
    public float transitionSpeed = 3f;

    [Header("Debug")]
    public bool showDebugLogs = false;

    // ============================================================
    // INTERNAL VARIABLES
    // ============================================================

    private PlayerController pc;

    private readonly List<FieldInfo> speedFields =
        new List<FieldInfo>();

    private const BindingFlags Flags =
        BindingFlags.Instance |
        BindingFlags.Public |
        BindingFlags.NonPublic;

    private static readonly string[] Skip =
    {
        "look",
        "rot",
        "turn",
        "sens",
        "mouse",
        "cam",
        "bob",
        "smooth",
        "accel"
    };

    private float currentMultiplier = 1f;
    private float targetMultiplier = 1f;

    private float slimeMultiplier = 1f;
    private float slimeHoldUntil = 0f;

    private float currentLivesMultiplier = 1f;

    private int lastKnownLives = -1;

    private bool warnedNoSpeedFields = false;

    // ============================================================
    // PUBLIC PROPERTIES
    // ============================================================

    public float CurrentMultiplier
    {
        get { return currentMultiplier; }
    }

    public float LivesMultiplier
    {
        get { return currentLivesMultiplier; }
    }

    public bool IsSlowedBySlime
    {
        get
        {
            return enableSlimeSlowdown &&
                   Time.time < slimeHoldUntil;
        }
    }

    // ============================================================
    // COMPATIBILITY - OTHER SCRIPTS
    // ============================================================

    public static PlayerSlow For(PlayerController player)
    {
        if (player == null)
            return null;

        PlayerSlow slow =
            player.GetComponent<PlayerSlow>();

        if (slow == null)
        {
            slow =
                player.gameObject.AddComponent<PlayerSlow>();
        }

        return slow;
    }

    // ============================================================
    // INITIALIZATION
    // ============================================================

    private void Awake()
    {
        pc = GetComponent<PlayerController>();

        if (pc == null)
        {
            Debug.LogWarning(
                "[PlayerSlow] PlayerController not found " +
                "on this GameObject.",
                this);

            return;
        }

        FindMovementSpeedFields();

        currentMultiplier = 1f;
        targetMultiplier = 1f;

        slimeMultiplier = 1f;

        Multiplier = 1f;
    }

    private void FindMovementSpeedFields()
    {
        speedFields.Clear();

        if (pc == null)
            return;

        FieldInfo[] fields =
            pc.GetType().GetFields(Flags);

        foreach (FieldInfo field in fields)
        {
            if (field.FieldType != typeof(float))
                continue;

            if (field.IsStatic || field.IsInitOnly)
                continue;

            string name =
                field.Name.ToLowerInvariant();

            if (!name.Contains("speed"))
                continue;

            bool skip = false;

            foreach (string word in Skip)
            {
                if (name.Contains(word))
                {
                    skip = true;
                    break;
                }
            }

            if (!skip)
                speedFields.Add(field);
        }

        if (showDebugLogs)
        {
            Debug.Log(
                "[PlayerSlow] Found " +
                speedFields.Count +
                " movement speed fields.",
                this);
        }
    }

    // ============================================================
    // UPDATE
    // ============================================================

    private void Update()
    {
        if (pc == null)
            return;

        UpdateLivesSlowdown();

        UpdateSlimeSlowdown();

        CalculateTargetMultiplier();

        ApplySmoothSlowdown();
    }

    // ============================================================
    // LIVES SLOWDOWN
    // ============================================================

    private void UpdateLivesSlowdown()
    {
        if (!enableLowLivesSlowdown)
        {
            currentLivesMultiplier = 1f;
            return;
        }

        int lives = GetRemainingLives();

        switch (lives)
        {
            case 1:
                currentLivesMultiplier = speedAt1Life;
                break;

            case 2:
                currentLivesMultiplier = speedAt2Lives;
                break;

            case 3:
                currentLivesMultiplier = speedAt3Lives;
                break;

            case 4:
                currentLivesMultiplier = speedAt4Lives;
                break;

            default:
                currentLivesMultiplier = speedAt5Lives;
                break;
        }

        // If the player has zero lives, retain the
        // slowest movement setting during death.
        if (lives <= 0)
        {
            currentLivesMultiplier = speedAt1Life;
        }

        if (lastKnownLives != lives)
        {
            lastKnownLives = lives;

            if (showDebugLogs)
            {
                Debug.Log(
                    "[PlayerSlow] Lives remaining: " +
                    lives +
                    " | Movement: " +
                    Mathf.RoundToInt(
                        currentLivesMultiplier * 100f) +
                    "%",
                    this);
            }
        }
    }

    private int GetRemainingLives()
    {
        // Uses the existing PlayerHealth system.
        return Mathf.Max(
            0,
            PlayerHealth.PersistedLives);
    }

    // ============================================================
    // SLIME SLOWDOWN
    // ============================================================

    // Called repeatedly while the player is in slime.
    //
    // Example:
    // PlayerSlow.For(player).Hold(0.35f);
    //
    // Means the slime reduces the player's speed
    // to 35% of their current life-adjusted speed.

    public void Hold(float multiplier)
    {
        if (!enableSlimeSlowdown)
            return;

        slimeMultiplier = Mathf.Clamp(
            multiplier,
            0.05f,
            1f);

        slimeHoldUntil = Time.time + 0.2f;
    }

    // Uses default slime slowdown.
    public void Hold()
    {
        Hold(defaultSlimeMultiplier);
    }

    public void Release()
    {
        slimeHoldUntil = 0f;
        slimeMultiplier = 1f;
    }

    private void UpdateSlimeSlowdown()
    {
        if (!enableSlimeSlowdown)
        {
            slimeMultiplier = 1f;
            return;
        }

        if (Time.time >= slimeHoldUntil)
        {
            slimeMultiplier = 1f;
        }
    }

    // ============================================================
    // COMBINE SLOWDOWN EFFECTS
    // ============================================================

    private void CalculateTargetMultiplier()
    {
        float livesEffect =
            enableLowLivesSlowdown
                ? currentLivesMultiplier
                : 1f;

        float slimeEffect =
            IsSlowedBySlime
                ? slimeMultiplier
                : 1f;

        targetMultiplier =
            Mathf.Clamp(
                livesEffect * slimeEffect,
                0.05f,
                1f);
    }

    // ============================================================
    // SMOOTH MOVEMENT
    // ============================================================

    private void ApplySmoothSlowdown()
    {
        float nextMultiplier =
            Mathf.MoveTowards(
                currentMultiplier,
                targetMultiplier,
                Time.deltaTime * transitionSpeed);

        if (Mathf.Approximately(
            nextMultiplier,
            currentMultiplier))
        {
            Multiplier = currentMultiplier;
            return;
        }

        float ratio =
            nextMultiplier /
            Mathf.Max(0.0001f, currentMultiplier);

        ScaleMovementFields(ratio);

        currentMultiplier = nextMultiplier;

        Multiplier = currentMultiplier;
    }

    // ============================================================
    // MODIFY PLAYERCONTROLLER MOVEMENT SPEED
    // ============================================================

    private void ScaleMovementFields(float ratio)
    {
        if (pc == null)
            return;

        if (speedFields.Count == 0)
        {
            if (!warnedNoSpeedFields)
            {
                warnedNoSpeedFields = true;

                Debug.LogWarning(
                    "[PlayerSlow] No movement speed fields " +
                    "found in PlayerController. " +
                    "Movement must use PlayerSlow.Multiplier " +
                    "if speeds are calculated elsewhere.",
                    this);
            }

            return;
        }

        foreach (FieldInfo field in speedFields)
        {
            float original =
                (float)field.GetValue(pc);

            float updated =
                original * ratio;

            field.SetValue(
                pc,
                updated);
        }
    }

    // ============================================================
    // RESET SPEED
    // ============================================================

    private void OnDisable()
    {
        RestoreOriginalSpeed();
    }

    private void OnDestroy()
    {
        RestoreOriginalSpeed();
    }

    private void RestoreOriginalSpeed()
    {
        if (pc != null &&
            currentMultiplier > 0.0001f &&
            !Mathf.Approximately(
                currentMultiplier,
                1f))
        {
            ScaleMovementFields(
                1f / currentMultiplier);
        }

        currentMultiplier = 1f;
        targetMultiplier = 1f;
        currentLivesMultiplier = 1f;

        slimeMultiplier = 1f;
        slimeHoldUntil = 0f;

        Multiplier = 1f;
    }

    // ============================================================
    // CROUCH DETECTION
    // ============================================================

    private static bool crouchSearched;
    private static FieldInfo crouchField;
    private static PropertyInfo crouchProperty;

    public static bool IsCrouching(PlayerController player)
    {
        if (player == null)
            return false;

        if (!crouchSearched)
        {
            crouchSearched = true;

            string[] preferredNames =
            {
                "iscrouching",
                "crouching",
                "iscrouched",
                "crouched"
            };

            int bestRank = int.MaxValue;

            System.Type type =
                player.GetType();

            foreach (FieldInfo field in
                type.GetFields(Flags))
            {
                if (field.FieldType != typeof(bool))
                    continue;

                string name =
                    field.Name.ToLowerInvariant();

                if (!name.Contains("crouch"))
                    continue;

                int rank =
                    System.Array.IndexOf(
                        preferredNames,
                        name);

                if (rank < 0)
                    rank = 10;

                if (rank < bestRank)
                {
                    bestRank = rank;

                    crouchField = field;
                    crouchProperty = null;
                }
            }

            foreach (PropertyInfo property in
                type.GetProperties(Flags))
            {
                if (property.PropertyType != typeof(bool))
                    continue;

                if (!property.CanRead)
                    continue;

                if (property.GetIndexParameters().Length > 0)
                    continue;

                string name =
                    property.Name.ToLowerInvariant();

                if (!name.Contains("crouch"))
                    continue;

                int rank =
                    System.Array.IndexOf(
                        preferredNames,
                        name);

                if (rank < 0)
                    rank = 10;

                if (rank < bestRank)
                {
                    bestRank = rank;

                    crouchProperty = property;
                    crouchField = null;
                }
            }
        }

        if (crouchField != null)
        {
            return (bool)crouchField.GetValue(player);
        }

        if (crouchProperty != null)
        {
            return (bool)crouchProperty.GetValue(
                player,
                null);
        }

        // Fallback controls.
        return Input.GetKey(KeyCode.C) ||
               Input.GetKey(KeyCode.LeftControl);
    }

    // ============================================================
    // MOVEMENT DETECTION
    // ============================================================

    public static bool IsMoving()
    {
        Vector2 input = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical"));

        return input.sqrMagnitude > 0.01f;
    }
}
