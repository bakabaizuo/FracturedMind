using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class IsCrouchingControl : MonoBehaviour
{
    private Animator playerAnimator;
    private int isCrouchingHash;
    private int isMovingCrouchHash;
    private PlayerControlls input;
    public float moveSpeed = 2.0f; // Speed while crouching
    public float normalMoveSpeed = 5.0f; // Speed while standing

    void Awake() 
    {
        input = new PlayerControlls();
        input.Player.Crouch.performed += ctx => ToggleCrouch();
    }

    void Start()
    {
        playerAnimator = GetComponent<Animator>();
        isCrouchingHash = Animator.StringToHash("isCrouching");
        isMovingCrouchHash = Animator.StringToHash("isMovingCrouch");
    }

    void Update()
    {
        HandleMovement();
    }

  private void HandleMovement()
{
    // Read input for movement
    Vector2 moveInput = input.Player.Move.ReadValue<Vector2>(); // Assuming you have a Move action
    float horizontalInput = moveInput.x; // Get the horizontal input
    float verticalInput = moveInput.y; // Get the vertical input

    Vector3 movement = new Vector3(horizontalInput, 0, verticalInput); // Include vertical movement

    // Update the animator based on crouching state
    bool isCrouching = playerAnimator.GetBool(isCrouchingHash);

    // Check if there is any input to allow movement
    if (movement.magnitude > 0) // Check the magnitude of movement vector
    {
        if (isCrouching)
        {
            // If crouching and moving
            playerAnimator.SetBool(isMovingCrouchHash, true);
            transform.Translate(movement * moveSpeed * Time.deltaTime);
        }
        else
        {
            // If not crouching, handle normal movement
            transform.Translate(movement * normalMoveSpeed * Time.deltaTime);
        }
    }
    else
    {
        // No input detected
        if (isCrouching)
        {
            playerAnimator.SetBool(isMovingCrouchHash, false);
            // Optional: Handle idle crouch animations if necessary
        }
    }
}
    private void ToggleCrouch()
    {
        bool isCrouching = playerAnimator.GetBool(isCrouchingHash);
        playerAnimator.SetBool(isCrouchingHash, !isCrouching);
        
        // Reset moving crouch state if toggling to standing
        if (isCrouching)
        {
            playerAnimator.SetBool(isMovingCrouchHash, false);
        }
    }

    void OnEnable()
    {
        input.Enable();
    }

    void OnDisable()
    {
        input.Disable();
    }
}
