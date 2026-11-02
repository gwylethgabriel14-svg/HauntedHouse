
using UnityEngine;
 
// UPDATED: the player now faces the door when hiding (so the Interactor can hit the
// door to leave) and faces out of the locker when leaving.
public class HidingSpot : MonoBehaviour, IInteractable
{
    public Transform hidePoint;
    public Transform exitPoint;
 
    bool inside;
 
    public string Prompt => inside ? "Leave hiding spot" : "Hide";
 
    public void Interact(PlayerController player)
    {
        inside = !inside;
 
        Transform target = inside ? hidePoint : exitPoint;
 
        var cc = player.GetComponent<CharacterController>();
        cc.enabled = false;   // must disable to teleport a CharacterController
        player.transform.position = target.position;
        player.transform.rotation = Quaternion.Euler(0f, target.eulerAngles.y, 0f);
        if (player.cam != null) player.cam.localRotation = Quaternion.identity;
        cc.enabled = true;
 
        player.IsHidden = inside;
        player.CanMove = !inside;
    }
}
 