
#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// =======================================================
// THE LAST LULLABY
// AUTOMATIC HOUSE PICTURE FRAME TOOL
//
// FIXED: CS0136 duplicate local variable error
//
// GALLERY SOURCE:
// Assets/Resources/Gallery
//
// FINDS:
// BigHouse > BigDecor > Room > Frame > Picture
//
// FEATURES:
// - Automatically uses pictures 01-08
// - Supports additional images
// - Fills every existing picture frame
// - Repeats images when there are more frames
// - Optional shuffle
// - Center crops without stretching
// - Optional unlit materials
// - Preserves furniture, frame positions and rotations
//
// MENU:
// The Last Lullaby > 13 - Picture Frame Setup
// =======================================================

public class HousePictureFrameTool : EditorWindow
{
    private const string GalleryFolder =
        "Assets/Resources/Gallery";

    private const string MaterialFolder =
        "Assets/Materials/Generated/FramePhotos";

    private bool shufflePictures = true;
    private int randomSeed = 1313;

    private bool cropToFit = true;
    private bool visibleInDarkness = false;

    private Vector2 scrollPosition;

    private readonly List<Texture2D> pictures =
        new List<Texture2D>();

    private readonly List<MeshRenderer> frames =
        new List<MeshRenderer>();

    // ===================================================
    // OPEN EDITOR WINDOW
    // ===================================================

    [MenuItem("The Last Lullaby/13 - Picture Frame Setup")]
    public static void OpenWindow()
    {
        HousePictureFrameTool window =
            GetWindow<HousePictureFrameTool>(
                "House Picture Frames");

        window.minSize =
            new Vector2(430f, 450f);

        window.RefreshAll();

        window.Show();
    }

    private void OnEnable()
    {
        RefreshAll();
    }

    // ===================================================
    // EDITOR UI
    // ===================================================

    private void OnGUI()
    {
        scrollPosition =
            EditorGUILayout.BeginScrollView(
                scrollPosition);

        GUILayout.Space(12);

        GUIStyle heading =
            new GUIStyle(EditorStyles.boldLabel);

        heading.fontSize = 18;
        heading.alignment =
            TextAnchor.MiddleCenter;

        GUILayout.Label(
            "THE LAST LULLABY",
            heading);

        GUILayout.Space(5);

        GUILayout.Label(
            "HOUSE PICTURE FRAME GENERATOR",
            EditorStyles.centeredGreyMiniLabel);

        GUILayout.Space(15);

        EditorGUILayout.HelpBox(
            "Automatically replaces every existing " +
            "picture inside BigHouse using images " +
            "from Assets/Resources/Gallery.\n\n" +
            "Existing frames, furniture, walls, " +
            "and colliders are preserved.",
            MessageType.Info);

        GUILayout.Space(12);

        EditorGUILayout.LabelField(
            "PICTURE SOURCE",
            EditorStyles.boldLabel);

        EditorGUILayout.SelectableLabel(
            GalleryFolder,
            EditorStyles.textField,
            GUILayout.Height(22f));

        GUILayout.Space(12);

        EditorGUILayout.LabelField(
            "DETECTED ITEMS",
            EditorStyles.boldLabel);

        EditorGUILayout.LabelField(
            "Pictures Detected",
            pictures.Count.ToString());

        EditorGUILayout.LabelField(
            "Picture Frames Detected",
            frames.Count.ToString());

        GUILayout.Space(12);

        EditorGUILayout.LabelField(
            "OPTIONS",
            EditorStyles.boldLabel);

        shufflePictures =
            EditorGUILayout.Toggle(
                "Shuffle Pictures",
                shufflePictures);

        if (shufflePictures)
        {
            randomSeed =
                EditorGUILayout.IntField(
                    "Random Seed",
                    randomSeed);
        }

        cropToFit =
            EditorGUILayout.Toggle(
                "Crop Without Stretching",
                cropToFit);

        visibleInDarkness =
            EditorGUILayout.Toggle(
                "Visible Even In Darkness",
                visibleInDarkness);

        GUILayout.Space(15);

        if (GUILayout.Button(
            "REFRESH GALLERY AND FRAMES",
            GUILayout.Height(34f)))
        {
            RefreshAll();
        }

        GUILayout.Space(12);

        if (pictures.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "No pictures found in " +
                GalleryFolder,
                MessageType.Warning);
        }
        else
        {
            EditorGUILayout.LabelField(
                "GALLERY IMAGES",
                EditorStyles.boldLabel);

            foreach (Texture2D picture in pictures)
            {
                if (picture == null)
                    continue;

                EditorGUILayout.ObjectField(
                    picture.name,
                    picture,
                    typeof(Texture2D),
                    false);
            }
        }

        if (frames.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "No picture frames detected.\n\n" +
                "Check that BigHouse > BigDecor " +
                "exists in the current scene.",
                MessageType.Warning);
        }

        GUILayout.Space(20);

        EditorGUI.BeginDisabledGroup(
            pictures.Count == 0 ||
            frames.Count == 0);

        GUI.backgroundColor =
            new Color(0.55f, 0.73f, 0.58f);

        if (GUILayout.Button(
            "APPLY ALL GALLERY PICTURES",
            GUILayout.Height(50f)))
        {
            ApplyPictures();
        }

        GUI.backgroundColor = Color.white;

        EditorGUI.EndDisabledGroup();

        GUILayout.Space(12);

        EditorGUILayout.HelpBox(
            "You can apply pictures again anytime.\n\n" +
            "If you rebuild the house decorations, " +
            "run this tool again afterward.",
            MessageType.None);

        EditorGUILayout.EndScrollView();
    }

    // ===================================================
    // REFRESH
    // ===================================================

    private void RefreshAll()
    {
        FindPictures();
        FindFrames();
        Repaint();
    }

    // ===================================================
    // FIND GALLERY PICTURES
    // ===================================================

    private void FindPictures()
    {
        pictures.Clear();

        if (!AssetDatabase.IsValidFolder(GalleryFolder))
            return;

        string[] guids =
            AssetDatabase.FindAssets(
                "t:Texture2D",
                new string[] { GalleryFolder });

        List<string> paths =
            new List<string>();

        foreach (string guid in guids)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guid);

            string extension =
                Path.GetExtension(path).ToLowerInvariant();

            if (extension == ".png" ||
                extension == ".jpg" ||
                extension == ".jpeg")
            {
                paths.Add(path);
            }
        }

        paths.Sort(
            StringComparer.OrdinalIgnoreCase);

        foreach (string path in paths)
        {
            Texture2D texture =
                AssetDatabase.LoadAssetAtPath<Texture2D>(
                    path);

            if (texture != null)
                pictures.Add(texture);
        }
    }

    // ===================================================
    // FIND EXISTING FRAMES
    // ===================================================

    private void FindFrames()
    {
        frames.Clear();

        GameObject house = null;

        Scene activeScene =
            SceneManager.GetActiveScene();

        GameObject[] sceneRoots =
            activeScene.GetRootGameObjects();

        // Search the active scene, including
        // inactive GameObjects.
        foreach (GameObject sceneRoot in sceneRoots)
        {
            Transform[] houseCandidates =
                sceneRoot.GetComponentsInChildren<
                    Transform>(true);

            foreach (Transform candidate in houseCandidates)
            {
                if (candidate.name == "BigHouse")
                {
                    house = candidate.gameObject;
                    break;
                }
            }

            if (house != null)
                break;
        }

        if (house == null)
        {
            Debug.LogWarning(
                "[HOUSE GALLERY] BigHouse not found.");

            return;
        }

        Transform decor =
            house.transform.Find("BigDecor");

        if (decor == null)
        {
            Debug.LogWarning(
                "[HOUSE GALLERY] BigDecor not found.");

            return;
        }

        // Different variable name from
        // houseCandidates to avoid CS0136.
        Transform[] decorObjects =
            decor.GetComponentsInChildren<Transform>(true);

        foreach (Transform frameObject in decorObjects)
        {
            if (frameObject.name != "Frame")
                continue;

            Transform pictureObject =
                frameObject.Find("Picture");

            if (pictureObject == null)
                continue;

            MeshRenderer renderer =
                pictureObject.GetComponent<MeshRenderer>();

            if (renderer != null)
                frames.Add(renderer);
        }

        frames.Sort(
            (first, second) =>
                string.CompareOrdinal(
                    GetHierarchyPath(first.transform),
                    GetHierarchyPath(second.transform)));
    }

    private static string GetHierarchyPath(Transform target)
    {
        string path = "";

        while (target != null)
        {
            path =
                target.GetSiblingIndex().ToString("D5") +
                "/" + path;

            target = target.parent;
        }

        return path;
    }

    // ===================================================
    // APPLY GALLERY PICTURES
    // ===================================================

    private void ApplyPictures()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Stop Play Mode",
                "Exit Play Mode before applying pictures.",
                "OK");

            return;
        }

        RefreshAll();

        if (pictures.Count == 0 || frames.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "Missing Pictures or Frames",
                "Make sure the Gallery has pictures " +
                "and BigHouse has generated frames.",
                "OK");

            return;
        }

        EnsureFolder(MaterialFolder);

        List<Texture2D> distribution =
            new List<Texture2D>(pictures);

        System.Random rng =
            new System.Random(randomSeed);

        if (shufflePictures)
        {
            Shuffle(distribution, rng);
        }

        int applied = 0;

        int undoGroup = Undo.GetCurrentGroup();

        Undo.SetCurrentGroupName(
            "Apply House Gallery Pictures");

        for (int i = 0; i < frames.Count; i++)
        {
            if (i > 0 &&
                shufflePictures &&
                i % distribution.Count == 0)
            {
                // Mix the order again after all
                // pictures have been used once.
                Shuffle(distribution, rng);
            }

            MeshRenderer frameRenderer = frames[i];

            if (frameRenderer == null)
                continue;

            Texture2D picture =
                distribution[i % distribution.Count];

            if (picture == null)
                continue;

            Material material =
                GetOrCreateMaterial(i);

            if (material == null)
                continue;

            Undo.RecordObject(
                frameRenderer,
                "Change Frame Picture");

            Undo.RecordObject(
                material,
                "Change Gallery Material");

            ConfigureMaterial(
                material,
                picture,
                frameRenderer.transform);

            frameRenderer.sharedMaterial = material;

            EditorUtility.SetDirty(material);
            EditorUtility.SetDirty(frameRenderer);

            applied++;
        }

        Undo.CollapseUndoOperations(undoGroup);

        AssetDatabase.SaveAssets();

        EditorSceneManager.MarkSceneDirty(
            SceneManager.GetActiveScene());

        Debug.Log(
            "[HOUSE GALLERY] SUCCESS! Updated " +
            applied + " frames using " +
            pictures.Count + " gallery pictures.");

        EditorUtility.DisplayDialog(
            "GALLERY COMPLETE!",
            "SUCCESS!\n\n" +
            "Gallery Pictures: " + pictures.Count +
            "\nFrames Updated: " + applied +
            "\n\nYour frames now use the gallery images!",
            "OK");
    }

    private static void Shuffle<T>(
        List<T> items,
        System.Random rng)
    {
        for (int i = items.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);

            T temp = items[i];
            items[i] = items[j];
            items[j] = temp;
        }
    }

    // ===================================================
    // MATERIALS
    // ===================================================

    private Material GetOrCreateMaterial(int frameIndex)
    {
        string path =
            MaterialFolder +
            "/GalleryFrame_" +
            frameIndex.ToString("D4") +
            ".mat";

        Material material =
            AssetDatabase.LoadAssetAtPath<Material>(
                path);

        Shader shader = FindPictureShader();

        if (shader == null)
        {
            Debug.LogError(
                "[HOUSE GALLERY] No picture shader found.");

            return null;
        }

        if (material == null)
        {
            material = new Material(shader);

            AssetDatabase.CreateAsset(material, path);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
        }

        return material;
    }

    private Shader FindPictureShader()
    {
        Shader shader = null;

        if (visibleInDarkness)
        {
            shader =
                Shader.Find(
                    "Universal Render Pipeline/Unlit");

            if (shader == null)
                shader = Shader.Find("Unlit/Texture");
        }
        else
        {
            shader =
                Shader.Find(
                    "Universal Render Pipeline/Lit");

            if (shader == null)
                shader = Shader.Find("Standard");
        }

        return shader;
    }

    // ===================================================
    // IMAGE FITTING
    // ===================================================

    private void ConfigureMaterial(
        Material material,
        Texture2D texture,
        Transform pictureTransform)
    {
        if (material == null || texture == null)
            return;

        material.mainTexture = texture;

        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", texture);

        if (material.HasProperty("_MainTex"))
            material.SetTexture("_MainTex", texture);

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", Color.white);

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", Color.white);

        Vector2 tiling = Vector2.one;
        Vector2 offset = Vector2.zero;

        if (cropToFit &&
            pictureTransform != null &&
            texture.width > 0 &&
            texture.height > 0)
        {
            Vector3 scale =
                pictureTransform.localScale;

            float frameAspect =
                Mathf.Abs(scale.x) /
                Mathf.Max(
                    0.001f,
                    Mathf.Abs(scale.y));

            float imageAspect =
                (float)texture.width / texture.height;

            if (imageAspect > frameAspect)
            {
                tiling.x =
                    frameAspect / imageAspect;

                offset.x =
                    (1f - tiling.x) * 0.5f;
            }
            else
            {
                tiling.y =
                    imageAspect / frameAspect;

                offset.y =
                    (1f - tiling.y) * 0.5f;
            }
        }

        if (material.HasProperty("_BaseMap"))
        {
            material.SetTextureScale(
                "_BaseMap",
                tiling);

            material.SetTextureOffset(
                "_BaseMap",
                offset);
        }

        if (material.HasProperty("_MainTex"))
        {
            material.SetTextureScale(
                "_MainTex",
                tiling);

            material.SetTextureOffset(
                "_MainTex",
                offset);
        }

        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 0.1f);

        if (material.HasProperty("_Glossiness"))
            material.SetFloat("_Glossiness", 0.1f);
    }

    // ===================================================
    // CREATE MATERIAL FOLDER
    // ===================================================

    private void EnsureFolder(string path)
    {
        path = path.Replace("\\", "/").TrimEnd('/');

        if (AssetDatabase.IsValidFolder(path))
            return;

        if (path == "Assets")
            return;

        string parent =
            Path.GetDirectoryName(path);

        if (string.IsNullOrEmpty(parent))
            return;

        parent = parent.Replace("\\", "/");

        EnsureFolder(parent);

        string folderName =
            Path.GetFileName(path);

        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(
                parent,
                folderName);
        }
    }
}

#endif
