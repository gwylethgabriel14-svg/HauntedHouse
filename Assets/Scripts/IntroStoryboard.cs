using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class IntroStoryboard : MonoBehaviour
{
    [System.Serializable]
    public class Slide
    {
        public Sprite image;   // optional, if empty the image is loaded from Resources
        [TextArea(2, 5)] public string[] lines;

        [Header("Sound (optional)")]
        public AudioClip sound;
        public int soundLine;  // plays when this line (0 = first) starts
    }

    [Header("UI")]
    public Image picture;
    public TMP_Text dialogue;
    public Image fade;
    public GameObject continueHint;

    [Header("Story")]
    public Slide[] slides;
    public string nextScene = "House";
    public string imagesFolder = "Intro";   // Assets/Resources/Intro/01, 02, 03...

    [Header("Timing")]
    public float charDelay = 0.04f;
    public float fadeTime = 0.8f;

    [Header("Audio (optional)")]
    public AudioSource source;              // created automatically if left empty
    public AudioClip typeClip;
    [Range(0f, 1f)] public float typeVolume = 0.4f;
    public AudioClip ambience;              // looped under the intro
    [Range(0f, 1f)] public float ambienceVolume = 0.5f;

    [Header("Ambience Fade Out")]
    [Tooltip("Element number where the ambience starts fading out. -1 = never, plays until the end.")]
    public int ambienceStopSlide = 2;
    public float ambienceFadeTime = 1.5f;

    AudioSource ambienceSource;
    bool typing;
    bool skipTyping;
    bool advance;
    bool leaving;

    void Start()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        dialogue.text = "";
        if (continueHint != null) continueHint.SetActive(false);
        SetAlpha(1f);

        if (source == null) source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;

        if (ambience != null)
        {
            ambienceSource = gameObject.AddComponent<AudioSource>();
            ambienceSource.clip = ambience;
            ambienceSource.loop = true;
            ambienceSource.volume = 0f;
            ambienceSource.Play();
            StartCoroutine(FadeAmbience(ambienceVolume, 2f));
        }

        StartCoroutine(Run());
    }

    void Update()
    {
        if (leaving) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Skip();
            return;
        }

        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
        {
            if (typing) skipTyping = true;
            else advance = true;
        }
    }

    IEnumerator Run()
    {
        for (int i = 0; i < slides.Length; i++)
        {
            Slide s = slides[i];
            Sprite img = GetImage(s, i);
            picture.sprite = img;
            picture.enabled = img != null;

            // rain fades out as this slide comes in (we're inside the house now)
            if (ambienceStopSlide >= 0 && i == ambienceStopSlide)
                StartCoroutine(FadeAmbience(0f, ambienceFadeTime));

            yield return Fade(1f, 0f);

            // a slide without lines still waits for a click instead of flashing by
            string[] lines = (s.lines == null || s.lines.Length == 0) ? new[] { "" } : s.lines;

            for (int j = 0; j < lines.Length; j++)
            {
                if (s.sound != null && j == s.soundLine)
                    source.PlayOneShot(s.sound);

                yield return Type(lines[j]);

                if (continueHint != null) continueHint.SetActive(true);
                advance = false;
                while (!advance) yield return null;
                if (continueHint != null) continueHint.SetActive(false);
            }

            yield return Fade(0f, 1f);
            dialogue.text = "";
        }

        yield return FadeAmbience(0f, 0.6f);
        SceneManager.LoadScene(nextScene);
    }

    Sprite GetImage(Slide s, int index)
    {
        if (s.image != null) return s.image;

        string path = imagesFolder + "/" + (index + 1).ToString("00");

        Sprite sprite = Resources.Load<Sprite>(path);
        if (sprite != null) return sprite;

        // works even if the file wasn't imported as a Sprite
        Texture2D tex = Resources.Load<Texture2D>(path);
        if (tex != null)
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));

        Debug.LogWarning("Intro image not found: Resources/" + path);
        return null;
    }

    IEnumerator Type(string line)
    {
        dialogue.text = line;
        dialogue.maxVisibleCharacters = 0;
        dialogue.ForceMeshUpdate();
        int total = dialogue.textInfo.characterCount;

        typing = true;
        skipTyping = false;

        for (int i = 1; i <= total && !skipTyping; i++)
        {
            dialogue.maxVisibleCharacters = i;
            if (typeClip != null && i % 2 == 0)
                source.PlayOneShot(typeClip, typeVolume);
            yield return new WaitForSecondsRealtime(charDelay);
        }

        dialogue.maxVisibleCharacters = total;
        typing = false;
    }

    IEnumerator Fade(float from, float to)
    {
        float t = 0f;
        while (t < fadeTime)
        {
            t += Time.unscaledDeltaTime;
            SetAlpha(Mathf.Lerp(from, to, t / fadeTime));
            yield return null;
        }
        SetAlpha(to);
    }

    IEnumerator FadeAmbience(float to, float time)
    {
        if (ambienceSource == null) yield break;

        float from = ambienceSource.volume;
        float t = 0f;
        while (t < time)
        {
            t += Time.unscaledDeltaTime;
            ambienceSource.volume = Mathf.Lerp(from, to, t / time);
            yield return null;
        }
        ambienceSource.volume = to;
    }

    // hook this to the Skip button's OnClick
    public void Skip()
    {
        if (leaving) return;
        leaving = true;
        StopAllCoroutines();
        StartCoroutine(Leave());
    }

    IEnumerator Leave()
    {
        StartCoroutine(FadeAmbience(0f, fadeTime));
        yield return Fade(fade.color.a, 1f);
        SceneManager.LoadScene(nextScene);
    }

    void SetAlpha(float a)
    {
        Color c = fade.color;
        c.a = a;
        fade.color = c;
    }
}