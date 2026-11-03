
using UnityEngine;
 
// UPDATED: the player now faces the door when hiding (so the Interactor can hit the
// door to leave) and faces out of the locker when leaving.
public class HidingSpot : MonoBehaviour, IInteractable
{
    public Transform hidePoint;
    public Transform exitPoint;
 
    bool inside;
 
    public bool IsOccupied => inside;
    public string Prompt => inside ? "Press E to Unhide" : "Press E to Hide";
 
    public void Interact(PlayerController player)
    {
        if (inside)
        {
            Leave(player);
            return;
        }

        Hide(player);
    }

    public void Hide(PlayerController player)
    {
        inside = true;
        MoveTo(player, hidePoint, true);
        player.Hide(this);
    }

    public void Leave(PlayerController player)
    {
        inside = false;
        MoveTo(player, exitPoint, false);
        player.CurrentHidingSpot = null;
        player.IsHidden = false;
        player.CanMove = true;
    }

    void MoveTo(PlayerController player, Transform target, bool hide)
    {
        if (target == null)
        {
            target = hide ? hidePoint : exitPoint;
        }

        if (target == null)
        {
            player.IsHidden = hide;
            player.CanMove = !hide;
            return;
        }

        var cc = player.GetComponent<CharacterController>();
        if (cc != null)
        {
            cc.enabled = false;
            player.transform.position = target.position;
            player.transform.rotation = Quaternion.Euler(0f, target.eulerAngles.y, 0f);
            if (player.cam != null) player.cam.localRotation = Quaternion.identity;
            cc.enabled = true;
        }
        else
        {
            player.transform.position = target.position;
            player.transform.rotation = Quaternion.Euler(0f, target.eulerAngles.y, 0f);
            if (player.cam != null) player.cam.localRotation = Quaternion.identity;
        }
    }
}
 