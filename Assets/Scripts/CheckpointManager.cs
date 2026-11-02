using System.Collections;
using UnityEngine;

[DefaultExecutionOrder(-200)]
public class CheckpointManager : MonoBehaviour
{
    public float checkInterval = 0.5f;

    bool wasContinue;
    int restoredPages;
    bool restoredKey;
    int spawnedAtStart;
    int lastSavedPages = -1;
    bool lastSavedKey;

    void Awake()
    {
        wasContinue = SaveGame.loadRequested && SaveGame.Exists;
        SaveGame.continued = wasContinue;
        SaveGame.loadRequested = false;
        if (!wasContinue) return;

        restoredPages = SaveGame.Pages;
        restoredKey = SaveGame.HasKey;

        // some pages are already collected, so fewer need to spawn
        PageSpawner spawner = FindFirstObjectByType<PageSpawner>();
        if (spawner != null)
            spawner.pagesToSpawn = Mathf.Max(0, spawner.pagesToSpawn - restoredPages);
    }

    IEnumerator Start()
    {
        yield return null;   // let PageSpawner and GameManager finish their Start

        GameManager gm = GameManager.Instance;
        if (gm == null) yield break;

        spawnedAtStart = FindObjectsByType<Page>(FindObjectsSortMode.None).Length;

        if (wasContinue) Restore(gm);

        WaitForSeconds wait = new WaitForSeconds(checkInterval);
        while (true)
        {
            yield return wait;

            if (gm.winPanel != null && gm.winPanel.activeInHierarchy)
            {
                SaveGame.Clear();   // finished the game, nothing left to continue
                yield break;
            }

            Track(gm);
        }
    }

    void Restore(GameManager gm)
    {
        // replays the page pickups so the counter, enemy speed and barricades match
        for (int i = 0; i < restoredPages; i++) gm.CollectPage();

        if (restoredKey)
        {
            foreach (KeyItem k in FindObjectsByType<KeyItem>(FindObjectsSortMode.None))
                Destroy(k.gameObject);
            gm.CollectKey();
        }

        Vector3 pos;
        float yaw;
        if (!SaveGame.TryGetPosition(out pos, out yaw)) return;

        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player == null) return;

        CharacterController cc = player.GetComponent<CharacterController>();
        cc.enabled = false;
        player.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
        cc.enabled = true;
    }

    void Track(GameManager gm)
    {
        int remaining = FindObjectsByType<Page>(FindObjectsSortMode.None).Length;
        int collected = restoredPages + (spawnedAtStart - remaining);
        bool hasKey = gm.HasKey;

        if (collected <= 0 && !hasKey) return;
        if (collected == lastSavedPages && hasKey == lastSavedKey) return;

        lastSavedPages = collected;
        lastSavedKey = hasKey;
        SaveGame.Save(collected, hasKey);
    }
}
