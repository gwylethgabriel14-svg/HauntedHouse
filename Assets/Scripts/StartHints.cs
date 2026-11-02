using System.Collections;
using UnityEngine;

public class StartHints : MonoBehaviour
{
    [TextArea(2, 4)]
    public string[] lines =
    {
        "The only way out was to find the four missing pages, uncover the truth behind the house, and escape before she finds you.",
        "But whatever you do...",
        "Don’t let her hear you."
    };

    public float startDelay = 1f;
    public float minSeconds = 2.5f;
    public float secondsPerChar = 0.06f;

    IEnumerator Start()
    {
        yield return new WaitForSeconds(startDelay);

        // no opening lines when the game was loaded from a saved game
        if (SaveSystem.Continued) yield break;

        foreach (string line in lines)
        {
            if (GameManager.Instance == null) yield break;

            float time = Mathf.Max(minSeconds, line.Length * secondsPerChar);
            GameManager.Instance.ShowHint(line, time);
            yield return new WaitForSeconds(time + 0.3f);
        }
    }
}