using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class MenuButtonSkin : MonoBehaviour
{
    public string normalPath = "Menu/btn_normal";
    public string highlightPath = "Menu/btn_highlight";
    public string pressedPath = "Menu/btn_pressed";

    void Awake()
    {
        Button button = GetComponent<Button>();
        Image image = GetComponent<Image>();

        Sprite normal = Load(normalPath);
        if (normal == null)
        {
            Debug.LogWarning("Button skin not found: Resources/" + normalPath);
            return;
        }

        Sprite highlight = Load(highlightPath);
        Sprite pressed = Load(pressedPath);

        image.sprite = normal;
        image.type = Image.Type.Simple;
        image.color = Color.white;

        SpriteState state = new SpriteState();
        state.highlightedSprite = highlight != null ? highlight : normal;
        state.pressedSprite = pressed != null ? pressed : normal;
        state.selectedSprite = normal;      // so a clicked button doesn't stay lit
        state.disabledSprite = normal;

        button.transition = Selectable.Transition.SpriteSwap;
        button.spriteState = state;
    }

    Sprite Load(string path)
    {
        Sprite sprite = Resources.Load<Sprite>(path);
        if (sprite != null) return sprite;

        // works even if the file wasn't imported as a Sprite
        Texture2D tex = Resources.Load<Texture2D>(path);
        if (tex != null)
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);

        return null;
    }
}
