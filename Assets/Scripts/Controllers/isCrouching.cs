using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Input router for string-based animator controller and third-person movement.
// Keeps the new Input System bindings but no longer uses Animator parameter hashes.
public class IsCrouchingControl : MonoBehaviour
{
    [Tooltip("Reference to the canonical string-based animator controller.")]
    public StringscriptAnimatior animController;

    private PlayerControlls input;

    [Header("Runtime State (read-only)")]
    [SerializeField] private bool isCrouching;
    [SerializeField] private bool isDodging;
    [SerializeField] private bool isSprinting;
    public bool IsCrouching => isCrouching;
    public bool IsDodging => isDodging;
    public bool IsSprinting => isSprinting;

    void Awake()
    {
        input = new PlayerControlls();
        // Use `started` so we trigger once on press-down.
        // `performed` can fire more than once depending on InputAction interactions (e.g., Press+Release).
        input.Player.Crouch.started += _ => RequestCrouchToggle();
        input.Player.Dodge.started += _ => RequestDodge();
        input.Player.Sprint.performed += ctx => SetSprint(true);
        input.Player.Sprint.canceled += ctx => SetSprint(false);
    }

    void Start()
    {
        if (animController == null)
            animController = GetComponent<StringscriptAnimatior>();

        if (animController == null)
        {
            Debug.LogError("StringscriptAnimatior not found on this GameObject. Please assign it.");
            enabled = false;
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

    private void RequestCrouchToggle()
    {
        if (animController == null) return;
        animController.RequestCrouchToggle();
        SyncFlagsFromAnimator();
    }

    private void RequestDodge()
    {
        if (animController == null) return;
        animController.RequestDodge();
        SyncFlagsFromAnimator();
    }
    private void SetSprint(bool value)
    {
        isSprinting = value;
        if (animController != null)
            animController.SetSprint(value);
        // Hook for future use (e.g., movement speed/sprint anims)
    }

    private void Update()
    {
        SyncFlagsFromAnimator();
    }

    private void SyncFlagsFromAnimator()
    {
        if (animController == null)
            return;

        isCrouching = animController.IsCrouched || animController.IsCrouchSettling;
        isDodging = animController.IsDodging;
    }
}