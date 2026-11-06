
using UnityEngine;

// One of THREE matching torn fragments of the same music sheet.
// Generated at runtime, so no prefab OnValidate / DestroyImmediate errors.
public class Page : MonoBehaviour, IInteractable
{
    public Sprite icon;
    [Range(1, 3)] public int pieceIndex = 1;

    [Header("Paper appearance")]
    public float sheetWidth = 1.5f;
    public float sheetHeight = 0.95f;
    public bool floatMotion = true;
    public float floatAmount = 0.035f;
    public float floatSpeed = 1.7f;
    public bool rotateSlowly = true;
    public float rotateSpeed = 13f;

    [Header("Visibility")]
    public bool glow = true;
    public float glowIntensity = 0.3f;
    public float glowRange = 1.4f;

    Transform visual;
    Light pickupLight;
    Vector3 visualPosition;
    static Texture2D sharedScore;
    static Material sharedPaper;
    bool collected;

    public string Prompt => "Pick up music note " + pieceIndex + "/3";

    // PageSpawner calls this immediately after Instantiate, before Start().
    public void SetPieceIndex(int index) { pieceIndex = Mathf.Clamp(index, 1, 3); }

    void Start()
    {
        // Ignore any old generated visual left on the prefab.
        Transform old = transform.Find("PageVisual");
        if (old != null) { old.gameObject.SetActive(false); Destroy(old.gameObject); }
        BuildVisual();

        // The ROOT keeps a collider so the existing Interactor can hit it.
        if (GetComponent<Collider>() == null)
        {
            SphereCollider col = gameObject.AddComponent<SphereCollider>();
            col.radius = 0.48f;
        }
        MeshRenderer original = GetComponent<MeshRenderer>();
        if (original != null) original.enabled = false;
    }

    void Update()
    {
        if (visual == null) return;
        if (floatMotion)
        {
            Vector3 p = visualPosition;
            p.y += Mathf.Sin(Time.time * floatSpeed + pieceIndex) * floatAmount;
            visual.localPosition = p;
        }
        if (rotateSlowly) visual.Rotate(0f, rotateSpeed * Time.deltaTime, 0f, Space.World);
        if (pickupLight != null)
            pickupLight.intensity = glowIntensity * (1f + 0.1f * Mathf.Sin(Time.time * 2f));
    }

    public void Interact(PlayerController player)
    {
        if (collected || GameManager.Instance == null) return;
        if (GameManager.Instance.Pages >= GameManager.Instance.totalPages) return;
        collected = true;
        GameManager.Instance.CollectPage();
        if (Inventory.Instance != null)
            Inventory.Instance.AddItem("Music Note", icon, null, true);
        Destroy(gameObject);
    }

    void BuildVisual()
    {
        if (sharedScore == null) sharedScore = MakeScore();
        if (sharedPaper == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Texture");
            sharedPaper = new Material(shader);
            if (sharedPaper.HasProperty("_BaseMap")) sharedPaper.SetTexture("_BaseMap", sharedScore);
            if (sharedPaper.HasProperty("_MainTex")) sharedPaper.SetTexture("_MainTex", sharedScore);
        }

        GameObject holder = new GameObject("GeneratedTornMusicVisual");
        holder.transform.SetParent(transform, false);
        visual = holder.transform;
        visualPosition = new Vector3(0f, 0.10f, 0f);
        visual.localPosition = visualPosition;

        GameObject sheet = new GameObject("TornPaperPiece" + pieceIndex);
        sheet.transform.SetParent(visual, false);
        sheet.AddComponent<MeshFilter>().sharedMesh = BuildPieceMesh();
        sheet.AddComponent<MeshRenderer>().sharedMaterial = sharedPaper;

        // Light does not cast expensive shadows.
        if (glow)
        {
            GameObject lamp = new GameObject("PickupGlow");
            lamp.transform.SetParent(transform, false);
            lamp.transform.localPosition = Vector3.up * 0.3f;
            pickupLight = lamp.AddComponent<Light>();
            pickupLight.type = LightType.Point;
            pickupLight.color = new Color(1f, 0.78f, 0.40f);
            pickupLight.range = glowRange;
            pickupLight.intensity = glowIntensity;
            pickupLight.shadows = LightShadows.None;
        }
    }

    // Vertical tear boundaries. Shared exactly by adjacent pieces.
    static float TearA(float y) => -0.17f + 0.040f * Mathf.Sin(y * 23f) + 0.018f * Mathf.Sin(y * 57f);
    static float TearB(float y) =>  0.16f + 0.045f * Mathf.Sin(y * 19f + 2f) + 0.014f * Mathf.Sin(y * 49f);

    Mesh BuildPieceMesh()
    {
        const int steps = 28;
        int count = steps + 1;
        Vector3[] v = new Vector3[count * 4];
        Vector2[] uv = new Vector2[v.Length];
        int[] tris = new int[steps * 12];
        float frontZ = -0.008f, backZ = 0.008f;
        for (int i = 0; i < count; i++)
        {
            float y = -0.5f + i / (float)steps;
            float left = pieceIndex == 1 ? -0.5f : pieceIndex == 2 ? TearA(y) : TearB(y);
            float right = pieceIndex == 1 ? TearA(y) : pieceIndex == 2 ? TearB(y) : 0.5f;
            float bottomTopFray = 0.006f * Mathf.Sin((left + right) * 83f + pieceIndex);
            float yy = y;
            if (i == 0) yy += bottomTopFray;
            if (i == steps) yy -= bottomTopFray;
            int k = i * 2;
            v[k] = new Vector3(left * sheetWidth, yy * sheetHeight, frontZ);
            v[k + 1] = new Vector3(right * sheetWidth, yy * sheetHeight, frontZ);
            v[count * 2 + k] = new Vector3(left * sheetWidth, yy * sheetHeight, backZ);
            v[count * 2 + k + 1] = new Vector3(right * sheetWidth, yy * sheetHeight, backZ);
            uv[k] = new Vector2(left + 0.5f, yy + 0.5f);
            uv[k + 1] = new Vector2(right + 0.5f, yy + 0.5f);
            uv[count * 2 + k] = uv[k];
            uv[count * 2 + k + 1] = uv[k + 1];
        }
        int t = 0;
        for (int i = 0; i < steps; i++)
        {
            int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
            // Front (+Z): left-bottom -> right-bottom -> left-top.
            tris[t++] = a; tris[t++] = b; tris[t++] = c;
            tris[t++] = b; tris[t++] = d; tris[t++] = c;
            int o = count * 2;
            tris[t++] = a + o; tris[t++] = c + o; tris[t++] = b + o;
            tris[t++] = b + o; tris[t++] = c + o; tris[t++] = d + o;
        }
        Mesh mesh = new Mesh();
        mesh.name = "MatchingTornNote_" + pieceIndex;
        mesh.vertices = v;
        mesh.uv = uv;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static Texture2D MakeScore()
    {
        const int size = 512;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] p = new Color[size * size];
        Color parchment = new Color(0.92f, 0.84f, 0.66f);
        Color ink = new Color(0.13f, 0.09f, 0.07f);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float grain = Mathf.PerlinNoise(x / 39f, y / 35f);
            float stain = Mathf.PerlinNoise(x / 121f + 10f, y / 118f);
            p[y * size + x] = parchment * (0.88f + grain * 0.12f - stain * 0.09f);
        }
        foreach (int band in new int[] { 135, 315 })
        for (int j = 0; j < 5; j++)
        for (int x = 24; x < 488; x++) Put(p, size, x, band + j * 16, ink, 1);
        // Musical notes continue across all three fragments.
        for (int i = 0; i < 18; i++)
        {
            int x = 42 + i * 24;
            int y = (i % 2 == 0 ? 135 : 315) + (i * 13 % 5) * 16;
            for (int dy = -6; dy <= 6; dy++)
            for (int dx = -10; dx <= 10; dx++)
                if ((dx * dx / 100f + dy * dy / 36f) <= 1f) Put(p, size, x + dx, y + dy, ink, 0);
            for (int yy = y; yy <= y + 39; yy++) Put(p, size, x + 9, yy, ink, 1);
        }
        tex.SetPixels(p);
        tex.Apply();
        return tex;
    }

    static void Put(Color[] p, int size, int x, int y, Color c, int radius)
    {
        for (int yy = -radius; yy <= radius; yy++)
        for (int xx = -radius; xx <= radius; xx++)
        {
            int px = x + xx, py = y + yy;
            if (px >= 0 && px < size && py >= 0 && py < size) p[py * size + px] = c;
        }
    }
}
