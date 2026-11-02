using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Lives on the Player in the House scene: pushes the saved settings into the game
// (mouse sensitivity, master volume and screen brightness through post-exposure).
[DefaultExecutionOrder(-100)]
public class ApplySettings : MonoBehaviour
{
    [Tooltip("The scene's Global Volume. Left empty it is found automatically.")]
    public Volume volume;

    [Tooltip("How many EV of exposure one unit of brightness is worth (-1..1 => -N..+N EV).")]
    public float exposurePerBrightness = 2f;

    void Start()
    {
        var player = FindFirstObjectByType<PlayerController>();
        if (player != null) player.mouseSensitivity = GameSettings.MouseSens;

        AudioListener.volume = GameSettings.MasterVolume;

        ApplyBrightness(GameSettings.Brightness);
    }

    public void ApplyBrightness(float brightness)
    {
        var vol = volume != null ? volume : FindGlobalVolume();
        if (vol == null || vol.sharedProfile == null)
        {
            Debug.LogWarning("ApplySettings: no Volume with a profile in the scene, brightness was not applied.");
            return;
        }

        // Work on a runtime copy so the profile asset on disk is never touched.
        var profile = Instantiate(vol.sharedProfile);
        profile.name = vol.sharedProfile.name + " (runtime)";
        vol.sharedProfile = profile;

        ColorAdjustments ca;
        if (!profile.TryGet(out ca) || ca == null)
        {
            // Missing on this profile: add a fresh one (owned by the copy).
            ca = profile.Add<ColorAdjustments>(true);
        }
        else
        {
            // Present on the profile: replace it with a copy so the shared
            // component inside the asset is not modified either.
            var copy = Instantiate(ca);
            int index = profile.components.IndexOf(ca);
            if (index >= 0) profile.components[index] = copy;
            ca = copy;
        }

        ca.active = true;
        ca.postExposure.Override(brightness * exposurePerBrightness);
    }

    static Volume FindGlobalVolume()
    {
        var volumes = FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Volume fallback = null;

        foreach (var v in volumes)
        {
            if (!v.isGlobal) continue;
            if (v.name == "Global Volume") return v;   // the one FinishGame created
            if (fallback == null) fallback = v;
        }

        // Last resort: a non-global volume is better than nothing.
        if (fallback == null && volumes.Length > 0) fallback = volumes[0];
        return fallback;
    }
}
