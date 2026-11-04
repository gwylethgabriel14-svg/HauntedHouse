// Assets/Editor/BrokenChandelierBuilder.cs
// Unity 2022.3+ / Unity 6: Select one or more ceiling Light objects and choose
// Haunted House > Models > Build Broken Chandeliers On Selected Lights.
// Creates editable 3D geometry in the scene, without using cubes as the model.
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class BrokenChandelierBuilder
{
    private const string RootFolder = "Assets/HauntedHouseGenerated";
    private const string MaterialFolder = RootFolder + "/ChandelierMaterials";
    private const string MeshFolder = RootFolder + "/ChandelierMeshes";
    private const string VisualName = "BrokenChandelier_Visual";

    [MenuItem("Haunted House/Models/Build Broken Chandeliers On Selected Lights")]
    private static void BuildSelected()
    {
        List<Light> lights = new List<Light>();
        foreach (GameObject selected in Selection.gameObjects)
        {
            Light own = selected.GetComponent<Light>();
            if (own != null && !lights.Contains(own)) lights.Add(own);
        }
        if (lights.Count == 0)
        {
            EditorUtility.DisplayDialog("Broken Chandelier", "Select one or more GameObjects that have a Light component first.", "OK");
            return;
        }

        EnsureAssetFolders();
        Materials m = LoadMaterials();
        Mesh shardMesh = LoadShardMesh();

        foreach (Light light in lights) BuildOne(light, m, shardMesh);
        Debug.Log($"[BrokenChandelierBuilder] Built {lights.Count} chandelier(s). Save your scene.");
    }

    [MenuItem("Haunted House/Models/Build Broken Chandeliers On Selected Lights", true)]
    private static bool ValidateBuild() => Selection.gameObjects.Length > 0;

    private class Materials
    {
        public Material darkMetal, rustyMetal, dustyGlass, paleGlass, bulbGlow, blackened;
    }

    private static void EnsureAssetFolders()
    {
        if (!AssetDatabase.IsValidFolder(RootFolder)) AssetDatabase.CreateFolder("Assets", "HauntedHouseGenerated");
        if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder(RootFolder, "ChandelierMaterials");
        if (!AssetDatabase.IsValidFolder(MeshFolder)) AssetDatabase.CreateFolder(RootFolder, "ChandelierMeshes");
    }

    private static Material MaterialAsset(string file, Color color, float metallic, float smoothness, bool emissive = false)
    {
        string path = MaterialFolder + "/" + file + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("HDRP/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Diffuse");
        material = new Material(shader) { name = file, color = color };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        if (emissive)
        {
            material.EnableKeyword("_EMISSION");
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", new Color(1.8f, 1.05f, 0.45f));
        }
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static Materials LoadMaterials() => new Materials
    {
        darkMetal = MaterialAsset("TarnishedIron", new Color(.12f, .105f, .095f), .65f, .2f),
        rustyMetal = MaterialAsset("OldBronze", new Color(.28f, .18f, .09f), .65f, .24f),
        dustyGlass = MaterialAsset("DustyGlass", new Color(.37f, .43f, .39f), .05f, .72f),
        paleGlass = MaterialAsset("CrackedGlass", new Color(.64f, .68f, .62f), .05f, .85f),
        bulbGlow = MaterialAsset("DyingBulb", new Color(.95f, .72f, .39f), .1f, .58f, true),
        blackened = MaterialAsset("BurnedSocket", new Color(.065f, .06f, .05f), .25f, .12f)
    };

    private static Mesh LoadShardMesh()
    {
        const string path = MeshFolder + "/JaggedGlassShard.asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh != null) return mesh;
        mesh = new Mesh { name = "JaggedGlassShard" };
        // Double-sided, uneven 3D sliver with real thickness, not a rectangular cube.
        Vector3[] vertices = {
            new Vector3(-.09f, 0f, -.013f), new Vector3(.08f, .01f, -.013f),
            new Vector3(.035f, -.28f, -.013f), new Vector3(-.025f, -.19f, -.013f),
            new Vector3(-.09f, 0f, .013f), new Vector3(.08f, .01f, .013f),
            new Vector3(.035f, -.28f, .013f), new Vector3(-.025f, -.19f, .013f)
        };
        int[] indices = {
            0,2,1, 0,3,2, 4,5,6, 4,6,7,
            0,1,5, 0,5,4, 1,2,6, 1,6,5,
            2,3,7, 2,7,6, 3,0,4, 3,4,7
        };
        mesh.vertices = vertices;
        mesh.triangles = indices;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static GameObject Create(string name, Transform parent)
    {
        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Build broken chandelier");
        go.transform.SetParent(parent, false);
        return go;
    }

    private static GameObject Primitive(PrimitiveType type, string name, Transform parent,
        Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        Undo.RegisterCreatedObjectUndo(go, "Build broken chandelier");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = localScale;
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
        // Pure decoration: don't make invisible physics obstacles out of every detail.
        Collider collider = go.GetComponent<Collider>();
        if (collider != null) Object.DestroyImmediate(collider);
        return go;
    }

    private static void Rod(string name, Transform parent, Vector3 a, Vector3 b, float radius, Material material)
    {
        Vector3 delta = b - a;
        if (delta.sqrMagnitude < .00001f) return;
        GameObject segment = Primitive(PrimitiveType.Cylinder, name, parent,
            (a + b) / 2f, new Vector3(radius * 2, delta.magnitude / 2f, radius * 2), material);
        segment.transform.localRotation = Quaternion.FromToRotation(Vector3.up, delta.normalized);
    }

    private static void Bead(string name, Transform parent, Vector3 pos, float radius, Material m)
    {
        Primitive(PrimitiveType.Sphere, name, parent, pos, Vector3.one * radius * 2f, m);
    }

    private static void Ring(string name, Transform parent, Vector3 center, float radius, float thickness,
        Material material, int segments = 12)
    {
        for (int i = 0; i < segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            float b = (i + 1) * Mathf.PI * 2f / segments;
            Vector3 p1 = center + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius;
            Vector3 p2 = center + new Vector3(Mathf.Cos(b), 0, Mathf.Sin(b)) * radius;
            Rod(name + "_" + i, parent, p1, p2, thickness, material);
        }
    }

    private static void Shard(string name, Transform parent, Vector3 position,
        Vector3 scale, Vector3 rotation, Material material, Mesh shardMesh)
    {
        GameObject go = Create(name, parent);
        go.transform.localPosition = position;
        go.transform.localEulerAngles = rotation;
        go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = shardMesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = material;
    }

    private static void BuildOne(Light light, Materials m, Mesh shardMesh)
    {
        Transform existing = light.transform.Find(VisualName);
        if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);

        // The existing Light is the chandelier's *central glow*, with its body built
        // around it. Place the Light about 1.3 m below the ceiling before building.
        GameObject root = Create(VisualName, light.transform);
        Transform t = root.transform;
        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.identity;
        t.localScale = Vector3.one;

        // Ceiling mount and rusted, irregular chain (up to 1.35 m above light).
        Primitive(PrimitiveType.Cylinder, "Ceiling_Rose", t, new Vector3(0, 1.36f, 0),
            new Vector3(.20f, .045f, .20f), m.darkMetal);
        for (int i = 0; i < 9; i++)
        {
            float y = .36f + i * .107f;
            float offset = (i % 2 == 0 ? .018f : -.018f);
            Rod("Old_Chain_Link_" + i + "A", t,
                new Vector3(offset - .042f, y, 0), new Vector3(offset + .042f, y + .065f, 0), .013f, m.rustyMetal);
            Rod("Old_Chain_Link_" + i + "B", t,
                new Vector3(offset + .042f, y, 0), new Vector3(offset - .042f, y + .065f, 0), .013f, m.darkMetal);
        }

        Primitive(PrimitiveType.Sphere, "Central_Tarnished_Hub", t, new Vector3(0, .12f, 0),
            new Vector3(.36f, .29f, .36f), m.rustyMetal);
        Primitive(PrimitiveType.Cylinder, "Center_Stem", t, new Vector3(0, -.1f, 0),
            new Vector3(.085f, .23f, .085f), m.darkMetal);
        Bead("Bottom_Finial", t, new Vector3(0, -.4f, 0), .09f, m.rustyMetal);
        Ring("Bent_Inner_Ring", t, new Vector3(0, -.18f, 0), .37f, .016f, m.rustyMetal);
        Ring("Damaged_Outer_Ring", t, new Vector3(0, -.11f, 0), .85f, .019f, m.darkMetal, 18);

        // Six candle arms: one snapped off completely, one hanging downward.
        const int arms = 6;
        for (int i = 0; i < arms; i++)
        {
            float angle = (i * 360f / arms + 12f) * Mathf.Deg2Rad;
            Vector3 radial = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            Vector3 origin = radial * .18f + Vector3.down * .13f;
            bool snapped = i == 2;
            bool bent = i == 4;
            float extent = snapped ? .43f : .95f;
            Vector3 mid = radial * (extent * .64f) + Vector3.down * .35f;
            Vector3 tip = radial * extent + Vector3.up * (bent ? -.57f : -.13f);

            // Multiple tapered-looking cylindrical joints make a curved metal arm.
            Vector3 prev = origin;
            for (int j = 1; j <= 7; j++)
            {
                float u = j / 7f;
                Vector3 next = Vector3.Lerp(Vector3.Lerp(origin, mid, u), Vector3.Lerp(mid, tip, u), u);
                next += Vector3.up * (.14f * Mathf.Sin(u * Mathf.PI));
                Rod("Arm_" + i + "_Curved_" + j, t, prev, next, .026f - u * .007f,
                    j % 3 == 0 ? m.rustyMetal : m.darkMetal);
                prev = next;
            }
            Bead("Arm_" + i + "_Joint", t, origin, .055f, m.rustyMetal);
            if (snapped)
            {
                Shard("Snapped_Metal_Edge", t, tip, new Vector3(.5f, .36f, .5f),
                    new Vector3(0, i * 60f, -55), m.blackened, shardMesh);
                continue;
            }

            Primitive(PrimitiveType.Cylinder, "Cup_" + i, t, tip,
                new Vector3(.105f, .045f, .105f), m.rustyMetal);
            if (bent)
            {
                Primitive(PrimitiveType.Cylinder, "Burned_Socket_" + i, t, tip + Vector3.down * .10f,
                    new Vector3(.048f, .10f, .048f), m.blackened);
            }
            else
            {
                Primitive(PrimitiveType.Cylinder, "Candle_Socket_" + i, t, tip + Vector3.up * .12f,
                    new Vector3(.048f, .14f, .048f), m.darkMetal);
                if (i == 0 || i == 5)
                {
                    // Two surviving glass bulbs, with fading material glow.
                    Primitive(PrimitiveType.Sphere, "Surviving_Bulb_" + i, t,
                        tip + Vector3.up * .30f, new Vector3(.12f, .20f, .12f), m.bulbGlow);
                }
                else
                {
                    // Other sockets contain chipped glass, not perfect spheres.
                    for (int s = 0; s < 3; s++)
                        Shard("Shattered_Bulb_Glass_" + i + "_" + s, t,
                            tip + Vector3.up * .27f + radial * (.015f * s),
                            new Vector3(.45f, .38f + s * .12f, .50f),
                            new Vector3(-125f, s * 120f, 30f), m.paleGlass, shardMesh);
                }
            }

            // Thin hanging ornament with a cracked, pointed glass pendant.
            Vector3 crystalStart = radial * .66f + Vector3.down * .12f;
            Rod("Crystal_Hanger_" + i, t, crystalStart,
                crystalStart + Vector3.down * .16f, .008f, m.darkMetal);
            Shard("Hanging_Crystal_" + i, t, crystalStart + Vector3.down * .18f,
                new Vector3(1.3f, 1.25f, 1.3f), new Vector3(0, i * 33f, i % 2 == 0 ? 8f : -12f),
                m.dustyGlass, shardMesh);
        }

        // Uneven damaged glass pieces on the outer ring.
        for (int i = 0; i < 11; i++)
        {
            if (i == 3 || i == 7) continue; // missing crystals
            float angle = i * Mathf.PI * 2f / 11f;
            Vector3 pos = new Vector3(Mathf.Cos(angle) * .85f, -.15f, Mathf.Sin(angle) * .85f);
            Shard("Rim_Shattered_Crystal_" + i, t, pos,
                new Vector3(.65f + (i % 3) * .22f, .65f + (i % 4) * .20f, 1f),
                new Vector3(0, i * 32f, (i % 2 == 0 ? 14f : -20f)), m.paleGlass, shardMesh);
        }

        if (light.type != LightType.Point)
        {
            Debug.LogWarning($"[BrokenChandelierBuilder] {light.name} is a {light.type} Light. Point Light looks most natural for chandeliers; you can change it in Inspector.");
        }
        EditorUtility.SetDirty(light.gameObject);
    }
}
#endif