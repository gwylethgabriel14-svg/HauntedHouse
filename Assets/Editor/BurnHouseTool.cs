#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Put this file in Assets/Editor/ (BurnState.cs goes in Assets/Scripts/).
//
// Makes the BIG House look like the aftermath of a fire: charred, sooty, ashy. Nothing is burning.
// Menu:  Haunted House/Burn House (open tool)
//        Haunted House/Burn House (quick apply)
//        Haunted House/Remove Burn
// Run it AFTER Build BIG House / Build 2ND FLOOR / Decorate BIG House.
// If you rebuild or redecorate the house later, run Burn again.
// Everything new goes under the root object "BurnDecor" (outside BigHouse, so the NavMesh is not affected).
public class BurnHouseTool : EditorWindow
{
    const string ROOT = "BurnDecor";
    const string GEN = "Assets/Materials/Generated";
    static readonly string[] TARGET_ROOTS = { "BigHouse", "BigLockers" };
    static readonly string[] SKIP_MATS = { "Mirror", "Water" };

    // ---- settings
    static float charAmount = 0.85f;      // 0 = original colors, 1 = fully charred (plain-color surfaces)
    static float texturedChar = 0.4f;     // same, but for surfaces that have a texture (wallpaper, wood) so the pattern stays visible
    static float soot = 0.6f;             // opacity of soot stains
    static float decalDensity = 1f;       // soot stains on walls
    static int floorScorch = 16;          // dark patches on floors
    static int ceilingScorch = 14;        // soot on ceilings
    static int beams = 45;                // fallen burnt beams / planks
    static int ashPiles = 14;             // ash + rubble piles
    static int dustEmitters = 8;          // floating ash
    static bool fog = true;
    static float fogDensity = 0.03f;
    static bool darkAmbient = false;
    static bool dimLights = true;
    static float lightMul = 0.8f;
    static float worldTile = 2.5f;        // meters per texture tile on big walls
    static int seed = 666;

    static System.Random rnd;
    static BurnState state;
    static Transform decalsT, debrisT, dustT;
    static Texture2D sootTex, blobTex, lickTex, dustTex;
    static Material blobMat, lickMat, dustMat, debrisMat, ashMat;
    static Dictionary<Material, Material> burntCache;

    // ================================================================== menu / window

    [MenuItem("Haunted House/Burn House (open tool)")]
    static void Open() { GetWindow<BurnHouseTool>("Burn House"); }

    [MenuItem("Haunted House/Burn House (quick apply)")]
    static void Quick() { Apply(); }

    [MenuItem("Haunted House/Remove Burn")]
    static void RemoveMenu() { Remove(false); }

    Vector2 scroll;

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("Surfaces", EditorStyles.boldLabel);
        charAmount = EditorGUILayout.Slider("Char (plain surfaces)", charAmount, 0f, 1f);
        texturedChar = EditorGUILayout.Slider("Char (wallpaper / textured)", texturedChar, 0f, 1f);
        soot = EditorGUILayout.Slider("Soot stain opacity", soot, 0.1f, 1f);
        worldTile = EditorGUILayout.Slider("Texture tile (m)", worldTile, 1f, 6f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Soot and scorch marks", EditorStyles.boldLabel);
        decalDensity = EditorGUILayout.Slider("Wall soot density", decalDensity, 0f, 3f);
        floorScorch = EditorGUILayout.IntSlider("Floor scorch patches", floorScorch, 0, 60);
        ceilingScorch = EditorGUILayout.IntSlider("Ceiling soot", ceilingScorch, 0, 60);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Debris", EditorStyles.boldLabel);
        beams = EditorGUILayout.IntSlider("Burnt beams / planks", beams, 0, 150);
        ashPiles = EditorGUILayout.IntSlider("Ash + rubble piles", ashPiles, 0, 60);
        dustEmitters = EditorGUILayout.IntSlider("Floating ash emitters", dustEmitters, 0, 30);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Atmosphere", EditorStyles.boldLabel);
        fog = EditorGUILayout.Toggle("Smoky fog", fog);
        if (fog) fogDensity = EditorGUILayout.Slider("Fog density", fogDensity, 0.005f, 0.1f);
        darkAmbient = EditorGUILayout.Toggle("Darken ambient light", darkAmbient);
        dimLights = EditorGUILayout.Toggle("Dim + dirty the lamps", dimLights);
        if (dimLights) lightMul = EditorGUILayout.Slider("Lamp intensity x", lightMul, 0.2f, 1f);

        EditorGUILayout.Space();
        seed = EditorGUILayout.IntField("Random seed", seed);

        EditorGUILayout.Space();
        if (GUILayout.Button("BURN THE HOUSE", GUILayout.Height(32))) Apply();
        if (GUILayout.Button("Remove burn (restore everything)", GUILayout.Height(24))) Remove(false);
        EditorGUILayout.EndScrollView();
    }

    // ================================================================== apply / remove

    static void Apply()
    {
        var big = FindRoot("BigHouse");
        if (big == null)
        {
            EditorUtility.DisplayDialog("Burn House",
                "BigHouse not found in the open scene. Open the House scene and run Build BIG House first.", "OK");
            return;
        }

        Remove(true);
        Physics.SyncTransforms();

        rnd = new System.Random(seed);
        burntCache = new Dictionary<Material, Material>();
        MakeAssets();

        var root = new GameObject(ROOT).transform;
        state = root.gameObject.AddComponent<BurnState>();
        decalsT = NewChild(root, "Decals");
        debrisT = NewChild(root, "Debris");
        dustT = NewChild(root, "Dust");

        // ---- 1. burn all surfaces, remember walls and lintels
        var walls = new List<Bounds>();
        var lintels = new List<Bounds>();
        Bounds hb = new Bounds(big.transform.position, Vector3.zero);
        bool first = true;

        foreach (var rn in TARGET_ROOTS)
        {
            var go = FindRoot(rn);
            if (go == null) continue;

            foreach (var mr in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (rn == "BigHouse")
                {
                    if (first) { hb = mr.bounds; first = false; } else hb.Encapsulate(mr.bounds);
                    if (mr.enabled) Classify(mr, walls, lintels);
                }
                BurnRenderer(mr);
            }
        }

        // ---- 2. soot on walls
        foreach (var b in walls) WallSoot(b);
        foreach (var b in lintels) LintelSoot(b);

        // ---- 3. floor / ceiling scorch
        for (int i = 0, tries = 0; i < floorScorch && tries < floorScorch * 30; tries++)
        {
            Vector3 p;
            if (!TryFloor(hb, out p, 0f)) continue;
            float w = Rand(1.6f, 3.8f);
            Decal(0, p + Vector3.up * 0.012f, Vector3.up, w, w * Rand(0.7f, 1f), Rand(0f, 360f));
            i++;
        }

        for (int i = 0, tries = 0; i < ceilingScorch && tries < ceilingScorch * 30; tries++)
        {
            Vector3 p;
            if (!TryFloor(hb, out p, 0f)) continue;
            RaycastHit hit;
            if (!Physics.Raycast(p + Vector3.up * 0.1f, Vector3.up, out hit, 5f, ~0, QueryTriggerInteraction.Ignore)) continue;
            if (hit.normal.y > -0.9f) continue;
            float w = Rand(1.8f, 3.8f);
            Decal(0, hit.point + hit.normal * 0.012f, hit.normal, w, w * Rand(0.7f, 1f), Rand(0f, 360f));
            i++;
        }

        // ---- 4. debris
        for (int i = 0, tries = 0; i < beams && tries < beams * 30; tries++)
        {
            Vector3 p;
            if (rnd.NextDouble() < 0.65)
            {
                float len = Rand(1f, 3f), s = Rand(0.1f, 0.22f);
                if (!TryFloor(hb, out p, Mathf.Min(len * 0.45f, 1f))) continue;
                float tilt = Rand(0f, 9f);
                float y = p.y + s * 0.5f + len * 0.5f * Mathf.Sin(tilt * Mathf.Deg2Rad);
                Prim(PrimitiveType.Cube, "BurntBeam", new Vector3(p.x, y, p.z),
                     new Vector3(len, s, s * Rand(0.8f, 1.2f)), Quaternion.Euler(0f, Rand(0f, 360f), tilt), debrisMat);
            }
            else
            {
                float len = Rand(0.8f, 1.8f), w = Rand(0.12f, 0.22f);
                if (!TryFloor(hb, out p, Mathf.Min(len * 0.45f, 1f))) continue;
                Prim(PrimitiveType.Cube, "BurntPlank", new Vector3(p.x, p.y + 0.03f, p.z),
                     new Vector3(len, 0.03f, w), Quaternion.Euler(0f, Rand(0f, 360f), Rand(-4f, 4f)), debrisMat);
            }
            i++;
        }

        for (int i = 0, tries = 0; i < ashPiles && tries < ashPiles * 30; tries++)
        {
            Vector3 p;
            float rr = Rand(0.5f, 0.9f);
            if (!TryFloor(hb, out p, rr)) continue;

            float sy = Rand(0.12f, 0.25f);
            Prim(PrimitiveType.Sphere, "AshMound", new Vector3(p.x, p.y + sy * 0.3f, p.z),
                 new Vector3(rr * 2.2f, sy, rr * 2f * Rand(0.8f, 1f)), Quaternion.Euler(0f, Rand(0f, 360f), 0f), ashMat);

            int k = rnd.Next(5, 10);
            for (int c = 0; c < k; c++)
            {
                float a = Rand(0f, Mathf.PI * 2f), d = Rand(0f, rr * 0.9f), s = Rand(0.05f, 0.2f);
                Prim(PrimitiveType.Cube, "Rubble",
                     new Vector3(p.x + Mathf.Cos(a) * d, p.y + s * 0.4f + (1f - d / rr) * 0.08f, p.z + Mathf.Sin(a) * d),
                     new Vector3(s, s * Rand(0.6f, 1.2f), s * Rand(0.8f, 1.4f)),
                     Quaternion.Euler(Rand(0f, 40f), Rand(0f, 360f), Rand(0f, 40f)), debrisMat);
            }
            i++;
        }

        // ---- 5. floating ash
        for (int i = 0, tries = 0; i < dustEmitters && tries < dustEmitters * 30; tries++)
        {
            Vector3 p;
            if (!TryFloor(hb, out p, 0f)) continue;
            MakeDust(p + Vector3.up * 1.3f);
            i++;
        }

        // ---- 6. atmosphere
        state.envSaved = true;
        state.fogWas = RenderSettings.fog;
        state.fogModeWas = RenderSettings.fogMode;
        state.fogColorWas = RenderSettings.fogColor;
        state.fogDensityWas = RenderSettings.fogDensity;
        state.ambientModeWas = RenderSettings.ambientMode;
        state.ambientLightWas = RenderSettings.ambientLight;

        if (fog)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.045f, 0.04f, 0.035f);
            RenderSettings.fogDensity = fogDensity;
        }
        if (darkAmbient)
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.03f, 0.027f, 0.025f);
        }

        if (dimLights) DimLamps();

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = root.gameObject;
        Debug.Log("House burnt: " + state.renderers.Count + " surfaces charred, " + decalsT.childCount + " soot marks, " +
                  debrisT.childCount + " debris parts, " + dustT.childCount + " ash emitters. Use Remove Burn to undo.");
    }

    static void Remove(bool silent)
    {
        var rootGo = FindRoot(ROOT);
        if (rootGo == null)
        {
            if (!silent) Debug.Log("Nothing to remove.");
            return;
        }

        var st = rootGo.GetComponent<BurnState>();
        if (st != null)
        {
            foreach (var rb in st.renderers)
            {
                if (rb.renderer == null) continue;
                rb.renderer.sharedMaterials = rb.materials;
                if (rb.filter != null)
                {
                    var cur = rb.filter.sharedMesh;
                    rb.filter.sharedMesh = rb.mesh;
                    if (cur != null && cur != rb.mesh && cur.name == "Burn_UV" && !AssetDatabase.Contains(cur))
                        Object.DestroyImmediate(cur);
                }
            }

            foreach (var lb in st.lights)
            {
                if (lb.light == null) continue;
                lb.light.color = lb.color;
                lb.light.intensity = lb.intensity;
                if (lb.hasBase)
                {
                    var f = lb.light.GetComponent<FlickerLight>();
                    if (f != null)
                    {
                        var so = new SerializedObject(f);
                        var bp = so.FindProperty("baseIntensity");
                        if (bp != null) { bp.floatValue = lb.baseIntensity; so.ApplyModifiedProperties(); }
                    }
                }
            }

            if (st.envSaved)
            {
                RenderSettings.fog = st.fogWas;
                RenderSettings.fogMode = st.fogModeWas;
                RenderSettings.fogColor = st.fogColorWas;
                RenderSettings.fogDensity = st.fogDensityWas;
                RenderSettings.ambientMode = st.ambientModeWas;
                RenderSettings.ambientLight = st.ambientLightWas;
            }
        }

        Object.DestroyImmediate(rootGo);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        if (!silent) Debug.Log("Burn removed. The house is back to normal.");
    }

    // ================================================================== surfaces

    static void Classify(MeshRenderer mr, List<Bounds> walls, List<Bounds> lintels)
    {
        Bounds b = mr.bounds;
        bool xThin = b.size.x < b.size.z;
        float thick = xThin ? b.size.x : b.size.z;
        float len = xThin ? b.size.z : b.size.x;
        if (thick > 0.35f || len < 1f) return;

        if (b.size.y >= 2f) walls.Add(b);
        else if (b.size.y > 0.3f && mr.name.ToLower().Contains("lintel")) lintels.Add(b);
    }

    static void BurnRenderer(MeshRenderer mr)
    {
        var bk = new BurnState.RendererBackup { renderer = mr, materials = mr.sharedMaterials };

        var mats = mr.sharedMaterials;
        var nm = new Material[mats.Length];
        for (int i = 0; i < mats.Length; i++) nm[i] = Burnt(mats[i]);
        mr.sharedMaterials = nm;

        // world-space UVs for big cubes so the soot texture is not stretched
        var mf = mr.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            bk.filter = mf;
            bk.mesh = mf.sharedMesh;

            Vector3 s = mr.transform.lossyScale;
            if (mf.sharedMesh.name == "Cube" && mf.sharedMesh.isReadable &&
                Mathf.Max(s.x, Mathf.Max(s.y, s.z)) >= 1.5f)
            {
                Mesh mesh = Object.Instantiate(mf.sharedMesh);
                mesh.name = "Burn_UV";
                var v = mesh.vertices;
                var n = mesh.normals;
                var uv = new Vector2[v.Length];

                for (int i = 0; i < v.Length; i++)
                {
                    Vector3 wp = mr.transform.TransformPoint(v[i]);
                    Vector3 wn = mr.transform.TransformDirection(n[i]);
                    float ax = Mathf.Abs(wn.x), ay = Mathf.Abs(wn.y), az = Mathf.Abs(wn.z);
                    float u, w;
                    if (ax >= ay && ax >= az) { u = wp.z; w = wp.y; }
                    else if (ay >= az) { u = wp.x; w = wp.z; }
                    else { u = wp.x; w = wp.y; }
                    uv[i] = new Vector2(u / worldTile, w / worldTile);
                }

                mesh.uv = uv;
                mf.sharedMesh = mesh;
            }
        }

        state.renderers.Add(bk);
    }

    static Material Burnt(Material src)
    {
        if (src == null) return null;

        Material cached;
        if (burntCache.TryGetValue(src, out cached)) return cached;

        foreach (var s in SKIP_MATS)
            if (src.name.Contains(s)) { burntCache[src] = src; return src; }

        var m = new Material(src);
        m.name = "Burn_" + src.name;

        Color c = Color.white;
        if (src.HasProperty("_BaseColor")) c = src.GetColor("_BaseColor");
        else if (src.HasProperty("_Color")) c = src.GetColor("_Color");

        bool hasTex = (src.HasProperty("_BaseMap") && src.GetTexture("_BaseMap") != null) || src.mainTexture != null;

        // textured surfaces (wallpaper, wood): lighter tint so the pattern is still visible
        Color nc = hasTex
            ? Color.Lerp(c, new Color(0.5f, 0.45f, 0.4f), texturedChar)
            : Color.Lerp(c, new Color(0.16f, 0.14f, 0.12f), charAmount);
        nc.a = c.a;

        if (!hasTex) SetTex(m, sootTex);

        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", nc);
        if (m.HasProperty("_Color")) m.SetColor("_Color", nc);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.05f);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.05f);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", m.GetFloat("_Metallic") * 0.3f);
        if (m.HasProperty("_EmissionColor")) { m.SetColor("_EmissionColor", Color.black); m.DisableKeyword("_EMISSION"); }

        string path = GEN + "/Burn_" + Safe(src.name) + ".mat";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(m, path);

        burntCache[src] = m;
        return m;
    }

    // ================================================================== soot decals

    static void WallSoot(Bounds b)
    {
        bool xThin = b.size.x < b.size.z;
        float thick = xThin ? b.size.x : b.size.z;
        float len = xThin ? b.size.z : b.size.x;
        float amin = xThin ? b.min.z : b.min.x;
        float amax = xThin ? b.max.z : b.max.x;
        if (len < 1.4f) return;

        int n = Mathf.RoundToInt(len / 3.5f * decalDensity + (float)rnd.NextDouble());
        for (int i = 0; i < n; i++)
        {
            float r = (float)rnd.NextDouble();
            int kind;
            float w, h, y;

            if (r < 0.45f)        // flame lick climbing from the floor
            {
                kind = 1; w = Rand(0.8f, 1.6f); h = Rand(1.4f, 2.6f);
                h = Mathf.Min(h, b.size.y - 0.1f);
                y = b.min.y + h * 0.5f;
            }
            else if (r < 0.8f)    // soot band near the ceiling
            {
                kind = 0; w = Rand(1.6f, 3.2f); h = Rand(0.9f, 1.6f);
                h = Mathf.Min(h, b.size.y - 0.1f);
                y = b.max.y - h * 0.5f - Rand(0f, 0.15f);
            }
            else                  // stain in the middle
            {
                kind = 0; w = Rand(1f, 2f); h = Rand(1f, 2f);
                h = Mathf.Min(h, b.size.y - 0.1f);
                y = Rand(b.min.y + h * 0.5f, b.max.y - h * 0.5f);
            }

            w = Mathf.Min(w, len - 0.2f);
            float a = Rand(amin + w * 0.5f + 0.05f, amax - w * 0.5f - 0.05f);
            int side = rnd.Next(2) == 0 ? -1 : 1;
            float face = (xThin ? b.center.x : b.center.z) + side * (thick * 0.5f + 0.014f);

            Vector3 pos = xThin ? new Vector3(face, y, a) : new Vector3(a, y, face);
            Vector3 nrm = xThin ? new Vector3(side, 0f, 0f) : new Vector3(0f, 0f, side);
            Decal(kind, pos, nrm, w, h, 0f);
        }
    }

    // dark smudge on the wall piece above each doorway, both sides
    static void LintelSoot(Bounds b)
    {
        bool xThin = b.size.x < b.size.z;
        float thick = xThin ? b.size.x : b.size.z;
        float len = xThin ? b.size.z : b.size.x;

        for (int side = -1; side <= 1; side += 2)
        {
            float face = (xThin ? b.center.x : b.center.z) + side * (thick * 0.5f + 0.014f);
            Vector3 pos = xThin ? new Vector3(face, b.center.y, b.center.z) : new Vector3(b.center.x, b.center.y, face);
            Vector3 nrm = xThin ? new Vector3(side, 0f, 0f) : new Vector3(0f, 0f, side);
            Decal(0, pos, nrm, len, b.size.y, 0f);
        }
    }

    // kind 0 = round soot blotch, kind 1 = tall flame lick
    static void Decal(int kind, Vector3 pos, Vector3 normal, float w, float h, float roll)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Quad);
        g.name = kind == 0 ? "Soot" : "SootLick";
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.transform.SetParent(decalsT, false);
        g.transform.position = pos;

        Vector3 up = Mathf.Abs(normal.y) > 0.9f ? Vector3.forward : Vector3.up;
        var rot = Quaternion.LookRotation(-normal, up);      // a Quad is visible from its -Z side
        if (Mathf.Abs(roll) > 0.01f) rot = Quaternion.AngleAxis(roll, normal) * rot;
        g.transform.rotation = rot;
        g.transform.localScale = new Vector3(w, h, 1f);

        var r = g.GetComponent<Renderer>();
        r.sharedMaterial = kind == 0 ? blobMat : lickMat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
    }

    // ================================================================== debris, dust, lights

    static GameObject Prim(PrimitiveType t, string name, Vector3 pos, Vector3 scale, Quaternion rot, Material m)
    {
        var g = GameObject.CreatePrimitive(t);
        g.name = name;
        Object.DestroyImmediate(g.GetComponent<Collider>());   // no collider: never blocks the player or the enemy
        g.transform.SetParent(debrisT, false);
        g.transform.position = pos;
        g.transform.rotation = rot;
        g.transform.localScale = scale;
        g.GetComponent<Renderer>().sharedMaterial = m;
        return g;
    }

    static void MakeDust(Vector3 pos)
    {
        var g = new GameObject("FloatingAsh");
        g.transform.SetParent(dustT, false);
        g.transform.position = pos;

        var ps = g.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.prewarm = true;
        main.startLifetime = 9f;
        main.startSpeed = 0.04f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.09f);
        main.startColor = new Color(0.75f, 0.72f, 0.68f, 0.35f);
        main.maxParticles = 120;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.01f;

        var em = ps.emission;
        em.rateOverTime = 12f;

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Box;
        sh.scale = new Vector3(5f, 2f, 5f);

        var nz = ps.noise;
        nz.enabled = true;
        nz.strength = 0.2f;
        nz.frequency = 0.25f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var gr = new Gradient();
        gr.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f),
                    new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
        col.color = gr;

        var pr = g.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = dustMat;
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    static void DimLamps()
    {
        foreach (var f in Object.FindObjectsByType<FlickerLight>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var l = f.GetComponent<Light>();
            if (l == null) continue;

            var lb = new BurnState.LightBackup { light = l, color = l.color, intensity = l.intensity };

            var so = new SerializedObject(f);
            var bp = so.FindProperty("baseIntensity");
            if (bp != null)
            {
                lb.hasBase = true;
                lb.baseIntensity = bp.floatValue;
                bp.floatValue *= lightMul;
                so.ApplyModifiedProperties();
            }

            l.color = Color.Lerp(l.color, new Color(1f, 0.5f, 0.22f), 0.35f);   // dirty ember tint
            l.intensity *= lightMul;
            state.lights.Add(lb);
        }
    }

    // ================================================================== floor sampling

    // Random point on a real floor (ground slab, upper slab, attic floor, basement) inside the house.
    // clearR > 0 also requires free space (no walls / furniture) around the point.
    static bool TryFloor(Bounds hb, out Vector3 p, float clearR)
    {
        p = Vector3.zero;
        float x = Rand(hb.min.x + 0.6f, hb.max.x - 0.6f);
        float z = Rand(hb.min.z + 0.6f, hb.max.z - 0.6f);

        var hits = Physics.RaycastAll(new Vector3(x, hb.max.y + 2f, z), Vector3.down, hb.size.y + 6f,
                                      ~0, QueryTriggerInteraction.Ignore);
        if (hits.Length == 0) return false;

        var valid = new List<RaycastHit>();
        foreach (var h in hits)
        {
            if (h.normal.y < 0.9f) continue;
            string n = h.collider.name;
            if (!(n.Contains("Floor") || n.Contains("Ceiling") || n.Contains("Slab"))) continue;

            // must be indoors: something above within 5 m, at least 2 m up
            RaycastHit up;
            if (!Physics.Raycast(h.point + Vector3.up * 0.05f, Vector3.up, out up, 5f, ~0, QueryTriggerInteraction.Ignore)) continue;
            if (up.distance < 2f) continue;

            valid.Add(h);
        }
        if (valid.Count == 0) return false;

        var pick = valid[rnd.Next(valid.Count)];

        if (clearR > 0.01f)
        {
            var cols = Physics.OverlapSphere(pick.point + Vector3.up * (clearR + 0.3f), clearR, ~0, QueryTriggerInteraction.Ignore);
            if (cols.Length > 0) return false;
        }

        p = pick.point;
        return true;
    }

    // ================================================================== generated assets

    static void MakeAssets()
    {
        EnsureFolder(GEN);

        sootTex = LoadOrMake("Burn_SootTex", MakeSoot, true, false);
        blobTex = LoadOrMake("Burn_BlobTex", MakeBlob, false, true);
        lickTex = LoadOrMake("Burn_LickTex", MakeLick, false, true);
        dustTex = LoadOrMake("Burn_DustTex", MakeDustTex, false, true);

        blobMat = MakeTransparent("Burn_SootDecal", blobTex, new Color(0f, 0f, 0f, soot), false);
        lickMat = MakeTransparent("Burn_SootLickDecal", lickTex, new Color(0f, 0f, 0f, soot), false);
        dustMat = MakeTransparent("Burn_AshDust", dustTex, Color.white, true);

        debrisMat = MakeLit("Burn_Debris", new Color(0.09f, 0.08f, 0.07f), sootTex);
        ashMat = MakeLit("Burn_Ash", new Color(0.2f, 0.19f, 0.18f), sootTex);
    }

    static Material MakeLit(string name, Color c, Texture2D tex)
    {
        var sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        var m = new Material(sh);
        m.color = c;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.03f);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.03f);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        SetTex(m, tex);

        string path = GEN + "/" + name + ".mat";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static Material MakeTransparent(string name, Texture2D tex, Color tint, bool particle)
    {
        Shader sh = Shader.Find(particle ? "Universal Render Pipeline/Particles/Unlit" : "Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Sprites/Default");

        var m = new Material(sh);
        SetTex(m, tex);
        m.color = tint;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);

        m.SetOverrideTag("RenderType", "Transparent");
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
        if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
        if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (m.HasProperty("_SrcBlendAlpha")) m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
        if (m.HasProperty("_DstBlendAlpha")) m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        m.SetShaderPassEnabled("ShadowCaster", false);

        string path = GEN + "/" + name + ".mat";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static void SetTex(Material m, Texture2D t)
    {
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", t);
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", t);
    }

    delegate Texture2D MakeFn();

    static Texture2D LoadOrMake(string name, MakeFn fn, bool repeat, bool alpha)
    {
        string path = GEN + "/" + name + ".png";
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null) return existing;

        var t = fn();
        File.WriteAllBytes(path, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path);

        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        imp.alphaIsTransparency = alpha;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // tileable perlin: blends four offset samples so the left/right and top/bottom edges match
    static float Tile(float u, float v, float s, float o)
    {
        float a = Mathf.PerlinNoise(u * s + o, v * s + o);
        float b = Mathf.PerlinNoise((u - 1f) * s + o, v * s + o);
        float c = Mathf.PerlinNoise(u * s + o, (v - 1f) * s + o);
        float d = Mathf.PerlinNoise((u - 1f) * s + o, (v - 1f) * s + o);
        return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
    }

    // charred surface: dark with lighter ash patches and thin cracks (greyscale, tinted by the material color)
    static Texture2D MakeSoot()
    {
        int res = 256;
        var t = new Texture2D(res, res, TextureFormat.RGBA32, false);
        var px = new Color[res * res];
        for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float u = x / (float)res, v = y / (float)res;
                float n = 0.5f * Tile(u, v, 4f, 1.3f) + 0.3f * Tile(u, v, 11f, 7.1f) + 0.2f * Tile(u, v, 29f, 3.7f);
                float ridge = Mathf.Abs(Tile(u, v, 6f, 19.2f) - 0.5f);
                float crack = ridge < 0.015f ? 0.45f : 1f;
                float g = (0.3f + 0.7f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.7f, n))) * crack;
                px[y * res + x] = new Color(g, g, g, 1f);
            }
        t.SetPixels(px);
        t.Apply();
        return t;
    }

    static Texture2D MakeBlob()
    {
        int res = 128;
        var t = new Texture2D(res, res, TextureFormat.RGBA32, false);
        var px = new Color[res * res];
        for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float nx = (x / (float)res - 0.5f) * 2f, ny = (y / (float)res - 0.5f) * 2f;
                float r = Mathf.Sqrt(nx * nx + ny * ny);
                float noise = Mathf.PerlinNoise(x * 0.07f + 5f, y * 0.07f + 5f) * 0.6f +
                              Mathf.PerlinNoise(x * 0.2f + 1f, y * 0.2f + 1f) * 0.4f;
                float a = Mathf.Clamp01((1f - r) * 1.6f + (noise - 0.5f) * 1.1f);
                a = a * a * (3f - 2f * a) * 0.9f * Mathf.Clamp01((1f - r) * 4f);
                px[y * res + x] = new Color(0.02f, 0.018f, 0.016f, a);
            }
        t.SetPixels(px);
        t.Apply();
        return t;
    }

    static Texture2D MakeLick()
    {
        int res = 128;
        var t = new Texture2D(res, res, TextureFormat.RGBA32, false);
        var px = new Color[res * res];
        for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float u = x / (float)res, v = y / (float)res;
                float xx = (u - 0.5f) * 2f;
                float w = Mathf.Lerp(0.95f, 0.18f, Mathf.Pow(v, 0.7f));
                float warp = (Mathf.PerlinNoise(v * 4f + 3f, u * 2f) - 0.5f) * 0.5f;
                float noise = Mathf.PerlinNoise(x * 0.09f + 2f, y * 0.09f + 8f);
                float edge = 1f - Mathf.Abs(xx + warp) / w;
                float a = Mathf.Clamp01(edge * 1.8f + (noise - 0.5f) * 0.5f);
                a *= (1f - Mathf.SmoothStep(0.55f, 1f, v)) * 0.9f;
                a *= Mathf.Clamp01((1f - Mathf.Abs(xx)) * 4f);
                px[y * res + x] = new Color(0.02f, 0.018f, 0.016f, a);
            }
        t.SetPixels(px);
        t.Apply();
        return t;
    }

    static Texture2D MakeDustTex()
    {
        int res = 64;
        var t = new Texture2D(res, res, TextureFormat.RGBA32, false);
        var px = new Color[res * res];
        for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float nx = (x / (float)res - 0.5f) * 2f, ny = (y / (float)res - 0.5f) * 2f;
                float a = Mathf.Exp(-(nx * nx + ny * ny) * 4f);
                px[y * res + x] = new Color(1f, 1f, 1f, a);
            }
        t.SetPixels(px);
        t.Apply();
        return t;
    }

    // ================================================================== helpers

    static float Rand(float a, float b) { return a + (float)rnd.NextDouble() * (b - a); }

    static Transform NewChild(Transform parent, string name)
    {
        var g = new GameObject(name).transform;
        g.SetParent(parent, false);
        return g;
    }

    static string Safe(string s)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string leaf = Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }

    static GameObject FindRoot(string name)
    {
        foreach (var go in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            if (go.name == name) return go;
        return null;
    }
}
#endif