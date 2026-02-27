using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using FracturedStudios.Abilities;

// Input router for string-based animator controller and third-person movement.
// Keeps the new Input System bindings but no longer uses Animator parameter hashes.
public class IsCrouchingControl : MonoBehaviour
{
    [Tooltip("Reference to the canonical string-based animator controller.")]
    public StringscriptAnimatior animController;

    [Header("Ability Hooks")]
    [SerializeField] private ThirdPersonBasic locomotion;

    private PlayerControlls input;
    private AbilityCaster abilityCaster;
    private InputAction abilityAction0;

    [Header("Runtime State (read-only)")]
    [SerializeField] private bool isCrouching;
    [SerializeField] private bool isDodging;
    [SerializeField] private bool isSprinting;
    public bool IsCrouching => isCrouching;
    public bool IsDodging => isDodging;
    public bool IsSprinting => isSprinting;
 #nullable enable
    void Awake()
    {
        input = new PlayerControlls();
        // Use `started` so we trigger once on press-down.
        // `performed` can fire more than once depending on InputAction interactions (e.g., Press+Release).
        input.Player.Crouch.started += _ => RequestCrouchToggle();
        input.Player.Dodge.started += _ => RequestDodge();
        input.Player.Sprint.performed += ctx => SetSprint(true);
        input.Player.Sprint.canceled += ctx => SetSprint(false);

        // Ability actions
        var playerMap = input.asset.FindActionMap("Player", throwIfNotFound: true);
        abilityAction0 = playerMap.FindAction("Ability_Flash", throwIfNotFound: false);
        if (abilityAction0 != null)
        {
            VerboseLogger.SafeLog("[Input] Ability_Flash action found");
            abilityAction0.started += ctx =>
            {
                VerboseLogger.SafeLog("[Input] Ability_Flash triggered");
                CastAbility();
            };
        }
        else
        {
            VerboseLogger.SafeLog("[Input] Ability_Flash action NOT found in control map");
        }
        // initialize local caster (value type)
        abilityCaster = new AbilityCaster();
                    
    }

    void Start()
    {
        if (animController == null)
            animController = GetComponent<StringscriptAnimatior>();

        if (locomotion == null)
            locomotion = GetComponent<ThirdPersonBasic>();

        if (animController == null)
        {
            Debug.LogError("StringscriptAnimatior not found on this GameObject. Please assign it.");
            enabled = false;
        }
    }

    void OnEnable()
    {
        input.Enable();
        abilityAction0?.Enable();
    }

    void OnDisable()
    {
        abilityAction0?.Disable();
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
        // raw key check for debugging
        if (Input.GetKeyDown(KeyCode.Alpha1))
            VerboseLogger.SafeLog("[Input] raw KeyCode.Alpha1 pressed");

        // also log state of action for diagnostics
        if (abilityAction0 != null && abilityAction0.triggered)
            VerboseLogger.SafeLog("[Input] abilityAction0.triggered true in Update");

        SyncFlagsFromAnimator();
    }

    private void SyncFlagsFromAnimator()
    {
        if (animController == null)
            return;

        isCrouching = animController.IsCrouched || animController.IsCrouchSettling;
        isDodging = animController.IsDodging;
    }

    private void CastAbility()
    {
        // Now check ability availability; if cooling down, do not run movement mods.
        var data = AbilityAtlas.GetInstance()[AbilityFlags.Skill0];
        if (data == null)
        {
            VerboseLogger.SafeLog("[Flash] ability data missing");
            return;
        }
        if (data.Waiting)
        {
            VerboseLogger.SafeLog("[Flash] ability on cooldown, cast ignored");
            return;
        }

        VerboseLogger.SafeLog("[Flash] casting ability (Skill0)");
        // Perform the cast and then apply movement changes.
        abilityCaster.Cast(AbilityFlags.Skill0);
        animController?.OnFlashAbilityTriggered();
        locomotion?.OnFlashAbilityTriggered();
    }
}