using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

// Added to the Player automatically by the slime. It slows the player WITHOUT you having to edit PlayerController:
// it finds the float fields with "speed" in their name (walkSpeed, sprintSpeed, crouchSpeed, ...) and scales them
// while the player is stuck in slime, then puts them back.
//
// If your PlayerController moves in some other way, just multiply your movement by PlayerSlow.Multiplier
// (it is 1 normally and ~0.35 inside slime).
public class PlayerSlow : MonoBehaviour
{
    public static float Multiplier = 1f;

    static readonly string[] Skip = { "look", "rot", "turn", "sens", "mouse", "cam", "bob", "smooth", "accel" };
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    PlayerController pc;
    readonly List<FieldInfo> speedFields = new List<FieldInfo>();
    float holdMult = 1f, holdUntil, applied = 1f;
    bool warned;

    public static PlayerSlow For(PlayerController p)
    {
        var s = p.GetComponent<PlayerSlow>();
        if (s == null) s = p.gameObject.AddComponent<PlayerSlow>();
        return s;
    }

    void Awake()
    {
        pc = GetComponent<PlayerController>();
        if (pc == null) return;
        foreach (var f in pc.GetType().GetFields(Flags))
        {
            if (f.FieldType != typeof(float)) continue;
            string n = f.Name.ToLowerInvariant();
            if (!n.Contains("speed")) continue;
            bool skip = false;
            foreach (var s in Skip) if (n.Contains(s)) { skip = true; break; }
            if (!skip) speedFields.Add(f);
        }
    }

    // called every frame by whatever is slowing the player
    public void Hold(float mult)
    {
        holdMult = mult;
        holdUntil = Time.time + 0.2f;
    }

    public void Release()
    {
        holdUntil = 0f;
    }

    void Update()
    {
        float target = Time.time < holdUntil ? holdMult : 1f;
        float next = Mathf.MoveTowards(applied, target, Time.deltaTime * 4f);
        if (!Mathf.Approximately(next, applied))
        {
            Scale(next / applied);
            applied = next;
        }
        Multiplier = applied;
    }

    void OnDisable()
    {
        if (!Mathf.Approximately(applied, 1f)) Scale(1f / applied);
        applied = 1f;
        Multiplier = 1f;
    }

    void Scale(float ratio)
    {
        if (pc == null) return;
        if (speedFields.Count == 0)
        {
            if (!warned)
            {
                warned = true;
                Debug.LogWarning("PlayerSlow: no speed fields found on PlayerController. Multiply your movement by PlayerSlow.Multiplier.");
            }
            return;
        }
        foreach (var f in speedFields)
            f.SetValue(pc, (float)f.GetValue(pc) * ratio);
    }

    // ------------------------------------------------------------------ crouch + movement checks

    static bool crouchSearched;
    static FieldInfo crouchField;
    static PropertyInfo crouchProp;

    public static bool IsCrouching(PlayerController p)
    {
        if (!crouchSearched)
        {
            crouchSearched = true;
            string[] best = { "iscrouching", "crouching", "iscrouched", "crouched" };
            int bestRank = int.MaxValue;
            var t = p.GetType();

            foreach (var f in t.GetFields(Flags))
            {
                if (f.FieldType != typeof(bool)) continue;
                string n = f.Name.ToLowerInvariant();
                if (!n.Contains("crouch")) continue;
                int rank = System.Array.IndexOf(best, n); if (rank < 0) rank = 10;
                if (rank < bestRank) { bestRank = rank; crouchField = f; crouchProp = null; }
            }
            foreach (var pr in t.GetProperties(Flags))
            {
                if (pr.PropertyType != typeof(bool) || !pr.CanRead || pr.GetIndexParameters().Length > 0) continue;
                string n = pr.Name.ToLowerInvariant();
                if (!n.Contains("crouch")) continue;
                int rank = System.Array.IndexOf(best, n); if (rank < 0) rank = 10;
                if (rank < bestRank) { bestRank = rank; crouchProp = pr; crouchField = null; }
            }
        }

        if (crouchField != null) return (bool)crouchField.GetValue(p);
        if (crouchProp != null) return (bool)crouchProp.GetValue(p, null);
        return Input.GetKey(KeyCode.C) || Input.GetKey(KeyCode.LeftControl);   // fallback if no crouch variable exists
    }

    public static bool IsMoving()
    {
        return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).sqrMagnitude > 0.01f;
    }
}
