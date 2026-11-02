using UnityEngine;

public class ThrowItem : MonoBehaviour
{
    public GameObject bottlePrefab;
    public Transform cam;
    public float throwForce = 12f;
    public int bottles = 1;

    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.G) || bottles <= 0) return;

        bottles--;
        var b = Instantiate(bottlePrefab, cam.position + cam.forward * 0.6f, cam.rotation);
        b.GetComponent<Rigidbody>().AddForce(cam.forward * throwForce, ForceMode.VelocityChange);
    }
}
