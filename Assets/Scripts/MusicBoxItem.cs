using UnityEngine;

// The music box the player has to find. Put on the MusicBox prefab (the editor tool does this).
public class MusicBoxItem : MonoBehaviour, IInteractable
{
    public string Prompt => "Take the music box";

    [Header("Look")]
    public Transform platter;            // the little record that spins
    public float spinSpeed = 25f;
    public Light glow;                   // soft light so it can be seen in the dark
    [Range(0f, 1f)] public float glowPulse = 0.25f;

    [Header("Audio (optional)")]
    public AudioClip pickupSound;
    [Range(0f, 1f)] public float pickupVolume = 0.8f;

    float baseIntensity;

    void Start()
    {
        if (glow != null) baseIntensity = glow.intensity;
    }

    void Update()
    {
        if (platter != null)
            platter.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.Self);

        if (glow != null)
            glow.intensity = baseIntensity * (1f + glowPulse * Mathf.Sin(Time.time * 2.2f));
    }

    public void Interact(PlayerController player)
    {
        if (pickupSound != null)
            AudioSource.PlayClipAtPoint(pickupSound, transform.position, pickupVolume);

        if (GameManager.Instance != null)
            GameManager.Instance.CollectMusicBox();

        Destroy(gameObject);
    }
}