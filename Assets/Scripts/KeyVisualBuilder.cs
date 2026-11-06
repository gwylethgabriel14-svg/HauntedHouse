
using UnityEngine;

// Generates an antique-style 3D key at runtime.
// Attach to the Key prefab beside KeyItem.
// Does not change the pickup logic.

public class KeyVisualBuilder : MonoBehaviour
{
    [Header("Build")]
    public bool buildOnAwake = true;

    [Header("Look")]
    public Material keyMaterial;
    public Color keyColor =
        new Color(0.82f, 0.67f, 0.22f, 1f);

    public bool metallicLook = true;

    [Header("Animation")]
    public bool rotate = true;
    public float rotateSpeed = 45f;

    public bool floatMotion = true;
    public float floatAmount = 0.05f;
    public float floatSpeed = 1.5f;

    [Header("Glow")]
    public bool addGlow = true;
    public Color glowColor =
        new Color(1f, 0.85f, 0.35f);

    public float glowIntensity = 0.6f;
    public float glowRange = 1.6f;

    Transform visualRoot;
    Vector3 baseLocalPosition;
    Light glowLight;

    void Awake()
    {
        if (buildOnAwake)
            EnsureBuilt();
    }

    void Update()
    {
        if (visualRoot == null)
            return;

        if (rotate)
        {
            visualRoot.Rotate(
                0f,
                rotateSpeed * Time.deltaTime,
                0f,
                Space.Self);
        }

        if (floatMotion)
        {
            Vector3 p = baseLocalPosition;

            p.y += Mathf.Sin(Time.time * floatSpeed)
                   * floatAmount;

            visualRoot.localPosition = p;
        }

        if (glowLight != null)
        {
            glowLight.intensity =
                glowIntensity *
                (1f + 0.15f *
                Mathf.Sin(Time.time * 2.1f));
        }
    }

    [ContextMenu("Rebuild Key Visual (Play Mode Only)")]
    public void RebuildNow()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "Enter Play Mode to rebuild the key.",
                this);
            return;
        }

        Transform old = transform.Find("KeyVisual");

        if (old != null)
        {
            old.gameObject.SetActive(false);
            Destroy(old.gameObject);
        }

        Transform oldGlow = transform.Find("KeyGlow");

        if (oldGlow != null)
        {
            oldGlow.gameObject.SetActive(false);
            Destroy(oldGlow.gameObject);
        }

        visualRoot = null;
        glowLight = null;

        EnsureBuilt();
    }

    void EnsureBuilt()
    {
        visualRoot = transform.Find("KeyVisual");

        if (visualRoot != null)
        {
            baseLocalPosition = visualRoot.localPosition;
            return;
        }

        Material mat = GetMaterial();

        GameObject group =
            new GameObject("KeyVisual");

        group.transform.SetParent(transform, false);

        visualRoot = group.transform;

        baseLocalPosition =
            new Vector3(0f, 0.85f, 0f);

        visualRoot.localPosition = baseLocalPosition;

        // Real hollow ring at the head of the key.
        GameObject bow =
            new GameObject("HollowRingBow");

        bow.transform.SetParent(visualRoot, false);

        MeshFilter filter =
            bow.AddComponent<MeshFilter>();

        filter.sharedMesh =
            CreateRingMesh(0.19f, 0.055f, 36, 8);

        bow.AddComponent<MeshRenderer>()
           .sharedMaterial = mat;

        bow.transform.localPosition =
            new Vector3(0f, 0.36f, 0f);

        // Key body.
        Part("Neck",
            new Vector3(0f, 0.115f, 0f),
            new Vector3(0.14f, 0.25f, 0.08f),
            mat);

        Part("Shaft",
            new Vector3(0f, -0.29f, 0f),
            new Vector3(0.105f, 0.62f, 0.08f),
            mat);

        Part("Collar",
            new Vector3(0f, -0.04f, 0f),
            new Vector3(0.18f, 0.075f, 0.095f),
            mat);

        // Teeth.
        Part("Tooth1",
            new Vector3(0.11f, -0.49f, 0f),
            new Vector3(0.19f, 0.10f, 0.08f),
            mat);

        Part("Tooth2",
            new Vector3(0.10f, -0.64f, 0f),
            new Vector3(0.17f, 0.09f, 0.08f),
            mat);

        Part("Tooth3",
            new Vector3(0.02f, -0.68f, 0f),
            new Vector3(0.11f, 0.11f, 0.08f),
            mat);

        // Hide old cube visual, but keep collider.
        MeshRenderer original =
            GetComponent<MeshRenderer>();

        if (original != null)
            original.enabled = false;

        if (addGlow)
        {
            GameObject g =
                new GameObject("KeyGlow");

            g.transform.SetParent(transform, false);

            g.transform.localPosition =
                new Vector3(0f, 1.08f, 0f);

            glowLight = g.AddComponent<Light>();
            glowLight.type = LightType.Point;
            glowLight.color = glowColor;
            glowLight.intensity = glowIntensity;
            glowLight.range = glowRange;
            glowLight.shadows = LightShadows.None;
        }
    }

    void Part(
        string partName,
        Vector3 pos,
        Vector3 scale,
        Material mat)
    {
        GameObject part =
            new GameObject(partName);

        part.transform.SetParent(visualRoot, false);
        part.transform.localPosition = pos;
        part.transform.localScale = scale;

        MeshFilter mf =
            part.AddComponent<MeshFilter>();

        mf.sharedMesh = CubeMesh();

        part.AddComponent<MeshRenderer>()
            .sharedMaterial = mat;
    }

    Material GetMaterial()
    {
        if (keyMaterial != null)
            return keyMaterial;

        Shader sh =
            Shader.Find("Universal Render Pipeline/Lit");

        if (sh == null)
            sh = Shader.Find("Standard");

        if (sh == null)
            sh = Shader.Find("Sprites/Default");

        Material m = new Material(sh);

        if (m.HasProperty("_BaseColor"))
            m.SetColor("_BaseColor", keyColor);

        if (m.HasProperty("_Color"))
            m.SetColor("_Color", keyColor);

        if (metallicLook)
        {
            if (m.HasProperty("_Metallic"))
                m.SetFloat("_Metallic", 0.78f);

            if (m.HasProperty("_Smoothness"))
                m.SetFloat("_Smoothness", 0.52f);
        }

        return m;
    }

    static Mesh CubeMesh()
    {
        Vector3[] v =
        {
            new Vector3(-.5f,-.5f,-.5f),
            new Vector3(-.5f,.5f,-.5f),
            new Vector3(.5f,.5f,-.5f),
            new Vector3(.5f,-.5f,-.5f),

            new Vector3(.5f,-.5f,.5f),
            new Vector3(.5f,.5f,.5f),
            new Vector3(-.5f,.5f,.5f),
            new Vector3(-.5f,-.5f,.5f),

            new Vector3(-.5f,-.5f,.5f),
            new Vector3(-.5f,.5f,.5f),
            new Vector3(-.5f,.5f,-.5f),
            new Vector3(-.5f,-.5f,-.5f),

            new Vector3(.5f,-.5f,-.5f),
            new Vector3(.5f,.5f,-.5f),
            new Vector3(.5f,.5f,.5f),
            new Vector3(.5f,-.5f,.5f),

            new Vector3(-.5f,.5f,-.5f),
            new Vector3(-.5f,.5f,.5f),
            new Vector3(.5f,.5f,.5f),
            new Vector3(.5f,.5f,-.5f),

            new Vector3(-.5f,-.5f,.5f),
            new Vector3(-.5f,-.5f,-.5f),
            new Vector3(.5f,-.5f,-.5f),
            new Vector3(.5f,-.5f,.5f)
        };

        int[] triangles = new int[36];

        for (int f = 0; f < 6; f++)
        {
            int a = f * 4;
            int t = f * 6;

            triangles[t] = a;
            triangles[t + 1] = a + 1;
            triangles[t + 2] = a + 2;

            triangles[t + 3] = a;
            triangles[t + 4] = a + 2;
            triangles[t + 5] = a + 3;
        }

        Mesh m = new Mesh();
        m.vertices = v;
        m.triangles = triangles;
        m.RecalculateNormals();

        return m;
    }

    static Mesh CreateRingMesh(
        float radius,
        float tube,
        int segments,
        int sides)
    {
        Vector3[] verts =
            new Vector3[(segments + 1) * (sides + 1)];

        Vector2[] uv = new Vector2[verts.Length];

        int[] triangles =
            new int[segments * sides * 6];

        for (int i = 0; i <= segments; i++)
        {
            float a =
                i * Mathf.PI * 2f / segments;

            Vector3 center = new Vector3(
                Mathf.Cos(a) * radius,
                Mathf.Sin(a) * radius,
                0f);

            for (int j = 0; j <= sides; j++)
            {
                float b =
                    j * Mathf.PI * 2f / sides;

                Vector3 normal = new Vector3(
                    Mathf.Cos(a) * Mathf.Cos(b),
                    Mathf.Sin(a) * Mathf.Cos(b),
                    Mathf.Sin(b));

                int index =
                    i * (sides + 1) + j;

                verts[index] =
                    center + normal * tube;

                uv[index] = new Vector2(
                    i / (float)segments,
                    j / (float)sides);
            }
        }

        int t = 0;

        for (int i = 0; i < segments; i++)
        {
            for (int j = 0; j < sides; j++)
            {
                int p = i * (sides + 1) + j;
                int q = p + sides + 1;

                triangles[t++] = p;
                triangles[t++] = q;
                triangles[t++] = p + 1;

                triangles[t++] = p + 1;
                triangles[t++] = q;
                triangles[t++] = q + 1;
            }
        }

        Mesh mesh = new Mesh();
        mesh.name = "KeyHollowRing";
        mesh.vertices = verts;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();

        return mesh;
    }
}
