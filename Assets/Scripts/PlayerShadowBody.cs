using UnityEngine;

// Put this on the "ShadowBody" capsule (a child of the Player).
// The capsule only casts a shadow (Shadows Only). It shrinks when the player crouches,
// with its feet staying on the floor.
public class PlayerShadowBody : MonoBehaviour
{
    CharacterController cc;
    float standHeight;
    float baseScaleY;
    float bottomLocalY;

    void Start()
    {
        cc = GetComponentInParent<CharacterController>();
        if (cc == null) { enabled = false; return; }

        standHeight = cc.height;
        baseScaleY = transform.localScale.y;
        bottomLocalY = transform.localPosition.y - baseScaleY;   // a Unity capsule is 2 units tall, so half = scale.y
    }

    void LateUpdate()
    {
        float ratio = cc.height / standHeight;
        float sy = baseScaleY * ratio;

        var s = transform.localScale;
        transform.localScale = new Vector3(s.x, sy, s.z);

        var p = transform.localPosition;
        transform.localPosition = new Vector3(p.x, bottomLocalY + sy, p.z);
    }
}
