using UnityEngine;

public class ThrowItem : MonoBehaviour
{
    public GameObject bottlePrefab;
    public Transform cam;
    public float throwForce = 12f;
    public int bottles = 1;

    void Update()
    {
        var pc = GetComponent<PlayerController>();
        if (pc != null && pc.IsHidden)
            return;

        if (!Input.GetKeyDown(KeyCode.G) || bottles <= 0) return;

        bottles--;
        var b = Instantiate(bottlePrefab, cam.position + cam.forward * 0.6f, cam.rotation);
        ItemPickup.EnsureBottleVisual(b);
        var rb = b.GetComponent<Rigidbody>();
        if (rb != null)
            rb.AddForce(cam.forward * throwForce, ForceMode.VelocityChange);
    }
}
