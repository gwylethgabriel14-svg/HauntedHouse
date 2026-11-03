// Menu: Haunted House > APPLY GAMEPLAY POLISH
// Safe to run again. Open the House scene, stop Play mode, then run it.
//
// Wires the Mixamo Woman.fbx (real skeleton) to the Enemy so Idle/Walk/Attack play,
// adds the lady's 3D chase voice + low-stamina breathing, cuts a peek slit in each
// locker door, and restyles bottles / collectible pages as glass bottles and sheet music.

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

public static class ApplyGameplayPolish
{
    const string WomanPath = "Assets/Animations/Woman.fbx";
    const string IdlePath = "Assets/Animations/Idle.fbx";
    const string WalkPath = "Assets/Animations/Walk.fbx";
    const string AttackPath = "Assets/Animations/Attack.fbx";
    const string ControllerPath = "Assets/WomanController.controller";
    const string BottlePath = "Assets/Prefabs/Bottle.prefab";
    const string PagePath = "Assets/Prefabs/Page.prefab";
    const string GenDir = "Assets/Materials/Generated";
    const string AudioDir = "Assets/Audio/";
    const float TwoPi = Mathf.PI * 2f;
    const int Rate = 22050;

    [MenuItem("Haunted House/APPLY GAMEPLAY POLISH")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Stop Play mode first, then run Haunted House > APPLY GAMEPLAY POLISH.");
            return;
        }

        try
        {
            EditorUtility.DisplayProgressBar("Gameplay polish", "Animation import", 0.08f);
            ConfigureEnemyAnimations();

            EditorUtility.DisplayProgressBar("Gameplay polish", "Textures", 0.2f);
            EnsureFolder(GenDir);
            var sheet = MakeSheetMusicTexture();
            var sheetMat = MakeLit("M_SheetMusic", Color.white, 0.12f, sheet);
            var glass = MakeGlass("M_BottleGlass", new Color(0.18f, 0.42f, 0.28f, 0.55f));
            var cork = MakeLit("M_BottleCork", new Color(0.42f, 0.28f, 0.14f), 0.2f, null);
            var neck = MakeLit("M_BottleDark", new Color(0.08f, 0.16f, 0.1f, 1f), 0.75f, null);
            var label = MakeLit("M_BottleLabel", Color.white, 0.18f, MakeBottleLabelTexture());

            EditorUtility.DisplayProgressBar("Gameplay polish", "Enemy model", 0.4f);
            AttachEnemyVisual();

            EditorUtility.DisplayProgressBar("Gameplay polish", "Lockers", 0.55f);
            ImproveLockers();

            EditorUtility.DisplayProgressBar("Gameplay polish", "Bottles and pages", 0.7f);
            ImproveBottles(glass, cork, neck, label);
            ImprovePages(sheetMat);

            EditorUtility.DisplayProgressBar("Gameplay polish", "Audio", 0.85f);
            WireAudio();

            EditorUtility.DisplayProgressBar("Gameplay polish", "GameManager", 0.95f);
            WireGameManagerEnemy();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Gameplay polish applied. Press Ctrl+S, then Play.\n" +
                  "Enemy uses Woman.fbx + WomanController (Idle / Walk / Attack).\n" +
                  "Lockers have a door slit. Pages are sheet music. Bottles are glass.\n" +
                  "Sprint (Shift) drains stamina; panting plays when it is low. The lady voices when she chases.");
    }

    // =====================================================================
    //  Animations
    // =====================================================================

    public static void ConfigureEnemyAnimations()
    {
        if (!File.Exists(WomanPath))
        {
            Debug.LogWarning("Woman.fbx not found at " + WomanPath + ". The GhostlyWoman OBJ has no skeleton and cannot play Mixamo clips.");
            return;
        }

        ConfigureHumanoid(WomanPath, false, null, true);
        Avatar womanAvatar = FindAvatar(WomanPath);
        ConfigureHumanoid(IdlePath, true, womanAvatar, false);
        ConfigureHumanoid(WalkPath, true, womanAvatar, false);
        ConfigureHumanoid(AttackPath, false, womanAvatar, false);
        string din = "Assets/Animations/Attack din.fbx";
        if (File.Exists(din)) ConfigureHumanoid(din, false, womanAvatar, false);
    }

    static void ConfigureHumanoid(string path, bool loop, Avatar copyFrom, bool createAvatar)
    {
        var imp = AssetImporter.GetAtPath(path) as ModelImporter;
        if (imp == null) return;

        imp.animationType = ModelImporterAnimationType.Human;
        imp.avatarSetup = createAvatar || copyFrom == null
            ? ModelImporterAvatarSetup.CreateFromThisModel
            : ModelImporterAvatarSetup.CopyFromOther;
        if (!createAvatar && copyFrom != null)
            imp.sourceAvatar = copyFrom;

        var clips = imp.defaultClipAnimations;
        if (clips != null && clips.Length > 0)
        {
            for (int i = 0; i < clips.Length; i++)
            {
                clips[i].loopTime = loop;
                clips[i].loopPose = loop;
                clips[i].lockRootRotation = true;
                clips[i].lockRootPositionXZ = true;
                clips[i].lockRootHeightY = true;
                clips[i].keepOriginalOrientation = true;
                clips[i].keepOriginalPositionY = true;
                clips[i].keepOriginalPositionXZ = true;
            }
            imp.clipAnimations = clips;
        }

        imp.SaveAndReimport();
    }

    static Avatar FindAvatar(string path)
    {
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            var a = o as Avatar;
            if (a != null) return a;
        }
        return null;
    }

    public static void AttachEnemyVisual()
    {
        var enemy = GameObject.Find("Enemy");
        if (enemy == null)
        {
            Debug.LogWarning("No Enemy in the scene.");
            return;
        }

        var modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WomanPath);
        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        if (modelPrefab == null)
        {
            Debug.LogWarning("Could not load " + WomanPath);
            return;
        }

        var old = enemy.transform.Find("WomanModel");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        var model = PrefabUtility.InstantiatePrefab(modelPrefab, enemy.transform) as GameObject;
        if (model == null) model = Object.Instantiate(modelPrefab, enemy.transform);
        model.name = "WomanModel";
        Undo.RegisterCreatedObjectUndo(model, "Enemy model");
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one;

        var agent = enemy.GetComponent<NavMeshAgent>();
        float baseOffset = agent != null ? agent.baseOffset : 1f;
        FitCharacter(model, enemy.transform, baseOffset, 1.78f);

        var cap = enemy.GetComponent<Renderer>();
        if (cap != null) cap.enabled = false;

        foreach (var n in new[] { "Eye_L", "Eye_R" })
        {
            var e = enemy.transform.Find(n);
            if (e != null)
            {
                var r = e.GetComponent<Renderer>();
                if (r != null) r.enabled = false;
            }
        }

        var anim = model.GetComponent<Animator>();
        if (anim == null) anim = model.GetComponentInChildren<Animator>();
        if (anim == null) anim = Undo.AddComponent<Animator>(model);
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        if (controller != null) anim.runtimeAnimatorController = controller;
        var avatar = FindAvatar(WomanPath);
        if (avatar != null) anim.avatar = avatar;

        var knife = enemy.transform.Find("Knife_Held");
        var hand = FindBone(model.transform, "RightHand");
        if (knife != null && hand != null)
        {
            Undo.SetTransformParent(knife, hand, "Parent knife");
            knife.localPosition = new Vector3(0.02f, 0.08f, 0.04f);
            knife.localRotation = Quaternion.Euler(0f, 90f, 90f);
        }

        var ai = enemy.GetComponent<EnemyAI>();
        if (ai != null)
        {
            var so = new SerializedObject(ai);
            so.FindProperty("anim").objectReferenceValue = anim;
            so.ApplyModifiedProperties();
        }
    }

    static void FitCharacter(GameObject model, Transform enemy, float baseOffset, float targetHeight)
    {
        var rs = model.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0)
        {
            model.transform.localPosition = new Vector3(0f, -baseOffset, 0f);
            return;
        }

        var b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        float h = Mathf.Max(0.01f, b.size.y);
        float s = targetHeight / h;
        model.transform.localScale = model.transform.localScale * s;

        rs = model.GetComponentsInChildren<Renderer>();
        b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        Vector3 feet = new Vector3(enemy.position.x, enemy.position.y - baseOffset, enemy.position.z);
        Vector3 delta = feet - new Vector3(b.center.x, b.min.y, b.center.z);
        model.transform.position += delta;
    }

    static Transform FindBone(Transform root, string part)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            string n = t.name;
            if (n.IndexOf(part, StringComparison.OrdinalIgnoreCase) < 0) continue;
            if (n.IndexOf("Thumb", StringComparison.OrdinalIgnoreCase) >= 0) continue;
            if (n.IndexOf("Index", StringComparison.OrdinalIgnoreCase) >= 0) continue;
            if (n.IndexOf("Middle", StringComparison.OrdinalIgnoreCase) >= 0) continue;
            if (n.IndexOf("Ring", StringComparison.OrdinalIgnoreCase) >= 0) continue;
            if (n.IndexOf("Pinky", StringComparison.OrdinalIgnoreCase) >= 0) continue;
            return t;
        }
        return null;
    }

    // =====================================================================
    //  Lockers
    // =====================================================================

    static void ImproveLockers()
    {
        var spots = Object.FindObjectsByType<HidingSpot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var hs in spots) ImproveLocker(hs);
    }

    static void ImproveLocker(HidingSpot hs)
    {
        var root = hs.transform;
        var hide = hs.hidePoint != null ? hs.hidePoint : root.Find("hidePoint");
        if (hide != null)
        {
            Undo.RecordObject(hide, "Locker peek");
            Vector3 lp = hide.localPosition;
            hide.localPosition = new Vector3(lp.x, lp.y, 0.18f);
        }

        if (root.Find("Door_Lower") != null) return;

        var door = root.Find("Door");
        Vector3 doorPos = new Vector3(0f, 1f, 0.46f);
        Vector3 doorScale = new Vector3(0.8f, 1.9f, 0.06f);
        Material doorMat = null;

        if (door != null)
        {
            doorPos = door.localPosition;
            doorScale = door.localScale;
            var r = door.GetComponent<Renderer>();
            if (r != null)
            {
                doorMat = r.sharedMaterial;
                r.enabled = false;
            }
            var col = door.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        float y0 = doorPos.y - doorScale.y * 0.5f;
        float y1 = doorPos.y + doorScale.y * 0.5f;
        float gapH = 0.11f;
        float gapCenter = Mathf.Lerp(y0, y1, 0.74f);
        float gap0 = gapCenter - gapH * 0.5f;
        float gap1 = gapCenter + gapH * 0.5f;

        MakeDoorPanel(root, "Door_Lower", doorPos.x, (y0 + gap0) * 0.5f, doorPos.z,
                      doorScale.x, Mathf.Max(0.05f, gap0 - y0), doorScale.z, doorMat);
        MakeDoorPanel(root, "Door_Upper", doorPos.x, (gap1 + y1) * 0.5f, doorPos.z,
                      doorScale.x, Mathf.Max(0.05f, y1 - gap1), doorScale.z, doorMat);
    }

    static void MakeDoorPanel(Transform parent, string name, float x, float y, float z,
                              float sx, float sy, float sz, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Undo.RegisterCreatedObjectUndo(go, "Locker slit");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, y, z);
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = new Vector3(sx, sy, sz);
        if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
        var col = go.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);
    }

    // =====================================================================
    //  Bottles / pages
    // =====================================================================

    static void ImproveBottles(Material glass, Material cork, Material dark, Material label)
    {
        if (File.Exists(BottlePath))
        {
            var root = PrefabUtility.LoadPrefabContents(BottlePath);
            DressBottle(root, glass, cork, dark, label);
            PrefabUtility.SaveAsPrefabAsset(root, BottlePath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        foreach (var p in Object.FindObjectsByType<ItemPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (p.type != ItemPickup.Type.Bottle) continue;
            Undo.RegisterCompleteObjectUndo(p.gameObject, "Bottle look");
            DressBottle(p.gameObject, glass, cork, dark, label);
        }
    }

    static void DressBottle(GameObject root, Material glass, Material cork, Material dark, Material label)
    {
        var vis = root.transform.Find("BottleVis");
        if (vis != null) Object.DestroyImmediate(vis.gameObject);

        var mr = root.GetComponent<MeshRenderer>();
        if (mr != null) mr.enabled = false;
        var mf = root.GetComponent<MeshFilter>();
        if (mf != null) mf.sharedMesh = null;

        root.transform.localScale = Vector3.one;

        var cap = root.GetComponent<CapsuleCollider>();
        if (cap == null) cap = root.AddComponent<CapsuleCollider>();
        cap.radius = 0.045f;
        cap.height = 0.28f;
        cap.direction = 1;
        cap.center = new Vector3(0f, 0.02f, 0f);

        vis = new GameObject("BottleVis").transform;
        vis.SetParent(root.transform, false);

        Prim(vis, "Body", PrimitiveType.Cylinder, new Vector3(0f, -0.02f, 0f), new Vector3(0.09f, 0.1f, 0.09f), glass);
        Prim(vis, "Shoulder", PrimitiveType.Sphere, new Vector3(0f, 0.07f, 0f), new Vector3(0.085f, 0.05f, 0.085f), glass);
        Prim(vis, "Neck", PrimitiveType.Cylinder, new Vector3(0f, 0.13f, 0f), new Vector3(0.032f, 0.05f, 0.032f), dark);
        Prim(vis, "Lip", PrimitiveType.Cylinder, new Vector3(0f, 0.175f, 0f), new Vector3(0.04f, 0.012f, 0.04f), glass);
        Prim(vis, "Cork", PrimitiveType.Cylinder, new Vector3(0f, 0.195f, 0f), new Vector3(0.028f, 0.018f, 0.028f), cork);

        var lab = GameObject.CreatePrimitive(PrimitiveType.Quad);
        lab.name = "Label";
        lab.transform.SetParent(vis, false);
        lab.transform.localPosition = new Vector3(0f, -0.02f, 0.046f);
        lab.transform.localScale = new Vector3(0.07f, 0.09f, 1f);
        if (label != null) lab.GetComponent<Renderer>().sharedMaterial = label;
        Object.DestroyImmediate(lab.GetComponent<Collider>());
    }

    static void Prim(Transform parent, string name, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
        Object.DestroyImmediate(go.GetComponent<Collider>());
    }

    static void ImprovePages(Material sheetMat)
    {
        if (File.Exists(PagePath))
        {
            var root = PrefabUtility.LoadPrefabContents(PagePath);
            DressPage(root, sheetMat);
            PrefabUtility.SaveAsPrefabAsset(root, PagePath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        foreach (var page in Object.FindObjectsByType<Page>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Undo.RegisterCompleteObjectUndo(page.gameObject, "Page look");
            DressPage(page.gameObject, sheetMat);
        }
    }

    static void DressPage(GameObject root, Material sheetMat)
    {
        root.transform.localScale = new Vector3(0.34f, 0.46f, 0.01f);
        var mr = root.GetComponent<MeshRenderer>();
        if (mr != null && sheetMat != null) mr.sharedMaterial = sheetMat;

        var extra = root.transform.Find("SheetFace");
        if (extra != null) Object.DestroyImmediate(extra.gameObject);

        QuadFace(root.transform, "SheetFace", new Vector3(0f, 0f, 0.51f), Quaternion.identity, sheetMat);
        QuadFace(root.transform, "SheetBack", new Vector3(0f, 0f, -0.51f), Quaternion.Euler(0f, 180f, 0f), sheetMat);
    }

    static void QuadFace(Transform parent, string name, Vector3 localPos, Quaternion rot, Material mat)
    {
        var old = parent.Find(name);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var face = GameObject.CreatePrimitive(PrimitiveType.Quad);
        face.name = name;
        face.transform.SetParent(parent, false);
        face.transform.localPosition = localPos;
        face.transform.localRotation = rot;
        face.transform.localScale = Vector3.one;
        if (mat != null) face.GetComponent<Renderer>().sharedMaterial = mat;
        Object.DestroyImmediate(face.GetComponent<Collider>());
    }

    // =====================================================================
    //  Audio
    // =====================================================================

    static void WireAudio()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Audio")) AssetDatabase.CreateFolder("Assets", "Audio");

        string pantPath = AudioDir + "panting.wav";
        if (!File.Exists(pantPath))
        {
            WriteWav(pantPath, GenPanting());
            AssetDatabase.ImportAsset(pantPath);
        }
        AssetDatabase.Refresh();

        var pant = AssetDatabase.LoadAssetAtPath<AudioClip>(pantPath);
        var voice = LoadClip(
            "Assets/Audio/ili_ili_sad_clean.wav",
            "Assets/Scary Sound FX/Environmental/H_Ahhh.wav",
            "Assets/Scary Sound FX/Ambiences/Danger.wav",
            "Assets/Audio/chase_music.wav");

        var player = GameObject.Find("Player");
        var enemy = GameObject.Find("Enemy");
        if (player == null) return;

        var ga = player.GetComponent<GameAudio>();
        if (ga == null) ga = Undo.AddComponent<GameAudio>(player);
        var so = new SerializedObject(ga);
        if (so.FindProperty("lowStaminaBreath") != null)
            so.FindProperty("lowStaminaBreath").objectReferenceValue = pant;
        if (so.FindProperty("chaseVoice") != null)
            so.FindProperty("chaseVoice").objectReferenceValue = voice;
        if (enemy != null && so.FindProperty("enemy") != null)
            so.FindProperty("enemy").objectReferenceValue = enemy.GetComponent<EnemyAI>();
        so.ApplyModifiedProperties();
    }

    static AudioClip LoadClip(params string[] paths)
    {
        foreach (var p in paths)
        {
            var c = AssetDatabase.LoadAssetAtPath<AudioClip>(p);
            if (c != null) return c;
        }
        return null;
    }

    static void WireGameManagerEnemy()
    {
        var e = GameObject.Find("Enemy");
        var gm = GameObject.Find("GameManager");
        if (e == null || gm == null) return;
        var ai = e.GetComponent<EnemyAI>();
        var comp = gm.GetComponent<GameManager>();
        if (ai == null || comp == null) return;
        var so = new SerializedObject(comp);
        var p = so.FindProperty("enemy");
        if (p == null) return;
        p.objectReferenceValue = ai;
        so.ApplyModifiedProperties();
    }

    // =====================================================================
    //  Textures / materials / wav
    // =====================================================================

    static Texture2D MakeSheetMusicTexture()
    {
        string path = GenDir + "/SheetMusic.png";
        int w = 512, h = 704;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var paper = new Color(0.89f, 0.84f, 0.70f, 1f);
        var ink = new Color(0.08f, 0.07f, 0.06f, 1f);
        var stain = new Color(0.62f, 0.48f, 0.32f, 0.18f);
        var px = new Color[w * h];
        var rng = new System.Random(13);

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float n = (float)rng.NextDouble() * 0.06f;
            px[y * w + x] = new Color(paper.r - n, paper.g - n * 1.1f, paper.b - n * 1.3f, 1f);
        }

        for (int i = 0; i < 18; i++)
        {
            int cx = rng.Next(w);
            int cy = rng.Next(h);
            int rad = rng.Next(12, 40);
            StampCircle(px, w, h, cx, cy, rad, stain, true);
        }

        int systems = 5;
        int top = 70;
        int gap = (h - 120) / systems;
        for (int s = 0; s < systems; s++)
        {
            int y0 = top + s * gap;
            for (int line = 0; line < 5; line++)
            {
                int y = y0 + line * 12;
                HLine(px, w, h, 36, w - 36, y, ink);
            }
            DrawGClef(px, w, h, 48, y0 + 24, ink);
            DrawNotes(px, w, h, 110, y0, ink, rng);
        }

        HLine(px, w, h, 24, w - 24, 28, ink);
        HLine(px, w, h, 24, w - 24, h - 28, ink);

        tex.SetPixels(px);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp != null)
        {
            imp.sRGBTexture = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.alphaIsTransparency = true;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Texture2D MakeBottleLabelTexture()
    {
        string path = GenDir + "/BottleLabel.png";
        int w = 128, h = 160;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var paper = new Color(0.78f, 0.72f, 0.55f, 1f);
        var ink = new Color(0.18f, 0.12f, 0.08f, 1f);
        var px = new Color[w * h];
        for (int i = 0; i < px.Length; i++) px[i] = paper;
        HLine(px, w, h, 8, w - 8, 16, ink);
        HLine(px, w, h, 8, w - 8, h - 16, ink);
        for (int x = 20; x < w - 20; x++)
            for (int y = h / 2 - 4; y < h / 2 + 4; y++)
                px[y * w + x] = ink;
        tex.SetPixels(px);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static void DrawNotes(Color[] px, int w, int h, int x0, int y0, Color ink, System.Random rng)
    {
        int x = x0;
        while (x < w - 50)
        {
            int staff = rng.Next(0, 5);
            int y = y0 + staff * 12;
            StampCircle(px, w, h, x, y, 5, ink, false);
            VLine(px, w, h, x + 5, y - 22, y, ink);
            if (rng.NextDouble() < 0.35)
            {
                StampCircle(px, w, h, x + 14, y + 6, 5, ink, false);
                VLine(px, w, h, x + 19, y - 16, y + 6, ink);
                x += 36;
            }
            else x += 22 + rng.Next(10);
        }
    }

    static void DrawGClef(Color[] px, int w, int h, int x, int y, Color ink)
    {
        for (int i = 0; i < 40; i++)
        {
            float t = i / 40f;
            float a = t * 4.2f;
            int px_ = x + (int)(Mathf.Sin(a) * (8f - t * 5f));
            int py = y - 18 + (int)(t * 42f);
            StampCircle(px, w, h, px_, py, 2, ink, false);
        }
    }

    static void StampCircle(Color[] px, int w, int h, int cx, int cy, int rad, Color c, bool blend)
    {
        int r2 = rad * rad;
        for (int y = -rad; y <= rad; y++)
        for (int x = -rad; x <= rad; x++)
        {
            if (x * x + y * y > r2) continue;
            int xx = cx + x, yy = cy + y;
            if (xx < 0 || yy < 0 || xx >= w || yy >= h) continue;
            int i = yy * w + xx;
            px[i] = blend ? Color.Lerp(px[i], c, c.a) : c;
        }
    }

    static void HLine(Color[] px, int w, int h, int x0, int x1, int y, Color c)
    {
        if (y < 0 || y >= h) return;
        if (x0 > x1) { int t = x0; x0 = x1; x1 = t; }
        x0 = Mathf.Clamp(x0, 0, w - 1);
        x1 = Mathf.Clamp(x1, 0, w - 1);
        for (int x = x0; x <= x1; x++) px[y * w + x] = c;
    }

    static void VLine(Color[] px, int w, int h, int x, int y0, int y1, Color c)
    {
        if (x < 0 || x >= w) return;
        if (y0 > y1) { int t = y0; y0 = y1; y1 = t; }
        y0 = Mathf.Clamp(y0, 0, h - 1);
        y1 = Mathf.Clamp(y1, 0, h - 1);
        for (int y = y0; y <= y1; y++) px[y * w + x] = c;
    }

    static Material MakeLit(string name, Color tint, float smooth, Texture2D tex)
    {
        string path = GenDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        var sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        if (m == null)
        {
            m = new Material(sh);
            AssetDatabase.CreateAsset(m, path);
        }
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
        m.color = tint;
        if (tex != null)
        {
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            m.mainTexture = tex;
        }
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material MakeGlass(string name, Color tint)
    {
        var m = MakeLit(name, tint, 0.92f, null);
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
        if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
        if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.08f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = 3000;
        EditorUtility.SetDirty(m);
        return m;
    }

    static float[] GenPanting()
    {
        int n = (int)(Rate * 2.4f);
        var d = new float[n];
        var rng = new System.Random(9);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float env1 = BreathEnv(t, 0.12f, 0.55f);
            float env2 = BreathEnv(t, 1.25f, 0.55f);
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            float air = noise * 0.35f * (env1 + env2);
            float tone = Mathf.Sin(TwoPi * 180f * t) * 0.08f * (env1 + env2);
            d[i] = (air + tone) * 0.9f;
        }
        return d;
    }

    static float BreathEnv(float t, float t0, float dur)
    {
        float u = (t - t0) / dur;
        if (u < 0f || u > 1f) return 0f;
        return Mathf.Sin(u * Mathf.PI);
    }

    static void WriteWav(string path, float[] samples)
    {
        using (var fs = new FileStream(path, FileMode.Create))
        using (var w = new BinaryWriter(fs))
        {
            int dataLen = samples.Length * 2;
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            w.Write(36 + dataLen);
            w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            w.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            w.Write(16);
            w.Write((short)1);
            w.Write((short)1);
            w.Write(Rate);
            w.Write(Rate * 2);
            w.Write((short)2);
            w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            w.Write(dataLen);
            foreach (var s in samples)
                w.Write((short)(Mathf.Clamp(s, -1f, 1f) * 32767f));
        }
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
