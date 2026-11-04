using UnityEngine;
using UnityEngine.Rendering;

// Small helpers used by the black slime and the dolls:
// building parts out of primitives, a lathe mesh (for the dress), ground / wall checks and materials.
public static class HorrorMeshes
{
    static readonly RaycastHit[] hits = new RaycastHit[24];

    // ------------------------------------------------------------------ parts

    public static GameObject Part(Transform parent, string name, PrimitiveType type, Vector3 pos, Vector3 scale,
                                  Material mat, Vector3 euler = default(Vector3))
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        var col = go.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localEulerAngles = euler;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    public static Transform Pivot(Transform parent, string name, Vector3 pos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        return go.transform;
    }

    public static GameObject MeshPart(Transform parent, string name, Mesh mesh, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    // profile = (radius, height) points from bottom to top, revolved around the Y axis
    public static Mesh Lathe(Vector2[] profile, int seg)
    {
        int rings = profile.Length;
        var v = new Vector3[rings * (seg + 1)];
        var uv = new Vector2[v.Length];
        for (int i = 0; i < rings; i++)
            for (int j = 0; j <= seg; j++)
            {
                float a = j / (float)seg * Mathf.PI * 2f;
                int idx = i * (seg + 1) + j;
                v[idx] = new Vector3(profile[i].x * Mathf.Cos(a), profile[i].y, profile[i].x * Mathf.Sin(a));
                uv[idx] = new Vector2(j / (float)seg, i / (float)(rings - 1));
            }

        var tris = new int[(rings - 1) * seg * 6];
        int k = 0;
        for (int i = 0; i < rings - 1; i++)
            for (int j = 0; j < seg; j++)
            {
                int a = i * (seg + 1) + j;
                int b = a + 1;
                int c = a + (seg + 1);
                int d = c + 1;
                tris[k++] = a; tris[k++] = c; tris[k++] = b;
                tris[k++] = b; tris[k++] = c; tris[k++] = d;
            }

        var m = new Mesh { name = "Lathe" };
        m.vertices = v;
        m.uv = uv;
        m.triangles = tris;
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    // ------------------------------------------------------------------ world checks

    // floor height under a position (ignores the player and trigger colliders)
    public static float GroundY(Vector3 pos, float fallback)
    {
        int n = Physics.RaycastNonAlloc(pos + Vector3.up * 0.5f, Vector3.down, hits, 3f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        float y = fallback;
        for (int i = 0; i < n; i++)
        {
            if (hits[i].distance < best && hits[i].collider.GetComponentInParent<PlayerController>() == null)
            {
                best = hits[i].distance;
                y = hits[i].point.y;
            }
        }
        return y;
    }

    // true if a wall / furniture is in the way (the player does not count)
    public static bool Blocked(Vector3 from, Vector3 dir, float dist)
    {
        int n = Physics.RaycastNonAlloc(from, dir, hits, dist, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
            if (hits[i].collider.GetComponentInParent<PlayerController>() == null) return true;
        return false;
    }

    // ------------------------------------------------------------------ materials

    public static Shader PipelineShader()
    {
        var rp = GraphicsSettings.currentRenderPipeline;
        if (rp != null && rp.defaultMaterial != null) return rp.defaultMaterial.shader;
        var s = Shader.Find("Universal Render Pipeline/Lit");
        if (s == null) s = Shader.Find("Standard");
        return s;
    }

    public static void Configure(Material m, Color c, float smooth, float metal = 0f, Color? emission = null)
    {
        m.color = c;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission.Value);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
    }

    // used only when no material was assigned (the editor menu assigns real material assets)
    public static Material Fallback(Color c, float smooth, Color? emission = null)
    {
        var m = new Material(PipelineShader());
        Configure(m, c, smooth, 0f, emission);
        return m;
    }
}
