using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Enemy animation controller based on StringscriptAnimator logic.
/// Driven by AI states rather than player input.
/// </summary>
[RequireComponent(typeof(Animator))]
public class EnemyAnimatorController : MonoBehaviour
{
    private Animator animator;
    private string currentAnimation = "";
    private Coroutine transitionCoroutine;

    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    /// <summary>
    /// Play a specific animation immediately
    /// </summary>
    public void PlayAnimation(string animation, float crossfade = 0.1f)
    {
        if (currentAnimation == animation) return;

        currentAnimation = animation;
        animator.CrossFade(animation, crossfade);
    }

    /// <summary>
    /// Optional smooth state change with delay
    /// </summary>
    public void ChangeState(string newState, float transitionDuration = 0.2f, float delay = 0f)
    {
        if (currentAnimation == newState) return;

        if (transitionCoroutine != null) StopCoroutine(transitionCoroutine);
        transitionCoroutine = StartCoroutine(TransitionToState(newState, transitionDuration, delay));
    }

    private IEnumerator TransitionToState(string newState, float duration, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        animator.CrossFade(newState, duration);
        currentAnimation = newState;
    }

    /// <summary>
    /// Determine animation from movement vector
    /// </summary>
    public void SetMovementAnimation(Vector3 velocity, bool isRunning = true)
    {
        if (velocity.sqrMagnitude < 0.01f)
        {
            PlayAnimation("Idle_Absolute", 0.1f);
            return;
        }

        // Simplified directional animation (N/S/E/W)
        if (Mathf.Abs(velocity.z) > Mathf.Abs(velocity.x))
        {
            PlayAnimation(isRunning ? "Run_N_Absolute" : "Walk_N_Absolute", 0.05f);
        }
        else
        {
            PlayAnimation(isRunning ? "Run_E_Absolute" : "Walk_N_Absolute", 0.05f);
        }
    }
}
