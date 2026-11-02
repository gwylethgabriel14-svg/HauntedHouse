using UnityEngine;

public class ItemPickup : MonoBehaviour, IInteractable
{
    public enum Type { Battery, Bottle }
    public Type type;
    public float batteryAmount = 40f;

    public string Prompt => type == Type.Battery ? "Take battery" : "Take bottle";

    public void Interact(PlayerController player)
    {
        if (type == Type.Battery)
        {
            var fl = player.GetComponentInChildren<Flashlight>();
            if (fl != null) fl.AddBattery(batteryAmount);
        }
        else
        {
            var th = player.GetComponent<ThrowItem>();
            if (th != null) th.bottles++;
        }

        Destroy(gameObject);
    }
}
