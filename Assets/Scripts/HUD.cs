using UnityEngine;
using TMPro;

// Controls reminder lang ang hawak nito ngayon.
//  - Battery ay nasa PlayerHUD na (kaliwang taas, sa ilalim ng stamina).
//  - Bottles ay nasa hotbar na (Inventory).
public class HUD : MonoBehaviour
{
    public TMP_Text statusText;      // hindi na ginagamit, tinatago na lang
    public TMP_Text controlsText;
    public float controlsSeconds = 20f;

    [Header("Controls text")]
    public bool controlsTopRight = true;                       // para hindi matakpan ng hotbar
    public Vector2 controlsMargin = new Vector2(30f, 30f);

    float elapsed;

    void Start()
    {
        if (statusText != null) statusText.gameObject.SetActive(false);

        if (controlsText != null && controlsTopRight)
        {
            var rt = controlsText.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-controlsMargin.x, -controlsMargin.y);
            rt.sizeDelta = new Vector2(1100f, 40f);
            controlsText.alignment = TextAlignmentOptions.TopRight;
            controlsText.textWrappingMode = TextWrappingModes.NoWrap;
        }
    }

    void Update()
    {
        elapsed += Time.deltaTime;

        if (controlsText != null && elapsed > controlsSeconds && controlsText.gameObject.activeSelf)
            controlsText.gameObject.SetActive(false);
    }
}