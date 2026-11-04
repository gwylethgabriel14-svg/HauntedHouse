using UnityEngine;

// BLACK SLIME. A glossy black puddle that creeps around slowly, wobbling and squelching like real slime.
// Its eyes only open when you are close, so in a dark room you may not see it until you are in it.
//
// If the player steps into it: the player is slowed down and loses a life every few seconds.
// ESCAPE: crouch + walk and the player slips out immediately (and the slime can't grab again for a few seconds).
//
// Add it with: Haunted House > Enemies > Add BLACK SLIME  (it builds its own body).
public class BlackSlime : MonoBehaviour
{
    [Header("Look")]
    public Material bodyMat;
    public Material eyeMat;
    public float radius = 1.1f;
    public float height = 0.42f;

    [Header("Movement (it creeps, slowly)")]
    public float wanderRadius = 3f;
    public float wanderSpeed = 0.35f;
    public float chaseRange = 6f;        // starts oozing toward the player inside this distance
    public float chaseSpeed = 0.7f;

    [Header("What it does to the player")]
    [Range(0.1f, 1f)] public float slowMultiplier = 0.35f;   // 0.35 = player moves at 35% speed
    public float damageDelay = 1f;       // seconds stuck before the first hit
    public float damageInterval = 2.5f;  // seconds between hits
    public int damage = 1;
    public float escapeImmunity = 3f;    // after crouch-walking out, it cannot grab you for this long

    // ------------------------------------------------------------------ internals

    const int RINGS = 14, SEGS = 40;
    const float EYE_RANGE = 8f;

    Mesh mesh;
    Vector3[] verts;
    float[] rim = new float[SEGS];
    float[] cosA = new float[SEGS];
    float[] sinA = new float[SEGS];

    Transform eyeL, eyeR;
    Transform[] bubbles = new Transform[5];
    float[] bubbleT = new float[5], bubbleSpeed = new float[5], bubbleSize = new float[5];
    Vector2[] bubblePos = new Vector2[5];

    Vector3 home, wanderTarget;
    float wanderWait;
    Vector3 moveDir = Vector3.forward, lookDir = Vector3.forward;
    float moveAmount;

    float eyeOpen, nextBlink, blinkUntil;
    float stuck, stuckAmt, flinch, immuneUntil, nextDamage;

    PlayerController player;
    PlayerHealth health;
    PlayerSlow slow;

    void Awake()
    {
        BuildBody();
    }

    void Start()
    {
        home = transform.position;
        PickWander();
        player = FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            health = player.GetComponent<PlayerHealth>();
            if (health == null) health = FindFirstObjectByType<PlayerHealth>();
            slow = PlayerSlow.For(player);
        }
        else Debug.LogWarning("BlackSlime: no PlayerController found.");
    }

    // ------------------------------------------------------------------ build

    void BuildBody()
    {
        for (int j = 0; j < SEGS; j++)
        {
            float a = j * Mathf.PI * 2f / SEGS;
            cosA[j] = Mathf.Cos(a);
            sinA[j] = Mathf.Sin(a);
        }

        if (bodyMat == null) bodyMat = HorrorMeshes.Fallback(new Color(0.015f, 0.015f, 0.02f), 0.95f);
        if (eyeMat == null) eyeMat = HorrorMeshes.Fallback(new Color(0.9f, 0.9f, 0.6f), 0.2f, new Color(1.6f, 1.5f, 0.8f));

        // body mesh: a center point + rings, with the rim wobbling every frame
        mesh = new Mesh { name = "SlimeBody" };
        mesh.MarkDynamic();
        verts = new Vector3[1 + RINGS * SEGS];
        var tris = new int[(SEGS + (RINGS - 1) * SEGS * 2) * 3];
        int k = 0;
        for (int j = 0; j < SEGS; j++)
        {
            int a = 1 + j, b = 1 + (j + 1) % SEGS;
            tris[k++] = 0; tris[k++] = b; tris[k++] = a;
        }
        for (int i = 1; i < RINGS; i++)
            for (int j = 0; j < SEGS; j++)
            {
                int a = 1 + (i - 1) * SEGS + j;
                int b = 1 + (i - 1) * SEGS + (j + 1) % SEGS;
                int c = 1 + i * SEGS + j;
                int d = 1 + i * SEGS + (j + 1) % SEGS;
                tris[k++] = a; tris[k++] = b; tris[k++] = c;
                tris[k++] = b; tris[k++] = d; tris[k++] = c;
            }
        UpdateMesh(0f, 1f);
        mesh.triangles = tris;
        mesh.RecalculateBounds();

        var body = new GameObject("Body");
        body.transform.SetParent(transform, false);
        body.AddComponent<MeshFilter>().sharedMesh = mesh;
        body.AddComponent<MeshRenderer>().sharedMaterial = bodyMat;

        // eyes (pale, glowing)
        eyeL = HorrorMeshes.Part(transform, "EyeL", PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.1f, eyeMat).transform;
        eyeR = HorrorMeshes.Part(transform, "EyeR", PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.1f, eyeMat).transform;
        eyeL.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        eyeR.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // bubbles that grow and pop on its back
        for (int i = 0; i < bubbles.Length; i++)
        {
            bubbles[i] = HorrorMeshes.Part(transform, "Bubble" + i, PrimitiveType.Sphere, Vector3.zero, Vector3.zero, bodyMat).transform;
            NewBubble(i);
            bubbleT[i] = Random.value;
        }
    }

    void NewBubble(int i)
    {
        bubbleT[i] = 0f;
        bubbleSpeed[i] = Random.Range(0.25f, 0.6f);
        bubbleSize[i] = Random.Range(0.07f, 0.17f);
        float ang = Random.value * Mathf.PI * 2f;
        float r = Random.Range(0.1f, 0.7f);
        bubblePos[i] = new Vector2(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r);   // as a fraction of the radius
    }

    // ------------------------------------------------------------------ update

    void Update()
    {
        float dt = Time.deltaTime;
        float t = Time.time;

        bool ended = GameManager.Instance != null && GameManager.Instance.IsEnded;
        float pd = 999f;
        if (player != null)
        {
            Vector3 d = player.transform.position - transform.position;
            d.y = 0f;
            pd = d.magnitude;
        }

        if (!ended) Move(dt, pd);
        HandlePlayer(dt, pd, ended);

        float squash = Mathf.Max(0.3f, 1f + 0.07f * Mathf.Sin(t * 2.2f) + 0.2f * stuckAmt - 0.35f * flinch);
        flinch = Mathf.MoveTowards(flinch, 0f, dt * 1.5f);

        UpdateMesh(t, squash);
        UpdateEyes(dt, squash, pd);
        UpdateBubbles(dt, squash);
    }

    void HandlePlayer(float dt, float pd, bool ended)
    {
        if (player == null || slow == null) return;

        bool near = !ended
                    && pd < radius * 0.8f
                    && Mathf.Abs(player.transform.position.y - transform.position.y) < 2f
                    && Time.time > immuneUntil;

        stuckAmt = Mathf.MoveTowards(stuckAmt, near ? 1f : 0f, dt * 2f);

        if (!near)
        {
            stuck = 0f;
            return;
        }

        // crouch + walk = slips out right away
        if (PlayerSlow.IsCrouching(player) && PlayerSlow.IsMoving())
        {
            immuneUntil = Time.time + escapeImmunity;
            stuck = 0f;
            slow.Release();
            flinch = 1f;
            return;
        }

        slow.Hold(slowMultiplier);
        stuck += dt;
        if (stuck >= damageDelay && Time.time >= nextDamage && health != null)
        {
            nextDamage = Time.time + damageInterval;
            health.TakeDamage(damage);
        }
    }

    // ------------------------------------------------------------------ movement

    void PickWander()
    {
        Vector2 c = Random.insideUnitCircle * wanderRadius;
        wanderTarget = home + new Vector3(c.x, 0f, c.y);
        wanderWait = Random.Range(1f, 3.5f);
    }

    void Move(float dt, float pd)
    {
        Vector3 pos = transform.position;
        Vector3 goal;
        float speed;
        bool chasing = player != null && Time.time > immuneUntil && pd < chaseRange && pd > 0.4f;

        if (chasing)
        {
            goal = player.transform.position;
            speed = chaseSpeed;
        }
        else
        {
            goal = wanderTarget;
            speed = wanderSpeed;
            Vector3 w = wanderTarget - pos; w.y = 0f;
            if (w.magnitude < 0.25f)
            {
                wanderWait -= dt;
                if (wanderWait <= 0f) PickWander();
                speed = 0f;
            }
        }

        Vector3 dir = goal - pos;
        dir.y = 0f;
        float dist = dir.magnitude;
        if (dist < 0.05f || speed <= 0f)
        {
            moveAmount = Mathf.MoveTowards(moveAmount, 0f, dt * 2f);
            return;
        }
        dir /= dist;

        // squelchy crawl: it surges forward, then rests
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 2.4f);
        float sp = speed * (0.15f + 0.85f * pulse * pulse);

        if (HorrorMeshes.Blocked(pos + Vector3.up * 0.25f, dir, radius * 0.8f + 0.15f))
        {
            if (!chasing) PickWander();
            moveAmount = Mathf.MoveTowards(moveAmount, 0f, dt * 2f);
            return;
        }

        Vector3 np = pos + dir * sp * dt;
        float gy = HorrorMeshes.GroundY(np, pos.y);
        if (Mathf.Abs(gy - pos.y) > 0.5f)
        {
            if (!chasing) PickWander();
            moveAmount = Mathf.MoveTowards(moveAmount, 0f, dt * 2f);
            return;
        }
        np.y = gy;
        transform.position = np;

        moveDir = Vector3.Slerp(moveDir, dir, dt * 3f);
        moveAmount = Mathf.MoveTowards(moveAmount, 1f, dt * 2f);
    }

    // ------------------------------------------------------------------ body animation

    float SurfaceY(float u, float x, float z, float t, float squash)
    {
        float dome = Mathf.Pow(Mathf.Clamp01(1f - u * u), 0.6f);
        float h = height * dome * squash;
        h += 0.03f * Mathf.Sin(u * 11f - t * 3f) * (1f - u);
        h += 0.06f * (Mathf.PerlinNoise(x * 1.6f + t * 0.5f, z * 1.6f - t * 0.4f) - 0.5f) * dome;
        return 0.012f + Mathf.Max(0f, h);
    }

    float DomeY(float u, float squash)
    {
        return 0.012f + height * Mathf.Pow(Mathf.Clamp01(1f - u * u), 0.6f) * squash;
    }

    void UpdateMesh(float t, float squash)
    {
        // the rim: lumpy, with pseudopods that reach in the direction it crawls
        for (int j = 0; j < SEGS; j++)
        {
            float n = Mathf.PerlinNoise(cosA[j] * 1.3f + 10f + t * 0.35f, sinA[j] * 1.3f + 10f - t * 0.27f);
            float r = radius * (0.85f + (n - 0.5f) * 0.9f);
            float align = Mathf.Max(0f, cosA[j] * moveDir.x + sinA[j] * moveDir.z);
            r *= 1f + 0.35f * align * moveAmount;
            rim[j] = r;
        }

        verts[0] = new Vector3(0f, SurfaceY(0f, 0f, 0f, t, squash), 0f);
        for (int i = 1; i <= RINGS; i++)
        {
            float u = i / (float)RINGS;
            for (int j = 0; j < SEGS; j++)
            {
                float rr = u * rim[j];
                float x = cosA[j] * rr, z = sinA[j] * rr;
                verts[1 + (i - 1) * SEGS + j] = new Vector3(x, SurfaceY(u, x, z, t, squash), z);
            }
        }
        mesh.vertices = verts;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    void UpdateEyes(float dt, float squash, float pd)
    {
        Vector3 want = moveAmount > 0.1f ? moveDir : lookDir;
        if (player != null && pd < EYE_RANGE)
        {
            Vector3 d = player.transform.position - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > 0.01f) want = d.normalized;
        }
        lookDir = Vector3.Slerp(lookDir, want, dt * 3f);

        if (Time.time > nextBlink)
        {
            blinkUntil = Time.time + 0.12f;
            nextBlink = Time.time + Random.Range(2f, 6f);
        }
        float tgt = (player != null && pd < EYE_RANGE) ? 1f : 0f;
        if (Time.time < blinkUntil) tgt *= 0.1f;
        eyeOpen = Mathf.MoveTowards(eyeOpen, tgt, dt * (tgt > eyeOpen ? 1.5f : 8f));

        bool on = eyeOpen > 0.03f;
        eyeL.gameObject.SetActive(on);
        eyeR.gameObject.SetActive(on);
        if (!on) return;

        Vector3 side = new Vector3(lookDir.z, 0f, -lookDir.x);
        Vector3 fwd = lookDir * radius * 0.30f;
        float sd = radius * 0.16f;
        float y = DomeY(0.33f, squash) + 0.03f;
        eyeL.localPosition = fwd + side * sd + Vector3.up * y;
        eyeR.localPosition = fwd - side * sd + Vector3.up * y;
        Vector3 s = new Vector3(0.13f, 0.18f * eyeOpen, 0.09f);
        eyeL.localScale = s;
        eyeR.localScale = s;
        eyeL.localRotation = eyeR.localRotation = Quaternion.LookRotation(lookDir);
    }

    void UpdateBubbles(float dt, float squash)
    {
        for (int i = 0; i < bubbles.Length; i++)
        {
            bubbleT[i] += dt * bubbleSpeed[i];
            if (bubbleT[i] >= 1f) NewBubble(i);

            float grow = Mathf.Sin(bubbleT[i] * Mathf.PI);
            float s = bubbleSize[i] * grow;
            Vector2 p = bubblePos[i] * radius;
            float u = Mathf.Clamp01(bubblePos[i].magnitude);
            bubbles[i].localPosition = new Vector3(p.x, DomeY(u, squash) + s * 0.25f, p.y);
            bubbles[i].localScale = Vector3.one * s;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 0f, 0.6f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, radius * 0.8f);
        Gizmos.color = new Color(0.4f, 0f, 0.6f, 0.3f);
        Vector3 c = Application.isPlaying ? home : transform.position;
        Gizmos.DrawWireSphere(c, wanderRadius);
    }
}
