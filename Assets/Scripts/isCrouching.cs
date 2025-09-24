using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class IsCrouchingControl : MonoBehaviour
{
    public Animator playerAnimator; // Public reference to the Animator component
    private int isCrouchingHash;
    private int isMovingCrouchHash;
    private int isDodgingHash;
    private int dodgeStateHash;

    private PlayerControlls input;
    public float moveSpeed = 2.0f;
    public float normalMoveSpeed = 5.0f;
    public float sprintSpeed = 7.0f; // New sprint speed

    private bool isDodging = false;
    private bool isSprinting = false; // New flag for sprinting
    private bool canDodgeWhileCrouching = false; // Advanced option: allow dodging from a crouch

    void Awake()
    {
        input = new PlayerControlls();
        input.Player.Crouch.performed += ctx => HandleCrouchOrDodge();
        input.Player.Dodge.performed += ctx => HandleCrouchOrDodge();

        // Sprint input logic
        input.Player.Sprint.performed += ctx => StartSprint();
        input.Player.Sprint.canceled += ctx => StopSprint();
    }

void Start()
{
    playerAnimator = GetComponent<Animator>();
    if (playerAnimator == null)
    {
        Debug.LogError("Animator component is missing!");
        enabled = false;
        return;
    }
    {
    if (playerAnimator != null)
    {
        foreach (var param in playerAnimator.parameters)
        {
            Debug.Log($"Animator Parameter: {param.name} ({param.type})");
        }
    }
    if (playerAnimator == null)
{
    Debug.LogError("Player Animator is not assigned properly!");
}
else
{
    Debug.Log("Player Animator is assigned and valid.");
}
if (playerAnimator != null)
{
    Debug.Log("Animator initialized");
    playerAnimator.SetBool(isCrouchingHash, true);
}
}

    isCrouchingHash = Animator.StringToHash("isCrouching");
    isMovingCrouchHash = Animator.StringToHash("isMovingCrouch");
    isDodgingHash = Animator.StringToHash("isDodging");
    dodgeStateHash = Animator.StringToHash("Dodge");

    // Debug log to check if the hash values are correct
    Debug.Log("isCrouchingHash: " + isCrouchingHash);
    Debug.Log("isMovingCrouchHash: " + isMovingCrouchHash);
    Debug.Log("isDodgingHash: " + isDodgingHash);
    Debug.Log("dodgeStateHash: " + dodgeStateHash);
}

    void Update()
    {
        if (!isDodging)
        {
            HandleMovement();
        }

        DebugDodgeState();
    }

    private void DebugDodgeState()
    {
        if (playerAnimator.GetCurrentAnimatorStateInfo(0).shortNameHash == dodgeStateHash)
        {
            Debug.Log($"Dodge state is active. isDodging: {playerAnimator.GetBool(isDodgingHash)}");
        }
    }

    private void HandleMovement()
    {
        Vector2 moveInput = input.Player.Move.ReadValue<Vector2>();
        Vector3 movement = new Vector3(moveInput.x, 0, moveInput.y).normalized;

        bool isCrouching = playerAnimator.GetBool(isCrouchingHash);

        if (movement.magnitude > 0)
        {
            if (isCrouching)
            {
                playerAnimator.SetBool(isMovingCrouchHash, true);
                isSprinting = false; // Prevent sprinting while crouching
                transform.Translate(movement * moveSpeed * Time.deltaTime);
            }
            else
            {
                float currentSpeed = isSprinting ? sprintSpeed : normalMoveSpeed;
                transform.Translate(movement * currentSpeed * Time.deltaTime);
            }
        }
        else if (isCrouching)
        {
            playerAnimator.SetBool(isMovingCrouchHash, false);
        }
    }

    private void StartSprint()
    {
        if (!playerAnimator.GetBool(isCrouchingHash)) // Prevent sprinting while crouching
        {
            isSprinting = true;
        }
    }

    private void StopSprint()
    {
        isSprinting = false;
    }

    private void HandleCrouchOrDodge()
    {
        if (isDodging)
        {
            Debug.Log("Cannot crouch or start a new dodge while already dodging.");
            return;
        }

        bool isCrouching = playerAnimator.GetBool(isCrouchingHash);

        if (input.Player.Dodge.WasPressedThisFrame() && (!isCrouching || canDodgeWhileCrouching))
        {
            StartDodge();
        }
        else if (input.Player.Crouch.WasPressedThisFrame())
        {
            ToggleCrouch();
        }
    }

    private void StartDodge()
    {
        isDodging = true;
        playerAnimator.SetBool(isDodgingHash, true);
    }

    public void EndDodge()
    {
        isDodging = false;
        playerAnimator.SetBool(isDodgingHash, false);
    }

    private void ToggleCrouch()
    {
        bool isCrouching = playerAnimator.GetBool(isCrouchingHash);
        playerAnimator.SetBool(isCrouchingHash, !isCrouching);

        if (isCrouching)
        {
            playerAnimator.SetBool(isMovingCrouchHash, false);
            isSprinting = false; // Stop sprinting when crouching
        }
    }

    void OnEnable()
    {
        input.Enable();

        // Reset animation states when re-enabled, if needed
        if (isDodging || playerAnimator.GetBool(isCrouchingHash))
        {
            Debug.Log("Reinitializing dodging or crouching states on enable.");
            playerAnimator.SetBool(isDodgingHash, false);
            playerAnimator.SetBool(isCrouchingHash, false);
        }
    }

    void OnDisable()
    {
        input.Disable();
    }
}