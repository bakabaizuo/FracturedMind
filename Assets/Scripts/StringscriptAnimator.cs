using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// String-based Animation State Machine.
/// Handles idle/run/dodge/jump transitions with faster, snappier movement response.
/// </summary>
public class StringscriptAnimatior : MonoBehaviour
{
    // ==============================
    // VARIABLES
    // ==============================

    /// <summary>Animator component reference.</summary>
    private Animator animator;

    /// <summary>Current high-level animation state name.</summary>
    private string currentState;

    /// <summary>Coroutine reference managing delayed state transitions.</summary>
    private Coroutine transitionCoroutine;

    /// <summary>Currently playing animation clip name.</summary>
    private string currentAnimation = "";

    /// <summary>Idle animation cycle counter.</summary>
    private int currentIdle = 0;

    /// <summary>Current movement input vector.</summary>
    private Vector2 movement = Vector2.zero;

    // ==============================
    // UNITY METHODS
    // ==============================

    /// <summary>
    /// Unity Start method, called before the first frame update.
    /// Initializes animator and sets initial animation state and idle cycling.
    /// </summary>
    void Start()
    {
        animator = GetComponent<Animator>();

        currentState = "Idle_Absolute";       // Start idle state
        ChangeState(currentState);             // Initialize state machine
        ChangeAnimation("Idle_Absolute");      // Play starting idle animation
        StartCoroutine(ChangeIdle());          // Start cycling idle animations
    }

    /// <summary>
    /// Unity Update method, called once per frame.
    /// Processes player input and updates animation state accordingly.
    /// </summary>
void Update()
{
    //  1. Get input for movement
    movement = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

    //  2. Check movement & update animations (idle, walk, run)
    CheckAnimation();

    //  3. Handle jump input separately so it works anytime
    if (Input.GetKeyDown(KeyCode.Space))
    {
        // If you want it to play as a *temporary* animation:
        ChangeAnimation("Jump_Absolute", 0.2f);

        // Or if you want it as a STATE change in the FSM:
        // ChangeState("Jump_Absolute", 0.2f);
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
    bool isMoving = movement.sqrMagnitude > 0.01f ;  // Movement threshold

    if (isMoving)
    {
        bool isRunning = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        if (Mathf.Abs(movement.y) > Mathf.Abs(movement.x))
        {
            ChangeAnimationFast(isRunning ?
                (movement.y > 0 ? "Run_N_Absolute" : "Run_N_Absolute") :  // N/S
                (movement.y > 0 ? "Walk_N_Absolute" : "Walk_N_Absolute"));
        }
        else
        {
            ChangeAnimationFast(isRunning ?
                (movement.x > 0 ? "Run_N_Absolute" : "Run_N_Absolute") :  // E/W
                (movement.x > 0 ? "Walk_N_Absolute" : "Walk_N_Absolute"));
        }
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
            currentAnimation = animation;
            animator.CrossFade(animation, crossfade);
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
            currentAnimation = animation;
            animator.CrossFade(animation, 0.05f); // Snappier blend for movement
        }
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
    //        Debug.Log($"State '{newState}' is already active. No transition needed.");
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
            //Debug.Log($"Delaying transition to '{newState}' for {delay} seconds...");
            yield return new WaitForSeconds(delay);
        }

        //Debug.Log($"Transitioning to state: {newState} with duration {transitionDuration} seconds.");
        animator.CrossFade(newState, transitionDuration);
        currentState = newState;
    }
}
