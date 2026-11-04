// Ilagay sa: Assets/Editor/ItemModelBuilder.cs
//
// Pinapalitan ang mga cube ng totoong model:
//   - ItemPickup (Bottle)  -> bote na may label at tapon
//   - ItemPickup (Battery) -> baterya na may gold top at nub
//   - KeyItem              -> lumang susi
//   - mga object na may "page" sa pangalan (walang script) -> 3 pirasong punit ng
//     iisang papel na may music notes (pag pinagdugtong, buo ang sheet)
//
// Menu: Haunted House > Items > PREVIEW  /  REBUILD
// Hindi ginagalaw ang scripts mo. Ang cube ay dini-disable lang ang renderer (hindi binubura),
// at may bagong child na "Visual_Model". Pwede i-REBUILD ulit anytime.

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class ItemModelBuilder
{
    const string OutDir = "Assets/Generated/ItemModels";
    const string VisualName = "Visual_Model";

    enum Kind { Bottle, Battery, Key, Page }

    class Target
    {
        public GameObject go;
        public Kind kind;
    }

    // =====================================================================
    //  MENU
    // =====================================================================
    [MenuItem("Haunted House/Items/PREVIEW (ilista kung ano papalitan)")]
    static void Preview()
    {
        var list = CollectTargets();
        if (list.Count == 0)
        {
            Debug.Log("[Items] Walang nahanap na ItemPickup, KeyItem o 'page' object sa scene.");
            return;
        }
        var objs = new List<Object>();
        foreach (var t in list)
        {
            Debug.Log("[Items] " + t.kind + "  ->  " + HierarchyPath(t.go.transform), t.go);
            objs.Add(t.go);
        }
        Selection.objects = objs.ToArray();
    }

    [MenuItem("Haunted House/Items/REBUILD models (bottle, battery, key, pages)")]
    static void RebuildAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Item models", "Itigil muna ang Play mode bago gamitin to.", "OK");
            return;
        }

        var list = CollectTargets();
        if (list.Count == 0)
        {
            EditorUtility.DisplayDialog("Item models",
                "Walang nahanap na ItemPickup, KeyItem o object na may 'page' sa pangalan sa scene.", "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog("Item models",
                list.Count + " object ang papalitan ng totoong model. Sige?", "Sige", "Cancel"))
            return;

        EnsureFolders();
        var kit = new Kit();

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Rebuild item models");
        int group = Undo.GetCurrentGroup();

        var done = new List<Object>();
        foreach (var t in list)
        {
            if (Apply(t, kit))
            {
                done.Add(t.go);
                EditorSceneManager.MarkSceneDirty(t.go.scene);
            }
        }

        Undo.CollapseUndoOperations(group);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.objects = done.ToArray();
        Debug.Log("[Items] Tapos: " + done.Count + " / " + list.Count + " ang napalitan. I-save ang scene (Ctrl+S).");
    }

    // =====================================================================
    //  HANAP NG TARGETS
    // =====================================================================
    static List<Target> CollectTargets()
    {
        var list = new List<Target>();
        var seen = new HashSet<GameObject>();

        foreach (var ip in FindInScene<ItemPickup>())
            if (seen.Add(ip.gameObject))
                list.Add(new Target
                {
                    go = ip.gameObject,
                    kind = ip.type == ItemPickup.Type.Battery ? Kind.Battery : Kind.Bottle
                });

        foreach (var k in FindInScene<KeyItem>())
            if (seen.Add(k.gameObject))
                list.Add(new Target { go = k.gameObject, kind = Kind.Key });

        // mga page: walang script, kaya sa pangalan hinahanap
        var pages = new List<GameObject>();
        foreach (var tr in FindInScene<Transform>())
        {
            var go = tr.gameObject;
            if (seen.Contains(go)) continue;
            if (!go.name.ToLower().Contains("page")) continue;
            if (go.GetComponent<RectTransform>() != null) continue;
            if (IsInsideVisual(tr)) continue;
            bool hasMesh = go.GetComponent<MeshFilter>() != null;
            bool built = tr.Find(VisualName) != null;
            if (!hasMesh && !built) continue;
            pages.Add(go);
        }
        pages.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase));
        foreach (var p in pages)
        {
            seen.Add(p);
            list.Add(new Target { go = p, kind = Kind.Page });
        }
        // page index = pwesto sa listahan (0,1,2). Kung higit sa 3, umuulit.
        pageOrder.Clear();
        for (int i = 0; i < pages.Count; i++) pageOrder[pages[i]] = i % 3;

        return list;
    }

    static readonly Dictionary<GameObject, int> pageOrder = new Dictionary<GameObject, int>();

    static List<T> FindInScene<T>() where T : Component
    {
        var res = new List<T>();
        foreach (var c in Resources.FindObjectsOfTypeAll<T>())
        {
            if (c == null || EditorUtility.IsPersistent(c)) continue;
            if (c.hideFlags != HideFlags.None) continue;
            var sc = c.gameObject.scene;
            if (!sc.IsValid() || !sc.isLoaded) continue;
            res.Add(c);
        }
        return res;
    }

    static bool IsInsideVisual(Transform t)
    {
        for (var p = t; p != null; p = p.parent)
            if (p.name == VisualName) return true;
        return false;
    }

    static string HierarchyPath(Transform t)
    {
        string s = t.name;
        while (t.parent != null) { t = t.parent; s = t.name + "/" + s; }
        return s;
    }

    // =====================================================================
    //  PAGPAPALIT NG ISANG OBJECT
    // =====================================================================
    static bool Apply(Target t, Kit kit)
    {
        var go = t.go;
        var root = go.transform;

        var meshes = OriginalMeshes(root);
        Bounds rb;
        if (!GetReferenceBounds(root, meshes, out rb))
        {
            Debug.LogWarning("[Items] Walang mesh o collider na mapagbabasehan ng laki: " + HierarchyPath(root), go);
            return false;
        }
        Vector3 sizeL = LocalProportions(root, meshes, rb);

        var old = root.Find(VisualName);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        // itago ang cube (hindi binubura)
        foreach (var mf in meshes)
        {
            var mr = mf.GetComponent<MeshRenderer>();
            if (mr != null && mr.enabled)
            {
                Undo.RecordObject(mr, "Hide cube");
                mr.enabled = false;
            }
        }

        // Visual_Model: binabawi ang scale ng cube para hindi ma-stretch ang model
        var vis = new GameObject(VisualName).transform;
        Undo.RegisterCreatedObjectUndo(vis.gameObject, "Item model");
        vis.SetParent(root, false);
        Vector3 s = root.lossyScale;
        vis.localScale = new Vector3(Inv(s.x), Inv(s.y), Inv(s.z));

        var pivot = new GameObject("Pivot").transform;
        pivot.SetParent(vis, false);

        float minCollider = 0f;
        switch (t.kind)
        {
            case Kind.Bottle:
                pivot.localRotation = UprightRotation(sizeL);
                Part(pivot, "Glass", kit.bottleGlass, kit.glass, Vector3.zero, Quaternion.identity, Vector3.one);
                Part(pivot, "Label", kit.bottleLabel, kit.label, Vector3.zero, Quaternion.identity, Vector3.one);
                Part(pivot, "Cork", kit.bottleCork, kit.cork, Vector3.zero, Quaternion.identity, Vector3.one);
                minCollider = 0.08f;
                break;

            case Kind.Battery:
                pivot.localRotation = UprightRotation(sizeL);
                Part(pivot, "Body", kit.batBody, kit.batDark, Vector3.zero, Quaternion.identity, Vector3.one);
                Part(pivot, "TopBand", kit.batBand, kit.batGold, Vector3.zero, Quaternion.identity, Vector3.one);
                Part(pivot, "Nub", kit.batNub, kit.silver, Vector3.zero, Quaternion.identity, Vector3.one);
                minCollider = 0.08f;
                break;

            case Kind.Key:
                pivot.localRotation = KeyRotation(sizeL);
                Part(pivot, "Bow", kit.keyBow, kit.brass, Vector3.zero, Quaternion.identity, Vector3.one);
                Part(pivot, "Shaft", kit.keyShaft, kit.brass, new Vector3(0.024f, 0f, 0f), Quaternion.Euler(0, 0, -90), Vector3.one);
                Part(pivot, "Collar", kit.keyCollar, kit.brass, new Vector3(0.045f, 0f, 0f), Quaternion.Euler(0, 0, -90), Vector3.one);
                Part(pivot, "ToothA", kit.cube, kit.brass, new Vector3(0.122f, 0f, -0.0115f), Quaternion.identity, new Vector3(0.018f, 0.006f, 0.017f));
                Part(pivot, "ToothB", kit.cube, kit.brass, new Vector3(0.100f, 0f, -0.010f), Quaternion.identity, new Vector3(0.012f, 0.006f, 0.012f));
                minCollider = 0.08f;
                break;

            case Kind.Page:
                int idx;
                if (!pageOrder.TryGetValue(go, out idx)) idx = 0;
                pivot.localRotation = PageRotation(sizeL);
                Part(pivot, "TornPage", kit.page[idx], kit.paper, Vector3.zero, Quaternion.identity, Vector3.one);
                minCollider = 0.04f;
                break;
        }

        // ilagay sa parehong pwesto ng cube (nakapatong sa parehong ibabaw)
        Bounds mb;
        if (TryGetBounds(vis, out mb))
        {
            Vector3 delta = t.kind == Kind.Page
                ? rb.center - mb.center
                : new Vector3(rb.center.x - mb.center.x, rb.min.y - mb.min.y, rb.center.z - mb.center.z);
            vis.position += delta;
        }

        // collider: sundin ang bagong hugis (madaling puntiryahin pag pickup)
        var col = go.GetComponent<Collider>();
        var bc = col as BoxCollider;
        if (col != null && bc == null)
        {
            bool trig = col.isTrigger;
            Undo.DestroyObjectImmediate(col);
            bc = Undo.AddComponent<BoxCollider>(go);
            bc.isTrigger = trig;
        }
        else if (col == null && t.kind != Kind.Page)
        {
            bc = Undo.AddComponent<BoxCollider>(go);
        }
        if (bc != null)
        {
            Undo.RecordObject(bc, "Fit collider");
            FitCollider(root, bc, vis, minCollider);
        }

        return true;
    }

    static void Part(Transform parent, string name, Mesh mesh, Material mat, Vector3 pos, Quaternion rot, Vector3 scale)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localRotation = rot;
        g.transform.localScale = scale;
        g.AddComponent<MeshFilter>().sharedMesh = mesh;
        g.AddComponent<MeshRenderer>().sharedMaterial = mat;
    }

    static float Inv(float v) { return Mathf.Abs(v) > 1e-5f ? 1f / v : 1f; }

    // ---------- mga mesh ng orihinal na cube ----------
    static List<MeshFilter> OriginalMeshes(Transform root)
    {
        var list = new List<MeshFilter>();
        var own = root.GetComponent<MeshFilter>();
        if (own != null) { list.Add(own); return list; }
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            if (!IsInsideVisual(mf.transform)) list.Add(mf);
        return list;
    }

    static IEnumerable<Vector3> Corners(MeshFilter mf)
    {
        var m = mf.sharedMesh;
        if (m == null) yield break;
        var b = m.bounds;
        var tr = mf.transform;
        for (int i = 0; i < 8; i++)
        {
            var sign = new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1);
            yield return tr.TransformPoint(b.center + Vector3.Scale(b.extents, sign));
        }
    }

    static bool TryGetBounds(Transform vis, out Bounds b)
    {
        b = new Bounds();
        bool has = false;
        foreach (var mf in vis.GetComponentsInChildren<MeshFilter>())
            foreach (var p in Corners(mf))
            {
                if (!has) { b = new Bounds(p, Vector3.zero); has = true; }
                else b.Encapsulate(p);
            }
        return has;
    }

    static bool GetReferenceBounds(Transform root, List<MeshFilter> meshes, out Bounds b)
    {
        b = new Bounds();
        bool has = false;
        foreach (var mf in meshes)
            foreach (var p in Corners(mf))
            {
                if (!has) { b = new Bounds(p, Vector3.zero); has = true; }
                else b.Encapsulate(p);
            }
        if (has) return true;

        var vis = root.Find(VisualName);
        if (vis != null && TryGetBounds(vis, out b)) return true;

        var col = root.GetComponent<Collider>();
        if (col != null) { b = col.bounds; return true; }
        return false;
    }

    // sukat ng cube sa sarili nitong axes (para malaman kung mahaba, patag, atbp.)
    static Vector3 LocalProportions(Transform root, List<MeshFilter> meshes, Bounds fallbackWorld)
    {
        bool has = false;
        Bounds lb = new Bounds();
        foreach (var mf in meshes)
            foreach (var p in Corners(mf))
            {
                var l = root.InverseTransformPoint(p);
                if (!has) { lb = new Bounds(l, Vector3.zero); has = true; }
                else lb.Encapsulate(l);
            }
        if (!has) return fallbackWorld.size;
        Vector3 ls = root.lossyScale;
        return new Vector3(lb.size.x * Mathf.Abs(ls.x), lb.size.y * Mathf.Abs(ls.y), lb.size.z * Mathf.Abs(ls.z));
    }

    static void FitCollider(Transform root, BoxCollider bc, Transform vis, float minWorld)
    {
        bool has = false;
        Bounds lb = new Bounds();
        foreach (var mf in vis.GetComponentsInChildren<MeshFilter>())
            foreach (var p in Corners(mf))
            {
                var l = root.InverseTransformPoint(p);
                if (!has) { lb = new Bounds(l, Vector3.zero); has = true; }
                else lb.Encapsulate(l);
            }
        if (!has) return;

        Vector3 ls = root.lossyScale;
        Vector3 min = new Vector3(
            minWorld / Mathf.Max(1e-4f, Mathf.Abs(ls.x)),
            minWorld / Mathf.Max(1e-4f, Mathf.Abs(ls.y)),
            minWorld / Mathf.Max(1e-4f, Mathf.Abs(ls.z)));
        bc.center = lb.center;
        bc.size = Vector3.Max(lb.size, min);
    }

    // ---------- orientation ----------
    // bote/baterya: ang taas ng model (Y) ay sumusunod sa pinakamahabang axis ng cube
    static Quaternion UprightRotation(Vector3 s)
    {
        if (s.y >= Mathf.Max(s.x, s.z) * 0.8f) return Quaternion.identity;
        return s.x >= s.z
            ? Quaternion.FromToRotation(Vector3.up, Vector3.right)
            : Quaternion.FromToRotation(Vector3.up, Vector3.forward);
    }

    // susi: ang haba ng model (X) ay sumusunod sa pinakamahabang axis
    static Quaternion KeyRotation(Vector3 s)
    {
        if (s.x >= Mathf.Max(s.y, s.z) * 0.8f) return Quaternion.identity;
        return s.z >= s.y
            ? Quaternion.FromToRotation(Vector3.right, Vector3.forward)
            : Quaternion.FromToRotation(Vector3.right, Vector3.up);
    }

    // page: ang patag na mukha ay sumusunod sa pinakamanipis na axis ng cube
    static Quaternion PageRotation(Vector3 s)
    {
        float m = Mathf.Min(s.x, Mathf.Min(s.y, s.z));
        if (s.y <= m * 1.15f)                       // nakahiga (sahig/mesa)
            return s.z > s.x * 1.25f ? Quaternion.Euler(0, 90, 0) : Quaternion.identity;
        if (s.z <= m * 1.15f)                       // nakadikit sa pader (harap sa Z)
        {
            var q = Quaternion.Euler(-90, 0, 0);
            return s.y > s.x * 1.25f ? Quaternion.Euler(0, 0, 90) * q : q;
        }
        var q2 = Quaternion.Euler(-90, 90, 0);      // nakadikit sa pader (harap sa X)
        return s.y > s.z * 1.25f ? Quaternion.Euler(90, 0, 0) * q2 : q2;
    }

    // =====================================================================
    //  ASSETS (meshes, materials, texture) -- sine-save sa Assets/Generated/ItemModels
    // =====================================================================
    static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Generated"))
            AssetDatabase.CreateFolder("Assets", "Generated");
        if (!AssetDatabase.IsValidFolder(OutDir))
            AssetDatabase.CreateFolder("Assets/Generated", "ItemModels");
    }

    class Kit
    {
        public Mesh cube;
        public Mesh bottleGlass, bottleLabel, bottleCork;
        public Mesh batBody, batBand, batNub;
        public Mesh keyBow, keyShaft, keyCollar;
        public Mesh[] page = new Mesh[3];
        public Material glass, label, cork, batDark, batGold, silver, brass, paper;

        public Kit()
        {
            cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");

            // ---- BOTE (taas ~0.32 m, nakapatong sa y = 0) ----
            bottleGlass = SaveMesh(Lathe("Item_BottleGlass", new[]
            {
                new Vector2(0f, 0f), new Vector2(0.040f, 0f), new Vector2(0.048f, 0.004f), new Vector2(0.0505f, 0.012f),
                new Vector2(0.0505f, 0.160f), new Vector2(0.049f, 0.180f), new Vector2(0.043f, 0.200f),
                new Vector2(0.033f, 0.215f), new Vector2(0.023f, 0.228f), new Vector2(0.0185f, 0.240f),
                new Vector2(0.0175f, 0.260f), new Vector2(0.0175f, 0.280f), new Vector2(0.0215f, 0.283f),
                new Vector2(0.0225f, 0.292f), new Vector2(0.0215f, 0.300f), new Vector2(0f, 0.300f)
            }));
            bottleLabel = SaveMesh(Lathe("Item_BottleLabel", new[]
            {
                new Vector2(0.0505f, 0.050f), new Vector2(0.0516f, 0.050f),
                new Vector2(0.0516f, 0.130f), new Vector2(0.0505f, 0.130f)
            }));
            bottleCork = SaveMesh(Lathe("Item_BottleCork", new[]
            {
                new Vector2(0f, 0.285f), new Vector2(0.0155f, 0.285f), new Vector2(0.0155f, 0.314f),
                new Vector2(0.0140f, 0.318f), new Vector2(0f, 0.318f)
            }));

            // ---- BATERYA (taas ~0.074 m) ----
            batBody = SaveMesh(Lathe("Item_BatteryBody", new[]
            {
                new Vector2(0f, 0f), new Vector2(0.0175f, 0f), new Vector2(0.0195f, 0.0025f),
                new Vector2(0.0200f, 0.006f), new Vector2(0.0200f, 0.052f), new Vector2(0f, 0.052f)
            }));
            batBand = SaveMesh(Lathe("Item_BatteryBand", new[]
            {
                new Vector2(0.0202f, 0.052f), new Vector2(0.0202f, 0.066f), new Vector2(0.0185f, 0.069f), new Vector2(0f, 0.069f)
            }));
            batNub = SaveMesh(Lathe("Item_BatteryNub", new[]
            {
                new Vector2(0f, 0.069f), new Vector2(0.0075f, 0.069f), new Vector2(0.0075f, 0.0725f),
                new Vector2(0.0055f, 0.0735f), new Vector2(0f, 0.0735f)
            }));

            // ---- SUSI ----
            var torus = new List<Vector2>();
            const float R = 0.022f, rho = 0.0065f;
            for (int i = 0; i <= 14; i++)
            {
                float a = i / 14f * Mathf.PI * 2f;
                torus.Add(new Vector2(R + rho * Mathf.Cos(a), rho * Mathf.Sin(a)));
            }
            keyBow = SaveMesh(Lathe("Item_KeyBow", torus.ToArray(), 32));
            keyShaft = SaveMesh(Lathe("Item_KeyShaft", new[]
            {
                new Vector2(0f, 0f), new Vector2(0.006f, 0f), new Vector2(0.006f, 0.105f),
                new Vector2(0.0045f, 0.108f), new Vector2(0f, 0.108f)
            }, 16));
            keyCollar = SaveMesh(Lathe("Item_KeyCollar", new[]
            {
                new Vector2(0f, 0f), new Vector2(0.0095f, 0f), new Vector2(0.0095f, 0.006f), new Vector2(0f, 0.006f)
            }, 16));

            // ---- PAGES (3 pirasong punit ng iisang sheet) ----
            var tex = BuildSheetTexture();
            for (int i = 0; i < 3; i++) page[i] = SaveMesh(TornPiece(i));

            // ---- MATERIALS ----
            glass = MakeMat("Item_BottleGlass", new Color(0.12f, 0.34f, 0.15f), 0f, 0.95f, null);
            label = MakeMat("Item_BottleLabel", new Color(0.82f, 0.76f, 0.60f), 0f, 0.10f, null);
            cork = MakeMat("Item_Cork", new Color(0.55f, 0.38f, 0.20f), 0f, 0.10f, null);
            batDark = MakeMat("Item_BatteryDark", new Color(0.07f, 0.07f, 0.08f), 0.1f, 0.50f, null);
            batGold = MakeMat("Item_BatteryGold", new Color(0.80f, 0.45f, 0.12f), 0.6f, 0.50f, null);
            silver = MakeMat("Item_Silver", new Color(0.75f, 0.75f, 0.78f), 0.9f, 0.70f, null);
            brass = MakeMat("Item_OldBrass", new Color(0.55f, 0.40f, 0.16f), 0.8f, 0.35f, null);
            paper = MakeMat("Item_TornPaper", Color.white, 0f, 0.05f, tex);
        }
    }

    static Mesh SaveMesh(Mesh m)
    {
        AssetDatabase.CreateAsset(m, OutDir + "/" + m.name + ".asset");
        return m;
    }

    static Material MakeMat(string name, Color col, float metallic, float smooth, Texture tex)
    {
        Shader sh = null;
        if (GraphicsSettings.currentRenderPipeline != null)
        {
            sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("HDRP/Lit");
        }
        if (sh == null) sh = Shader.Find("Standard");

        var m = new Material(sh) { name = name };
        m.color = col;
        if (tex != null) m.mainTexture = tex;
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
        AssetDatabase.CreateAsset(m, OutDir + "/" + name + ".mat");
        return m;
    }

    // =====================================================================
    //  LATHE: umiikot na profile (radius, taas) -> bote, baterya, susi
    // =====================================================================
    static Mesh Lathe(string name, Vector2[] prof, int sides = 28)
    {
        int segs = prof.Length - 1;
        var segN = new Vector2[segs];
        for (int i = 0; i < segs; i++)
        {
            Vector2 d = prof[i + 1] - prof[i];
            segN[i] = new Vector2(d.y, -d.x).normalized;
        }

        var cum = new float[prof.Length];
        for (int i = 1; i < prof.Length; i++) cum[i] = cum[i - 1] + (prof[i] - prof[i - 1]).magnitude;
        float total = Mathf.Max(1e-6f, cum[prof.Length - 1]);

        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        const float smoothDot = 0.7f;
        int row = sides + 1;

        for (int s = 0; s < segs; s++)
        {
            Vector2 nStart = segN[s];
            if (s > 0 && Vector2.Dot(segN[s - 1], segN[s]) > smoothDot) nStart = (segN[s - 1] + segN[s]).normalized;
            Vector2 nEnd = segN[s];
            if (s < segs - 1 && Vector2.Dot(segN[s], segN[s + 1]) > smoothDot) nEnd = (segN[s] + segN[s + 1]).normalized;

            int baseIdx = verts.Count;
            for (int ring = 0; ring < 2; ring++)
            {
                Vector2 p = ring == 0 ? prof[s] : prof[s + 1];
                Vector2 n = ring == 0 ? nStart : nEnd;
                float v = (ring == 0 ? cum[s] : cum[s + 1]) / total;
                for (int k = 0; k <= sides; k++)
                {
                    float a = k / (float)sides * Mathf.PI * 2f;
                    float c = Mathf.Cos(a), sn = Mathf.Sin(a);
                    verts.Add(new Vector3(p.x * c, p.y, p.x * sn));
                    norms.Add(new Vector3(n.x * c, n.y, n.x * sn));
                    uvs.Add(new Vector2(k / (float)sides, v));
                }
            }

            for (int k = 0; k < sides; k++)
            {
                int s0 = baseIdx + k, s1 = s0 + 1;
                int e0 = baseIdx + row + k, e1 = e0 + 1;
                tris.Add(s0); tris.Add(e0); tris.Add(e1);
                tris.Add(s0); tris.Add(e1); tris.Add(s1);
            }
        }

        var mesh = new Mesh { name = name };
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    // =====================================================================
    //  PUNIT NA PAPEL (isang piraso ng 3). Magkatugma ang punit sa pagitan ng mga piraso.
    // =====================================================================
    static Mesh TornPiece(int index)
    {
        const float W = 0.21f, H = 0.33f, pieceH = 0.11f;
        const int N = 32;
        float centerY = (2 - index) * pieceH + pieceH * 0.5f;

        // tear 0 = nasa y 0.22 (pagitan ng piece 0 at 1), tear 1 = nasa y 0.11 (pagitan ng 1 at 2)
        float Tear(int id, float x01)
        {
            float smooth = Mathf.PerlinNoise(x01 * 5f + id * 13.7f, id * 3.1f + 0.5f) - 0.5f;
            float rough = Mathf.PerlinNoise(x01 * 37f + id * 7.7f, 9.3f + id) - 0.5f;
            return smooth * 0.016f + rough * 0.006f;
        }
        float Outer(float t, float seed)
        {
            return (Mathf.PerlinNoise(t * 30f + seed, 1.2f) - 0.5f) * 0.004f;
        }
        float BottomY(float x01)
        {
            if (index == 2) return 0f + Outer(x01, 11f);
            return (index == 0 ? 0.22f + Tear(0, x01) : 0.11f + Tear(1, x01));
        }
        float TopY(float x01)
        {
            if (index == 0) return H + Outer(x01, 23f);
            return (index == 1 ? 0.22f + Tear(0, x01) : 0.11f + Tear(1, x01));
        }

        // perimeter, clockwise (kaliwa pataas, taas pakanan, kanan pababa, baba pakaliwa)
        var pts = new List<Vector2>();
        for (int j = 0; j <= 4; j++)
        {
            float y = Mathf.Lerp(BottomY(0f), TopY(0f), j / 4f);
            pts.Add(new Vector2(Outer(y, 5f), y));
        }
        for (int i = 1; i <= N; i++)
        {
            float x01 = i / (float)N;
            pts.Add(new Vector2(x01 * W, TopY(x01)));
        }
        for (int j = 1; j <= 4; j++)
        {
            float y = Mathf.Lerp(TopY(1f), BottomY(1f), j / 4f);
            pts.Add(new Vector2(W + Outer(y, 9f), y));
        }
        for (int i = N - 1; i >= 1; i--)
        {
            float x01 = i / (float)N;
            pts.Add(new Vector2(x01 * W, BottomY(x01)));
        }

        int n = pts.Count;
        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();

        // gitna
        Vector2 c2 = Vector2.zero;
        foreach (var p in pts) c2 += p;
        c2 /= n;

        for (int side = 0; side < 2; side++)
        {
            int b = verts.Count;
            Vector3 nrm = side == 0 ? Vector3.up : Vector3.down;
            verts.Add(new Vector3(c2.x - W * 0.5f, 0f, c2.y - centerY));
            norms.Add(nrm);
            uvs.Add(new Vector2(c2.x / W, c2.y / H));
            foreach (var p in pts)
            {
                verts.Add(new Vector3(p.x - W * 0.5f, 0f, p.y - centerY));
                norms.Add(nrm);
                uvs.Add(new Vector2(p.x / W, p.y / H));
            }
            for (int i = 0; i < n; i++)
            {
                int a = b + 1 + i;
                int c = b + 1 + (i + 1) % n;
                if (side == 0) { tris.Add(b); tris.Add(a); tris.Add(c); }
                else { tris.Add(b); tris.Add(c); tris.Add(a); }
            }
        }

        var mesh = new Mesh { name = "Item_TornPage" + (index + 1) };
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    // =====================================================================
    //  TEXTURE: lumang papel na may music notes (isang buong sheet; hatiin sa 3 ang mesh)
    // =====================================================================
    class Canvas2D
    {
        public int W, H;
        public Color[] px;
        public Canvas2D(int w, int h) { W = w; H = h; px = new Color[w * h]; }

        public void Blend(int x, int y, Color c, float a)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return;
            int i = y * W + x;
            px[i] = Color.Lerp(px[i], c, Mathf.Clamp01(a));
        }

        public void Disc(float cx, float cy, float r, Color c, float a)
        {
            int x0 = Mathf.FloorToInt(cx - r - 1), x1 = Mathf.CeilToInt(cx + r + 1);
            int y0 = Mathf.FloorToInt(cy - r - 1), y1 = Mathf.CeilToInt(cy + r + 1);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    float cov = Mathf.Clamp01(r - d + 0.5f);
                    if (cov > 0f) Blend(x, y, c, a * cov);
                }
        }

        public void Line(float x0, float y0, float x1, float y1, float thick, Color c, float a)
        {
            float len = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
            int steps = Mathf.CeilToInt(len * 1.5f) + 1;
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                Disc(Mathf.Lerp(x0, x1, t), Mathf.Lerp(y0, y1, t), thick * 0.5f, c, a);
            }
        }

        public void Ellipse(float cx, float cy, float rx, float ry, float ang, bool filled, Color c, float a)
        {
            float R = Mathf.Max(rx, ry) + 2f;
            float ca = Mathf.Cos(ang), sa = Mathf.Sin(ang);
            float irx = Mathf.Max(0.5f, rx - 2.2f), iry = Mathf.Max(0.5f, ry - 2.2f);
            for (int y = Mathf.FloorToInt(cy - R); y <= Mathf.CeilToInt(cy + R); y++)
                for (int x = Mathf.FloorToInt(cx - R); x <= Mathf.CeilToInt(cx + R); x++)
                {
                    float dx = x - cx, dy = y - cy;
                    float xr = dx * ca + dy * sa;
                    float yr = -dx * sa + dy * ca;
                    float q = Mathf.Sqrt((xr / rx) * (xr / rx) + (yr / ry) * (yr / ry));
                    float cov = Mathf.Clamp01((1f - q) * Mathf.Min(rx, ry) + 0.5f);
                    if (!filled)
                    {
                        float qi = Mathf.Sqrt((xr / irx) * (xr / irx) + (yr / iry) * (yr / iry));
                        float covIn = Mathf.Clamp01((1f - qi) * Mathf.Min(irx, iry) + 0.5f);
                        cov *= (1f - covIn);
                    }
                    if (cov > 0f) Blend(x, y, c, a * cov);
                }
        }
    }

    static void Stain(Canvas2D cv, int cx, int cy, int r, Color col, float strength)
    {
        for (int y = Mathf.Max(0, cy - r); y < Mathf.Min(cv.H, cy + r); y++)
            for (int x = Mathf.Max(0, cx - r); x < Mathf.Min(cv.W, cx + r); x++)
            {
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                float t = 1f - d / r;
                if (t <= 0f) continue;
                float a = strength * Mathf.Clamp01(t * 2.5f) * (0.55f + 0.45f * (1f - t));
                cv.Blend(x, y, col, a);
            }
    }

    static Texture2D BuildSheetTexture()
    {
        const int TW = 512, TH = 800;
        var cv = new Canvas2D(TW, TH);
        var rng = new System.Random(20261);
        var stainCol = new Color(0.42f, 0.27f, 0.12f);
        var ink = new Color(0.10f, 0.06f, 0.05f);

        // papel
        for (int y = 0; y < TH; y++)
            for (int x = 0; x < TW; x++)
            {
                float u = x / (float)TW, v = y / (float)TH;
                float shade = 0.82f
                    + Mathf.PerlinNoise(u * 6f + 3f, v * 9f + 7f) * 0.20f
                    + (Mathf.PerlinNoise(u * 45f, v * 70f) - 0.5f) * 0.08f;
                var c = new Color(0.86f * shade, 0.77f * shade, 0.57f * shade, 1f);
                float edge = Mathf.Min(Mathf.Min(x, TW - 1 - x), Mathf.Min(y, TH - 1 - y));
                float burn = Mathf.Clamp01(1f - edge / 55f);
                c = Color.Lerp(c, new Color(0.30f, 0.18f, 0.08f, 1f), burn * burn * 0.6f);
                cv.px[y * TW + x] = c;
            }

        // mantsa
        for (int i = 0; i < 7; i++)
            Stain(cv, rng.Next(40, TW - 40), rng.Next(40, TH - 40), 25 + rng.Next(45), stainCol,
                0.25f + (float)rng.NextDouble() * 0.2f);
        Stain(cv, 390, 330, 32, new Color(0.38f, 0.04f, 0.03f), 0.65f);   // parang dugo
        Stain(cv, 120, 140, 16, new Color(0.38f, 0.04f, 0.03f), 0.55f);

        // tupi sa gitna
        for (int y = 0; y < TH; y++)
        {
            cv.Blend(256, y, Color.black, 0.10f);
            cv.Blend(257, y, Color.white, 0.08f);
        }

        // musika: 6 staves (2 sa bawat piraso)
        int[] centers = { 735, 605, 475, 345, 215, 85 };
        float g = 9f;
        int[] noteX = { 105, 135, 195, 225, 255, 285, 345, 375, 405, 435 };
        float[][] clef =
        {
            new[] { -3f,-26f }, new[] { 1f,-31f }, new[] { 7f,-28f }, new[] { 7f,-22f }, new[] { 6f,-8f },
            new[] { 4f,8f }, new[] { 2f,20f }, new[] { 5f,31f }, new[] { 9f,35f }, new[] { 12f,29f },
            new[] { 10f,20f }, new[] { 3f,12f }, new[] { -3f,4f }, new[] { -1f,-4f }, new[] { 6f,-6f },
            new[] { 12f,-1f }, new[] { 11f,6f }, new[] { 5f,8f }, new[] { 2f,3f }
        };

        foreach (int c in centers)
        {
            float a = 0.75f + 0.2f * (float)rng.NextDouble();

            for (int i = 0; i < 5; i++)
            {
                int ly = Mathf.RoundToInt(c - 2 * g + i * g);
                for (int x = 36; x <= 476; x++)
                {
                    float f = 0.85f + 0.15f * Mathf.PerlinNoise(x * 0.05f, ly * 0.1f);
                    cv.Blend(x, ly, ink, a * 0.60f * f);
                    cv.Blend(x, ly + 1, ink, a * 0.30f * f);
                }
            }
            foreach (int bx in new[] { 36, 170, 320, 476 })
                cv.Line(bx, c - 18, bx, c + 18, 1.4f, ink, a * 0.8f);

            // clef
            for (int i = 0; i < clef.Length - 1; i++)
                cv.Line(50 + clef[i][0], c + clef[i][1], 50 + clef[i + 1][0], c + clef[i + 1][1], 1.8f, ink, a);

            // notes
            foreach (int nx in noteX)
            {
                int p = rng.Next(-2, 11);                    // half-space mula sa pinakababang linya
                float ny = c - 18f + p * (g * 0.5f);
                bool hollow = rng.NextDouble() < 0.25;

                if (p <= -2)
                    for (int lp = -2; lp >= p; lp -= 2)
                        cv.Line(nx - 10, c - 18f + lp * (g * 0.5f), nx + 10, c - 18f + lp * (g * 0.5f), 1.3f, ink, a * 0.9f);
                if (p >= 10)
                    for (int lp = 10; lp <= p; lp += 2)
                        cv.Line(nx - 10, c - 18f + lp * (g * 0.5f), nx + 10, c - 18f + lp * (g * 0.5f), 1.3f, ink, a * 0.9f);

                cv.Ellipse(nx, ny, 6.8f, 5f, 0.35f, !hollow, ink, a);

                bool flag = rng.NextDouble() < 0.4;
                if (p < 4)
                {
                    cv.Line(nx + 6.2f, ny, nx + 6.2f, ny + 30f, 1.6f, ink, a);
                    if (flag) cv.Line(nx + 6.2f, ny + 30f, nx + 14f, ny + 18f, 1.8f, ink, a);
                }
                else
                {
                    cv.Line(nx - 6.2f, ny, nx - 6.2f, ny - 30f, 1.6f, ink, a);
                    if (flag) cv.Line(nx - 6.2f, ny - 30f, nx + 2f, ny - 19f, 1.8f, ink, a);
                }
            }
        }

        // maliliit na batik ng tanda
        for (int i = 0; i < 260; i++)
            cv.Disc(rng.Next(TW), rng.Next(TH), 0.6f + (float)rng.NextDouble() * 1.4f, stainCol, 0.35f);

        var tex = new Texture2D(TW, TH, TextureFormat.RGBA32, true);
        tex.SetPixels(cv.px);
        tex.Apply();
        string path = OutDir + "/SheetMusic.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);

        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp != null)
        {
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.anisoLevel = 4;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
}
