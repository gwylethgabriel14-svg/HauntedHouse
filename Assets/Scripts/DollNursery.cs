using System.Collections.Generic;
using UnityEngine;

// The NURSERY zone. The dolls (HorrorDoll, children of this object) lie still until the player walks into this zone.
// Then they wake up and go after the player. If the player leaves the room and stays out for a few seconds,
// they calm down, walk back to where they were and lie down again.
//
// NEW: at the start of every game the dolls are scattered at random spots on the nursery floor,
// far from each other, each one lying in a different direction.
//
// The dolls never leave this box. The box is drawn in the Scene view (select the object to see it).
// Add it with: Haunted House > Enemies > Add NURSERY DOLLS  (select the nursery room first so the box fits it).
public class DollNursery : MonoBehaviour
{
    [Header("Zone (the object's position = the floor in the middle of the room)")]
    public Vector3 size = new Vector3(6f, 3f, 6f);

    [Header("Behaviour")]
    public float calmDelay = 4f;       // seconds the player must be OUT of the room before the dolls calm down
    public float hitCooldown = 1.3f;   // all the dolls together hit at most once per this many seconds
    public float yBelow = 0.5f;        // how far below the floor still counts as "inside"
    public float yAbove = 0f;          // how far above the box top still counts (keep 0 so the floor above does not trigger)

    [Header("Hiwa-hiwalay na dolls")]
    [Tooltip("Ikalat ang dolls sa random na pwesto pagka-start ng laro.")]
    public bool scatterOnStart = true;
    [Tooltip("Pinakamaliit na layo ng bawat doll sa isa't isa (metro).")]
    public float dollSpacing = 1.6f;
    [Tooltip("Layo mula sa pader ng nursery.")]
    public float wallMargin = 0.6f;
    [Tooltip("0 = iba-iba kada laro. Maglagay ng numero para pare-pareho (pang-test).")]
    public int seed = 0;

    [Header("Audio (i-drag ang clip dito, optional)")]
    [Tooltip("Tuloy-tuloy habang gising ang dolls. Humihinto pag kumalma.")]
    public AudioClip alertLoop;
    [Range(0f, 1f)] public float volume = 0.8f;
    [Tooltip("0 = parang nasa tenga mo lang, 1 = lumalakas habang papalapit sa nursery.")]
    [Range(0f, 1f)] public float spatialBlend = 1f;
    public float maxHearDistance = 15f;

    [Header("Materials (the menu assigns these)")]
    public Material porcelainMat;
    public Material dressMat;
    public Material hairMat;
    public Material eyeMat;
    public Material mouthMat;

    [HideInInspector] public float nextHitTime;

    public bool Alert { get; private set; }

    PlayerController player;
    float lastInside = -99f;

    AudioSource loopSource;

    public Bounds WorldBounds
    {
        get { return new Bounds(transform.position + Vector3.up * size.y * 0.5f, size); }
    }

    // Awake runs before any doll's Start, so every doll remembers its NEW spot as "home".
    void Awake()
    {
        SetupAudio();
        if (scatterOnStart) Scatter();
    }

    void SetupAudio()
    {
        loopSource = gameObject.AddComponent<AudioSource>();
        loopSource.playOnAwake = false;
        loopSource.loop = true;
        loopSource.clip = alertLoop;
        loopSource.volume = volume;
        loopSource.spatialBlend = spatialBlend;
        loopSource.maxDistance = maxHearDistance;
        loopSource.rolloffMode = AudioRolloffMode.Linear;
    }

    void SetAlert(bool value)
    {
        if (Alert == value) return;
        Alert = value;

        if (value)
        {
            if (alertLoop != null)
            {
                loopSource.clip = alertLoop;
                loopSource.Play();
            }
        }
        else
        {
            loopSource.Stop();
        }
    }

    void Scatter()
    {
        var dolls = GetComponentsInChildren<HorrorDoll>();
        if (dolls.Length == 0) return;
        if (seed != 0) Random.InitState(seed);

        Bounds b = WorldBounds;
        float baseY = transform.position.y;
        float margin = wallMargin;
        if (b.size.x < margin * 2f + 0.5f || b.size.z < margin * 2f + 0.5f) margin = 0.1f;

        var placed = new List<Vector3>();
        float angleStep = 360f / dolls.Length;
        float angleOffset = Random.Range(0f, 360f);
        int ok = 0;

        for (int i = 0; i < dolls.Length; i++)
        {
            var d = dolls[i];
            bool done = false;

            for (int t = 0; t < 300 && !done; t++)
            {
                // habang dumadami ang palya, lumuluwag ang layo
                float spacing = dollSpacing * Mathf.Lerp(1f, 0.4f, t / 300f);

                float x = Random.Range(b.min.x + margin, b.max.x - margin);
                float z = Random.Range(b.min.z + margin, b.max.z - margin);

                // sahig lang (hindi ibabaw ng gamit)
                float gy = HorrorMeshes.GroundY(new Vector3(x, baseY, z), baseY);
                if (Mathf.Abs(gy - baseY) > 0.15f) continue;

                // walang bagay na tatama sa nakahigang doll
                if (Physics.CheckBox(new Vector3(x, gy + 0.13f, z), new Vector3(0.28f, 0.1f, 0.28f),
                        Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                    continue;

                Vector3 p = new Vector3(x, gy, z);
                bool tooClose = false;
                for (int k = 0; k < placed.Count; k++)
                {
                    Vector3 dd = placed[k] - p;
                    dd.y = 0f;
                    if (dd.magnitude < spacing) { tooClose = true; break; }
                }
                if (tooClose) continue;

                d.transform.position = p;
                d.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                d.approachAngle = angleOffset + i * angleStep;   // iba-ibang gilid ng player ang pupuntahan
                placed.Add(p);
                done = true;
                ok++;
            }

            if (!done)
                Debug.LogWarning("[DollNursery] Walang nahanap na pwesto para sa " + d.name +
                                 ". Iniwan sa dating pwesto. (Bawasan ang Doll Spacing o palakihin ang Size.)", d);
        }

        Debug.Log("[DollNursery] " + ok + " / " + dolls.Length + " doll ang nailagay nang hiwa-hiwalay.");
    }

    void Update()
    {
        if (player == null)
        {
            player = FindFirstObjectByType<PlayerController>();
            if (player == null) return;
        }

        Vector3 p = player.transform.position;
        Bounds b = WorldBounds;
        bool inside = Mathf.Abs(p.x - b.center.x) <= b.extents.x
                   && Mathf.Abs(p.z - b.center.z) <= b.extents.z
                   && p.y >= transform.position.y - yBelow
                   && p.y <= transform.position.y + size.y + yAbove;

        if (inside)
        {
            SetAlert(true);
            lastInside = Time.time;
        }
        else if (Alert && Time.time - lastInside > calmDelay)
        {
            SetAlert(false);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.9f);
        Gizmos.DrawWireCube(WorldBounds.center, size);

        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.4f);
        Gizmos.DrawWireSphere(WorldBounds.center, maxHearDistance);
    }
}