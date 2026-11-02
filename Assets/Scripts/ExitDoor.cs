using UnityEngine;

public class ExitDoor : MonoBehaviour, IInteractable
{
    public string Prompt => GameManager.Instance.HasKey ? "Unlock and escape" : "Locked";

    public void Interact(PlayerController player)
    {
        if (GameManager.Instance.HasKey)
            GameManager.Instance.Win();
        else
            GameManager.Instance.ShowHint("It's locked. You need the key.", 3f);
    }
}
