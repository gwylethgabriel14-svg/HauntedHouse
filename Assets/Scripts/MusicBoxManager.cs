using System.Collections;
using UnityEngine;

// Spawns the music box once the player has found enough pages.
// The editor tool (Haunted House > Create MUSIC BOX) creates this object and fills it in.
public class MusicBoxManager : MonoBehaviour
{
    public GameObject musicBoxPrefab;
    public Transform[] spots;                 // possible places for the music box

    [Header("When")]
    public int triggerPages = 3;              // appears after this many pages
    public float minDistanceFromPlayer = 12f; // never right next to you

    [Header("Extras")]
    public bool lullabyAttractsEnemy = false; // the enemy goes to check the sound once
    public string spawnHint = "A lullaby is playing somewhere in the house... find the music box.";

    public bool Spawned { get; private set; }

    float nextCheck;

    void Update()
    {
        if (Spawned || Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + 0.5f;

        var gm = GameManager.Instance;
        if (gm == null || gm.MusicBoxFound) return;

        int need = Mathf.Min(triggerPages, gm.totalPages);
        if (gm.Pages >= need) Spawn(gm);
    }

    void Spawn(GameManager gm)
    {
        Spawned = true;

        if (musicBoxPrefab == null || spots == null || spots.Length == 0)
        {
            Debug.LogWarning("MusicBoxManager: no prefab or spots set. Run Haunted House > Create MUSIC BOX.");
            return;
        }

        // prefer places that are far from the player
        var player = FindFirstObjectByType<PlayerController>();
        var far = new System.Collections.Generic.List<Transform>();
        foreach (var s in spots)
        {
            if (s == null) continue;
            if (player == null || Vector3.Distance(s.position, player.transform.position) >= minDistanceFromPlayer)
                far.Add(s);
        }
        if (far.Count == 0) foreach (var s in spots) if (s != null) far.Add(s);
        if (far.Count == 0) return;

        Transform spot = far[Random.Range(0, far.Count)];
        Instantiate(musicBoxPrefab, spot.position, spot.rotation);

        StartCoroutine(AnnounceLater(gm, spot.position));
    }

    IEnumerator AnnounceLater(GameManager gm, Vector3 pos)
    {
        yield return new WaitForSeconds(3.5f);   // let the "Page 3/4" message finish first
        if (gm != null && !string.IsNullOrEmpty(spawnHint)) gm.ShowHint(spawnHint, 5f);
        if (lullabyAttractsEnemy && gm != null && gm.enemy != null) gm.enemy.HearNoise(pos);
    }

    // used by the save system
    public void RestoreFromSave(bool found, bool exists, Vector3 pos, Vector3 euler)
    {
        Spawned = found || exists;
        if (exists && musicBoxPrefab != null)
            Instantiate(musicBoxPrefab, pos, Quaternion.Euler(euler));
    }
}