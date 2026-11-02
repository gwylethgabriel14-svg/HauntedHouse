using UnityEngine;

// Saved player settings. Everything lives in PlayerPrefs so it survives a restart.
// Read the properties anywhere; writing them saves immediately.
public static class GameSettings
{
    public const string MasterVolumeKey = "masterVolume";
    public const string MouseSensKey    = "mouseSens";
    public const string BrightnessKey   = "brightness";
    public const string FullscreenKey   = "fullscreen";

    public const float DefaultMasterVolume = 1f;
    public const float DefaultMouseSens    = 2f;
    public const float DefaultBrightness   = 0f;
    public const int   DefaultFullscreen   = 1;

    public const float MinMouseSens = 0.5f;
    public const float MaxMouseSens = 5f;

    // 0 .. 1
    public static float MasterVolume
    {
        get { return Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumeKey, DefaultMasterVolume)); }
        set { PlayerPrefs.SetFloat(MasterVolumeKey, Mathf.Clamp01(value)); PlayerPrefs.Save(); }
    }

    // 0.5 .. 5 (same range as PlayerController.mouseSensitivity is tuned for)
    public static float MouseSens
    {
        get { return Mathf.Clamp(PlayerPrefs.GetFloat(MouseSensKey, DefaultMouseSens), MinMouseSens, MaxMouseSens); }
        set { PlayerPrefs.SetFloat(MouseSensKey, Mathf.Clamp(value, MinMouseSens, MaxMouseSens)); PlayerPrefs.Save(); }
    }

    // -1 .. 1  (0 = the scene's own exposure)
    public static float Brightness
    {
        get { return Mathf.Clamp(PlayerPrefs.GetFloat(BrightnessKey, DefaultBrightness), -1f, 1f); }
        set { PlayerPrefs.SetFloat(BrightnessKey, Mathf.Clamp(value, -1f, 1f)); PlayerPrefs.Save(); }
    }

    // 0 = windowed, 1 = fullscreen
    public static int Fullscreen
    {
        get { return Mathf.Clamp(PlayerPrefs.GetInt(FullscreenKey, DefaultFullscreen), 0, 1); }
        set { PlayerPrefs.SetInt(FullscreenKey, Mathf.Clamp(value, 0, 1)); PlayerPrefs.Save(); }
    }

    public static void ResetToDefaults()
    {
        MasterVolume = DefaultMasterVolume;
        MouseSens    = DefaultMouseSens;
        Brightness   = DefaultBrightness;
        Fullscreen   = DefaultFullscreen;
    }
}
