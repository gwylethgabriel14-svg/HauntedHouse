#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// One click: adds the lives, the HUD and the game over sequence to the House scene
// and connects the sounds it finds in your Assets.
//
//   Haunted House > Setup LIVES + STAMINA + GAME OVER
public static class SetupPhase1
{
    [MenuItem("Haunted House/Setup LIVES + STAMINA + GAME OVER")]
    public static void Run()
    {
        var pc = Object.FindFirstObjectByType<PlayerController>();
        if (pc == null)
        {
            EditorUtility.DisplayDialog("Setup", "No Player (PlayerController) found. Open the House scene first.", "OK");
            return;
        }

        // lives
        var health = pc.GetComponent<PlayerHealth>();
        if (health == null) health = pc.gameObject.AddComponent<PlayerHealth>();
        SetClip(health, "heartbeatClip", "heartbeat_loop");
        SetClip(health, "hurtClip", "player_hurt");

        // HUD
        if (Object.FindFirstObjectByType<PlayerHUD>() == null)
            new GameObject("HUD").AddComponent<PlayerHUD>();

        // capture / death sequence
        var seq = Object.FindFirstObjectByType<GameOverSequence>();
        if (seq == null) seq = new GameObject("GameOverSequence").AddComponent<GameOverSequence>();
        SetClip(seq, "stingClip", "capture_sting");
        SetClip(seq, "deathClip", "death_thud");

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Setup done: PlayerHealth on the Player, HUD, GameOverSequence. Press Ctrl+S.");
    }

    static void SetClip(Object target, string field, string clipName)
    {
        AudioClip clip = null;
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip " + clipName))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) == clipName)
            {
                clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                break;
            }
        }
        if (clip == null)
        {
            Debug.LogWarning("Setup: sound '" + clipName + "' not found in Assets (optional). Drag it into " + field + " later.");
            return;
        }
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p != null)
        {
            p.objectReferenceValue = clip;
            so.ApplyModifiedProperties();
        }
    }
}
#endif