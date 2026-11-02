using UnityEngine;

public class KeyItem : MonoBehaviour, IInteractable
{
    public string Prompt => "Take key";

    public void Interact(PlayerController player)
    {
        GameManager.Instance.CollectKey();
        Destroy(gameObject);
    }
}
