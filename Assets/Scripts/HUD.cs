using UnityEngine;
using TMPro;

// Shows flashlight battery and bottle count, plus a controls reminder that disappears after a while.
public class HUD : MonoBehaviour
{
    public TMP_Text statusText;
    public TMP_Text controlsText;
    public float controlsSeconds = 20f;

    Flashlight flashlight;
    ThrowItem thrower;
    float elapsed;

    void Start()
    {
        flashlight = FindFirstObjectByType<Flashlight>();
        thrower = FindFirstObjectByType<ThrowItem>();
    }

    void Update()
    {
        elapsed += Time.deltaTime;

        if (controlsText != null && elapsed > controlsSeconds && controlsText.gameObject.activeSelf)
            controlsText.gameObject.SetActive(false);

        if (statusText == null) return;

        int battery = flashlight != null ? Mathf.CeilToInt(flashlight.Battery) : 0;
        int bottles = thrower != null ? thrower.bottles : 0;

        statusText.text = "Battery: " + battery + "%    Bottles: " + bottles;
        statusText.color = battery <= 15 ? new Color(1f, 0.3f, 0.3f) : Color.white;
    }
}
