
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Attach to the locker/closet object that implements IInteractable.
// The normal Interactor enters the locker. E can also exit independently
// of the interaction raycast, so the player does not need to look at the door.
public class HidingSpot : MonoBehaviour, IInteractable
{
    [Header("Positions")]
    public Transform hidePoint;
    public Transform exitPoint;

    [Header("Peek View")]
    [Tooltip("Optional: place this in front of the locker opening/slit. While hidden, the player faces this point.")]
    public Transform peekLookPoint;

    [Header("Unhide")]
    [Tooltip("Allows E to leave even when the interaction raycast cannot hit the locker.")]
    public bool pressEToUnhide = true;

    private bool inside;
    private PlayerController hiddenPlayer;
    private int lastInteractionFrame = -1;

    public bool IsOccupied => inside;
    public string Prompt => inside ? "Press E to Unhide" : "Press E to Hide";

    private void Update()
    {
        if (!pressEToUnhide || !inside || hiddenPlayer == null)
            return;

        if (Time.frameCount == lastInteractionFrame)
            return;

        if (UnhideKeyPressed())
            Leave(hiddenPlayer);
    }

    private bool UnhideKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.E))
            return true;
#endif
        return false;
    }

    public void Interact(PlayerController player)
    {
        if (player == null || Time.frameCount == lastInteractionFrame)
            return;

        if (inside)
        {
            if (hiddenPlayer == player)
                Leave(player);
        }
        else
        {
            Hide(player);
        }
    }

    public void Hide(PlayerController player)
    {
        if (player == null || inside)
            return;

        if (hidePoint == null)
        {
            Debug.LogWarning(
                "HidingSpot '" + name +
                "' is missing Hide Point. Assign it in the Inspector.",
                this);
            return;
        }

        hiddenPlayer = player;
        inside = true;
        lastInteractionFrame = Time.frameCount;

        MoveTo(player, hidePoint, true);
        player.Hide(this);
    }

    public void Leave(PlayerController player)
    {
        if (!inside)
            return;

        if (hiddenPlayer != null)
            player = hiddenPlayer;

        if (player == null)
            return;

        if (exitPoint == null)
        {
            Debug.LogWarning(
                "HidingSpot '" + name +
                "' is missing Exit Point. Assign it in the Inspector.",
                this);
            return;
        }

        lastInteractionFrame = Time.frameCount;
        MoveTo(player, exitPoint, false);

        player.CurrentHidingSpot = null;
        player.IsHidden = false;
        player.CanMove = true;

        inside = false;
        hiddenPlayer = null;
    }

    private void MoveTo(
        PlayerController player,
        Transform target,
        bool hiding)
    {
        Vector3 forward = FindFacingDirection(target, hiding);
        Quaternion rotation =
            Quaternion.LookRotation(forward, Vector3.up);

        CharacterController cc =
            player.GetComponent<CharacterController>();

        bool wasEnabled = cc != null && cc.enabled;

        if (wasEnabled)
            cc.enabled = false;

        player.transform.SetPositionAndRotation(
            target.position, rotation);

        if (player.cam != null)
            player.cam.localRotation = Quaternion.identity;

        if (wasEnabled)
            cc.enabled = true;
    }

    private Vector3 FindFacingDirection(
        Transform target,
        bool hiding)
    {
        Vector3 forward;

        if (hiding && peekLookPoint != null)
        {
            forward =
                peekLookPoint.position - target.position;
        }
        else if (hidePoint != null && exitPoint != null)
        {
            forward =
                exitPoint.position - hidePoint.position;
        }
        else
        {
            forward = target.forward;
        }

        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = target.forward;
            forward.y = 0f;
        }

        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;

        return forward.normalized;
    }
}
