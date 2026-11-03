using UnityEngine;

public class NoiseOnImpact : MonoBehaviour
{
    public AudioClip shatterSound;
    public float glassMarkSize = 0.14f;
    public float glassMarkLifetime = 6f;
    public Color glassColor = ItemPickup.BottleGlassColor;
    bool used;

    static Shader PreferredShader()
    {
        return Shader.Find("Universal Render Pipeline/Lit")
            ?? Shader.Find("URP/Lit")
            ?? Shader.Find("Standard");
    }

    void OnCollisionEnter(Collision c)
    {
        if (used) return;
        used = true;

        if (shatterSound != null)
            AudioSource.PlayClipAtPoint(shatterSound, transform.position);

        SpawnGlassShards(c);

        var gm = GameManager.Instance;
        if (gm != null && gm.enemy != null)
            gm.enemy.HearNoise(transform.position);

        Destroy(gameObject);
    }

    void SpawnGlassShards(Collision c)
    {
        Vector3 point = c.contacts.Length > 0 ? c.contacts[0].point : transform.position;
        Vector3 normal = c.contacts.Length > 0 ? c.contacts[0].normal : Vector3.up;

        int shardCount = 10;
        for (int i = 0; i < shardCount; i++)
        {
            var shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shard.name = "GlassShard";
            shard.transform.position = point + new Vector3(Random.Range(-0.08f, 0.08f), 0.03f, Random.Range(-0.08f, 0.08f));
            shard.transform.localScale = new Vector3(Random.Range(0.012f, 0.024f), Random.Range(0.028f, 0.06f), Random.Range(0.01f, 0.018f));
            shard.transform.rotation = Quaternion.Euler(
                Random.Range(0f, 180f),
                Random.Range(0f, 180f),
                Random.Range(0f, 180f));

            var renderer = shard.GetComponent<Renderer>();
            if (renderer != null)
            {
                var mat = new Material(PreferredShader());
                mat.color = new Color(glassColor.r, glassColor.g, glassColor.b, 0.9f);
                mat.SetFloat("_Mode", 3f);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                renderer.sharedMaterial = mat;
            }

            var rb = shard.AddComponent<Rigidbody>();
            rb.mass = 0.02f;
            rb.linearDamping = 0.25f;
            var forceDir = (Quaternion.Euler(Random.Range(-18f, 18f), Random.Range(0f, 360f), 0f) * normal + Vector3.up * 0.3f).normalized;
            rb.AddForce(forceDir * Random.Range(0.8f, 1.8f), ForceMode.VelocityChange);
            rb.AddTorque(Random.onUnitSphere * Random.Range(0.8f, 2.2f), ForceMode.VelocityChange);

            var col = shard.GetComponent<Collider>();
            if (col != null) Destroy(col);

            Destroy(shard, glassMarkLifetime);
        }

        var mark = GameObject.CreatePrimitive(PrimitiveType.Quad);
        mark.name = "BrokenGlassMark";
        mark.transform.position = point + normal * 0.01f + Vector3.up * 0.015f;
        mark.transform.rotation = Quaternion.LookRotation(normal, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);
        mark.transform.localScale = new Vector3(glassMarkSize, glassMarkSize, 1f);

        var mat2 = new Material(PreferredShader());
        mat2.color = new Color(glassColor.r, glassColor.g, glassColor.b, 0.28f);
        var r2 = mark.GetComponent<Renderer>();
        r2.sharedMaterial = mat2;
        var collider = mark.GetComponent<Collider>();
        if (collider != null) Destroy(collider);

        Destroy(mark, Mathf.Max(0.5f, glassMarkLifetime * 0.4f));
    }
}
