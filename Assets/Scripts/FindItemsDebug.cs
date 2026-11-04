using UnityEngine;
using HauntedItems;

public class FindItemsDebug : MonoBehaviour
{
    public KeyCode teleportKey = KeyCode.F9;
    public KeyCode logKey = KeyCode.F10;
    int index;

    void Update()
    {
        if (Input.GetKeyDown(logKey)) LogAll();
        if (Input.GetKeyDown(teleportKey)) TeleportToNext();
    }

    PickupItem[] All() =>
        FindObjectsByType<PickupItem>(FindObjectsInactive.Include, FindObjectsSortMode.None);

    void LogAll()
    {
        var gun = FindFirstObjectByType<TranquilizerGun>(FindObjectsInactive.Include);
        Debug.Log(gun
            ? $"GUN at {gun.transform.position} (active: {gun.gameObject.activeInHierarchy})"
            : "GUN: wala sa scene");

        foreach (var p in All())
            Debug.Log($"{p.name} at {p.transform.position} (active: {p.gameObject.activeInHierarchy})", p);
    }

    void TeleportToNext()
    {
        var items = All();
        if (items.Length == 0) { Debug.Log("Walang PickupItem sa scene"); return; }

        index = (index + 1) % items.Length;
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) { Debug.Log("Walang object na naka-tag na Player"); return; }

        var cc = player.GetComponent<CharacterController>();
        if (cc) cc.enabled = false;
        player.transform.position = items[index].transform.position + Vector3.up * 1.5f + Vector3.back * 1.5f;
        if (cc) cc.enabled = true;

        Debug.Log("Teleported near " + items[index].name, items[index]);
    }
}