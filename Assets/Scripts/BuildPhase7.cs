// Put this file in Assets/Editor/ next to the other builder scripts.
// Menu items (all under "Haunted House"):
//   Create Flashlight             - spot light on the Main Camera + Flashlight.cs
//   Create Hiding Spots & Batteries - 3 lockers (HidingSpot.cs) and 4 battery pickups (ItemPickup.cs)
//   Darkness On / Darkness Off    - toggles the dark horror look (turn On once the flashlight works)
//
// You also need the UPDATED HidingSpot.cs (sent with this file).
// Safe to run again: it rebuilds what it created.

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class BuildPhase7
{
    // ---------------- Flashlight ----------------

    [MenuItem("Haunted House/Create Flashlight")]
    public static void CreateFlashlight()
    {
        var camGo = GameObject.Find("Main Camera");
        if (camGo == null) { Debug.LogError("No 'Main Camera' found (it should be a child of Player)."); return; }

        var old = camGo.transform.Find("Flashlight");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        var go = new GameObject("Flashlight");
        Undo.RegisterCreatedObjectUndo(go, "Create flashlight");
        go.transform.SetParent(camGo.transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;

        var l = go.AddComponent<Light>();
        l.type = LightType.Spot;
        l.range = 15f;
        l.spotAngle = 55f;
        l.innerSpotAngle = 30f;
        l.intensity = 4f;                      // tweak to taste
        l.color = new Color(1f, 0.95f, 0.8f);
        l.shadows = LightShadows.Soft;

        var flType = System.Type.GetType("Flashlight, Assembly-CSharp");
        if (flType == null) { Debug.LogWarning("Flashlight.cs not found: add it to the 'Flashlight' object and assign Spot yourself."); return; }
        var comp = Undo.AddComponent(go, flType);
        var so = new SerializedObject(comp);
        so.FindProperty("spot").objectReferenceValue = l;
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Flashlight created. Press Play, then F to toggle it.");
    }

    // ---------------- Darkness ----------------

    [MenuItem("Haunted House/Darkness On")]
    public static void DarknessOn()
    {
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.02f, 0.02f, 0.03f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.08f;
        RenderSettings.fogColor = new Color(0.02f, 0.02f, 0.03f);

        SetActive("Directional Light", false);
        SetActive("TempLights", false);

        var cam = Camera.main;
        if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black; }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Darkness ON. You will only see what your flashlight (F) lights up.");
    }

    [MenuItem("Haunted House/Darkness Off")]
    public static void DarknessOff()
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.35f, 0.35f, 0.38f);
        RenderSettings.fog = false;

        SetActive("Directional Light", true);
        SetActive("TempLights", true);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Darkness OFF (building mode).");
    }

    static void SetActive(string name, bool active)
    {
        // GameObject.Find ignores inactive objects, so search all scene roots
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            if (root.name == name) root.SetActive(active);
    }

    // ---------------- Hiding spots & batteries ----------------

    static readonly Vector3[] BatteryPos =
    {
        new Vector3( 1.5f, 0.2f,  6.4f),    // Hallway
        new Vector3( 3.2f, 0.2f,  3.0f),    // Bathroom
        new Vector3(-1.5f, 0.2f, -2.5f),    // Living room
        new Vector3(-3.0f, 0.2f, -6.4f),    // Bedroom 2
    };

    [MenuItem("Haunted House/Create Hiding Spots & Batteries")]
    public static void CreateSpotsAndBatteries()
    {
        var hsType = System.Type.GetType("HidingSpot, Assembly-CSharp");
        var ipType = System.Type.GetType("ItemPickup, Assembly-CSharp");
        if (hsType == null || ipType == null)
        { Debug.LogError("HidingSpot.cs / ItemPickup.cs not found. Import all scripts first."); return; }

        var lockerMat = MakeMat("GB_Locker",     new Color(0.25f, 0.18f, 0.12f));
        var doorMat   = MakeMat("GB_LockerDoor", new Color(0.38f, 0.27f, 0.18f));
        var battMat   = MakeMat("GB_Battery",    new Color(0.9f, 0.8f, 0.1f));

        // ----- lockers -----
        var hsRoot = FreshRoot("HidingSpots");
        // position, yaw (so local +Z points into the room)
        MakeLocker(hsRoot, "Locker_Bedroom1", new Vector3(-5.4f, 0f,  3.3f),  90f, hsType, lockerMat, doorMat);
        MakeLocker(hsRoot, "Locker_Kitchen",  new Vector3(-5.4f, 0f,  1.2f),  90f, hsType, lockerMat, doorMat);
        MakeLocker(hsRoot, "Locker_Storage",  new Vector3( 5.4f, 0f, -1.0f), -90f, hsType, lockerMat, doorMat);

        // ----- batteries -----
        var pkRoot = FreshRoot("Pickups");
        for (int i = 0; i < BatteryPos.Length; i++)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            b.name = "Battery_" + (i + 1);
            b.transform.SetParent(pkRoot.transform, false);
            b.transform.position = BatteryPos[i];
            b.transform.localScale = new Vector3(0.15f, 0.3f, 0.15f);
            b.GetComponent<Renderer>().sharedMaterial = battMat;
            Undo.RegisterCreatedObjectUndo(b, "Create battery");

            var comp = Undo.AddComponent(b, ipType);
            var so = new SerializedObject(comp);
            var t = so.FindProperty("type");
            if (t != null) t.enumValueIndex = 0;   // Battery
            so.ApplyModifiedProperties();
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Created 3 hiding lockers and " + BatteryPos.Length + " batteries.");
    }

    static void MakeLocker(GameObject parent, string name, Vector3 pos, float yaw,
                           System.Type hsType, Material bodyMat, Material doorMat)
    {
        var root = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(root, "Create locker");
        root.transform.SetParent(parent.transform, false);
        root.transform.position = pos;
        root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        // Visual body: no collider (the player's camera sits inside it while hiding)
        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = new Vector3(0f, 1f, 0f);
        body.transform.localScale = new Vector3(0.9f, 2f, 0.9f);
        body.GetComponent<Renderer>().sharedMaterial = bodyMat;
        Object.DestroyImmediate(body.GetComponent<Collider>());
        Undo.RegisterCreatedObjectUndo(body, "Create locker");

        // Door: a trigger the Interactor can hit from outside and from inside
        var door = GameObject.CreatePrimitive(PrimitiveType.Cube);
        door.name = "Door";
        door.transform.SetParent(root.transform, false);
        door.transform.localPosition = new Vector3(0f, 1f, 0.46f);
        door.transform.localScale = new Vector3(0.8f, 1.9f, 0.06f);
        door.GetComponent<Renderer>().sharedMaterial = doorMat;
        door.GetComponent<Collider>().isTrigger = true;
        Undo.RegisterCreatedObjectUndo(door, "Create locker");

        // Player stands inside (feet on floor), facing the door
        var hide = new GameObject("hidePoint");
        hide.transform.SetParent(root.transform, false);
        hide.transform.localPosition = new Vector3(0f, 0.05f, -0.1f);
        Undo.RegisterCreatedObjectUndo(hide, "Create locker");

        // Player reappears in front of the locker
        var exit = new GameObject("exitPoint");
        exit.transform.SetParent(root.transform, false);
        exit.transform.localPosition = new Vector3(0f, 0.05f, 1.2f);
        Undo.RegisterCreatedObjectUndo(exit, "Create locker");

        var comp = Undo.AddComponent(root, hsType);
        var so = new SerializedObject(comp);
        so.FindProperty("hidePoint").objectReferenceValue = hide.transform;
        so.FindProperty("exitPoint").objectReferenceValue = exit.transform;
        so.ApplyModifiedProperties();
    }

    static GameObject FreshRoot(string name)
    {
        var go = GameObject.Find(name);
        if (go == null)
        {
            go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        }
        for (int i = go.transform.childCount - 1; i >= 0; i--)
            Undo.DestroyObjectImmediate(go.transform.GetChild(i).gameObject);
        go.transform.position = Vector3.zero;
        return go;
    }

    static Material MakeMat(string name, Color c)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            AssetDatabase.CreateFolder("Assets", "Materials");

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
}
