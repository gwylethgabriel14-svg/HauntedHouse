using UnityEngine;

public class Page : MonoBehaviour, IInteractable
{
    public string Prompt => "Pick up page";

    void Awake()
    {
        EnsurePageVisual();
    }

    public void Interact(PlayerController player)
    {
        GameManager.Instance.CollectPage();
        Destroy(gameObject);
    }

    static Shader PreferredShader()
    {
        return Shader.Find("Universal Render Pipeline/Lit")
            ?? Shader.Find("URP/Lit")
            ?? Shader.Find("Standard");
    }

    void EnsurePageVisual()
    {
        if (transform.Find("PageVisual") != null) return;

        var root = new GameObject("PageVisual");
        root.transform.SetParent(transform, false);

        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "Sheet";
        quad.transform.SetParent(root.transform, false);
        quad.transform.localScale = new Vector3(1.1f, 1.45f, 1f);
        quad.transform.localRotation = Quaternion.identity;

        var tex = MakeMusicTexture();
        var mat = new Material(PreferredShader());
        mat.mainTexture = tex;
        mat.color = Color.white;

        var r = quad.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        var col = quad.GetComponent<Collider>();
        if (col != null) Destroy(col);

        var mr = GetComponent<MeshRenderer>();
        if (mr != null) mr.enabled = false;
        var mf = GetComponent<MeshFilter>();
        if (mf != null) mf.sharedMesh = null;
    }

    Texture2D MakeMusicTexture()
    {
        const int w = 256;
        const int h = 256;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var pixels = new Color[w * h];
        var paper = new Color(0.92f, 0.89f, 0.78f, 1f);
        var ink = new Color(0.12f, 0.10f, 0.09f, 1f);

        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = paper;

        for (int y = 30; y <= 220; y += 24)
        {
            for (int x = 26; x <= 230; x++)
                pixels[y * w + x] = ink;
        }

        for (int x = 44; x <= 220; x += 26)
        {
            int x0 = x;
            int y0 = 52;
            DrawNote(pixels, w, h, x0, y0, 12, ink);
            DrawNote(pixels, w, h, x0 + 18, y0 + 18, 10, ink);
        }

        for (int i = 0; i < 12; i++)
        {
            int cx = 148 + (i % 4) * 22;
            int cy = 50 + (i / 4) * 24;
            DrawNote(pixels, w, h, cx, cy, 9, ink);
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    void DrawNote(Color[] pixels, int w, int h, int cx, int cy, int radius, Color ink)
    {
        for (int y = -radius; y <= radius; y++)
        for (int x = -radius; x <= radius; x++)
        {
            if (x * x + y * y > radius * radius) continue;
            int xx = cx + x;
            int yy = cy + y;
            if (xx < 0 || xx >= w || yy < 0 || yy >= h) continue;
            pixels[yy * w + xx] = ink;
        }

        for (int y = 0; y < 30; y++)
        {
            int yy = cy - 8 + y;
            if (yy < 0 || yy >= h) continue;
            int xx = cx + 6;
            if (xx >= 0 && xx < w)
                pixels[yy * w + xx] = ink;
        }
    }
}
