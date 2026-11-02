using UnityEngine;
using System.Collections.Generic;

public class PageSpawner : MonoBehaviour
{
    public GameObject pagePrefab;
    public Transform[] candidateSpots;   // about 8-10 spots
    public int pagesToSpawn = 4;

    void Start()
    {
        // when continuing a saved game the save system puts the remaining pages back
        if (SaveSystem.LoadingFromSave) return;

        var spots = new List<Transform>(candidateSpots);

        for (int i = 0; i < pagesToSpawn && spots.Count > 0; i++)
        {
            int r = Random.Range(0, spots.Count);
            Instantiate(pagePrefab, spots[r].position, spots[r].rotation);
            spots.RemoveAt(r);
        }
    }
}