using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FracturedStudios.Invoker;

/// <summary>
/// Canonical string-driven animator controller.
/// Input is driven by <see cref="IsCrouchingControl"/> (Input System).
/// This class owns animation state (enums), guards, and animation switching.
/// </summary>
public class StringscriptAnimatior : MonoBehaviour
{
    public enum CrouchState { Standing, Entering, Crouched }
    public enum DodgeState { Ready, Dodging, Recovering }
    public enum SprintState { NotSprinting, Sprinting }
    public enum MovementMode { Idle, Walk, Run, CrouchEnter, CrouchStill, CrouchWalk, Dodge }

    [Header("References")]
    [SerializeField] private Animator animator;

    [Header("Footsteps (optional)")]
    [SerializeField] private AudioSource footstepSource;
    [SerializeField] private AudioClip[] footstepClips;
    [Range(0f, 1f)] [SerializeField] private float footstepVolume = 1f;

    [Header("Timings")]
    [SerializeField] private float dodgeDuration = 0.5f;
    [SerializeField] private float crouchEnterDuration = 0.85f;
    [SerializeField] private float crouchEnterCrossfade = 0.08f;
    [SerializeField] private float crouchStillCrossfade = 0.10f;

    [Header("Dodge Lock")]
    [SerializeField] private float dodgeCrossfade = 0.05f;
    [SerializeField] private bool useDodgeClipLength = true;
    [SerializeField] private string dodgeClipName = ""; // optional: actual clip name if different from state name
    [SerializeField] private AnimationClip dodgeClipOverride;
    [SerializeField] private float postDodgeCrouchStillLock = 0.15f; // seconds to force crouch-still after a crouch-dodge

    [Header("Dodge End")]
    [SerializeField] private bool useEndDodgeAnimationEvent = true;
    [SerializeField] private float dodgeExitEpsilonSeconds = 0.03f; // helps avoid rolling into a 2nd loop on looping clips

    [Header("Debug")]
    [SerializeField] private bool debugDodge;
    [SerializeField] private bool debugDodgeStackTrace;

    [Header("Animator Controller Names")]
    [SerializeField] private string animIdle = "Idle_Absolute";
    [SerializeField] private string animCrouchEnter = "Crouching_Absolute"; // your enter/to-crouch state (e.g. "IsToCrouch")
    [SerializeField] private string crouchEnterClipName = "IsToCrouch"; // optional: actual clip name if different from state name
    [SerializeField] private string animCrouchStill = "CrouchingStill_Absolute";
    [SerializeField] private string animCrouchWalk = "CrouchWalk_Absolute";
    [SerializeField] private string animDodge = "Dodge_Absolute";
    [SerializeField] private string animHandWave = "HandWave_Absolute"; // played when flash ability is cast

    [Header("Crouch Enter Timing")]
    [SerializeField] private bool useCrouchEnterClipLength = true;
    [SerializeField] private AnimationClip crouchEnterClipOverride;
    [SerializeField] private float crouchEnterAnimSpeed = 0.8f;

    [Header("Flash Ability")]
    [SerializeField] private float handWaveDuration = 1.2f; // how long the hand-wave anim plays before returning to locomotion
    private bool isHandWaveActive;
    private Coroutine handWaveCoroutine;
    private string currentState;
    private Coroutine transitionCoroutine;
    private string currentAnimation = "";
    private int currentIdle;
    private Vector2 movement;
    
    // Added runtime references
    private PlayerCaseController CaseLambdas;
    private AudioSource audioSource;
    private CharacterController controller;
    private Transform RigRoot;

    // Per-layer tracking (initialized at runtime)
    private string[] currentLayerAnimation;
    private float[] currentLayerWeight;
    private float[] targetLayerWeight;

    private CrouchState crouchState = CrouchState.Standing;
    private DodgeState dodgeState = DodgeState.Ready;
    private SprintState sprintState = SprintState.NotSprinting;

    private Coroutine dodgeCoroutine;
    private Coroutine crouchCoroutine;

    private bool dodgedFromCrouch;
    private float forceCrouchStillUntil;

    private int dodgeToken;
    private bool dodgeEndEventReceived;

    public MovementMode Mode { get; private set; } = MovementMode.Idle;
    public float DodgeDuration => dodgeDuration;
    public bool IsHandWaveActive => isHandWaveActive;

    // State flags for movement + other systems
    public bool IsCrouched => crouchState == CrouchState.Crouched;
    public bool IsCrouchSettling => crouchState == CrouchState.Entering;
    public bool IsDodging => dodgeState != DodgeState.Ready;
    public bool IsSprinting => sprintState == SprintState.Sprinting;

    // Guards
    public bool CanToggleCrouch => !IsDodging;
    public bool CanDodge => dodgeState == DodgeState.Ready && !IsCrouchSettling;
    public bool CanMove => !IsCrouchSettling;
    public bool ShouldApplyCrouchSpeedMultiplier => IsCrouched && !IsDodging && !IsCrouchSettling;

    void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
        InitializeReferences();
    }

    void Start()
    {
        currentState = animIdle;
        ChangeState(currentState);
        ChangeAnimation(animIdle);
        StartCoroutine(ChangeIdle());

        // Help diagnose "no dodge logs" confusion: this prints once per play session.
        if (!debugDodge)
            VerboseLogger.SafeLog("[Dodge] debugDodge is OFF. Enable 'Debug Dodge' on StringscriptAnimatior to capture dodge traces.");
    }

    void Update()
    {
        // Movement input still comes from old axes for now.
        // (Later you can pipe your Input System move vector into this class instead.)
        movement = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

        TickLocomotionMode();
        TickAnimationByMode();
    }

    // Input driver API
    public void SetSprint(bool on) => sprintState = on ? SprintState.Sprinting : SprintState.NotSprinting;

    /// <summary>Plays the hand-wave animation for the configured duration then returns to normal locomotion.</summary>
    public void OnHandWaveTriggered(float durationOverride = -1f)
    {
        float duration = durationOverride > 0f ? durationOverride : handWaveDuration;
        if (duration <= 0f) return;

        VerboseLogger.SafeLog($"[Animator] hand-wave triggered, duration={duration:0.000}");
        if (handWaveCoroutine != null)
            StopCoroutine(handWaveCoroutine);
        handWaveCoroutine = StartCoroutine(HandWaveRoutine(duration));
    }

    private IEnumerator HandWaveRoutine(float duration)
    {
        isHandWaveActive = true;
        ChangeAnimation(animHandWave, 0.1f);
        yield return new WaitForSeconds(duration);
        isHandWaveActive = false;
        handWaveCoroutine = null;
        VerboseLogger.SafeLog("[Animator] hand-wave finished, returning to locomotion");
        // TickAnimationByMode will resume normal locomotion next frame
    }

    public void RequestCrouchToggle()
    {
        if (!CanToggleCrouch)
            return;

        SetCrouch(!IsCrouched && !IsCrouchSettling);
    }

    public void SetCrouch(bool crouch)
    {
        if (!CanToggleCrouch)
            return;

        if (crouch)
        {
            if (crouchState == CrouchState.Standing)
                BeginCrouchEnter();
        }
        else
        {
            if (crouchState != CrouchState.Standing)
                ExitCrouch();
        }
    }

    public void RequestDodge()
    {
        if (IsDodging)
        {
            if (debugDodge)
                VerboseLogger.SafeLog($"[Dodge] Ignored (already dodging). state={dodgeState} crouch={crouchState} mode={Mode} frame={Time.frameCount}");
            return;
        }

        if (!CanDodge)
        {
            if (debugDodge)
                VerboseLogger.SafeLog($"[Dodge] Denied by guard. CanDodge={CanDodge} IsCrouchSettling={IsCrouchSettling} state={dodgeState} crouch={crouchState} mode={Mode} frame={Time.frameCount}");
            return;
        }

        if (debugDodge)
        {
            string stack = debugDodgeStackTrace ? ("\n" + System.Environment.StackTrace) : "";
            VerboseLogger.SafeLog($"[Dodge] Request accepted. crouch={crouchState} mode={Mode} frame={Time.frameCount}{stack}");
        }

        if (dodgeCoroutine != null)
        {
            if (debugDodge)
                VerboseLogger.SafeLog($"[Dodge] Stopping existing dodge coroutine (unexpected). frame={Time.frameCount}");
            StopCoroutine(dodgeCoroutine);
            dodgeCoroutine = null;
        }

        dodgedFromCrouch = crouchState == CrouchState.Crouched;
        dodgeState = DodgeState.Dodging;
        Mode = MovementMode.Dodge;
        ChangeAnimation(animDodge, dodgeCrossfade);

        int token = ++dodgeToken;
        dodgeEndEventReceived = false;
        dodgeCoroutine = StartCoroutine(DodgeRoutine(token));
    }

    // Gather common runtime references and initialize per-layer arrays.
    private void InitializeReferences()
    {
        // Prefer the global singleton when available, otherwise fall back to local component.
        CaseLambdas = PlayerCaseController.Instance ?? GetComponent<PlayerCaseController>();
        if (CaseLambdas == null)
            Debug.LogWarning("[PlayerAnimator] No CaseLambdas found on player or children! ");

        audioSource = GetComponent<AudioSource>();
        controller = GetComponent<CharacterController>();

        RigRoot = FracturedStudios.Components.ComponentExtensions.FindDeep<Transform>(this, "_Player_v0.1")
                  ?? (transform.root != null ? transform.root : transform);

        if (animator != null)
        {
            int layers = Mathf.Max(1, animator.layerCount);
            currentLayerAnimation = new string[layers];
            currentLayerWeight = new float[layers];
            targetLayerWeight = new float[layers];
            for (int i = 0; i < layers; i++)
            {
                currentLayerAnimation[i] = string.Empty;
                currentLayerWeight[i] = animator.GetLayerWeight(i);
                targetLayerWeight[i] = currentLayerWeight[i];
            }
        }
    }

    private void BeginCrouchEnter()
    {
        if (crouchCoroutine != null)
            StopCoroutine(crouchCoroutine);

        crouchCoroutine = StartCoroutine(CrouchEnterRoutine());
    }

    private IEnumerator CrouchEnterRoutine()
    {
        crouchState = CrouchState.Entering;
        ChangeAnimation(animCrouchEnter, crouchEnterCrossfade);
        float oldSpeed = animator != null ? animator.speed : 1f;
    if (animator != null) animator.speed = crouchEnterAnimSpeed;

        // Hard hold: do not allow still/walk until the enter animation is fully done
        float holdSeconds = crouchEnterDuration;
        if (useCrouchEnterClipLength)
        {
            float clipLen = GetCrouchEnterClipLengthSeconds();
            if (clipLen > 0f)
            {
                float speed = animator != null ? Mathf.Abs(animator.speed) : 1f;
                holdSeconds = clipLen / Mathf.Max(0.01f, speed);
            }
        }

        yield return new WaitForSeconds(holdSeconds);

        // If we got cancelled mid-enter
        if (crouchState != CrouchState.Entering)
        {
        if (animator != null) animator.speed = oldSpeed;
        yield break;
    }

        crouchState = CrouchState.Crouched;
        if (animator != null) animator.speed = oldSpeed;
    }

    private float GetCrouchEnterClipLengthSeconds()
    {
        if (crouchEnterClipOverride != null)
            return crouchEnterClipOverride.length;

        if (animator == null || animator.runtimeAnimatorController == null)
            return -1f;

        string nameToFind = !string.IsNullOrEmpty(crouchEnterClipName) ? crouchEnterClipName : animCrouchEnter;
        if (string.IsNullOrEmpty(nameToFind))
            return -1f;

        // Linear scan is fine here (small list); can optimize later if needed.
        foreach (var clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip != null && clip.name == nameToFind)
                return clip.length;
        }

        return -1f;
    }

    private void ExitCrouch()
    {
        if (crouchCoroutine != null)
        {
            StopCoroutine(crouchCoroutine);
            crouchCoroutine = null;
        }

        crouchState = CrouchState.Standing;
         if (animator != null) animator.speed = 1f;
        ChangeAnimation(animIdle, 0.1f);
    }

    private IEnumerator DodgeRoutine(int token)
    {
        float holdSeconds = dodgeDuration;
        if (useDodgeClipLength)
        {
            float clipLen = GetDodgeClipLengthSeconds();
            if (clipLen > 0f)
            {
                float speed = animator != null ? Mathf.Abs(animator.speed) : 1f;
                holdSeconds = clipLen / Mathf.Max(0.01f, speed);
            }
        }

        // If the clip/state is looping, waiting the full length can drift into the next cycle
        // ("plays full then starts again and gets cut off").
        holdSeconds = Mathf.Max(0f, holdSeconds - Mathf.Max(0f, dodgeExitEpsilonSeconds));

        if (debugDodge)
            VerboseLogger.SafeLog($"[Dodge:{token}] Start. hold={holdSeconds:0.000}s fromClipLen={useDodgeClipLength} dodgedFromCrouch={dodgedFromCrouch} crouch={crouchState} mode={Mode} frame={Time.frameCount}");

        if (useEndDodgeAnimationEvent)
        {
            float endTime = Time.time + holdSeconds;
            while (!dodgeEndEventReceived && Time.time < endTime)
                yield return null;

            if (debugDodge)
                VerboseLogger.SafeLog($"[Dodge:{token}] End condition met. endEvent={dodgeEndEventReceived} timeRemaining={Mathf.Max(0f, endTime - Time.time):0.000}s frame={Time.frameCount}");
        }
        else
        {
            yield return new WaitForSeconds(holdSeconds);
        }

        if (debugDodge)
            VerboseLogger.SafeLog($"[Dodge:{token}] Hold complete. Transitioning to Recovering. frame={Time.frameCount}");

        dodgeState = DodgeState.Recovering;
        yield return null;
        dodgeState = DodgeState.Ready;

        if (debugDodge)
            VerboseLogger.SafeLog($"[Dodge:{token}] End. state={dodgeState} crouch={crouchState} mode={Mode} frame={Time.frameCount}");

        // If we dodged out of crouch, force return to crouch-still briefly.
        if (dodgedFromCrouch && crouchState == CrouchState.Crouched)
        {
            forceCrouchStillUntil = Time.time + Mathf.Max(0f, postDodgeCrouchStillLock);
            ChangeAnimation(animCrouchStill, crouchStillCrossfade);

            if (debugDodge)
                VerboseLogger.SafeLog($"[Dodge:{token}] Forced CrouchStill for {postDodgeCrouchStillLock:0.000}s (until {forceCrouchStillUntil:0.000}). frame={Time.frameCount}");
        }

        if (dodgeCoroutine != null)
            dodgeCoroutine = null;
    }

    private float GetDodgeClipLengthSeconds()
    {
        if (dodgeClipOverride != null)
            return dodgeClipOverride.length;

        if (animator == null || animator.runtimeAnimatorController == null)
            return -1f;

        string nameToFind = !string.IsNullOrEmpty(dodgeClipName) ? dodgeClipName : animDodge;
        if (string.IsNullOrEmpty(nameToFind))
            return -1f;

        foreach (var clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip != null && clip.name == nameToFind)
                return clip.length;
        }

        return -1f;
    }

    private void TickLocomotionMode()
    {
        if (IsDodging)
        {
            Mode = MovementMode.Dodge;
            return;
        }

        if (crouchState == CrouchState.Entering)
        {
            Mode = MovementMode.CrouchEnter;
            return;
        }

        bool isMoving = movement.sqrMagnitude > 0.01f;

        if (crouchState == CrouchState.Crouched)
        {
            if (Time.time < forceCrouchStillUntil)
            {
                Mode = MovementMode.CrouchStill;
                return;
            }
            Mode = isMoving ? MovementMode.CrouchWalk : MovementMode.CrouchStill;
            return;
        }

        // hand-wave: stay in current locomotion mode (animation is played directly, no mode change)
        if (isHandWaveActive)
            return;

        if (!isMoving)
        {
            Mode = MovementMode.Idle;
            return;
        }

        Mode = IsSprinting ? MovementMode.Run : MovementMode.Walk;
    }

    private void TickAnimationByMode()
    {
        // while the hand-wave coroutine is running, don't interfere with animation
        if (isHandWaveActive)
            return;

        switch (Mode)
        {
            case MovementMode.Dodge:
            case MovementMode.CrouchEnter:
                return; // routines play/hold these

            case MovementMode.CrouchStill:
                ChangeAnimation(animCrouchStill, crouchStillCrossfade);
                return;

            case MovementMode.CrouchWalk:
                ChangeAnimationFast(animCrouchWalk);
                return;

            case MovementMode.Idle:
            case MovementMode.Walk:
            case MovementMode.Run:
                CheckAnimation(); // preserves N/S/E/W logic
                return;
            default:
                CheckAnimation();
                return;
        }
    }


    // ==============================
    // MOVEMENT CHECK & ANIMATIONS
    // ==============================

    /// <summary>
    /// Determines which animation to play based on movement input.
    /// Faster response when running (shift key held).
    /// </summary>    private void CheckAnimation()
   //    private void CheckAnimation()
    //{
     //   if (movement.y == 1)
     //       ChangeAnimation("Run_N_Absolute");
     //   else if (movement.y == -1)
     //       ChangeAnimation("Run__S_Absolute");
     //   else if (movement.x == 1)
     //       ChangeAnimation("Dodge_Absolute");
     //   else if (movement.x == -1)
     //       ChangeAnimation("Dodge_Absolute");
    //    else if (movement.x == 0|| movement.y == 0)
    //        ChangeAnimation("Idle_Absolute");
    //    else
    //        CheckIdle(); // Default animation when no input
    //}
 //   private void CheckAnimation()

// ==============================
// MOVEMENT CHECK & ANIMATIONS
// ==============================

/// <summary>
/// Determines which animation to play based on movement input.
/// Faster response when running (shift key held).
/// </summary>
private void CheckAnimation()
{
    bool isMoving = movement.sqrMagnitude > 0.01f;  // Movement threshold

    // Crouch branch
    if (crouchState == CrouchState.Entering)
    {
        return; // let enter animation play out
    }
    if (crouchState == CrouchState.Crouched)
    {
        if (isMoving)
        {
            ChangeAnimationFast(animCrouchWalk);
        }
        else
        {
            ChangeAnimation(animCrouchStill, 0.1f);
        }
        return;
    }

    // Standing/walking/running branch DO NOT EDIT OR CHANGE THIS FUNCTION unless HAVE ANIMATIONS
    if (isMoving)
    {
        bool isRunning = IsSprinting;
        if(Mathf.Abs(movement.y) > Mathf.Abs(movement.x))
        {
            ChangeAnimationFast(isRunning ? 
            (movement.y > 0 ? "Run_N_Absolute" : "Run_N_Absolute") : // N/S //TODO: add new animations
            (movement.y > 0 ? "Walk_N_Absolute" : "Walk_N_Absolute")); //walk variant
        }
        else
        {
            ChangeAnimationFast(isRunning ? 
            (movement.x > 0 ? "Run_R_Absolute" : "Run_N_Absolute") : // R/L or /e/w
            (movement.x > 0 ? "Walk_N_Absolute" : "Walk_N_Absolute")); //walk variant use one anim for now
        }
        //ChangeAnimationFast(isRunning ? "Run_N_Absolute" : "Walk_N_Absolute");
    }
    else
    {
        CheckIdle();
    }
}
    // Jump should be checked OUTSIDE movement conditions


    

    /// <summary>
    /// Rotates through different idle animations for variety.
    /// </summary>
    private void CheckIdle()
    {
        switch (currentIdle)
        {
            case 0:
                ChangeAnimation("Idle_Absolute");
                break;
            case 1:
                ChangeAnimation("Idle2_Absolute");
                break;
            case 2:
                ChangeAnimation("Idle_Absolute");
                break;
            case 3:
                ChangeAnimation("Idle2_Absolute");
                break;
            case 4:
                ChangeAnimation("Idle_Absolute");
                break;
            case 5:
                ChangeAnimation("Idle2_Absolute");
                break;
            case 6:
                ChangeAnimation("Idle0_Absolute");
                break;
            default:
                ChangeAnimation("Idle_Absolute");
                break;
        }
    }

    // ==============================
    // ANIMATION HELPERS
    // ==============================

    /// <summary>
    /// Smoothly transitions to the given animation using crossfade.
    /// </summary>
    /// <param name="animation">Animation clip name to play.</param>
    /// <param name="crossfade">Duration of crossfade transition in seconds.</param>
    private void ChangeAnimation(string animation, float crossfade = 0.1f)
    {
        if (currentAnimation != animation)
        {
            VerboseLogger.SafeLog($"[Animator] ChangeAnimation requested '{animation}' crossfade={crossfade}");
            currentAnimation = animation;
            if (animator == null)
                return;

            var stateToPlay = ResolveAnimationStateName(animation);
            if (string.IsNullOrEmpty(stateToPlay))
                return;

            animator.CrossFade(stateToPlay, crossfade);
        }
    }

    /// <summary>
    /// Faster transition for movement animations to improve responsiveness.
    /// Uses a shorter crossfade duration.
    /// </summary>
    /// <param name="animation">Animation clip name to play.</param>
    private void ChangeAnimationFast(string animation)
    {
        if (currentAnimation != animation)
        {
            VerboseLogger.SafeLog($"[Animator] ChangeAnimationFast requested '{animation}'");
            currentAnimation = animation;
            if (animator == null)
                return;

            var stateToPlay = ResolveAnimationStateName(animation);
            if (string.IsNullOrEmpty(stateToPlay))
                return;

            animator.CrossFade(stateToPlay, 0.05f);
        }
    }

    private string ResolveAnimationStateName(string requested)
    {
        if (string.IsNullOrWhiteSpace(requested) || animator == null)
            return null;

        bool HasState(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;
            return animator.HasState(0, Animator.StringToHash(name));
        }

        if (HasState(requested))
            return requested;

        var stripped = requested.EndsWith("_Absolute") ? requested.Substring(0, requested.Length - "_Absolute".Length) : requested;
        if (HasState(stripped))
        {
            VerboseLogger.SafeLog($"[Animator] fallback state '{requested}' -> '{stripped}'");
            return stripped;
        }

        if (requested.StartsWith("Idle"))
        {
            if (HasState("Idle"))
            {
                VerboseLogger.SafeLog($"[Animator] fallback state '{requested}' -> 'Idle'");
                return "Idle";
            }
            if (HasState(animIdle))
            {
                VerboseLogger.SafeLog($"[Animator] fallback state '{requested}' -> '{animIdle}'");
                return animIdle;
            }
        }

        VerboseLogger.SafeLog($"[Animator] WARNING: state '{requested}' not found on layer 0 (crossfade skipped)");
        var clips = animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.animationClips : null;
        if (clips != null)
        {
            string names = string.Join(", ", System.Array.ConvertAll(clips, c => c != null ? c.name : "<null>"));
            VerboseLogger.SafeLog($"[Animator] controller clips: {names}");
        }
        return null;
    }

    /// <summary>
    /// Coroutine that cycles through idle animations periodically.
    /// </summary>
    /// <returns>IEnumerator for coroutine control.</returns>
    private IEnumerator ChangeIdle()
    {
        while (true)
        {
            yield return new WaitForSeconds(2f); // Cycle every 2 seconds
            currentIdle++;
            if (currentIdle >= 7)
                currentIdle = 0;
        }
    }

    // ==============================
    // STATE MACHINE CORE
    // ==============================

    /// <summary>
    /// Gets the current animation state.
    /// </summary>
    /// <returns>Current state string.</returns>
    public string GetCurrentState() => currentState;

    /// <summary>
    /// Changes the current animation state with optional transition duration and delay.
    /// </summary>
    /// <param name="newState">Target state to transition to.</param>
    /// <param name="transitionDuration">Duration of transition crossfade in seconds.</param>
    /// <param name="delay">Optional delay before transition starts, in seconds.</param>
    public void ChangeState(string newState, float transitionDuration = 0.2f, float delay = 0f)
    {
        if (currentState == newState)
        {
            Debug.Log($"State '{newState}' is already active. No transition needed.");
            return;
        }

        // Stop any running transition coroutine
        if (transitionCoroutine != null)
            StopCoroutine(transitionCoroutine);

        // Start the transition coroutine
        transitionCoroutine = StartCoroutine(TransitionToState(newState, transitionDuration, delay));
    }

    /// <summary>
    /// Coroutine that handles optional delayed transitions between animation states.
    /// </summary>
    /// <param name="newState">Target animation state.</param>
    /// <param name="transitionDuration">Duration of crossfade transition.</param>
    /// <param name="delay">Delay before transition starts.</param>
    /// <returns>IEnumerator for coroutine control.</returns>
    private IEnumerator TransitionToState(string newState, float transitionDuration, float delay)
    {
        if (delay > 0f)
        {
            Debug.Log($"Delaying transition to '{newState}' for {delay} seconds...");
            yield return new WaitForSeconds(delay);
        }

        Debug.Log($"Transitioning to state: {newState} with duration {transitionDuration} seconds.");
        animator.CrossFade(newState, transitionDuration);
        currentState = newState;
    }

    /// <summary>
    /// AnimationEvent receiver for footstep sounds. Attach this name
    /// to the `OnFootstep` AnimationEvent on walk/run animations.
    /// Plays a random clip from `footstepClips` using `footstepSource` if available,
    /// otherwise falls back to `AudioSource.PlayClipAtPoint`.
    /// </summary>
    public void OnFootstep()
    {
        if (footstepClips == null || footstepClips.Length == 0)
            return; // nothing to play AnimationEvent receiver for footstep sounds. Attach this name to the `OnFootstep` AnimationEvent on walk/run animations.

        AudioClip clip = footstepClips[Random.Range(0, footstepClips.Length)];

        if (clip == null)
            return;

        if (footstepSource != null)
        {
            footstepSource.PlayOneShot(clip, footstepVolume);
        }
        else
        {
            AudioSource.PlayClipAtPoint(clip, transform.position, footstepVolume);
        }
    }

    // ==============================
    // AnimationEvent receivers
    // ==============================

    // Hook this from the Dodge clip if you have an AnimationEvent named "EndDodge".
    public void EndDodge()
    {
        dodgeEndEventReceived = true;
        VerboseLogger.SafeLog($"[Dodge] AnimationEvent EndDodge received. instance={GetInstanceID()} state={dodgeState} token={dodgeToken} mode={Mode} frame={Time.frameCount}");
    }

    // If your clip currently uses "OnCrouchAnimationComplete()" (with parentheses),
    // change the AnimationEvent function name to "OnCrouchAnimationComplete".
    public void OnCrouchAnimationComplete()
    {
        VerboseLogger.SafeLog($"[Crouch] AnimationEvent OnCrouchAnimationComplete received. state={crouchState} mode={Mode} frame={Time.frameCount}");
    }
}
