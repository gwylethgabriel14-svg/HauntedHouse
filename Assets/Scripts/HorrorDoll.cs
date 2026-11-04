using System.Collections.Generic;
using UnityEngine;

// A small creepy DOLL. It builds its own body (porcelain head, black button eyes, stitched grin, pigtails, dress, little arms).
// It lies still in the nursery. When the player enters the nursery (see DollNursery) it gets up and scuttles toward
// the player in jerky stop-motion steps, then SCRATCHES the player (loses a life).
//
// HOW THE PLAYER STOPS THEM: LOOK AT THEM.
// A doll that is on the player's screen (and not hidden behind a wall or furniture) freezes completely and cannot
// move or scratch. Look away and it moves again. The player can also just leave the room: dolls never leave it.
//
// NEW: the dolls keep apart from each other. Each one comes at the player from a different side and they push
// away from each other, so they never pile up in one spot.
//
// Dolls must be children of a DollNursery object (the menu Haunted House > Enemies > Add NURSERY DOLLS does this).
public class HorrorDoll : MonoBehaviour
{
    public float size = 0.7f;          // 0.7 = about 45 cm tall
    public float walkSpeed = 2f;
    public float reach = 0.75f;        // scratches from this distance
    public float riseTime = 1.1f;      // time to get up from lying down

    [Header("Hiwa-hiwalay")]
    [Tooltip("Lumalayo ang doll sa ibang doll na mas malapit dito (metro).")]
    public float personalSpace = 0.6f;
    [Tooltip("Gaano kalayo sa gilid ng player ang unang tinutungo ng bawat doll (para hindi sila magsiksikan).")]
    public float surroundRadius = 1.2f;
    // ang DollNursery ang nagbibigay nito; kung wala, random
    [HideInInspector] public float approachAngle = -1f;

    DollNursery nursery;
    PlayerController player;
    PlayerHealth health;

    Transform visual, head, armL, armR;
    Vector3 homePos;
    Quaternion homeRot;

    float wake;          // 0 = lying down, 1 = standing
    float wakeDelay;     // so they don't all wake up at the same moment
    float alertTimer;
    float contactTimer;
    float animT;
    bool frozen;

    static readonly RaycastHit[] seeHits = new RaycastHit[16];
    static readonly List<HorrorDoll> all = new List<HorrorDoll>();
    const float H0 = 0.10f;   // height of the bottom of the dress above the feet (the legs)

    void Awake()
    {
        nursery = GetComponentInParent<DollNursery>();
        Build();
    }

    void OnEnable() { all.Add(this); }
    void OnDisable() { all.Remove(this); }

    void Start()
    {
        homePos = transform.position;
        homeRot = transform.rotation;
        wakeDelay = Random.Range(0f, 1.8f);
        if (approachAngle < 0f) approachAngle = Random.Range(0f, 360f);
        player = FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            health = player.GetComponent<PlayerHealth>();
            if (health == null) health = FindFirstObjectByType<PlayerHealth>();
        }
        if (nursery == null) Debug.LogWarning("HorrorDoll '" + name + "' is not inside a DollNursery object, it will do nothing.");
        ApplyPose(0f, false, false);
    }

    // ------------------------------------------------------------------ build

    Material M(Material given, Color c, float smooth)
    {
        return given != null ? given : HorrorMeshes.Fallback(c, smooth);
    }

    void Build()
    {
        Material skin = M(nursery != null ? nursery.porcelainMat : null, new Color(0.86f, 0.80f, 0.72f), 0.55f);
        Material dress = M(nursery != null ? nursery.dressMat : null, new Color(0.38f, 0.07f, 0.09f), 0.1f);
        Material hair = M(nursery != null ? nursery.hairMat : null, new Color(0.05f, 0.03f, 0.03f), 0.35f);
        Material eye = M(nursery != null ? nursery.eyeMat : null, new Color(0.01f, 0.01f, 0.01f), 0.95f);
        Material mouth = M(nursery != null ? nursery.mouthMat : null, new Color(0.30f, 0.02f, 0.03f), 0.2f);

        visual = HorrorMeshes.Pivot(transform, "Visual", Vector3.zero);
        visual.localScale = Vector3.one * size;

        // dress: a bell shape made with a lathe (skirt, waist, chest)
        var prof = new[]
        {
            new Vector2(0f, H0 + 0.04f),
            new Vector2(0.15f, H0 + 0.04f),
            new Vector2(0.16f, H0 + 0.07f),
            new Vector2(0.13f, H0 + 0.15f),
            new Vector2(0.09f, H0 + 0.22f),
            new Vector2(0.065f, H0 + 0.27f),
            new Vector2(0.07f, H0 + 0.31f),
            new Vector2(0.045f, H0 + 0.34f),
            new Vector2(0f, H0 + 0.345f)
        };
        HorrorMeshes.MeshPart(visual, "Dress", HorrorMeshes.Lathe(prof, 20), dress);

        // legs and little black shoes
        for (int s = -1; s <= 1; s += 2)
        {
            HorrorMeshes.Part(visual, "Leg", PrimitiveType.Capsule, new Vector3(s * 0.05f, 0.06f, 0f), new Vector3(0.035f, 0.06f, 0.035f), skin);
            HorrorMeshes.Part(visual, "Shoe", PrimitiveType.Sphere, new Vector3(s * 0.05f, 0.02f, 0.015f), new Vector3(0.06f, 0.035f, 0.085f), eye);
        }

        // neck
        HorrorMeshes.Part(visual, "Neck", PrimitiveType.Cylinder, new Vector3(0f, H0 + 0.36f, 0f), new Vector3(0.05f, 0.03f, 0.05f), skin);

        // arms (pivot at the shoulder so they can swing)
        armL = BuildArm("ArmL", -0.085f, skin, dress);
        armR = BuildArm("ArmR", 0.085f, skin, dress);

        // head (pivot at the neck so it can tilt)
        head = HorrorMeshes.Pivot(visual, "Head", new Vector3(0f, H0 + 0.37f, 0f));
        HorrorMeshes.Part(head, "Skull", PrimitiveType.Sphere, new Vector3(0f, 0.085f, 0f), new Vector3(0.17f, 0.185f, 0.17f), skin);
        HorrorMeshes.Part(head, "Hair", PrimitiveType.Sphere, new Vector3(0f, 0.10f, -0.012f), new Vector3(0.185f, 0.19f, 0.185f), hair);
        HorrorMeshes.Part(head, "PigtailL", PrimitiveType.Sphere, new Vector3(-0.1f, 0.05f, -0.01f), new Vector3(0.055f, 0.08f, 0.055f), hair);
        HorrorMeshes.Part(head, "PigtailR", PrimitiveType.Sphere, new Vector3(0.1f, 0.05f, -0.01f), new Vector3(0.055f, 0.08f, 0.055f), hair);
        // black button eyes (one a bit bigger, it looks wrong)
        HorrorMeshes.Part(head, "EyeL", PrimitiveType.Sphere, new Vector3(-0.03f, 0.095f, 0.077f), Vector3.one * 0.036f, eye);
        HorrorMeshes.Part(head, "EyeR", PrimitiveType.Sphere, new Vector3(0.03f, 0.095f, 0.078f), Vector3.one * 0.028f, eye);
        // stitched grin
        HorrorMeshes.Part(head, "Mouth", PrimitiveType.Cube, new Vector3(0f, 0.04f, 0.074f), new Vector3(0.06f, 0.007f, 0.008f), mouth);
        for (int i = -2; i <= 2; i++)
            HorrorMeshes.Part(head, "Stitch", PrimitiveType.Cube, new Vector3(i * 0.012f, 0.04f, 0.0745f), new Vector3(0.002f, 0.02f, 0.008f), mouth);
    }

    Transform BuildArm(string nm, float x, Material skin, Material dress)
    {
        var pivot = HorrorMeshes.Pivot(visual, nm, new Vector3(x, H0 + 0.31f, 0f));
        HorrorMeshes.Part(pivot, "Sleeve", PrimitiveType.Sphere, new Vector3(0f, -0.01f, 0f), new Vector3(0.07f, 0.06f, 0.07f), dress);
        HorrorMeshes.Part(pivot, "Arm", PrimitiveType.Capsule, new Vector3(0f, -0.085f, 0f), new Vector3(0.03f, 0.085f, 0.03f), skin);
        HorrorMeshes.Part(pivot, "Hand", PrimitiveType.Sphere, new Vector3(0f, -0.18f, 0f), Vector3.one * 0.045f, skin);
        // thin claw-like fingers
        for (int f = -1; f <= 1; f++)
            HorrorMeshes.Part(pivot, "Finger", PrimitiveType.Capsule, new Vector3(f * 0.014f, -0.215f, 0.005f),
                              new Vector3(0.008f, 0.025f, 0.008f), skin, new Vector3(0f, 0f, -f * 14f));
        return pivot.transform;
    }

    // ------------------------------------------------------------------ behaviour

    void Update()
    {
        if (nursery == null || player == null) return;
        if (GameManager.Instance != null && GameManager.Instance.IsEnded) return;

        float dt = Time.deltaTime;
        bool alert = nursery.Alert;
        if (alert) alertTimer += dt; else alertTimer = 0f;
        bool go = alert && alertTimer >= wakeDelay;

        // the player looks at it -> it freezes (stop-motion, like it was never moving)
        frozen = go && IsSeen();
        if (!frozen) animT += dt;

        bool moving = false, scratching = false;

        if (!frozen)
        {
            if (go)
            {
                wake = Mathf.MoveTowards(wake, 1f, dt / riseTime);
                if (wake >= 1f)
                {
                    Vector3 to = player.transform.position - transform.position;
                    bool sameLevel = Mathf.Abs(to.y) < 2.2f;
                    to.y = 0f;
                    if (to.magnitude > reach * 0.85f || !sameLevel)
                    {
                        moving = Step(ChaseTarget(to.magnitude), dt);
                    }
                    else
                    {
                        scratching = true;
                        Face(to, dt);
                        contactTimer += dt;
                        if (contactTimer > 0.4f && Time.time >= nursery.nextHitTime && health != null)
                        {
                            nursery.nextHitTime = Time.time + nursery.hitCooldown;
                            health.TakeDamage(1);
                        }
                    }
                }
            }
            else if (wake > 0f || (transform.position - homePos).sqrMagnitude > 0.01f)
            {
                // calm: walk back home, then lie down
                Vector3 toHome = homePos - transform.position;
                toHome.y = 0f;
                if (toHome.magnitude > 0.1f)
                {
                    wake = Mathf.MoveTowards(wake, 1f, dt / riseTime);
                    if (wake >= 1f) moving = Step(homePos, dt);
                }
                else
                {
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, homeRot, 180f * dt);
                    wake = Mathf.MoveTowards(wake, 0f, dt / riseTime);
                }
            }
        }

        if (!scratching) contactTimer = 0f;
        ApplyPose(dt, moving, scratching);
    }

    void Face(Vector3 dir, float dt)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), 420f * dt);
    }

    // Each doll aims for a point to the SIDE of the player (a different side for each doll).
    // The closer it gets, the more it aims straight at the player.
    Vector3 ChaseTarget(float flatDist)
    {
        Vector3 pp = player.transform.position;
        float k = Mathf.Clamp01((flatDist - reach) / 2f);
        Vector3 slot = Quaternion.Euler(0f, approachAngle, 0f) * Vector3.forward * (surroundRadius * k);
        return pp + slot;
    }

    // one step toward a target, in jerky bursts. returns true if it moved.
    bool Step(Vector3 target, float dt)
    {
        Vector3 pos = transform.position;
        Vector3 d = target - pos;
        d.y = 0f;
        float dist = d.magnitude;
        if (dist < 0.05f) return false;
        Vector3 dir = d / dist;

        // keep apart from the other dolls
        Vector3 push = Vector3.zero;
        for (int i = 0; i < all.Count; i++)
        {
            var o = all[i];
            if (o == this || o.nursery != nursery) continue;
            Vector3 dd = pos - o.transform.position;
            dd.y = 0f;
            float m = dd.magnitude;
            if (m >= personalSpace) continue;
            Vector3 away = m < 0.001f ? (Quaternion.Euler(0f, approachAngle, 0f) * Vector3.forward) : dd / m;
            push += away * (1f - m / personalSpace);
        }
        if (push.sqrMagnitude > 0.0001f)
        {
            dir += push * 1.8f;
            if (dir.sqrMagnitude < 0.0001f) dir = push;
            dir.Normalize();
        }

        Face(dir, dt);

        float pulse = Mathf.Abs(Mathf.Sin(animT * 9f));
        float sp = walkSpeed * (0.3f + 0.7f * pulse);

        // go around small obstacles: try straight, then 40 and 80 degrees to each side
        float[] angles = { 0f, 40f, -40f, 80f, -80f };
        Vector3 use = Vector3.zero;
        bool free = false;
        for (int i = 0; i < angles.Length; i++)
        {
            Vector3 t = Quaternion.Euler(0f, angles[i], 0f) * dir;
            if (!HorrorMeshes.Blocked(pos + Vector3.up * 0.12f, t, 0.35f)) { use = t; free = true; break; }
        }
        if (!free) return false;

        Vector3 np = pos + use * sp * dt;

        // stay inside the nursery
        Bounds b = nursery.WorldBounds;
        np.x = Mathf.Clamp(np.x, b.min.x + 0.2f, b.max.x - 0.2f);
        np.z = Mathf.Clamp(np.z, b.min.z + 0.2f, b.max.z - 0.2f);

        float gy = HorrorMeshes.GroundY(np, pos.y);
        if (Mathf.Abs(gy - pos.y) > 0.35f) return false;
        np.y = gy;
        transform.position = np;
        return true;
    }

    // can the player see this doll right now? (on screen, and no wall or furniture in between)
    bool IsSeen()
    {
        var cam = Camera.main;
        if (cam == null) return false;

        Vector3 p = transform.position + Vector3.up * (0.25f * size);
        Vector3 vp = cam.WorldToViewportPoint(p);
        if (vp.z < 0.1f || vp.x < -0.05f || vp.x > 1.05f || vp.y < -0.05f || vp.y > 1.05f) return false;

        Vector3 from = cam.transform.position;
        Vector3 d = p - from;
        float dist = d.magnitude;
        if (dist < 0.1f) return true;

        int n = Physics.RaycastNonAlloc(from, d / dist, seeHits, dist - 0.05f, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
            if (seeHits[i].collider.GetComponentInParent<PlayerController>() == null) return false;
        return true;
    }

    // ------------------------------------------------------------------ pose / animation

    void ApplyPose(float dt, bool moving, bool scratching)
    {
        if (visual == null) return;

        float e = Mathf.SmoothStep(0f, 1f, wake);

        // lying on its back -> standing up
        visual.localRotation = Quaternion.Euler(Mathf.Lerp(-90f, 0f, e), 0f, 0f);
        float hop = moving ? Mathf.Abs(Mathf.Sin(animT * 9f)) * 0.03f * size : 0f;
        visual.localPosition = new Vector3(0f, Mathf.Lerp(0.16f * size, 0f, e) + hop, Mathf.Lerp(0.3f * size, 0f, e));

        // head: drooping and loose when lying, twitchy when awake
        Quaternion headWant = Quaternion.Euler(
            Mathf.Lerp(25f, 6f * Mathf.Sin(animT * 3f), e),
            Mathf.Lerp(15f, 14f * Mathf.Sin(animT * 2.3f), e),
            Mathf.Lerp(-10f, 16f * Mathf.Sin(animT * 1.7f), e));
        head.localRotation = Quaternion.Slerp(head.localRotation, headWant, dt > 0f ? dt * 10f : 1f);

        // arms: hang when lying, reach forward when hunting, flail when scratching
        float ax = 0f;
        float az = 8f;
        if (e > 0.95f)
        {
            ax = -80f + 8f * Mathf.Sin(animT * 14f);
            az = 4f;
        }
        if (scratching)
        {
            ax = -70f + 45f * Mathf.Sin(animT * 22f);
        }
        float axR = scratching ? -70f + 45f * Mathf.Sin(animT * 22f + Mathf.PI) : ax;
        Quaternion l = Quaternion.Euler(ax, 0f, -az);
        Quaternion r = Quaternion.Euler(axR, 0f, az);
        float k = dt > 0f ? dt * 12f : 1f;
        armL.localRotation = Quaternion.Slerp(armL.localRotation, l, k);
        armR.localRotation = Quaternion.Slerp(armR.localRotation, r, k);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.5f, 0.5f, 0.8f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.15f, 0.2f);
    }
}