#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;

public class DiaryJumpscareSetupWindow : EditorWindow
{
    Camera cam;
    Transform player;
    GameObject woman;
    TMP_FontAsset handwriting;
    AudioClip sting;

    DiaryPageInteractable[] page = new DiaryPageInteractable[4];
    Vector2 scroll;

    [MenuItem("The Last Lullaby/10 - Diary Jumpscare Setup")]
    public static void Open()
    {
        GetWindow<DiaryJumpscareSetupWindow>("Diary Jumpscares");
    }

    void OnEnable()
    {
        Refresh();
    }

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        GUILayout.Label(
            "THE LAST LULLABY - PARCHMENT DIARIES",
            EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Fixes page overlap, improves readable parchment view, adds faint world scribbles, and keeps the jumpscare fallback.",
            MessageType.Info);

        cam = (Camera)EditorGUILayout.ObjectField(
            "Player Camera",
            cam,
            typeof(Camera),
            true);

        player = (Transform)EditorGUILayout.ObjectField(
            "Player (not CHARACTERS)",
            player,
            typeof(Transform),
            true);

        woman = (GameObject)EditorGUILayout.ObjectField(
            "Woman Enemy - WHOLE ROOT",
            woman,
            typeof(GameObject),
            true);

        handwriting = (TMP_FontAsset)EditorGUILayout.ObjectField(
            "Handwriting TMP Font (optional)",
            handwriting,
            typeof(TMP_FontAsset),
            false);

        sting = (AudioClip)EditorGUILayout.ObjectField(
            "Jumpscare WAV",
            sting,
            typeof(AudioClip),
            false);

        GUILayout.Space(8);

        for (int i = 0; i < 4; i++)
        {
            page[i] = (DiaryPageInteractable)EditorGUILayout.ObjectField(
                "Diary " + (i + 1),
                page[i],
                typeof(DiaryPageInteractable),
                true);
        }

        if (GUILayout.Button("FIND EXISTING DIARY OBJECTS"))
            Refresh();

        if (GUILayout.Button("CREATE MISSING PAGES"))
            CreateMissing();

        if (GUILayout.Button("APPLY EXACT 4 STORY ENTRIES"))
        {
            ApplyEntries();
            EditorUtility.DisplayDialog(
                "Updated",
                "All four diary texts applied.",
                "OK");
        }

        if (GUILayout.Button("BUILD PARCHMENT + RANDOMIZE + SNAP", GUILayout.Height(36)))
        {
            UpdateSetup(false);
        }

        if (GUILayout.Button("INSTALL / UPDATE COMPLETE DIARY SYSTEM", GUILayout.Height(45)))
        {
            UpdateSetup(true);
        }

        EditorGUILayout.HelpBox(
            "After install: Ctrl+S, Play, look at paper, E to read, E to close.",
            MessageType.Info);

        EditorGUILayout.EndScrollView();
    }

    void Refresh()
    {
        DiaryJumpscareDirector d =
            FindFirstObjectByType<DiaryJumpscareDirector>();

        if (d)
        {
            cam = d.playerCamera;
            player = d.player;
            woman = d.eleanorEnemy;
            handwriting = d.handwritingFont;
            sting = d.jumpscareSound;

            if (d.pages != null)
            {
                for (int i = 0; i < Mathf.Min(4, d.pages.Length); i++)
                    page[i] = d.pages[i];
            }
        }

        if (!cam)
            cam = Camera.main;

        if (!player)
        {
            PlayerController p =
                FindFirstObjectByType<PlayerController>();

            if (p) player = p.transform;
        }

        if (!woman)
        {
            EnemyAI enemy =
                FindFirstObjectByType<EnemyAI>();

            if (enemy) woman = enemy.gameObject;
        }

        foreach (DiaryPageInteractable p in
            FindObjectsByType<DiaryPageInteractable>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None))
        {
            if (p &&
                p.pageNumber >= 1 &&
                p.pageNumber <= 4 &&
                !page[p.pageNumber - 1])
            {
                page[p.pageNumber - 1] = p;
            }
        }
    }

    void CreateMissing()
    {
        if (EditorApplication.isPlaying)
            return;

        GameObject group = GameObject.Find(
            "DiaryPages_4_SeparateFromMusicNotes");

        if (!group)
        {
            group = new GameObject(
                "DiaryPages_4_SeparateFromMusicNotes");

            Undo.RegisterCreatedObjectUndo(
                group, "Create Diary Pages");
        }

        for (int i = 0; i < 4; i++)
        {
            if (page[i])
                continue;

            GameObject go = new GameObject(
                "Eleanor_Diary_Page_" + (i + 1));

            Undo.RegisterCreatedObjectUndo(
                go, "Create Diary");

            go.transform.SetParent(group.transform);
            go.transform.position =
                (player ? player.position : Vector3.zero) +
                new Vector3(i, 1f, 0);

            page[i] =
                Undo.AddComponent<DiaryPageInteractable>(go);

            page[i].pageNumber = i + 1;
        }

        ApplyEntries();
        EditorSceneManager.MarkSceneDirty(group.scene);
    }

    void UpdateSetup(bool install)
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Play Mode",
                "Exit Play Mode first.",
                "OK");
            return;
        }

        if (!cam || !woman)
        {
            EditorUtility.DisplayDialog(
                "Missing",
                "Assign Camera and WHOLE Woman Enemy root first.",
                "OK");
            return;
        }

        for (int i = 0; i < 4; i++)
        {
            if (!page[i])
            {
                EditorUtility.DisplayDialog(
                    "Missing",
                    "Click CREATE MISSING PAGES first.",
                    "OK");
                return;
            }
        }

        DiaryJumpscareDirector d =
            FindFirstObjectByType<DiaryJumpscareDirector>();

        if (!d)
        {
            GameObject go = new GameObject("DiaryJumpscareSystem");
            Undo.RegisterCreatedObjectUndo(go, "Diary System");
            d = Undo.AddComponent<DiaryJumpscareDirector>(go);
        }

        Undo.RecordObject(d, "Update Diary System");

        d.playerCamera = cam;
        d.player = player;
        d.eleanorEnemy = woman;
        d.handwritingFont = handwriting;
        d.jumpscareSound = sting;
        d.pages = (DiaryPageInteractable[])page.Clone();
        d.randomizeOnNewGame = true;

        // tuned defaults
        d.minimumDistanceFromPlayer = 5f;
        d.minimumDistanceBetweenPages = 4f;
        d.placementClearRadius = 0.38f;
        d.pageSurfaceLift = 0.022f;
        d.readDistance = 3.5f;
        d.scareDistance = 1.55f;
        d.scareSeconds = 1.15f;
        d.screenShake = 0.09f;

        ApplyEntries();

        foreach (DiaryPageInteractable p in page)
        {
            Undo.RecordObject(p.transform, "Style and place parchment");
            EditorUtility.SetDirty(p);
        }

        d.StyleAllPages();
        d.RandomizeAndSnapPages();

        EditorUtility.SetDirty(d);

        EditorSceneManager.MarkSceneDirty(d.gameObject.scene);
        Selection.activeGameObject = d.gameObject;

        EditorUtility.DisplayDialog(
            "Diary System",
            "Parchment look, exact entries, better placement, and updated reading UI applied.\n\nSave scene (Ctrl+S) and Play.",
            "OK");
    }

    void ApplyEntries()
    {
        string[] titles =
        {
            "Diary Entry - June 12",
            "Diary Entry - October 3",
            "Diary Entry - November 19",
            "Diary Entry - December 1"
        };

        string[] entries =
        {
@"My little Lily is finally asleep.

She is only a few months old, but she already recognizes my voice. Whenever she cries, I sing her favorite lullaby, and she slowly closes her little eyes.

Sometimes, I wonder what her first words will be.

I hope she calls me Mommy.

I love her more than anything in this world.

I will protect her. Always.",

@"The rain was heavy last night.

I left Lily in her crib while I went downstairs to prepare her milk.

Then, I smelled smoke.

The fire spread so quickly. I ran upstairs, calling her name, but the smoke was too thick.

I could not find her.

I searched every room, but my baby was gone.

Please, God.

Please bring my little girl back to me.",

@"I hear her crying.

Every night, I hear the soft cries of a baby somewhere inside the house.

I follow the sound, but it always disappears.

I know it is Lily.

She must be cold. She must be scared.

I sing her lullaby, hoping she will hear me.

I will find her.

No one will take my baby away from me.

No one.",

@"I found her.

The little room behind the old wall.

Her crib. Her blanket. Her favorite doll.

I remember now.

She was hiding here when the fire started.

I searched everywhere, but I never found her.

My little Lily was here all along.

She was waiting for me.

I have been singing to an empty room.

I am so sorry, my little Lily.

Mommy is here now.

Sleep, my little angel.

Sleep."
        };

        for (int i = 0; i < 4; i++)
        {
            if (!page[i])
                continue;

            Undo.RecordObject(page[i], "Update Diary Story");

            page[i].pageNumber = i + 1;
            page[i].pageTitle = titles[i];
            page[i].diaryEntry = entries[i];

            EditorUtility.SetDirty(page[i]);
        }
    }
}
#endif