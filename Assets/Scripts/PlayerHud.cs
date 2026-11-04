using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Lives (hearts), health bar, stamina bar, battery text, and the BLOOD on the screen when you are nearly dead.
// Everything is built by the script. Put it on an empty GameObject called "HUD" in the House scene
// (Haunted House > Setup LIVES + STAMINA + GAME OVER does this for you).
//
// Top-left, from top to bottom:  hearts, health bar, stamina bar, battery.
//
// BLOOD: nothing at 5, 4 or 3 lives. At 2 lives dark blood creeps in from the edges of the screen
// and a few drips run down. At 1 life it covers more, pulses, and many drips run down.
public class PlayerHUD : MonoBehaviour
{
    [Header("Position (from the top-left corner)")]
    public Vector2 position = new Vector2(60f, 50f);

    [Header("Look")]
    public TMP_FontAsset font;
    public Color lifeColor = new Color(0.62f, 0.04f, 0.05f, 1f);
    public Color lostLifeColor = new Color(0.16f, 0.14f, 0.14f, 0.65f);
    public Color healthColor = new Color(0.80f, 0.10f, 0.08f, 0.95f);
    public Color staminaColor = new Color(0.82f, 0.78f, 0.68f, 0.9f);
    public Color staminaLowColor = new Color(0.65f, 0.05f, 0.05f, 0.95f);
    public Color batteryColor = Color.white;
    public Color batteryLowColor = new Color(1f, 0.3f, 0.3f, 1f);

    [Header("Blood on the screen")]
    public int bloodStartsAtLives = 2;                      // blood appears at this many lives or fewer
    public Color bloodColor = new Color(0.30f, 0.01f, 0.02f, 1f);   // dark, old blood (not bright red)
    [Range(0f, 1f)] public float bloodMaxAlpha = 0.92f;
    public int dripCount = 10;                              // most drips running at once (at 1 life)
    public float dripSpeed = 1f;                            // 1 = normal, 2 = twice as fast
    [Range(0f, 0.6f)] public float hitFlashAlpha = 0.22f;   // quick dark flash when you are hit (0 = none)

    [Header("Testing (turn off before you build the game)")]
    public bool testKeys = false;                           // K = take one hit, L = gain a life

    // ------------------------------------------------------------------ internals

    class Drip
    {
        public RectTransform streak, bulb;
        public Image streakImg;
        public RawImage bulbImg;
        public float x, width, len, maxLen, speed, delay, hold, alpha;
        public int state;   // 0 waiting, 1 running down, 2 hanging, 3 fading
    }

    PlayerHealth health;
    PlayerController player;
    Flashlight flashlight;

    Canvas canvas;
    RawImage blood;
    Image flash;
    RawImage[] drops;
    Vector2[] dropBase;
    float[] shakeUntil;
    Image staminaFill, staminaBack;
    Image healthFill, healthBack;
    TMP_Text staminaLabel;
    TMP_Text batteryLabel;
    RectTransform dripRoot;
    readonly List<Drip> drips = new List<Drip>();

    float flashAlpha;
    float staminaShown = 1f;
    float healthShown = 1f;
    float barAlpha = 0.2f;
    float bloodShown;
    int lastLives;
    int lastBattery = -1;

    void Start()
    {
        health = FindFirstObjectByType<PlayerHealth>();
        player = FindFirstObjectByType<PlayerController>();
        flashlight = FindFirstObjectByType<Flashlight>();
        if (health == null) Debug.LogWarning("PlayerHUD: no PlayerHealth found on the Player.");

        Build();

        if (health != null)
        {
            lastLives = health.Lives;
            health.LivesChanged += OnLivesChanged;
            health.Damaged += OnDamaged;
        }
    }

    void OnDestroy()
    {
        if (health != null)
        {
            health.LivesChanged -= OnLivesChanged;
            health.Damaged -= OnDamaged;
        }
    }

    // ------------------------------------------------------------------ build

    void Build()
    {
        var cgo = new GameObject("HUDCanvas");
        cgo.transform.SetParent(transform, false);
        canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;
        var scaler = cgo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // blood creeping in from the edges of the screen
        var bg = new GameObject("Blood", typeof(RectTransform), typeof(RawImage));
        bg.transform.SetParent(cgo.transform, false);
        Stretch(bg.GetComponent<RectTransform>());
        blood = bg.GetComponent<RawImage>();
        blood.texture = MakeBloodTexture();
        blood.raycastTarget = false;
        blood.color = new Color(bloodColor.r, bloodColor.g, bloodColor.b, 0f);

        // drips running down from the top edge
        var dr = new GameObject("Drips", typeof(RectTransform));
        dr.transform.SetParent(cgo.transform, false);
        dripRoot = dr.GetComponent<RectTransform>();
        Stretch(dripRoot);
        BuildDrips();

        // quick dark flash when hit
        var fl = new GameObject("Flash", typeof(RectTransform), typeof(Image));
        fl.transform.SetParent(cgo.transform, false);
        Stretch(fl.GetComponent<RectTransform>());
        flash = fl.GetComponent<Image>();
        flash.raycastTarget = false;
        flash.color = new Color(0.25f, 0f, 0f, 0f);

        // top-left corner block (positions below are measured from the bottom-left of this block)
        var block = new GameObject("Block", typeof(RectTransform));
        block.transform.SetParent(cgo.transform, false);
        var br = block.GetComponent<RectTransform>();
        br.anchorMin = br.anchorMax = new Vector2(0f, 1f);
        br.pivot = new Vector2(0f, 1f);
        br.anchoredPosition = new Vector2(position.x, -position.y);
        br.sizeDelta = new Vector2(400f, 156f);

        // lives: one heart each (top row)
        int count = health != null ? health.maxLives : 5;
        drops = new RawImage[count];
        dropBase = new Vector2[count];
        shakeUntil = new float[count];
        Texture2D heartTex = MakeHeartTexture();
        for (int i = 0; i < count; i++)
        {
            var d = new GameObject("Life_" + (i + 1), typeof(RectTransform), typeof(RawImage));
            d.transform.SetParent(block.transform, false);
            var r = d.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = Vector2.zero;
            r.pivot = new Vector2(0f, 0f);
            r.sizeDelta = new Vector2(40f, 40f);
            dropBase[i] = new Vector2(i * 46f, 88f);
            r.anchoredPosition = dropBase[i];
            var raw = d.GetComponent<RawImage>();
            raw.texture = heartTex;
            raw.raycastTarget = false;
            raw.color = lifeColor;
            drops[i] = raw;
        }

        Sprite grain = MakeGrainSprite();

        // health bar (under the hearts)
        var hback = new GameObject("HealthBack", typeof(RectTransform), typeof(Image));
        hback.transform.SetParent(block.transform, false);
        var hbk = hback.GetComponent<RectTransform>();
        hbk.anchorMin = hbk.anchorMax = Vector2.zero;
        hbk.pivot = Vector2.zero;
        hbk.anchoredPosition = new Vector2(0f, 66f);
        hbk.sizeDelta = new Vector2(260f, 14f);
        healthBack = hback.GetComponent<Image>();
        healthBack.color = new Color(0f, 0f, 0f, 0.55f);
        healthBack.raycastTarget = false;

        var hfill = new GameObject("HealthFill", typeof(RectTransform), typeof(Image));
        hfill.transform.SetParent(hback.transform, false);
        var hfr = hfill.GetComponent<RectTransform>();
        hfr.anchorMin = Vector2.zero; hfr.anchorMax = Vector2.one;
        hfr.offsetMin = new Vector2(2f, 2f); hfr.offsetMax = new Vector2(-2f, -2f);
        healthFill = hfill.GetComponent<Image>();
        healthFill.sprite = grain;
        healthFill.type = Image.Type.Filled;
        healthFill.fillMethod = Image.FillMethod.Horizontal;
        healthFill.fillOrigin = 0;
        healthFill.fillAmount = 1f;
        healthFill.color = healthColor;
        healthFill.raycastTarget = false;

        // stamina bar (dark back, grainy fill)
        var back = new GameObject("StaminaBack", typeof(RectTransform), typeof(Image));
        back.transform.SetParent(block.transform, false);
        var bk = back.GetComponent<RectTransform>();
        bk.anchorMin = bk.anchorMax = Vector2.zero;
        bk.pivot = Vector2.zero;
        bk.anchoredPosition = new Vector2(0f, 46f);
        bk.sizeDelta = new Vector2(260f, 12f);
        staminaBack = back.GetComponent<Image>();
        staminaBack.color = new Color(0f, 0f, 0f, 0.55f);
        staminaBack.raycastTarget = false;

        var fill = new GameObject("StaminaFill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(back.transform, false);
        var fr = fill.GetComponent<RectTransform>();
        fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one;
        fr.offsetMin = new Vector2(2f, 2f); fr.offsetMax = new Vector2(-2f, -2f);
        staminaFill = fill.GetComponent<Image>();
        staminaFill.sprite = grain;
        staminaFill.type = Image.Type.Filled;
        staminaFill.fillMethod = Image.FillMethod.Horizontal;
        staminaFill.fillOrigin = 0;
        staminaFill.fillAmount = 1f;
        staminaFill.color = staminaColor;
        staminaFill.raycastTarget = false;

        var label = new GameObject("StaminaLabel", typeof(RectTransform));
        label.transform.SetParent(block.transform, false);
        var lr = label.GetComponent<RectTransform>();
        lr.anchorMin = lr.anchorMax = Vector2.zero;
        lr.pivot = Vector2.zero;
        lr.anchoredPosition = new Vector2(0f, 26f);
        lr.sizeDelta = new Vector2(260f, 20f);
        staminaLabel = label.AddComponent<TextMeshProUGUI>();
        if (font != null) staminaLabel.font = font;
        staminaLabel.text = "STAMINA";
        staminaLabel.fontSize = 15f;
        staminaLabel.characterSpacing = 8f;
        staminaLabel.alignment = TextAlignmentOptions.BottomLeft;
        staminaLabel.color = new Color(staminaColor.r, staminaColor.g, staminaColor.b, 0.55f);
        staminaLabel.raycastTarget = false;
        staminaLabel.textWrappingMode = TextWrappingModes.NoWrap;

        // battery text (under the stamina bar)
        var bat = new GameObject("BatteryLabel", typeof(RectTransform));
        bat.transform.SetParent(block.transform, false);
        var btr = bat.GetComponent<RectTransform>();
        btr.anchorMin = btr.anchorMax = Vector2.zero;
        btr.pivot = Vector2.zero;
        btr.anchoredPosition = new Vector2(0f, 0f);
        btr.sizeDelta = new Vector2(260f, 24f);
        batteryLabel = bat.AddComponent<TextMeshProUGUI>();
        if (font != null) batteryLabel.font = font;
        batteryLabel.text = "Battery: 100%";
        batteryLabel.fontSize = 20f;
        batteryLabel.alignment = TextAlignmentOptions.MidlineLeft;
        batteryLabel.color = batteryColor;
        batteryLabel.raycastTarget = false;
        batteryLabel.textWrappingMode = TextWrappingModes.NoWrap;
    }

    void BuildDrips()
    {
        Texture2D bulbTex = MakeDropTexture();
        for (int i = 0; i < dripCount; i++)
        {
            var d = new Drip();

            var s = new GameObject("DripStreak", typeof(RectTransform), typeof(Image));
            s.transform.SetParent(dripRoot, false);
            d.streak = s.GetComponent<RectTransform>();
            d.streak.anchorMin = d.streak.anchorMax = new Vector2(0f, 1f);
            d.streak.pivot = new Vector2(0.5f, 1f);
            d.streakImg = s.GetComponent<Image>();
            d.streakImg.raycastTarget = false;

            var b = new GameObject("DripBulb", typeof(RectTransform), typeof(RawImage));
            b.transform.SetParent(dripRoot, false);
            d.bulb = b.GetComponent<RectTransform>();
            d.bulb.anchorMin = d.bulb.anchorMax = new Vector2(0f, 1f);
            d.bulb.pivot = new Vector2(0.5f, 0.7f);
            d.bulbImg = b.GetComponent<RawImage>();
            d.bulbImg.texture = bulbTex;
            d.bulbImg.raycastTarget = false;

            ResetDrip(d, true);
            drips.Add(d);
        }
    }

    void ResetDrip(Drip d, bool first)
    {
        d.x = Random.Range(40f, 1880f);
        d.width = Random.Range(5f, 13f);
        d.maxLen = Random.Range(110f, 460f);
        d.speed = Random.Range(14f, 38f);
        d.len = 0f;
        d.alpha = 0f;
        d.state = 0;
        d.delay = first ? Random.Range(0f, 3f) : Random.Range(0.5f, 4f);
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    // ------------------------------------------------------------------ events

    void OnLivesChanged(int lives, int max)
    {
        if (lives < lastLives)
        {
            // the heart that was just lost shakes
            for (int i = lives; i < lastLives && i < shakeUntil.Length; i++)
                shakeUntil[i] = Time.unscaledTime + 0.45f;
        }
        lastLives = lives;
    }

    void OnDamaged()
    {
        flashAlpha = hitFlashAlpha;
    }

    // ------------------------------------------------------------------ update

    void Update()
    {
        // hide everything on the game over / capture screens
        bool ended = GameManager.Instance != null && GameManager.Instance.IsEnded;
        canvas.enabled = !ended;
        if (ended) return;

        if (testKeys && health != null)
        {
            if (Input.GetKeyDown(KeyCode.K)) health.TakeDamage(1);
            if (Input.GetKeyDown(KeyCode.L)) health.Heal(1);
        }

        UpdateLives();
        UpdateHealth();
        UpdateStamina();
        UpdateBattery();
        UpdateBlood();
    }

    void UpdateLives()
    {
        if (health == null) return;
        for (int i = 0; i < drops.Length; i++)
        {
            bool alive = i < health.Lives;
            drops[i].color = alive ? lifeColor : lostLifeColor;

            var rt = drops[i].rectTransform;
            Vector2 p = dropBase[i];
            if (Time.unscaledTime < shakeUntil[i])
                p += new Vector2(Random.Range(-4f, 4f), Random.Range(-4f, 4f));
            rt.anchoredPosition = p;

            // the last heart throbs
            float s = (alive && health.Lives == 1) ? 1f + 0.12f * Mathf.Sin(Time.unscaledTime * 8f) : 1f;
            rt.localScale = new Vector3(s, s, 1f);
        }
    }

    void UpdateHealth()
    {
        if (health == null) return;

        float target = health.maxHealth > 0f ? health.Health / health.maxHealth : 1f;
        healthShown = Mathf.MoveTowards(healthShown, target, Time.unscaledDeltaTime * 1.5f);
        healthFill.fillAmount = healthShown;

        Color c = healthColor;
        if (target < 0.3f)
            c.a *= 0.7f + 0.3f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 7f));   // flickers when low
        healthFill.color = c;
    }

    void UpdateStamina()
    {
        if (player == null) return;

        float target = player.maxStamina > 0f ? player.Stamina / player.maxStamina : 1f;
        staminaShown = Mathf.MoveTowards(staminaShown, target, Time.unscaledDeltaTime * 2.5f);
        staminaFill.fillAmount = staminaShown;

        bool low = target < 0.25f || player.IsExhausted;
        Color c = low ? staminaLowColor : staminaColor;
        if (player.IsExhausted)
            c.a *= 0.65f + 0.35f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 9f));   // flickers when empty
        staminaFill.color = c;

        // fades away when full
        bool show = target < 0.995f || player.IsSprinting;
        barAlpha = Mathf.MoveTowards(barAlpha, show ? 1f : 0.2f, Time.unscaledDeltaTime * 2f);
        staminaBack.color = new Color(0f, 0f, 0f, 0.55f * barAlpha);
        var fc = staminaFill.color; fc.a *= barAlpha; staminaFill.color = fc;
        var lc = staminaLabel.color; lc.a = 0.55f * barAlpha; staminaLabel.color = lc;
    }

    void UpdateBattery()
    {
        if (batteryLabel == null) return;

        int battery = flashlight != null ? Mathf.CeilToInt(flashlight.Battery) : 0;
        if (battery != lastBattery)
        {
            lastBattery = battery;
            batteryLabel.text = "Battery: " + battery + "%";
        }
        batteryLabel.color = battery <= 15 ? batteryLowColor : batteryColor;
    }

    void UpdateBlood()
    {
        // how much blood: none above the limit, medium at 2 lives, full at 1 (and 0)
        float level = 0f;
        int lives = health != null ? health.Lives : 99;
        if (lives <= bloodStartsAtLives)
        {
            float t = (bloodStartsAtLives - lives) / (float)Mathf.Max(1, bloodStartsAtLives - 1);
            level = Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(t));
        }

        // it creeps in and out slowly, like it is spreading
        bloodShown = Mathf.MoveTowards(bloodShown, level, Time.unscaledDeltaTime * 0.7f);

        float a = bloodMaxAlpha * bloodShown;
        if (lives <= 1 && bloodShown > 0.9f)
            a *= 0.88f + 0.12f * Mathf.Sin(Time.unscaledTime * 6f);           // pulses with the heartbeat
        blood.color = new Color(bloodColor.r, bloodColor.g, bloodColor.b, Mathf.Clamp01(a));

        UpdateDrips(lives);

        flashAlpha = Mathf.MoveTowards(flashAlpha, 0f, Time.unscaledDeltaTime * 1.6f);
        flash.color = new Color(0.25f, 0f, 0f, flashAlpha);
    }

    void UpdateDrips(int lives)
    {
        // 4 drips at 2 lives, all of them at 1 life, none above the limit
        int active = 0;
        if (bloodShown > 0.05f)
            active = lives <= 1 ? drips.Count : Mathf.Min(4, drips.Count);

        float dt = Time.unscaledDeltaTime;
        for (int i = 0; i < drips.Count; i++)
        {
            var d = drips[i];
            bool allowed = i < active;

            switch (d.state)
            {
                case 0:   // waiting for its turn
                    d.delay -= dt;
                    if (d.delay <= 0f && allowed) { d.state = 1; d.alpha = 1f; }
                    break;

                case 1:   // running down, speeding up as it goes
                    d.len += d.speed * dripSpeed * (1f + d.len / 150f) * dt;
                    if (d.len >= d.maxLen) { d.state = 2; d.hold = Random.Range(1.5f, 4f); }
                    break;

                case 2:   // hanging at the end
                    d.hold -= dt;
                    if (d.hold <= 0f || !allowed) d.state = 3;
                    break;

                case 3:   // fading away
                    d.alpha -= dt * 0.6f;
                    if (d.alpha <= 0f) ResetDrip(d, false);
                    break;
            }

            float vis = d.alpha * Mathf.Clamp01(bloodShown * 1.6f);
            Color c = new Color(bloodColor.r * 0.9f, bloodColor.g, bloodColor.b, 0.92f * vis);

            d.streak.anchoredPosition = new Vector2(d.x, 0f);
            d.streak.sizeDelta = new Vector2(d.width * 0.55f, d.len);
            d.streakImg.color = c;

            float bs = d.width * 2.6f;
            d.bulb.sizeDelta = new Vector2(bs, bs);
            d.bulb.anchoredPosition = new Vector2(d.x, -d.len);
            d.bulbImg.color = c;
        }
    }

    // ------------------------------------------------------------------ generated textures

    // the heart used for lives
    static Texture2D MakeHeartTexture()
    {
        const int N = 64;
        var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float cov = 0f;
                for (int sy = 0; sy < 3; sy++)
                    for (int sx = 0; sx < 3; sx++)
                    {
                        float u = (x + (sx + 0.5f) / 3f) / N;
                        float v = (y + (sy + 0.5f) / 3f) / N;
                        if (InHeart(u, v)) cov += 1f / 9f;
                    }
                // a little shine on the upper left so it looks wet
                float du = x / (float)N - 0.34f, dv = y / (float)N - 0.66f;
                float shine = Mathf.Clamp01(1f - Mathf.Sqrt(du * du + dv * dv) * 7f) * 0.45f;
                float shade = 0.72f + 0.2f * Mathf.Clamp01((x / (float)N) * 0.4f + (y / (float)N) * 0.6f) + shine;
                t.SetPixel(x, y, new Color(shade, shade, shade, cov));
            }
        t.Apply();
        return t;
    }

    static bool InHeart(float u, float v)
    {
        // classic heart curve: (x^2 + y^2 - 1)^3 - x^2 * y^3 <= 0, point at the bottom
        float x = (u - 0.5f) * 2.5f;
        float y = (v - 0.5f) * 2.6f + 0.15f;
        float a = x * x + y * y - 1f;
        return a * a * a - x * x * y * y * y <= 0f;
    }

    // the little blood drop used at the end of each drip
    static Texture2D MakeDropTexture()
    {
        const int N = 64;
        var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float cov = 0f;
                for (int sy = 0; sy < 3; sy++)
                    for (int sx = 0; sx < 3; sx++)
                    {
                        float u = (x + (sx + 0.5f) / 3f) / N;
                        float v = (y + (sy + 0.5f) / 3f) / N;
                        if (InDrop(u, v)) cov += 1f / 9f;
                    }
                // a little shine on the upper left so it looks wet
                float du = x / (float)N - 0.38f, dv = y / (float)N - 0.45f;
                float shine = Mathf.Clamp01(1f - Mathf.Sqrt(du * du + dv * dv) * 7f) * 0.45f;
                float shade = 0.72f + 0.2f * Mathf.Clamp01((x / (float)N) * 0.4f + (y / (float)N) * 0.6f) + shine;
                t.SetPixel(x, y, new Color(shade, shade, shade, cov));
            }
        t.Apply();
        return t;
    }

    static bool InDrop(float u, float v)
    {
        // circle at the bottom, point at the top
        float dx = u - 0.5f;
        if (dx * dx + (v - 0.34f) * (v - 0.34f) <= 0.27f * 0.27f) return true;
        if (v > 0.34f && v < 0.95f)
        {
            float half = 0.27f * (0.95f - v) / (0.95f - 0.34f);
            if (Mathf.Abs(dx) <= half) return true;
        }
        return false;
    }

    static float Fbm(float x, float y)
    {
        return Mathf.PerlinNoise(x, y) * 0.55f
             + Mathf.PerlinNoise(x * 2.1f + 7.3f, y * 2.1f + 1.9f) * 0.30f
             + Mathf.PerlinNoise(x * 4.3f + 3.1f, y * 4.3f + 9.7f) * 0.15f;
    }

    // blood that clings to the edges of the screen: ragged inside border, thick clots and splatters
    static Texture2D MakeBloodTexture()
    {
        const int N = 384;
        var rng = new System.Random(2718);

        // random splatters near the edges
        const int splats = 90;
        var sx = new float[splats]; var sy = new float[splats]; var sr = new float[splats];
        for (int i = 0; i < splats; i++)
        {
            float u = (float)rng.NextDouble(), v = (float)rng.NextDouble();
            float depth = Mathf.Pow((float)rng.NextDouble(), 2f) * 0.30f;
            switch (rng.Next(4))
            {
                case 0: sx[i] = u; sy[i] = depth; break;
                case 1: sx[i] = u; sy[i] = 1f - depth; break;
                case 2: sx[i] = depth; sy[i] = v; break;
                default: sx[i] = 1f - depth; sy[i] = v; break;
            }
            sr[i] = 0.004f + Mathf.Pow((float)rng.NextDouble(), 3f) * 0.03f;
        }

        var px = new Color32[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = x / (float)(N - 1), v = y / (float)(N - 1);
                float d = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));   // 0 at the edge

                float n = Fbm(u * 3.5f + 11f, v * 3.5f + 5f);
                float cover = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.03f, 0.19f, d + (n - 0.5f) * 0.20f));

                if (d < 0.36f)
                    for (int i = 0; i < splats; i++)
                    {
                        float dx = u - sx[i], dy = v - sy[i];
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);
                        if (dist < sr[i])
                            cover = Mathf.Max(cover, 1f - Mathf.SmoothStep(sr[i] * 0.65f, sr[i], dist));
                    }

                // darker clotted patches and lighter wet streaks
                float n2 = Fbm(u * 12f + 40f, v * 12f + 20f);
                float shade = 0.62f + 0.38f * n2;
                float alpha = Mathf.Clamp01(cover) * (0.78f + 0.22f * n);

                byte g = (byte)Mathf.Clamp(shade * 255f, 0f, 255f);
                px[y * N + x] = new Color32(g, g, g, (byte)(alpha * 255f));
            }

        var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        t.SetPixels32(px);
        t.Apply();
        return t;
    }

    static Sprite MakeGrainSprite()
    {
        const int W = 128, H = 16;
        var t = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float g = 0.72f + 0.28f * Mathf.PerlinNoise(x * 0.18f, y * 0.9f + 3f);
                g *= 0.92f + 0.08f * Random.value;
                t.SetPixel(x, y, new Color(g, g, g, 1f));
            }
        t.Apply();
        return Sprite.Create(t, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
    }
}