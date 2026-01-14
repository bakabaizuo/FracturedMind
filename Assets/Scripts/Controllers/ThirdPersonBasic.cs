using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class ThirdPersonBasic : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 6f;
    public float rotationSpeed = 10f;
    public float jumpHeight = 2f;
    public float gravity = -40f;   // Stronger gravity for snappier feel
    public float slopeLimit = 45f; // Max walkable slope angle

    [Header("Mouse Look")]
    [SerializeField] private bool enableMouseLook = true;
    [SerializeField] private float mouseSensitivity = 120f;
    [SerializeField] private float pitchClampMin = -60f;
    [SerializeField] private float pitchClampMax = 75f;
    [SerializeField] private Transform cameraPivotOverride; // optional pivot the mouse rotates instead of Camera.main
    [SerializeField] private Transform cameraFollowSocket;  // optional position anchor (no rotation inheritance)
    [SerializeField] private bool detachPivotFromPlayer = true; // unparent pivot so it does not inherit player rotation
    [SerializeField] private bool alignBodyToCameraOnStart = true; // rotate character to camera yaw on spawn
    private float orbitYaw;
    private float orbitPitch;
    private float currentPitch;

    [Header("Crouch Settings")]
    [SerializeField] private float crouchSpeedMultiplier = 0.5f;
    [SerializeField] private StringscriptAnimatior animController;
    [SerializeField] private IsCrouchingControl inputDriver;

    [Header("Rotation Options")]
    [SerializeField] private bool alignCharacterToMovement = false; // default off: no auto-facing or snap
    [SerializeField] private bool cameraRelativeMovement = false;   // when false, A/D/W/S are world-space (ignore camera)
    [SerializeField] private bool capRotationDelta180 = false;      // clamp single-step target to +/-180 deg for testing

    [Header("Interaction")]
    [SerializeField] private LampVisionSensor lampVisionSensor;

    [Header("Ground Detection")]
    public Transform groundCheck;
    public float groundDistance = 0.2f;
    public LayerMask groundMask;
    public bool debugGroundCheck = false; // toggle debug drawing/logging

    private CharacterController controller;
    private Vector3 velocity;
    private bool isGrounded;

    // NEW: Jump cooldown to prevent spamming
    private float jumpCooldown = 0.1f;  // short buffer
    private float lastJumpTime = -1f;
    public bool isCrouching;

    //IsCrouchingControl; is a movement driver for PlayerControlls
    private void EnsureLampVisionSensor()
    {
        if (lampVisionSensor == null)
            lampVisionSensor = GetComponent<LampVisionSensor>();

        if (lampVisionSensor == null)
            lampVisionSensor = gameObject.AddComponent<LampVisionSensor>();

        lampVisionSensor.EnsureSocket();
    }

    private void Start()
    {
        controller = GetComponent<CharacterController>();

        if (animController == null)
            animController = GetComponent<StringscriptAnimatior>();

        if (inputDriver == null)
            inputDriver = GetComponent<IsCrouchingControl>();

        EnsureLampVisionSensor();

        // If pivot override is parented to the player, detach so it stops inheriting rotation
        if (cameraPivotOverride != null && detachPivotFromPlayer && cameraPivotOverride.parent != null)
            cameraPivotOverride.SetParent(null, true);

        // Initialize orbit yaw from current camera or player yaw so mouse look starts aligned
        if (Camera.main != null)
            orbitYaw = Camera.main.transform.eulerAngles.y;
        else
            orbitYaw = transform.eulerAngles.y;

        if (alignBodyToCameraOnStart)
            transform.rotation = Quaternion.Euler(0f, orbitYaw, 0f);
    }

    private void Update()
    {
        HandleGroundCheck();
        HandleJumpAndGravity();

        if (cameraPivotOverride != null)
            SyncCameraPivotPosition();

        if (enableMouseLook)
            HandleMouseLook();

        // Movement lock while crouch is entering (so Crouching_Absolute can actually play)
        if (animController != null && !animController.CanMove)
            return;

        HandleMovement();
    }



private void HandleGroundCheck()
{
    // Raycast straight down for better detection
    isGrounded = Physics.Raycast(transform.position, Vector3.down, controller.height / 2 + 0.1f, groundMask);
        // ✅ Debugging info
        if (debugGroundCheck)
        {
//            Debug.Log($"[GroundCheck] Grounded: {isGrounded}");
        }
    if (debugGroundCheck)
 //       Debug.Log($"Grounded: {isGrounded}");

    if (isGrounded && velocity.y < 0)
        velocity.y = -2f;
}

    

    private void HandleMovement()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        Vector3 inputDirection = new Vector3(horizontal, 0f, vertical).normalized;

        bool crouchActive = animController != null ? animController.IsCrouched : isCrouching;
        bool crouchSettling = animController != null && animController.IsCrouchSettling;
        bool dodging = animController != null && animController.IsDodging;

        // Block movement while entering crouch so the enter anim can finish
        if (crouchSettling)
            return;

        // During dodge, don't slow by crouch multiplier
        float effectiveSpeed = dodging
            ? moveSpeed
            : (crouchActive ? moveSpeed * crouchSpeedMultiplier : moveSpeed);

        if (inputDirection.magnitude >= 0.1f)
        {
            // Choose reference frame (camera-relative or world)
            float referenceYaw = cameraRelativeMovement ? GetCameraYaw() : 0f;
            float targetAngle = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg + referenceYaw;

            if (capRotationDelta180)
            {
                float delta = Mathf.DeltaAngle(transform.eulerAngles.y, targetAngle);
                targetAngle = transform.eulerAngles.y + Mathf.Clamp(delta, -180f, 180f); // hard cap per-step
            }
            if (alignCharacterToMovement)
            {
                Quaternion targetRotation = Quaternion.Euler(0f, targetAngle, 0f);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
            }

            // Move in rotated direction
            Vector3 moveDirection = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;

            if (CanWalkOnSlope(moveDirection))
            {
                controller.Move(moveDirection.normalized * effectiveSpeed * Time.deltaTime);
            }
        }
    }

    private void HandleJumpAndGravity()
    {
        //  Only allow jump if grounded + cooldown passed
        bool canJumpNow = isGrounded && (Time.time > lastJumpTime + jumpCooldown);

        if (Input.GetButtonDown("Jump") && canJumpNow)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            lastJumpTime = Time.time; // prevent spam
            Debug.Log("[Jump] Jumped!");
        }

        // Apply gravity
        velocity.y += gravity * Time.deltaTime;

        // Apply vertical movement
        controller.Move(velocity * Time.deltaTime);
    }
 




    private bool CanWalkOnSlope(Vector3 moveDirection)
    {
        // Raycast down to detect slope angle
        if (Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, out RaycastHit hit, 1.5f, groundMask))
        {
            float slopeAngle = Vector3.Angle(hit.normal, Vector3.up);

            if (debugGroundCheck)
            {
                Debug.Log($"[SlopeCheck] Angle: {slopeAngle}");
            }

            return slopeAngle <= slopeLimit;
        }
        return true; // No ground detected → assume walkable
    }

    //  Draw Gizmos in Scene View for easier debugging
private void OnDrawGizmosSelected()
{
    if (groundCheck != null)
    {
        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(groundCheck.position, groundDistance);
    }
}

    private void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        orbitYaw = Mathf.Repeat(orbitYaw + mouseX, 360f);
        orbitPitch = Mathf.Clamp(orbitPitch - mouseY, pitchClampMin, pitchClampMax);
        currentPitch = orbitPitch;

        // Rotate camera pivot (or main camera) without moving its position
        if (cameraPivotOverride != null)
        {
            Vector3 camPos = cameraPivotOverride.position;
            cameraPivotOverride.rotation = Quaternion.Euler(orbitPitch, orbitYaw, 0f);
            cameraPivotOverride.position = camPos;
        }
        else if (Camera.main != null)
        {
            Vector3 camPos = Camera.main.transform.position;
            Camera.main.transform.rotation = Quaternion.Euler(orbitPitch, orbitYaw, 0f);
            Camera.main.transform.position = camPos;
        }
    }

    private void SyncCameraPivotPosition()
    {
        if (cameraPivotOverride == null || cameraFollowSocket == null)
            return;

        // Follow position only; rotation handled in HandleMouseLook to avoid inheriting player transform
        cameraPivotOverride.position = cameraFollowSocket.position;
    }

    private float GetCameraYaw()
    {
        if (enableMouseLook)
            return orbitYaw;

        if (cameraPivotOverride != null)
            return cameraPivotOverride.eulerAngles.y;

        if (Camera.main != null)
            return Camera.main.transform.eulerAngles.y;

        return transform.eulerAngles.y;
    }

}
