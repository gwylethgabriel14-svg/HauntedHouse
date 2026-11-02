using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Menu: Haunted House > ADD CRIB WITH BABY
// Builds a simple crib out of cubes and puts the crying baby (CribBaby) in it.
// Place it where the selected object is, or where the Scene view is looking.
// Put this file in Assets/Editor.
public static class BuildCribBaby
{
    const string Folder = "Assets/Resources/Baby";
    const string SpritePath = Folder + "/baby_cry.png";
    const string CryPath = Folder + "/baby_cry_loop.wav";

    [MenuItem("Haunted House/ADD CRIB WITH BABY")]
    static void Build()
    {
        EnsureSprite(SpritePath);
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        AudioClip cry = AssetDatabase.LoadAssetAtPath<AudioClip>(CryPath);

        if (sprite == null || cry == null)
        {
            EditorUtility.DisplayDialog("Crib with baby",
                "Put baby_cry.png and baby_cry_loop.wav in " + Folder + " first, then run this again.", "OK");
            return;
        }

        Vector3 pos = TargetPosition();

        Material wood = GetMaterial("CribWood", new Color(0.20f, 0.15f, 0.11f));
        Material cloth = GetMaterial("CribCloth", new Color(0.55f, 0.53f, 0.50f));

        var root = new GameObject("Crib");
        Undo.RegisterCreatedObjectUndo(root, "Add crib with baby");
        root.transform.position = pos;

        const float halfX = 0.57f, halfZ = 0.32f;

        // posts
        foreach (float sx in new[] { -halfX, halfX })
            foreach (float sz in new[] { -halfZ, halfZ })
                Box(root.transform, "Post", new Vector3(sx, 0.5f, sz), new Vector3(0.07f, 1.0f, 0.07f), wood);

        // top and bottom rails
        foreach (float sz in new[] { -halfZ, halfZ })
        {
            Box(root.transform, "RailTopLong", new Vector3(0f, 0.97f, sz), new Vector3(1.2f, 0.05f, 0.05f), wood);
            Box(root.transform, "RailLowLong", new Vector3(0f, 0.45f, sz), new Vector3(1.14f, 0.05f, 0.04f), wood);
        }
        foreach (float sx in new[] { -halfX, halfX })
        {
            Box(root.transform, "RailTopShort", new Vector3(sx, 0.97f, 0f), new Vector3(0.05f, 0.05f, 0.64f), wood);
            Box(root.transform, "RailLowShort", new Vector3(sx, 0.45f, 0f), new Vector3(0.04f, 0.05f, 0.6f), wood);
        }

        // slats
        for (int i = -5; i <= 5; i++)
            foreach (float sz in new[] { -halfZ, halfZ })
                Box(root.transform, "Slat", new Vector3(i * 0.1f, 0.71f, sz), new Vector3(0.025f, 0.52f, 0.025f), wood);
        for (int i = -2; i <= 2; i++)
            foreach (float sx in new[] { -halfX, halfX })
                Box(root.transform, "Slat", new Vector3(sx, 0.71f, i * 0.11f), new Vector3(0.025f, 0.52f, 0.025f), wood);

        // mattress
        Box(root.transform, "Mattress", new Vector3(0f, 0.5f, 0f), new Vector3(1.08f, 0.1f, 0.58f), cloth);

        // solid collider so the player can't walk through it
        var col = new GameObject("CribCollider");
        col.transform.SetParent(root.transform, false);
        var box = col.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, 0.5f, 0f);
        box.size = new Vector3(1.25f, 1f, 0.75f);
        int obstacle = LayerMask.NameToLayer("Obstacle");
        if (obstacle >= 0) col.layer = obstacle;

        // the baby
        var baby = new GameObject("Baby");
        baby.transform.SetParent(root.transform, false);
        baby.transform.localPosition = new Vector3(0f, 0.72f, 0f);
        var cb = Undo.AddComponent<CribBaby>(baby);
        cb.sprite = sprite;
        cb.cry = cry;
        EditorUtility.SetDirty(cb);

        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log("Crib with baby added at " + pos + ". Move or rotate the 'Crib' object, then re-bake the NavMesh.");
    }

    static Vector3 TargetPosition()
    {
        Vector3 pos = Vector3.zero;

        if (Selection.activeTransform != null)
            pos = Selection.activeTransform.position;
        else if (SceneView.lastActiveSceneView != null)
            pos = SceneView.lastActiveSceneView.pivot;

        // drop it onto the floor below
        Physics.SyncTransforms();
        if (Physics.Raycast(pos + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 12f))
            pos.y = hit.point.y;

        return pos;
    }

    static GameObject Box(Transform parent, string name, Vector3 localPos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<BoxCollider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    static Material GetMaterial(string name, Color color)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            AssetDatabase.CreateFolder("Assets", "Materials");

        string path = "Assets/Materials/" + name + ".mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        mat = new Material(shader);
        mat.color = color;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.1f);

        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static void EnsureSprite(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        if (importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single) return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
    }
}
