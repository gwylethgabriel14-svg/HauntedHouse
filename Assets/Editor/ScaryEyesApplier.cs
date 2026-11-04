// Ilagay sa: Assets/Editor/ScaryEyesApplier.cs
// Ang texture na ScaryEye ay hinahanap kahit saang folder.
//
// Gamit: piliin ang Woman sa Hierarchy, tapos Haunted House > Enemy > Apply Scary Eyes

using UnityEditor;
using UnityEngine;

public static class ScaryEyesApplier
{
    const string EyeMatPath = "Assets/Materials/WomanEyes.mat";
    const string LashMatPath = "Assets/Materials/WomanEyelashes.mat";

    [MenuItem("Haunted House/Enemy/Apply Scary Eyes")]
    static void Apply()
    {
        var root = Selection.activeGameObject;
        if (root == null)
        {
            EditorUtility.DisplayDialog("Scary Eyes", "Piliin muna ang Woman sa Hierarchy.", "OK");
            return;
        }

        Texture2D tex = null;
        foreach (var g in AssetDatabase.FindAssets("ScaryEye t:Texture2D"))
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            if (System.IO.Path.GetFileNameWithoutExtension(path) == "ScaryEye")
            {
                tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                break;
            }
        }
        if (tex == null)
        {
            EditorUtility.DisplayDialog("Scary Eyes", "Walang texture na pangalang ScaryEye sa project.", "OK");
            return;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            AssetDatabase.CreateFolder("Assets", "Materials");

        Material eyeMat = MakeMat(EyeMatPath);
        eyeMat.SetTexture("_BaseMap", tex);
        eyeMat.SetColor("_BaseColor", Color.white);
        eyeMat.SetFloat("_Smoothness", 0.7f);
        eyeMat.EnableKeyword("_EMISSION");
        eyeMat.SetTexture("_EmissionMap", tex);
        eyeMat.SetColor("_EmissionColor", new Color(0.35f, 0.35f, 0.35f));
        eyeMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        EditorUtility.SetDirty(eyeMat);

        Material lashMat = MakeMat(LashMatPath);
        lashMat.SetColor("_BaseColor", Color.black);
        EditorUtility.SetDirty(lashMat);

        int count = 0;
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            string n = r.name.ToLower();
            Material m = null;
            if (n.Contains("eyelash")) m = lashMat;
            else if (n.Contains("eye")) m = eyeMat;
            if (m == null) continue;

            Undo.RecordObject(r, "Scary Eyes");
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = m;
            r.sharedMaterials = mats;
            EditorUtility.SetDirty(r);
            count++;
            if (m == eyeMat) LogUV(r);
            Debug.Log("[ScaryEyes] " + r.name + " -> " + m.name + " (" + r.GetType().Name + ", enabled=" + r.enabled + ", bounds=" + r.bounds.size + ")", r);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[ScaryEyes] " + count + " renderer ang na-update.");
    }

    static void LogUV(Renderer r)
    {
        Mesh mesh = null;
        var smr = r as SkinnedMeshRenderer;
        if (smr != null) mesh = smr.sharedMesh;
        else
        {
            var mf = r.GetComponent<MeshFilter>();
            if (mf != null) mesh = mf.sharedMesh;
        }
        if (mesh == null) return;

        var uv = mesh.uv;
        var v = mesh.vertices;
        Vector2 minL = new Vector2(999, 999), maxL = new Vector2(-999, -999);
        Vector2 minR = minL, maxR = maxL;
        int nl = 0, nr = 0;
        for (int i = 0; i < uv.Length; i++)
        {
            if (v[i].x >= 0) { minL = Vector2.Min(minL, uv[i]); maxL = Vector2.Max(maxL, uv[i]); nl++; }
            else { minR = Vector2.Min(minR, uv[i]); maxR = Vector2.Max(maxR, uv[i]); nr++; }
        }
        Debug.Log("[ScaryEyes] UV x>=0 (" + nl + " verts): min " + minL + " max " + maxL +
                  " | UV x<0 (" + nr + " verts): min " + minR + " max " + maxR, r);
    }

    static Material MakeMat(string path)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        AssetDatabase.CreateAsset(m, path);
        return m;
    }
}