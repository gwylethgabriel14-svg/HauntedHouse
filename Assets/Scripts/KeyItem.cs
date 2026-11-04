using UnityEngine;

public class KeyItem : MonoBehaviour, IInteractable
{
    public Sprite icon;              // optional, icon sa hotbar

    public string Prompt => "Take key";

    public void Interact(PlayerController player)
    {
        GameManager.Instance.CollectKey();
        if (Inventory.Instance != null) Inventory.Instance.AddItem("Key", icon, null, true);
        Destroy(gameObject);
    }
}