using UnityEngine;
using UnityEngine.AI;

// A crying baby that shows up in a crib when the player gets close,
// then fades away as the player walks up to it.
public class CribBaby : MonoBehaviour
{
    [Header("Look")]
    public Sprite sprite;                            // optional, loaded from Resources if empty
    public string spriteResource = "Baby/baby_cry";
    public float height = 0.75f;                     // how tall she looks, in meters
    public float tilt = 55f;                         // 90 = lying flat, 0 = standing up
    [Range(0f, 1f)] public float maxAlpha = 0.9f;

    [Header("Sound")]
    public AudioClip cry;                            // optional, loaded from Resources if empty
    public string cryResource = "Baby/baby_cry_loop";
    [Range(0f, 1f)] public float cryVolume = 0.8f;
    public float hearRange = 14f;

    [Header("Behaviour")]
    public float appearDistance = 9f;                // she shows up when the player gets this close
    public float fadeStartDistance = 4.5f;           // starts fading when the player is closer than this
    public float goneDistance = 1.6f;                // completely gone at this distance
    public float appearTime = 2f;

    [Header("Enemy")]
    public bool attractEnemy = true;     // the enemy hears the crying and comes to the crib
    public float firstCallDelay = 3f;    // seconds of crying before she hears it, so the player gets a moment
    public float callInterval = 6f;      // seconds between each time she hears it
    public int maxCalls = 3;             // after this she stops hearing it, so it can't park her there forever
    public float callRange = 40f;        // how far away she can hear it from

    SpriteRenderer sr;
    AudioSource source;
    Transform player;
    Quaternion baseRotation;
    float appeared;
    float closeness = 1f;
    bool started;
    EnemyAI enemy;
    float cryTimer;
    int calls;

    void Awake()
    {
        if (sprite == null) sprite = LoadSprite();
        if (cry == null) cry = Resources.Load<AudioClip>(cryResource);

        if (sprite == null)
        {
            Debug.LogWarning("CribBaby: sprite not found (Resources/" + spriteResource + ").");
            enabled = false;
            return;
        }

        sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sharedMaterial = new Material(Shader.Find("Sprites/Default"));   // unlit, works in any render pipeline
        sr.color = new Color(1f, 1f, 1f, 0f);

        transform.localScale = Vector3.one * (height / sprite.bounds.size.y);

        source = gameObject.AddComponent<AudioSource>();
        source.clip = cry;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 1.5f;
        source.maxDistance = hearRange;
        source.volume = 0f;
    }

    void Start()
    {
        PlayerController pc = FindFirstObjectByType<PlayerController>();
        if (pc != null) player = pc.transform;
        else if (Camera.main != null) player = Camera.main.transform;

        enemy = FindFirstObjectByType<EnemyAI>();
    }

    void Update()
    {
        if (player == null) return;

        Vector3 flat = player.position - transform.position;
        flat.y = 0f;
        float dist = flat.magnitude;

        if (!started)
        {
            if (dist > appearDistance) return;
            started = true;
            Face(flat);
            if (cry != null) source.Play();
        }

        appeared = Mathf.MoveTowards(appeared, 1f, Time.deltaTime / Mathf.Max(0.01f, appearTime));

        // the closer the player gets, the fainter she is, and she never comes back
        float target = Mathf.Clamp01((dist - goneDistance) / Mathf.Max(0.01f, fadeStartDistance - goneDistance));
        closeness = Mathf.Min(closeness, target);

        float visible = Mathf.Min(appeared, closeness);
        sr.color = new Color(1f, 1f, 1f, visible * maxAlpha);
        source.volume = visible * cryVolume;

        if (attractEnemy && enemy != null && visible > 0.3f) CallEnemy();

        // small rocking so she doesn't look like a flat picture
        transform.rotation = baseRotation * Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 3f) * 2.5f);

        if (closeness <= 0.001f)
        {
            source.Stop();
            sr.enabled = false;
            enabled = false;
        }
    }

    // the enemy hears the crying the same way she hears a thrown bottle
    void CallEnemy()
    {
        cryTimer += Time.deltaTime;
        if (calls >= maxCalls) return;

        float wait = calls == 0 ? firstCallDelay : callInterval;
        if (cryTimer < wait) return;
        cryTimer = 0f;

        if (Vector3.Distance(enemy.transform.position, transform.position) > callRange) return;
        calls++;

        // the crib itself blocks the NavMesh, so send her to the closest walkable spot
        Vector3 target = transform.position;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(target, out hit, 3f, NavMesh.AllAreas))
            target = hit.position;

        enemy.HearNoise(target);
    }

    void Face(Vector3 toPlayer)
    {
        if (toPlayer.sqrMagnitude < 0.01f) toPlayer = transform.forward;
        baseRotation = Quaternion.LookRotation(-toPlayer.normalized, Vector3.up) * Quaternion.Euler(tilt, 0f, 0f);
        transform.rotation = baseRotation;
    }

    Sprite LoadSprite()
    {
        Sprite s = Resources.Load<Sprite>(spriteResource);
        if (s != null) return s;

        Texture2D tex = Resources.Load<Texture2D>(spriteResource);
        if (tex != null)
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);

        return null;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 1f, 0f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, appearDistance);
        Gizmos.color = new Color(1f, 0.6f, 0f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, fadeStartDistance);
        Gizmos.color = new Color(1f, 0f, 0f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, goneDistance);
    }
}