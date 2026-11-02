using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 3f;
    public float runSpeed = 5.5f;
    public float crouchSpeed = 1.5f;
    public float gravity = -9.81f;

    [Header("Look")]
    public Transform cam;
    public float mouseSensitivity = 2f;

    [Header("Crouch (Left Ctrl or C)")]
    public bool toggleCrouch = false;          // false = hold the key, true = press to switch
    public float crouchHeight = 1.1f;          // body height while crouching
    public float crouchTransitionSpeed = 8f;   // how fast you go down / up
    public LayerMask crouchBlockMask = ~0;     // what counts as a low ceiling

    [Header("Noise radius (meters the enemy can hear)")]
    public float noiseCrouch = 2f;
    public float noiseWalk = 6f;
    public float noiseRun = 14f;

    public bool IsHidden { get; set; }
    public bool CanMove { get; set; } = true;
    public float CurrentNoise { get; private set; }
    public bool IsCrouching { get; private set; }

    CharacterController cc;
    float pitch;
    float yVel;

    // crouch bookkeeping
    float standHeight;
    float bottomY;          // bottom of the capsule, in local space (kept fixed while changing height)
    float camStandY;
    bool wantCrouch;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        standHeight = cc.height;
        bottomY = cc.center.y - cc.height / 2f;
        camStandY = cam != null ? cam.localPosition.y : 0f;

        // keep the crouch capsule valid
        crouchHeight = Mathf.Clamp(crouchHeight, cc.radius * 2f + 0.1f, standHeight);
    }

    void Update()
    {
        if (!CanMove) { CurrentNoise = 0f; return; }

        // Look
        float mx = Input.GetAxis("Mouse X") * mouseSensitivity;
        float my = Input.GetAxis("Mouse Y") * mouseSensitivity;
        pitch = Mathf.Clamp(pitch - my, -80f, 80f);
        cam.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        transform.Rotate(Vector3.up * mx);

        // Crouch input
        bool crouchDown = Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.C);
        bool crouchHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C);
        if (toggleCrouch)
        {
            if (crouchDown) wantCrouch = !wantCrouch;
        }
        else
        {
            wantCrouch = crouchHeld;
        }

        // stay crouched while something low is above you
        bool crouch = wantCrouch || (IsCrouching && !CanStandUp());
        IsCrouching = crouch;
        ApplyCrouchHeight(crouch);

        // Move
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        bool run = Input.GetKey(KeyCode.LeftShift) && !crouch && v > 0f;
        float speed = crouch ? crouchSpeed : (run ? runSpeed : walkSpeed);

        Vector3 move = (transform.right * h + transform.forward * v).normalized * speed;

        if (cc.isGrounded && yVel < 0f) yVel = -2f;
        yVel += gravity * Time.deltaTime;

        cc.Move((move + Vector3.up * yVel) * Time.deltaTime);

        // Noise for the enemy to hear
        bool moving = h != 0f || v != 0f;
        CurrentNoise = !moving ? 0f : (crouch ? noiseCrouch : (run ? noiseRun : noiseWalk));
    }

    // smoothly changes the capsule height and the camera height, keeping your feet on the floor
    void ApplyCrouchHeight(bool crouch)
    {
        float targetHeight = crouch ? crouchHeight : standHeight;
        float newHeight = Mathf.MoveTowards(cc.height, targetHeight, crouchTransitionSpeed * Time.deltaTime * standHeight);

        if (!Mathf.Approximately(newHeight, cc.height))
        {
            cc.height = newHeight;
            cc.center = new Vector3(cc.center.x, bottomY + newHeight / 2f, cc.center.z);
        }

        if (cam != null)
        {
            // the eyes drop by the same amount the body shrinks
            float camY = camStandY - (standHeight - cc.height);
            Vector3 p = cam.localPosition;
            cam.localPosition = new Vector3(p.x, camY, p.z);
        }
    }

    // is there room above to stand up?
    bool CanStandUp()
    {
        float r = cc.radius * 0.9f;
        float extra = standHeight - cc.height;
        if (extra <= 0.01f) return true;

        // start from the top of the current capsule and look up by how much we still need to grow
        Vector3 top = transform.TransformPoint(new Vector3(cc.center.x, bottomY + cc.height - cc.radius, cc.center.z));
        var hits = Physics.SphereCastAll(top, r, Vector3.up, extra + 0.05f, crouchBlockMask, QueryTriggerInteraction.Ignore);
        foreach (var hit in hits)
        {
            if (hit.collider.transform.IsChildOf(transform)) continue;   // our own body
            return false;
        }
        return true;
    }
}