#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Put in Assets/Editor. Converts existing cube lockers to hollow lockers
// with ONE narrow physical viewing slit only on the FRONT, and assigns
// HidingSpot.peekLookPoint so the player looks out through that slit.
public static class MakeAllLockersPeekable
{
    private const float SlitHeight = 0.18f;
    private const float SlitWidthRatio = 0.50f;
    private const float DesiredEyeHeight = 1.55f;
    private const float HideDepthBackOffset = 0.14f; // move the player slightly deeper into the locker
    private const float PeekDistanceOutside = 2.2f;  // look farther outside so the view clearly faces the room

    [MenuItem("Haunted House/Make ALL Lockers Peekable")]
    public static void Run()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Stop Play Mode", "Exit Play Mode before changing the lockers.", "OK");
            return;
        }

        HidingSpot[] spots = Object.FindObjectsByType<HidingSpot>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        int fixedCount = 0;
        int skipped = 0;
        int missingPeekField = 0;

        foreach (HidingSpot spot in spots)
        {
            if (!spot.gameObject.scene.IsValid())
                continue;

            Transform locker = spot.transform;
            Transform body = locker.Find("Body");
            Transform door = locker.Find("Door");

            if (body == null || door == null ||
                body.GetComponent<BoxCollider>() == null ||
                door.GetComponent<BoxCollider>() == null)
            {
                skipped++;
                continue;
            }

            Transform oldGenerated = locker.Find("PeekLockerGenerated");
            if (oldGenerated != null)
                Undo.DestroyObjectImmediate(oldGenerated.gameObject);

            Vector3 bc = body.localPosition;
            Vector3 bs = body.localScale;
            Vector3 dc = door.localPosition;
            Vector3 ds = door.localScale;

            if (bs.x <= 0f || bs.y <= 0f || bs.z <= 0f)
            {
                skipped++;
                continue;
            }

            Material bodyMat = body.GetComponent<Renderer>() != null
                ? body.GetComponent<Renderer>().sharedMaterial : null;
            Material doorMat = door.GetComponent<Renderer>() != null
                ? door.GetComponent<Renderer>().sharedMaterial : bodyMat;

            Transform generated = NewGroup(locker, "PeekLockerGenerated");

            float thickness = Mathf.Min(0.07f, bs.x * 0.09f, bs.z * 0.09f);
            float innerW = Mathf.Max(0.05f, bs.x - thickness * 2f);
            float innerD = Mathf.Max(0.05f, bs.z - thickness * 2f);

            // Hollow cabinet shell.
            Cube(generated, "Back",
                bc + new Vector3(0f, 0f, bs.z * 0.5f - thickness * 0.5f),
                new Vector3(bs.x, bs.y, thickness), bodyMat, body.gameObject.layer);

            Cube(generated, "Left",
                bc + new Vector3(-bs.x * 0.5f + thickness * 0.5f, 0f, 0f),
                new Vector3(thickness, bs.y, innerD), bodyMat, body.gameObject.layer);

            Cube(generated, "Right",
                bc + new Vector3(bs.x * 0.5f - thickness * 0.5f, 0f, 0f),
                new Vector3(thickness, bs.y, innerD), bodyMat, body.gameObject.layer);

            Cube(generated, "Top",
                bc + new Vector3(0f, bs.y * 0.5f - thickness * 0.5f, 0f),
                new Vector3(innerW, thickness, innerD), bodyMat, body.gameObject.layer);

            Cube(generated, "Bottom",
                bc + new Vector3(0f, -bs.y * 0.5f + thickness * 0.5f, 0f),
                new Vector3(innerW, thickness, innerD), bodyMat, body.gameObject.layer);

            // Build a proper FRONT frame so there are NO gaps on the sides.
            float frontZ = dc.z;
            float frontWidth = bs.x;
            float frontHeight = bs.y;

            float slitY = Mathf.Clamp(
                DesiredEyeHeight,
                bc.y - bs.y * 0.5f + 0.30f,
                bc.y + bs.y * 0.5f - 0.30f);

            float halfGap = SlitHeight * 0.5f;
            float lowerY = bc.y - frontHeight * 0.5f;
            float upperY = bc.y + frontHeight * 0.5f;

            float bottomH = Mathf.Max(0.05f, slitY - halfGap - lowerY);
            float topH = Mathf.Max(0.05f, upperY - (slitY + halfGap));

            float openingW = Mathf.Clamp(frontWidth * SlitWidthRatio, 0.18f, frontWidth - 0.16f);
            float sideW = Mathf.Max(0.04f, (frontWidth - openingW) * 0.5f);
            float frontDepth = Mathf.Max(0.04f, ds.z);

            // Bottom panel
            Cube(generated, "Front_Bottom",
                new Vector3(bc.x, lowerY + bottomH * 0.5f, frontZ),
                new Vector3(frontWidth, bottomH, frontDepth),
                doorMat, door.gameObject.layer);

            // Top panel
            Cube(generated, "Front_Top",
                new Vector3(bc.x, upperY - topH * 0.5f, frontZ),
                new Vector3(frontWidth, topH, frontDepth),
                doorMat, door.gameObject.layer);

            // Left panel beside slit
            Cube(generated, "Front_Left",
                new Vector3(bc.x - openingW * 0.5f - sideW * 0.5f, slitY, frontZ),
                new Vector3(sideW, SlitHeight, frontDepth),
                doorMat, door.gameObject.layer);

            // Right panel beside slit
            Cube(generated, "Front_Right",
                new Vector3(bc.x + openingW * 0.5f + sideW * 0.5f, slitY, frontZ),
                new Vector3(sideW, SlitHeight, frontDepth),
                doorMat, door.gameObject.layer);

            // Hide original solid body/door.
            SetOriginalHidden(body.gameObject);
            SetOriginalHidden(door.gameObject);

            // Peek point: place well OUTSIDE the locker so the player's yaw faces the room.
            Transform peek = locker.Find("PeekLookPoint");
            if (peek == null) peek = NewGroup(locker, "PeekLookPoint");
            Undo.RecordObject(peek, "Update locker peek point");
            peek.localPosition = new Vector3(bc.x, slitY, frontZ - PeekDistanceOutside);
            peek.localRotation = Quaternion.identity;
            EditorUtility.SetDirty(peek);

            // Move hide point deeper and align the EYE to the slit height.
            Transform hidePoint = spot.hidePoint;
            if (hidePoint != null)
            {
                float eyeOffset = 1.55f;
                PlayerController player = Object.FindFirstObjectByType<PlayerController>();
                if (player != null && player.cam != null)
                    eyeOffset = player.transform.InverseTransformPoint(player.cam.position).y;

                Undo.RecordObject(hidePoint, "Align locker hide point to slit");
                Vector3 hp = hidePoint.localPosition;
                hp.x = bc.x;
                hp.z = bc.z + (bs.z * 0.5f - thickness - HideDepthBackOffset);
                hp.y = slitY - eyeOffset;
                hidePoint.localPosition = hp;
                EditorUtility.SetDirty(hidePoint);
            }

            // Optional: keep exit point centered in front if it exists.
            if (spot.exitPoint != null)
            {
                Undo.RecordObject(spot.exitPoint, "Align locker exit point");
                Vector3 ep = spot.exitPoint.localPosition;
                ep.x = bc.x;
                ep.y = DesiredEyeHeight - 0.55f;
                ep.z = frontZ - 1.05f;
                spot.exitPoint.localPosition = ep;
                EditorUtility.SetDirty(spot.exitPoint);
            }

            SerializedObject serialized = new SerializedObject(spot);
            SerializedProperty property = serialized.FindProperty("peekLookPoint");
            if (property != null)
            {
                property.objectReferenceValue = peek;
                serialized.ApplyModifiedProperties();
            }
            else
            {
                missingPeekField++;
                Debug.LogWarning("HidingSpot needs a 'public Transform peekLookPoint' field: " + spot.name, spot);
            }

            EditorUtility.SetDirty(spot);
            fixedCount++;
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Peekable lockers: updated " + fixedCount + ", skipped " + skipped +
                  ", missing peek field " + missingPeekField + ". Save scene (Ctrl+S).");

        EditorUtility.DisplayDialog(
            "Locker peek setup",
            fixedCount + " locker(s) updated. Save the scene (Ctrl+S), then test Hide / E to Unhide. " +
            "Skipped: " + skipped + ".",
            "OK");
    }

    private static Transform NewGroup(Transform parent, string name)
    {
        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Make peekable locker");
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static void SetOriginalHidden(GameObject go)
    {
        Renderer r = go.GetComponent<Renderer>();
        if (r != null)
        {
            Undo.RecordObject(r, "Hide original locker mesh");
            r.enabled = false;
        }

        Collider c = go.GetComponent<Collider>();
        if (c != null)
        {
            Undo.RecordObject(c, "Disable original locker collider");
            c.enabled = false;
        }
    }

    private static void Cube(
        Transform parent,
        string name,
        Vector3 pos,
        Vector3 scale,
        Material material,
        int layer)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Undo.RegisterCreatedObjectUndo(go, "Make peekable locker");
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = scale;
        go.layer = layer;

        if (material != null)
            go.GetComponent<Renderer>().sharedMaterial = material;
    }
}
#endif
