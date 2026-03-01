using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FracturedStudios.Abilities;
using FracturedStudios.Data; // for PlayerData reference
using FracturedStudios.Invoker; //for worldbridge.

[RequireComponent(typeof(CharacterController))]
public class ThirdPersonBasic : MonoBehaviour
{
     #nullable enable
    public static ThirdPersonBasic? Instance;
    void Awake(){
      if (Instance != null){
        Destroy(this);
      }else{
        Instance = this;
      }
    }
    AbilityCaster Caster;
    [Header("Movement Settings")]
    // moveSpeed is driven from player data if available; inspector value acts as a fallback/default.
    // keep old serialized value when upgrading prefabs
    [UnityEngine.Serialization.FormerlySerializedAs("moveSpeed")]
    [SerializeField] private float _fallbackMoveSpeed = 5f;
    public float rotationSpeed = 10f;
    public float jumpHeight = 2f;
    public float gravity = -40f;   // Stronger gravity for snappier feel
    public float slopeLimit = 45f; // Max walkable slope angle

    /// <summary>
    /// Effective movement speed.  If a PlayerData instance exists, read
    /// the value from there; otherwise fall back to the serialized field.
    /// </summary>
    public float moveSpeed
    {
        get
        {
            // use safe navigation; ensure both Instance and data are valid
            return WorldBridgeSystem.Instance?.data?.moveSpeed ?? _fallbackMoveSpeed;
        }
        set
        {
            // write through to the PlayerData if available, otherwise update fallback
            if (WorldBridgeSystem.Instance?.data != null)
            {
                WorldBridgeSystem.Instance.data.moveSpeed = value;
            }
            else
            {
                _fallbackMoveSpeed = value;
            }
        }
    }

    [Header("Mouse Look")]
    [SerializeField] private bool enableMouseLook = true;
    [SerializeField] private float mouseSensitivity = 120f;
    [SerializeField] private float pitchClampMin = -60f;
    [SerializeField] private float pitchClampMax = 75f;
    [SerializeField] private Transform cameraPivotOverride; // optional pivot the mouse rotates instead of Camera.main
    [SerializeField] private Transform cameraFollowSocket;  // optional position anchor (no rotation inheritance)
    [SerializeField] private bool detachPivotFromPlayer = true; // unparent pivot so it does not inherit player rotation
    [SerializeField] private bool alignBodyToCameraOnStart = true; // rotate character to camera yaw on spawn
    [Header("Camera Control")]
    [Tooltip("If enabled the controller will override camera transforms at runtime. Disable to edit camera in the Inspector/prefab.")]
    [SerializeField] private bool controlCamera = true;
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

    [Header("Abilities")]
    // Sprint boost (temporary speed modifier triggered by tap-sprint)
    private float sprintBoostTimer = 10f;
    private float sprintBoostMultiplier = 1f;

    [Header("Sprint")]
    [SerializeField] private float sprintMultiplier = 1.5f; // multiplier when holding shift (ignored while crouched)

    // NEW: Jump cooldown to prevent spamming
    private float jumpCooldown = 0.1f;  // short buffer
    private float lastJumpTime = -1f;

    [SerializeField] private bool crouchFallback; // used only if animator is missing
    public bool isCrouching => animController != null ? animController.IsCrouched : crouchFallback;

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
        if (controlCamera && cameraPivotOverride != null && detachPivotFromPlayer && cameraPivotOverride.parent != null)
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

        if (cameraPivotOverride != null && controlCamera)
            SyncCameraPivotPosition();

        if (enableMouseLook && controlCamera)
            HandleMouseLook();

        // Movement lock while crouch is entering (so Crouching_Absolute can actually play)
        if (animController != null && !animController.CanMove)
            return;

        HandleMovement();
       
       //moved to input driver in isCrouching.cs see PlayerControlls.input in InputSystem
       // if (Input.GetKeyDown(KeyCode.1)) //or dpad up
       // Caster.Cast(AbilityFlags.Skill0);
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
        float baseEffective = dodging
            ? moveSpeed
            : (crouchActive ? moveSpeed * crouchSpeedMultiplier : moveSpeed);

        // SHIFT to sprint, but disable sprinting while crouched
        bool sprinting = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && !crouchActive;
        // Can stack with temporary boost abilities
        float sprintFactor = sprinting ? sprintMultiplier : 1f;
        float effectiveSpeed = baseEffective * sprintBoostMultiplier * sprintFactor;

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
 

  // void OnDestroy(){
  //   AbilityAtlas.Clear();
  //
  // }



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

    /// <summary>
    /// Apply a temporary sprint boost multiplier to player movement.
    /// </summary>
    public void StartSprintBoost(float multiplier, float duration)
    {
        if (multiplier <= 1f || duration <= 0f)
            return;
        sprintBoostMultiplier = multiplier;
        sprintBoostTimer = duration;
        // Ensure any existing flash timer is unaffected.
    }

    private void LateUpdate()
    {
        // Decrease sprint boost timer
        if (sprintBoostTimer > 0f)
        {
            sprintBoostTimer -= Time.deltaTime;
            if (sprintBoostTimer <= 0f)
                sprintBoostMultiplier = 1f;
        }
    }

}
