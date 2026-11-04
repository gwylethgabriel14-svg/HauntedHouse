using UnityEngine;

// G = itapon ang HAWAK mong bote (dapat napili ang Bottle sa hotbar).
public class ThrowItem : MonoBehaviour
{
    public GameObject bottlePrefab;
    public Transform cam;
    public float throwForce = 12f;
    public int bottles = 1;          // panimulang bote pag nagsimula ang laro
    public Sprite bottleIcon;        // optional, icon sa hotbar

    void Start()
    {
        // ilagay ang panimulang bote sa hotbar
        if (Inventory.Instance != null && bottles > 0)
            Inventory.Instance.AddItem("Bottle", bottleIcon, null, true, bottles);
    }

    void Update()
    {
        var pc = GetComponent<PlayerController>();
        if (pc != null && pc.IsHidden)
            return;

        if (!Input.GetKeyDown(KeyCode.G)) return;

        var inv = Inventory.Instance;
        if (inv != null)
        {
            if (!inv.IsHolding("Bottle")) return;   // kailangang hawak mo ang bote
            inv.RemoveSelected();
        }
        else
        {
            // walang Inventory sa scene: dating gawi
            if (bottles <= 0) return;
            bottles--;
        }

        var b = Instantiate(bottlePrefab, cam.position + cam.forward * 0.6f, cam.rotation);
        ItemPickup.EnsureBottleVisual(b);
        var rb = b.GetComponent<Rigidbody>();
        if (rb != null)
            rb.AddForce(cam.forward * throwForce, ForceMode.VelocityChange);
    }
}