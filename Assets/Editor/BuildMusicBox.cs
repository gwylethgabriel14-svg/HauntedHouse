#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Builds an old music box out of simple shapes (wooden case, open lid with a mirror,
// a little spinning record, tonearm, winding key, brass lock and studs),
// saves it as Assets/Prefabs/MusicBox.prefab, puts spawn spots in the house,
// and sets up the MusicBoxManager in the scene.
//
//   Haunted House > Create MUSIC BOX
//
// Safe to run again: it rebuilds the prefab and the spots.
public static class BuildMusicBox
{
    const string PrefabPath = "Assets/Prefabs/MusicBox.prefab";
    const float BoxScale = 1.5f;     // real music boxes are tiny; this makes it easy to spot

    static Material mWood, mTrim, mBrass, mVelvet, mMetal, mDisc, mLabel, mMirror;

    [MenuItem("Haunted House/Create MUSIC BOX")]
    public static void Build()
    {
        MakeMaterials();
        GameObject prefab = MakePrefab();
        List<Transform> spots = MakeSpots();
        SetupManager(prefab, spots);

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("MUSIC BOX created: prefab at " + PrefabPath + ", " + spots.Count +
                  " spawn spots, MusicBoxManager in the scene. Press Ctrl+S.");
    }

    // ------------------------------------------------------------------ the model

    static GameObject MakePrefab()
    {
        var root = new GameObject("MusicBox");

        // ---- feet + plinth + case
        foreach (var f in new[] { new Vector2(-0.14f, -0.09f), new Vector2(0.14f, -0.09f), new Vector2(-0.14f, 0.09f), new Vector2(0.14f, 0.09f) })
            Piece(root.transform, "Foot", PrimitiveType.Cylinder, new Vector3(f.x, 0.01f, f.y), new Vector3(0.03f, 0.01f, 0.03f), mBrass);

        Piece(root.transform, "Plinth", PrimitiveType.Cube, new Vector3(0f, 0.03f, 0f), new Vector3(0.34f, 0.02f, 0.24f), mTrim);
        Piece(root.transform, "Case", PrimitiveType.Cube, new Vector3(0f, 0.10f, 0f), new Vector3(0.32f, 0.12f, 0.22f), mWood);

        // carved front panel with a thin frame
        Piece(root.transform, "FrontPanel", PrimitiveType.Cube, new Vector3(0f, 0.10f, -0.1115f), new Vector3(0.26f, 0.07f, 0.003f), mTrim);
        Piece(root.transform, "FrontFrameTop", PrimitiveType.Cube, new Vector3(0f, 0.138f, -0.113f), new Vector3(0.27f, 0.006f, 0.004f), mBrass);
        Piece(root.transform, "FrontFrameBottom", PrimitiveType.Cube, new Vector3(0f, 0.062f, -0.113f), new Vector3(0.27f, 0.006f, 0.004f), mBrass);
        Piece(root.transform, "FrontFrameLeft", PrimitiveType.Cube, new Vector3(-0.132f, 0.10f, -0.113f), new Vector3(0.006f, 0.07f, 0.004f), mBrass);
        Piece(root.transform, "FrontFrameRight", PrimitiveType.Cube, new Vector3(0.132f, 0.10f, -0.113f), new Vector3(0.006f, 0.07f, 0.004f), mBrass);

        // brass lock plate with a keyhole
        Piece(root.transform, "LockPlate", PrimitiveType.Cube, new Vector3(0f, 0.15f, -0.112f), new Vector3(0.03f, 0.035f, 0.004f), mBrass);
        Piece(root.transform, "Keyhole", PrimitiveType.Cube, new Vector3(0f, 0.15f, -0.1145f), new Vector3(0.006f, 0.014f, 0.003f), mMetal);

        // ---- open rim of the case (a frame) with the lining inside
        Piece(root.transform, "RimFront", PrimitiveType.Cube, new Vector3(0f, 0.17f, -0.11f), new Vector3(0.34f, 0.02f, 0.02f), mTrim);
        Piece(root.transform, "RimBack", PrimitiveType.Cube, new Vector3(0f, 0.17f, 0.11f), new Vector3(0.34f, 0.02f, 0.02f), mTrim);
        Piece(root.transform, "RimLeft", PrimitiveType.Cube, new Vector3(-0.16f, 0.17f, 0f), new Vector3(0.02f, 0.02f, 0.20f), mTrim);
        Piece(root.transform, "RimRight", PrimitiveType.Cube, new Vector3(0.16f, 0.17f, 0f), new Vector3(0.02f, 0.02f, 0.20f), mTrim);
        Piece(root.transform, "Lining", PrimitiveType.Cube, new Vector3(0f, 0.165f, 0f), new Vector3(0.30f, 0.01f, 0.20f), mVelvet);

        // brass studs on the corners
        foreach (var c in new[] { new Vector2(-0.165f, -0.115f), new Vector2(0.165f, -0.115f), new Vector2(-0.165f, 0.115f), new Vector2(0.165f, 0.115f) })
            Piece(root.transform, "Stud", PrimitiveType.Sphere, new Vector3(c.x, 0.18f, c.y), new Vector3(0.02f, 0.02f, 0.02f), mBrass);

        // ---- the spinning record (platter) with label and spindle
        var platter = new GameObject("Platter").transform;
        platter.SetParent(root.transform, false);
        platter.localPosition = new Vector3(-0.025f, 0.172f, 0f);
        Piece(platter, "Disc", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.17f, 0.002f, 0.17f), mDisc);
        Piece(platter, "Groove1", PrimitiveType.Cylinder, new Vector3(0f, 0.0021f, 0f), new Vector3(0.13f, 0.0005f, 0.13f), mMetal);
        Piece(platter, "Groove2", PrimitiveType.Cylinder, new Vector3(0f, 0.0023f, 0f), new Vector3(0.11f, 0.0005f, 0.11f), mDisc);
        Piece(platter, "Label", PrimitiveType.Cylinder, new Vector3(0f, 0.0025f, 0f), new Vector3(0.05f, 0.0005f, 0.05f), mLabel);
        Piece(platter, "Spindle", PrimitiveType.Cylinder, new Vector3(0f, 0.008f, 0f), new Vector3(0.007f, 0.007f, 0.007f), mBrass);

        // ---- tonearm
        Vector3 post = new Vector3(0.105f, 0.19f, -0.075f);
        Vector3 tip = new Vector3(0.02f, 0.177f, -0.015f);
        Piece(root.transform, "ArmBase", PrimitiveType.Cylinder, new Vector3(post.x, 0.172f, post.z), new Vector3(0.03f, 0.008f, 0.03f), mBrass);
        Piece(root.transform, "ArmPost", PrimitiveType.Cylinder, new Vector3(post.x, 0.182f, post.z), new Vector3(0.012f, 0.012f, 0.012f), mBrass);
        Vector3 dir = (tip - post);
        float armLen = dir.magnitude;
        var arm = Piece(root.transform, "Arm", PrimitiveType.Cube, (post + tip) / 2f, new Vector3(0.006f, 0.005f, armLen), mMetal);
        arm.transform.localRotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        var head = Piece(root.transform, "ArmHead", PrimitiveType.Cube, tip, new Vector3(0.012f, 0.008f, 0.02f), mBrass);
        head.transform.localRotation = Quaternion.LookRotation(dir.normalized, Vector3.up);

        // ---- winding key on the right side
        Piece(root.transform, "KeyShaft", PrimitiveType.Cylinder, new Vector3(0.18f, 0.10f, 0f), new Vector3(0.012f, 0.02f, 0.012f), mBrass, new Vector3(0f, 0f, 90f));
        Piece(root.transform, "KeyBase", PrimitiveType.Cylinder, new Vector3(0.165f, 0.10f, 0f), new Vector3(0.026f, 0.004f, 0.026f), mBrass, new Vector3(0f, 0f, 90f));
        Piece(root.transform, "KeyHandle", PrimitiveType.Cube, new Vector3(0.205f, 0.10f, 0f), new Vector3(0.008f, 0.06f, 0.014f), mBrass);
        Piece(root.transform, "KeyEndTop", PrimitiveType.Sphere, new Vector3(0.205f, 0.13f, 0f), new Vector3(0.02f, 0.02f, 0.02f), mBrass);
        Piece(root.transform, "KeyEndBottom", PrimitiveType.Sphere, new Vector3(0.205f, 0.07f, 0f), new Vector3(0.02f, 0.02f, 0.02f), mBrass);

        // ---- the lid: hinged at the back, standing open
        var lidPivot = new GameObject("LidPivot").transform;
        lidPivot.SetParent(root.transform, false);
        lidPivot.localPosition = new Vector3(0f, 0.18f, 0.11f);
        lidPivot.localRotation = Quaternion.Euler(105f, 0f, 0f);

        Piece(lidPivot, "Lid", PrimitiveType.Cube, new Vector3(0f, 0f, -0.12f), new Vector3(0.34f, 0.012f, 0.24f), mWood);
        Piece(lidPivot, "LidEdgeL", PrimitiveType.Cube, new Vector3(-0.164f, 0f, -0.12f), new Vector3(0.012f, 0.022f, 0.24f), mTrim);
        Piece(lidPivot, "LidEdgeR", PrimitiveType.Cube, new Vector3(0.164f, 0f, -0.12f), new Vector3(0.012f, 0.022f, 0.24f), mTrim);
        Piece(lidPivot, "LidEdgeFar", PrimitiveType.Cube, new Vector3(0f, 0f, -0.234f), new Vector3(0.34f, 0.022f, 0.012f), mTrim);
        Piece(lidPivot, "LidLining", PrimitiveType.Cube, new Vector3(0f, -0.0075f, -0.12f), new Vector3(0.30f, 0.004f, 0.20f), mVelvet);
        Piece(lidPivot, "Mirror", PrimitiveType.Cube, new Vector3(0f, -0.0098f, -0.12f), new Vector3(0.15f, 0.002f, 0.11f), mMirror);
        Piece(lidPivot, "MirrorFrame", PrimitiveType.Cube, new Vector3(0f, -0.0092f, -0.12f), new Vector3(0.16f, 0.001f, 0.12f), mBrass);
        Piece(lidPivot, "LidPlate", PrimitiveType.Cube, new Vector3(0f, 0.0075f, -0.12f), new Vector3(0.12f, 0.003f, 0.08f), mBrass);
        foreach (float x in new[] { -0.10f, 0.10f })
            Piece(lidPivot, "Hinge", PrimitiveType.Cylinder, new Vector3(x, 0f, 0f), new Vector3(0.012f, 0.02f, 0.012f), mBrass, new Vector3(0f, 0f, 90f));

        // ---- a soft glow so you can find it in the dark
        var glowGo = new GameObject("Glow");
        glowGo.transform.SetParent(root.transform, false);
        glowGo.transform.localPosition = new Vector3(0f, 0.30f, -0.07f);
        var light = glowGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.78f, 0.5f);
        light.intensity = 0.7f;
        light.range = 3f;
        light.shadows = LightShadows.None;

        // ---- behaviour, sound, collider
        var item = root.AddComponent<MusicBoxItem>();
        item.platter = platter;
        item.glow = light;
        item.pickupSound = FindClip("menu_click_soft");

        var src = root.AddComponent<AudioSource>();
        src.clip = FindLullaby();
        src.loop = true;
        src.playOnAwake = true;
        src.spatialBlend = 1f;
        src.rolloffMode = AudioRolloffMode.Logarithmic;
        src.minDistance = 1.5f;
        src.maxDistance = 25f;
        src.dopplerLevel = 0f;
        src.volume = 0.9f;
        if (src.clip == null)
            Debug.LogWarning("MusicBox: no lullaby clip found. Drag one into the AudioSource of Assets/Prefabs/MusicBox.prefab.");

        var col = root.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 0.2f, 0f);
        col.size = new Vector3(0.36f, 0.42f, 0.28f);

        root.transform.localScale = Vector3.one * BoxScale;

        if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    static GameObject Piece(Transform parent, string name, PrimitiveType type, Vector3 localPos, Vector3 localScale, Material m, Vector3? euler = null)
    {
        var g = GameObject.CreatePrimitive(type);
        g.name = name;
        var c = g.GetComponent<Collider>();
        if (c != null) Object.DestroyImmediate(c);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = localPos;
        g.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
        g.transform.localScale = localScale;
        if (m != null) g.GetComponent<Renderer>().sharedMaterial = m;
        return g;
    }

    // ------------------------------------------------------------------ audio lookup

    static AudioClip FindClip(string name)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip " + name))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) == name)
                return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
        return null;
    }

    static AudioClip FindLullaby()
    {
        foreach (string n in new[] { "ili_ili_sad_clean", "sad_lullaby", "ili_ili_sad", "scary_lullaby" })
        {
            var c = FindClip(n);
            if (c != null) return c;
        }
        return null;
    }

    // ------------------------------------------------------------------ materials

    static void MakeMaterials()
    {
        mWood = Mat("MB_Wood", new Color(0.24f, 0.12f, 0.06f), 0f, 0.35f);
        mTrim = Mat("MB_Trim", new Color(0.36f, 0.19f, 0.09f), 0f, 0.4f);
        mBrass = Mat("MB_Brass", new Color(0.72f, 0.55f, 0.25f), 0.85f, 0.7f);
        mVelvet = Mat("MB_Velvet", new Color(0.30f, 0.03f, 0.05f), 0f, 0.1f);
        mMetal = Mat("MB_Metal", new Color(0.10f, 0.10f, 0.11f), 0.8f, 0.6f);
        mDisc = Mat("MB_Disc", new Color(0.03f, 0.03f, 0.035f), 0.2f, 0.85f);
        mLabel = Mat("MB_Label", new Color(0.55f, 0.12f, 0.10f), 0f, 0.3f);
        mMirror = Mat("MB_Mirror", new Color(0.78f, 0.82f, 0.85f), 0.95f, 0.97f);
    }

    static Material Mat(string name, Color c, float metallic, float smooth)
    {
        string path = "Assets/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            m = new Material(sh);
            if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
            AssetDatabase.CreateAsset(m, path);
        }
        m.color = c;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
        EditorUtility.SetDirty(m);
        return m;
    }

    // ------------------------------------------------------------------ spawn spots

    static List<Transform> MakeSpots()
    {
        foreach (var go in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            if (go.name == "BigMusicBoxSpots") Object.DestroyImmediate(go);

        var root = new GameObject("BigMusicBoxSpots").transform;
        var list = new List<Transform>();

        // each spot is the top of a table, dresser, crate or trunk (y = height of its top)
        float F2 = HouseDims.F2, F3 = HouseDims.F3, B = HouseDims.BASEMENT_Y;
        AddSpot(list, root, "Spot_LivingCoffeeTable", 4f, 0.5f, 2.9f, 20f);
        AddSpot(list, root, "Spot_KitchenTable", 23.5f, 0.8f, 3.5f, 200f);
        AddSpot(list, root, "Spot_NurseryDresser", 13.2f, 1.0f, 18.5f, 160f);
        AddSpot(list, root, "Spot_BedroomNightstand", 3.2f, 0.6f, 18.6f, 110f);
        AddSpot(list, root, "Spot_BathroomSink", 24f, 0.9f, 19.6f, 180f);
        AddSpot(list, root, "Spot_BasementCrate1", 45f, B + 1.0f, 15f, 250f);
        AddSpot(list, root, "Spot_BasementCrate2", 50f, B + 1.0f, 16.5f, 190f);
        AddSpot(list, root, "Spot_StudyDesk", 3f, F2 + 0.8f, 2f, 30f);
        AddSpot(list, root, "Spot_LibraryTable", 8f, F2 + 0.8f, 15f, 90f);
        AddSpot(list, root, "Spot_PlayroomToybox", 22f, F2 + 0.7f, 17f, 140f);
        AddSpot(list, root, "Spot_AtticTrunk", 6f, F3 + 0.7f, 17f, 70f);
        AddSpot(list, root, "Spot_AtticCrate", 15f, F3 + 1.0f, 14f, 300f);
        return list;
    }

    static void AddSpot(List<Transform> list, Transform parent, string name, float x, float y, float z, float yaw)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.position = new Vector3(x, y, z);
        g.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        list.Add(g.transform);
    }

    // ------------------------------------------------------------------ manager in the scene

    static void SetupManager(GameObject prefab, List<Transform> spots)
    {
        var mgr = Object.FindFirstObjectByType<MusicBoxManager>();
        if (mgr == null)
        {
            var go = new GameObject("MusicBoxManager");
            mgr = go.AddComponent<MusicBoxManager>();
        }

        var so = new SerializedObject(mgr);
        so.FindProperty("musicBoxPrefab").objectReferenceValue = prefab;
        var arr = so.FindProperty("spots");
        arr.arraySize = spots.Count;
        for (int i = 0; i < spots.Count; i++)
            arr.GetArrayElementAtIndex(i).objectReferenceValue = spots[i];
        so.ApplyModifiedProperties();
    }
}
#endif