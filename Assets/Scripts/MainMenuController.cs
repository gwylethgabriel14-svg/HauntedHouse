using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Drives the styled main menu: press any key -> main buttons -> settings panel.
// All panels are CanvasGroups that this component fades in and out.
public class MainMenuController : MonoBehaviour
{
    public enum MenuState { PressKey, Main, Settings }

    [Header("Panels")]
    public CanvasGroup pressKeyPrompt;
    public CanvasGroup mainPanel;
    public CanvasGroup settingsPanel;

    [Header("Presentation")]
    public CanvasGroup titleGroup;
    public TMP_Text promptText;
    public Image brightnessOverlay;
    public float fadeDuration = 0.25f;
    public float titleFadeInTime = 2f;

    [Header("Audio (optional, left empty)")]
    public AudioSource audioSource;
    public AudioClip hoverClip;
    public AudioClip clickClip;

    [Header("Settings widgets")]
    public Slider masterVolumeSlider;
    public Slider mouseSensSlider;
    public Slider brightnessSlider;
    public Toggle fullscreenToggle;

    public MenuState State { get; private set; }

    readonly Dictionary<int, Coroutine> fades = new Dictionary<int, Coroutine>();
    float pulseTime;

    void Awake()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
    }

    void Start()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        ApplySavedSettings();
        HookWidgets();
        HookButtonSounds();

        SetState(MenuState.PressKey);

        if (titleGroup != null) StartCoroutine(FadeTitleIn());
    }

    void Update()
    {
        if (State == MenuState.PressKey)
        {
            if (Input.anyKeyDown) SetState(MenuState.Main);
        }
        else if (State == MenuState.Settings)
        {
            if (Input.GetKeyDown(KeyCode.Escape)) SetState(MenuState.Main);
        }

        PulsePrompt();
    }

    // ------------------------------------------------------------------
    //  State
    // ------------------------------------------------------------------

    public void SetState(MenuState next)
    {
        State = next;
        Show(pressKeyPrompt, next == MenuState.PressKey);
        Show(mainPanel, next == MenuState.Main);
        Show(settingsPanel, next == MenuState.Settings);
    }

    // Button hooks (wired by BuildMainMenu as persistent OnClick listeners)
    public void OpenSettings() { SetState(MenuState.Settings); }

    public void BackToMain() { SetState(MenuState.Main); }

    public void ResetSettings()
    {
        GameSettings.ResetToDefaults();
        ApplySavedSettings();
        SyncWidgets();
    }

    void Show(CanvasGroup group, bool visible)
    {
        if (group == null) return;

        group.interactable = visible;
        group.blocksRaycasts = visible;

        int id = group.GetInstanceID();
        Coroutine running;
        if (fades.TryGetValue(id, out running) && running != null) StopCoroutine(running);

        if (!isActiveAndEnabled || fadeDuration <= 0f)
        {
            group.alpha = visible ? 1f : 0f;
            return;
        }
        fades[id] = StartCoroutine(FadeGroup(group, visible ? 1f : 0f));
    }

    IEnumerator FadeGroup(CanvasGroup group, float target)
    {
        float start = group.alpha;
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(t / fadeDuration));
            yield return null;
        }
        group.alpha = target;
    }

    IEnumerator FadeTitleIn()
    {
        titleGroup.alpha = 0f;
        float t = 0f;
        while (t < titleFadeInTime)
        {
            t += Time.unscaledDeltaTime;
            titleGroup.alpha = Mathf.Clamp01(t / titleFadeInTime);
            yield return null;
        }
        titleGroup.alpha = 1f;
    }

    void PulsePrompt()
    {
        if (promptText == null) return;
        pulseTime += Time.unscaledDeltaTime * 2.4f;
        var c = promptText.color;
        c.a = Mathf.Lerp(0.25f, 1f, 0.5f + 0.5f * Mathf.Sin(pulseTime));
        promptText.color = c;
    }

    // ------------------------------------------------------------------
    //  Settings
    // ------------------------------------------------------------------

    void ApplySavedSettings()
    {
        Screen.fullScreen = GameSettings.Fullscreen == 1;
        AudioListener.volume = GameSettings.MasterVolume;
        ApplyBrightnessToOverlay(GameSettings.Brightness);
    }

    void HookWidgets()
    {
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.minValue = 0f;
            masterVolumeSlider.maxValue = 1f;
            masterVolumeSlider.SetValueWithoutNotify(GameSettings.MasterVolume);
            masterVolumeSlider.onValueChanged.AddListener(OnMasterVolume);
        }
        if (mouseSensSlider != null)
        {
            mouseSensSlider.minValue = GameSettings.MinMouseSens;
            mouseSensSlider.maxValue = GameSettings.MaxMouseSens;
            mouseSensSlider.SetValueWithoutNotify(GameSettings.MouseSens);
            mouseSensSlider.onValueChanged.AddListener(OnMouseSensitivity);
        }
        if (brightnessSlider != null)
        {
            brightnessSlider.minValue = -1f;
            brightnessSlider.maxValue = 1f;
            brightnessSlider.SetValueWithoutNotify(GameSettings.Brightness);
            brightnessSlider.onValueChanged.AddListener(OnBrightness);
        }
        if (fullscreenToggle != null)
        {
            fullscreenToggle.SetIsOnWithoutNotify(Screen.fullScreen);
            fullscreenToggle.onValueChanged.AddListener(OnFullscreen);
        }
    }

    void SyncWidgets()
    {
        if (masterVolumeSlider != null) masterVolumeSlider.SetValueWithoutNotify(GameSettings.MasterVolume);
        if (mouseSensSlider != null) mouseSensSlider.SetValueWithoutNotify(GameSettings.MouseSens);
        if (brightnessSlider != null) brightnessSlider.SetValueWithoutNotify(GameSettings.Brightness);
        if (fullscreenToggle != null) fullscreenToggle.SetIsOnWithoutNotify(Screen.fullScreen);
    }

    void OnMasterVolume(float value)
    {
        GameSettings.MasterVolume = value;
        AudioListener.volume = value;
    }

    void OnMouseSensitivity(float value) { GameSettings.MouseSens = value; }

    void OnBrightness(float value)
    {
        GameSettings.Brightness = value;
        ApplyBrightnessToOverlay(value);
    }

    void OnFullscreen(bool on)
    {
        GameSettings.Fullscreen = on ? 1 : 0;
        Screen.fullScreen = on;
    }

    // The menu has no post-processing volume, so brightness can only dim here
    // (negative values darken the whole screen). Positive values are stored and
    // applied as post-exposure in the House scene by ApplySettings.
    void ApplyBrightnessToOverlay(float brightness)
    {
        if (brightnessOverlay == null) return;
        var c = brightnessOverlay.color;
        c.a = brightness < 0f ? Mathf.Clamp01(-brightness) * 0.75f : 0f;
        brightnessOverlay.color = c;
        brightnessOverlay.enabled = c.a > 0.001f;
    }

    // ------------------------------------------------------------------
    //  Optional UI sound
    // ------------------------------------------------------------------

    void HookButtonSounds()
    {
        foreach (var button in GetComponentsInChildren<Button>(true))
        {
            var btn = button;
            btn.onClick.AddListener(PlayClick);

            var trigger = btn.GetComponent<EventTrigger>();
            if (trigger == null) trigger = btn.gameObject.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            entry.callback.AddListener(_ => PlayHover());
            trigger.triggers.Add(entry);
        }
    }

    public void PlayClick()
    {
        if (audioSource != null && clickClip != null) audioSource.PlayOneShot(clickClip);
    }

    public void PlayHover()
    {
        if (audioSource != null && hoverClip != null) audioSource.PlayOneShot(hoverClip);
    }
}
