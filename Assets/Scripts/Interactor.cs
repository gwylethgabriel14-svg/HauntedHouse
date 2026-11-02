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
        IInteractable target = null;

        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit,
                            range, mask, QueryTriggerInteraction.Collide))
        {
            target = hit.collider.GetComponentInParent<IInteractable>();
        }

        promptText.text = target != null ? $"[E] {target.Prompt}" : "";

        if (target != null && Input.GetKeyDown(KeyCode.E))
            target.Interact(player);
    }
}
