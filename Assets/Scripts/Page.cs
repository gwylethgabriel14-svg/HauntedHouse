using UnityEngine;

public class Page : MonoBehaviour, IInteractable
{
    public string Prompt => "Pick up page";

    public void Interact(PlayerController player)
    {
        GameManager.Instance.CollectPage();
        Destroy(gameObject);
    }
}
