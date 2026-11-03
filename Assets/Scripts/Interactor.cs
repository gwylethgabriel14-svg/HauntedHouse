using UnityEngine;
using TMPro;

public class Interactor : MonoBehaviour
{
    public PlayerController player;
    public TMP_Text promptText;
    public float range = 2.5f;
    public LayerMask mask = ~0;

    void Update()
    {
        if (player != null && player.IsHidden)
        {
            if (promptText != null)
                promptText.text = "[E] Press E to Unhide";
            if (Input.GetKeyDown(KeyCode.E))
                player.Unhide();
            return;
        }

        IInteractable target = null;

        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit,
                            range, mask, QueryTriggerInteraction.Collide))
        {
            target = hit.collider.GetComponentInParent<IInteractable>();
        }

        if (promptText != null)
            promptText.text = target != null ? $"[E] {target.Prompt}" : "";

        if (target != null && Input.GetKeyDown(KeyCode.E))
            target.Interact(player);
    }
}
