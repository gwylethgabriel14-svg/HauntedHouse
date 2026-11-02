// Put this file in Assets/Editor/.  The five files in the "Scripts" folder go in Assets/Scripts/
// (EnemyAI.cs REPLACES your old one).
//
// Then, with your House scene open and NOT in Play mode, click:
//     Haunted House > FINISH GAME (run all)
//
// It does, in order:
//   1. Saves the scene as Assets/Scenes/House.unity
//   2. Generates placeholder sound effects (Assets/Audio/*.wav) and wires them up:
//      enemy hum, footsteps, heartbeat, chase sting + music, ambience, bottle shatter
//   3. Adds the HUD (battery, bottles, controls) and a "Main Menu" button on the win/lose panels
//   4. Adds dim room lamps that flicker when the enemy is near
//   5. Adds vignette, film grain and slight desaturation (URP)
//   6. Turns the darkness on
//   7. Builds the MainMenu scene and sets the Build Settings (MainMenu first, House second)
//
// Then use  Haunted House > Build Game  to export the game.
// Safe to run again. Existing .wav files are NOT overwritten (so you can swap in your own sounds).

using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;
using TMPro;
#if UNITY_RENDER_PIPELINE_URP
using UnityEngine.Rendering.Universal;
#endif

public static class FinishGame
{
    const string HousePath = "Assets/Scenes/House.unity";
    const string MenuPath  = "Assets/Scenes/MainMenu.unity";
    const string AudioDir  = "Assets/Audio/";
    const string BottlePath = "Assets/Prefabs/Bottle.prefab";
    const float TwoPi = Mathf.PI * 2f;
    const int Rate = 22050;

    // =====================================================================
    //  MAIN ENTRY
    // =====================================================================

    [MenuItem("Haunted House/FINISH GAME (run all)")]
    public static void RunAll()
    {
        if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Stop Play mode first", "Press the Play button again to stop the game, then run this menu item.", "OK");
            return;
        }

        string missing = FindMissing();
        if (missing != null)
        {
            EditorUtility.DisplayDialog("Open your House scene first",
                "This scene is missing: " + missing + "\n\nOpen the scene that has your Player, Enemy, GameManager and Canvas, then run this again.", "OK");
            return;
        }

        try
        {
            Step("Saving scene", 0.05f);
            if (!SaveHouse()) return;

            Step("Generating and wiring audio", 0.2f);
            WireAudio();

            Step("Building HUD and panel buttons", 0.35f);
            BuildHud();

            Step("Adding room lamps", 0.5f);
            BuildLamps();

            Step("Setting up post-processing", 0.6f);
            ApplyPostFx();

            Step("Turning the darkness on", 0.7f);
            Darkness();

            PlayerSettings.productName = "Haunted House Escape";

            Step("Saving House scene", 0.78f);
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            Step("Building the main menu scene", 0.88f);
            if (File.Exists(MenuPath))
                Debug.Log("MainMenu.unity already exists - left untouched. " +
                          "Delete it first if you want FINISH GAME to rebuild it.");
            else
                BuildMenuScene();

            Step("Setting Build Settings", 0.95f);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MenuPath, true),
                new EditorBuildSettingsScene(HousePath, true),
            };

            EditorSceneManager.OpenScene(HousePath, OpenSceneMode.Single);

            EditorUtility.DisplayDialog("Game finished!",
                "Done. Press Play in the House scene to test it.\n\n" +
                "To test the menu, open Assets/Scenes/MainMenu.unity and press Play.\n\n" +
                "When you are happy: Haunted House > Build Game.", "OK");
        }
        catch (System.Exception e)
        {
            Debug.LogError("FINISH GAME failed: " + e);
            EditorUtility.DisplayDialog("Something went wrong", "Check the Console for the red error and send it to me.\n\n" + e.Message, "OK");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    [MenuItem("Haunted House/Build Game")]
    public static void BuildGame()
    {
        if (!File.Exists(MenuPath) || !File.Exists(HousePath))
        {
            EditorUtility.DisplayDialog("Run FINISH GAME first", "MainMenu.unity or House.unity is missing. Run Haunted House > FINISH GAME (run all) first.", "OK");
            return;
        }

        EditorSceneManager.SaveOpenScenes();

        var target = EditorUserBuildSettings.activeBuildTarget;
        string ext = target == BuildTarget.StandaloneOSX ? ".app"
                   : target == BuildTarget.StandaloneWindows64 ? ".exe" : "";
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string folder = Path.Combine(projectRoot, "Builds");
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "HauntedHouseEscape" + ext);

        var options = new BuildPlayerOptions
        {
            scenes = new[] { MenuPath, HousePath },
            locationPathName = path,
            target = target,
            options = BuildOptions.None
        };

        var report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result == BuildResult.Succeeded)
        {
            EditorUtility.RevealInFinder(path);
            EditorUtility.DisplayDialog("Build finished", "Saved to:\n" + path + "\n\nZip it and send it to your friends. They need the same kind of computer (Mac build for Mac, Windows build for Windows).", "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("Build failed", "Result: " + report.summary.result + "\nCheck the Console for red errors. If it says a platform module is missing, add it in Unity Hub > Installs > Add modules.", "OK");
        }
    }

    // =====================================================================
    //  HELPERS
    // =====================================================================

    static void Step(string text, float p)
    {
        EditorUtility.DisplayProgressBar("Finishing the game", text, p);
    }

    static GameObject FindRoot(string name)
    {
        foreach (var go in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            if (go.name == name) return go;
        return null;
    }

    static string FindMissing()
    {
        foreach (var n in new[] { "Player", "Enemy", "GameManager", "Canvas" })
            if (FindRoot(n) == null) return "'" + n + "'";
        return null;
    }

    static bool SaveHouse()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path == HousePath) return EditorSceneManager.SaveScene(scene);

        if (File.Exists(HousePath))
        {
            bool ok = EditorUtility.DisplayDialog("House.unity already exists",
                "A scene called House.unity already exists and the open scene is a different one (" +
                (string.IsNullOrEmpty(scene.path) ? "unsaved" : scene.path) + ").\n\nOverwrite House.unity with the open scene?",
                "Overwrite", "Cancel");
            if (!ok) return false;
        }
        return EditorSceneManager.SaveScene(scene, HousePath);
    }

    static Material MakeMat(string name, Color c)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
        string path = "Assets/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) { m.color = c; return m; }
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        m = new Material(shader);
        m.color = c;
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    // =====================================================================
    //  AUDIO (generated placeholder sounds)
    // =====================================================================

    static float[] Make(float seconds, System.Func<float, float> f)
    {
        int n = (int)(Rate * seconds);
        var d = new float[n];
        for (int i = 0; i < n; i++) d[i] = f(i / (float)Rate);
        return d;
    }

    static void WriteWav(string path, float[] samples)
    {
        using (var fs = new FileStream(path, FileMode.Create))
        using (var w = new BinaryWriter(fs))
        {
            int dataLen = samples.Length * 2;
            w.Write(Encoding.ASCII.GetBytes("RIFF"));
            w.Write(36 + dataLen);
            w.Write(Encoding.ASCII.GetBytes("WAVE"));
            w.Write(Encoding.ASCII.GetBytes("fmt "));
            w.Write(16);
            w.Write((short)1);        // PCM
            w.Write((short)1);        // mono
            w.Write(Rate);
            w.Write(Rate * 2);
            w.Write((short)2);
            w.Write((short)16);
            w.Write(Encoding.ASCII.GetBytes("data"));
            w.Write(dataLen);
            foreach (var s in samples)
                w.Write((short)(Mathf.Clamp(s, -1f, 1f) * 32767f));
        }
    }

    static AudioClip Clip(string file, System.Func<float[]> generate)
    {
        string path = AudioDir + file;
        if (!File.Exists(path))
        {
            WriteWav(path, generate());
            AssetDatabase.ImportAsset(path);
        }
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }

    // 4 s loop: eerie minor chord with slow vibrato and breathing
    static float[] GenHum()
    {
        return Make(4f, t =>
        {
            float vib = 0.8f * Mathf.Sin(TwoPi * 0.5f * t);
            float v = 0.50f * Mathf.Sin(TwoPi * 220f * t + vib)
                    + 0.35f * Mathf.Sin(TwoPi * 262f * t + vib)
                    + 0.25f * Mathf.Sin(TwoPi * 330f * t + vib);
            float env = 0.6f + 0.4f * Mathf.Sin(TwoPi * 0.25f * t);
            return v * env * 0.4f;
        });
    }

    static float[] GenFootstep()
    {
        var rng = new System.Random(1);
        float y = 0f;
        return Make(0.28f, t =>
        {
            float x = (float)(rng.NextDouble() * 2.0 - 1.0);
            y += 0.15f * (x - y);   // low-pass: dull thud
            float env = Mathf.Exp(-t * 18f);
            float thump = Mathf.Sin(TwoPi * 70f * t) * Mathf.Exp(-t * 25f);
            return (y * 3f + thump * 0.7f) * env;
        });
    }

    static float Thump(float t, float t0, float amp)
    {
        float u = t - t0;
        if (u < 0f || u > 0.3f) return 0f;
        return Mathf.Sin(TwoPi * 55f * u) * Mathf.Exp(-u * 14f) * amp;
    }

    // 1 s loop: lub-dub
    static float[] GenHeartbeat()
    {
        return Make(1f, t => Thump(t, 0f, 0.9f) + Thump(t, 0.32f, 0.65f));
    }

    static float[] GenSting()
    {
        var rng = new System.Random(2);
        return Make(1.6f, t =>
        {
            float env = Mathf.Exp(-t * 2.2f);
            float v = (Mathf.Sin(TwoPi * 233f * t) + Mathf.Sin(TwoPi * 247f * t)
                     + 0.7f * Mathf.Sin(TwoPi * 466f * t) + 0.4f * Mathf.Sin(TwoPi * 698f * t)) * 0.22f * env;
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * 0.3f * Mathf.Exp(-t * 8f);
            return v + noise;
        });
    }

    // 8 s loop: pulsing bass with dissonant shimmer
    static float[] GenChaseMusic()
    {
        return Make(8f, t =>
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(TwoPi * 4f * t);
            float bass = Mathf.Sin(TwoPi * 55f * t) * pulse * 0.6f;
            float shimmer = (Mathf.Sin(TwoPi * 440f * t) + Mathf.Sin(TwoPi * 466f * t)) * 0.12f
                          * (0.5f + 0.5f * Mathf.Sin(TwoPi * 0.5f * t));
            return bass + shimmer;
        });
    }

    static float[] GenShatter()
    {
        var rng = new System.Random(3);
        float lp = 0f;
        return Make(0.7f, t =>
        {
            float x = (float)(rng.NextDouble() * 2.0 - 1.0);
            lp += 0.3f * (x - lp);
            float hp = x - lp;   // high-pass: glassy hiss
            float env = Mathf.Exp(-t * 9f);
            float tink = Mathf.Sin(TwoPi * 3200f * t) * Mathf.Exp(-t * 14f) * 0.3f
                       + Mathf.Sin(TwoPi * 4700f * t) * Mathf.Exp(-t * 20f) * 0.2f;
            return hp * env * 0.9f + tink;
        });
    }

    // 10 s loop: very low drone
    static float[] GenAmbience()
    {
        return Make(10f, t =>
        {
            float swell = 0.5f + 0.5f * Mathf.Sin(TwoPi * 0.1f * t);
            float v = 0.4f * Mathf.Sin(TwoPi * 40f * t)
                    + 0.25f * Mathf.Sin(TwoPi * 60f * t) * swell
                    + 0.12f * Mathf.Sin(TwoPi * 90f * t) * (1f - swell);
            return v * 0.6f;
        });
    }

    static void WireAudio()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Audio")) AssetDatabase.CreateFolder("Assets", "Audio");

        var hum      = Clip("hum.wav", GenHum);
        var step     = Clip("footstep.wav", GenFootstep);
        var heart    = Clip("heartbeat.wav", GenHeartbeat);
        var sting    = Clip("chase_sting.wav", GenSting);
        var music    = Clip("chase_music.wav", GenChaseMusic);
        var shatter  = Clip("shatter.wav", GenShatter);
        var ambience = Clip("ambience.wav", GenAmbience);
        AssetDatabase.Refresh();

        var player = FindRoot("Player");
        var enemy = FindRoot("Enemy");

        // ---- GameAudio on the Player ----
        var ga = player.GetComponent<GameAudio>();
        if (ga == null) ga = Undo.AddComponent<GameAudio>(player);
        var so = new SerializedObject(ga);
        so.FindProperty("footstep").objectReferenceValue = step;
        so.FindProperty("heartbeat").objectReferenceValue = heart;
        so.FindProperty("chaseSting").objectReferenceValue = sting;
        so.FindProperty("chaseMusic").objectReferenceValue = music;
        so.FindProperty("ambience").objectReferenceValue = ambience;
        so.FindProperty("enemy").objectReferenceValue = enemy.GetComponent<EnemyAI>();
        so.ApplyModifiedProperties();

        // ---- 3D humming on the Enemy ----
        var src = enemy.GetComponent<AudioSource>();
        if (src == null) src = Undo.AddComponent<AudioSource>(enemy);
        src.clip = hum;
        src.loop = true;
        src.playOnAwake = true;
        src.spatialBlend = 1f;
        src.minDistance = 2f;
        src.maxDistance = 20f;
        src.rolloffMode = AudioRolloffMode.Logarithmic;
        src.volume = 0.8f;
        EditorUtility.SetDirty(src);

        // ---- Bottle shatter sound ----
        if (File.Exists(BottlePath))
        {
            var root = PrefabUtility.LoadPrefabContents(BottlePath);
            var noise = root.GetComponent<NoiseOnImpact>();
            if (noise != null) noise.shatterSound = shatter;
            PrefabUtility.SaveAsPrefabAsset(root, BottlePath);
            PrefabUtility.UnloadPrefabContents(root);
        }
        else Debug.LogWarning("Bottle prefab not found: run Haunted House > Create Bottle System, then run FINISH GAME again to get the shatter sound.");
    }

    // =====================================================================
    //  HUD AND PANEL BUTTONS
    // =====================================================================

    static TextMeshProUGUI MakeText(Transform parent, string name, string text, int size, TextAlignmentOptions align,
                                    Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = sizeDelta;
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        return tmp;
    }

    static void BuildHud()
    {
        var canvas = FindRoot("Canvas");
        var ct = canvas.transform;

        foreach (var n in new[] { "StatusText", "ControlsText" })
        {
            var old = ct.Find(n);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        }

        var status = MakeText(ct, "StatusText", "Battery: 100%    Bottles: 1", 32, TextAlignmentOptions.BottomLeft,
                              new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(30f, 30f), new Vector2(700f, 60f));
        var controls = MakeText(ct, "ControlsText",
                              "WASD move    Shift run    Ctrl crouch    F flashlight    E interact    G throw bottle", 24,
                              TextAlignmentOptions.BottomRight,
                              new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-30f, 30f), new Vector2(1200f, 50f));
        controls.color = new Color(1f, 1f, 1f, 0.7f);

        var hud = canvas.GetComponent<HUD>();
        if (hud == null) hud = Undo.AddComponent<HUD>(canvas);
        var so = new SerializedObject(hud);
        so.FindProperty("statusText").objectReferenceValue = status;
        so.FindProperty("controlsText").objectReferenceValue = controls;
        so.ApplyModifiedProperties();

        // ---- SceneLoader + "Main Menu" buttons on the win/lose panels ----
        var loaderGo = FindRoot("SceneLoader");
        if (loaderGo == null)
        {
            loaderGo = new GameObject("SceneLoader");
            Undo.RegisterCreatedObjectUndo(loaderGo, "Create SceneLoader");
        }
        var loader = loaderGo.GetComponent<SceneLoader>();
        if (loader == null) loader = Undo.AddComponent<SceneLoader>(loaderGo);

        AddMenuButton(ct, "WinPanel", loader);
        AddMenuButton(ct, "LosePanel", loader);
    }

    static void AddMenuButton(Transform canvas, string panelName, SceneLoader loader)
    {
        var panel = canvas.Find(panelName);
        if (panel == null) { Debug.LogWarning(panelName + " not found: skipped its Main Menu button."); return; }

        var old = panel.Find("MenuButton");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        var restart = panel.Find("RestartButton");
        if (restart == null) { Debug.LogWarning(panelName + " has no RestartButton to copy: skipped."); return; }

        var copy = Object.Instantiate(restart.gameObject, panel);
        copy.name = "MenuButton";
        Undo.RegisterCreatedObjectUndo(copy, "Create MenuButton");
        copy.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -240f);

        var btn = copy.GetComponent<Button>();
        for (int i = btn.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            UnityEventTools.RemovePersistentListener(btn.onClick, i);
        UnityEventTools.AddPersistentListener(btn.onClick, loader.LoadMenu);

        var label = copy.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = "Main Menu";
    }

    // =====================================================================
    //  ROOM LAMPS THAT FLICKER
    // =====================================================================

    static readonly Vector3[] LampPos =
    {
        new Vector3(-4f, 2.7f,  4.5f), new Vector3(0f, 2.7f,  4.5f), new Vector3(4f, 2.7f,  4.5f),
        new Vector3(-4f, 2.7f,  0.0f), new Vector3(0f, 2.7f,  1.5f), new Vector3(4f, 2.7f,  0.0f),
        new Vector3(-4f, 2.7f, -5.0f), new Vector3(0f, 2.7f, -5.0f), new Vector3(4f, 2.7f, -5.0f),
    };

    static void BuildLamps()
    {
        var root = FindRoot("RoomLamps");
        if (root == null)
        {
            root = new GameObject("RoomLamps");
            Undo.RegisterCreatedObjectUndo(root, "Create RoomLamps");
        }
        for (int i = root.transform.childCount - 1; i >= 0; i--)
            Undo.DestroyObjectImmediate(root.transform.GetChild(i).gameObject);

        var enemyAI = FindRoot("Enemy").GetComponent<EnemyAI>();

        for (int i = 0; i < LampPos.Length; i++)
        {
            var g = new GameObject("Lamp_" + (i + 1));
            g.transform.SetParent(root.transform, false);
            g.transform.position = LampPos[i];
            Undo.RegisterCreatedObjectUndo(g, "Create lamp");

            var l = g.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = 6.5f;
            l.intensity = 0.8f;
            l.color = new Color(1f, 0.75f, 0.5f);
            l.shadows = LightShadows.None;

            var fl = g.AddComponent<FlickerLight>();
            var so = new SerializedObject(fl);
            so.FindProperty("enemy").objectReferenceValue = enemyAI;
            so.ApplyModifiedProperties();
        }
    }

    // =====================================================================
    //  POST-PROCESSING (URP)
    // =====================================================================

    static void ApplyPostFx()
    {
#if UNITY_RENDER_PIPELINE_URP
        if (!AssetDatabase.IsValidFolder("Assets/Settings")) AssetDatabase.CreateFolder("Assets", "Settings");

        const string profilePath = "Assets/Settings/HorrorVolumeProfile.asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);
        }

        Vignette vg;
        if (!profile.TryGet(out vg)) vg = profile.Add<Vignette>(false);
        if (!AssetDatabase.Contains(vg)) AssetDatabase.AddObjectToAsset(vg, profile);
        vg.intensity.Override(0.45f);
        vg.smoothness.Override(0.5f);
        vg.color.Override(Color.black);

        FilmGrain fg;
        if (!profile.TryGet(out fg)) fg = profile.Add<FilmGrain>(false);
        if (!AssetDatabase.Contains(fg)) AssetDatabase.AddObjectToAsset(fg, profile);
        fg.type.Override(FilmGrainLookup.Large02);
        fg.intensity.Override(0.4f);
        fg.response.Override(0.8f);

        ColorAdjustments ca;
        if (!profile.TryGet(out ca)) ca = profile.Add<ColorAdjustments>(false);
        if (!AssetDatabase.Contains(ca)) AssetDatabase.AddObjectToAsset(ca, profile);
        ca.saturation.Override(-25f);

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        var volGo = FindRoot("Global Volume");
        if (volGo == null)
        {
            volGo = new GameObject("Global Volume");
            Undo.RegisterCreatedObjectUndo(volGo, "Create Global Volume");
        }
        var vol = volGo.GetComponent<Volume>();
        if (vol == null) vol = Undo.AddComponent<Volume>(volGo);
        vol.isGlobal = true;
        vol.weight = 1f;
        vol.sharedProfile = profile;
        EditorUtility.SetDirty(vol);

        var cam = Camera.main;
        if (cam != null) cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
#else
        Debug.LogWarning("URP was not detected, so vignette and film grain were skipped.");
#endif
    }

    // =====================================================================
    //  DARKNESS
    // =====================================================================

    static void SetRootActive(string name, bool active)
    {
        var go = FindRoot(name);
        if (go != null) go.SetActive(active);
    }

    static void Darkness()
    {
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.02f, 0.02f, 0.03f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.08f;
        RenderSettings.fogColor = new Color(0.02f, 0.02f, 0.03f);

        SetRootActive("Directional Light", false);
        SetRootActive("TempLights", false);

        var cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
        }
    }

    // =====================================================================
    //  MAIN MENU SCENE
    // =====================================================================

    static Button MakeButton(Transform parent, string name, string label, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(500f, 110f);

        var img = go.GetComponent<Image>();
        img.color = Color.white;

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.normalColor      = new Color(0.25f, 0.07f, 0.07f);
        colors.highlightedColor = new Color(0.55f, 0.15f, 0.15f);
        colors.pressedColor     = new Color(0.35f, 0.10f, 0.10f);
        colors.selectedColor    = new Color(0.25f, 0.07f, 0.07f);
        btn.colors = colors;

        var text = MakeText(go.transform, "Text (TMP)", label, 56, TextAlignmentOptions.Center,
                            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(500f, 110f));
        text.fontStyle = FontStyles.Bold;
        return btn;
    }

    static void BuildMenuScene()
    {
        // Never overwrite a menu scene that already exists (BuildMainMenu owns it).
        if (File.Exists(MenuPath))
        {
            Debug.Log("MainMenu.unity already exists - not overwriting it.");
            return;
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.02f, 0.02f, 0.04f);
        camGo.AddComponent<AudioListener>();

        var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        var title = MakeText(canvasGo.transform, "Title", "HAUNTED HOUSE", 130, TextAlignmentOptions.Center,
                             new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 300f), new Vector2(1600f, 200f));
        title.fontStyle = FontStyles.Bold;
        title.color = new Color(0.75f, 0.1f, 0.1f);

        MakeText(canvasGo.transform, "Subtitle", "Find the 4 pages. Find the key. Escape before she finds you.", 40,
                 TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                 new Vector2(0f, 190f), new Vector2(1600f, 70f));

        var loaderGo = new GameObject("SceneLoader");
        var loader = loaderGo.AddComponent<SceneLoader>();

        var play = MakeButton(canvasGo.transform, "PlayButton", "PLAY", new Vector2(0f, 20f));
        UnityEventTools.AddPersistentListener(play.onClick, loader.PlayGame);

        var quit = MakeButton(canvasGo.transform, "QuitButton", "QUIT", new Vector2(0f, -120f));
        UnityEventTools.AddPersistentListener(quit.onClick, loader.QuitGame);

        var help = MakeText(canvasGo.transform, "Controls",
                 "WASD move    Shift run    Ctrl crouch    F flashlight    E interact    G throw bottle", 30,
                 TextAlignmentOptions.Center, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                 new Vector2(0f, 60f), new Vector2(1700f, 60f));
        help.color = new Color(1f, 1f, 1f, 0.6f);

        EditorSceneManager.SaveScene(scene, MenuPath);
    }
}
