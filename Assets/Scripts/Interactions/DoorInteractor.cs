using UnityEngine;

/// <summary>
/// Handles door interaction with IK hand placement, head-look, smooth rotation,
/// and cancels if the player moves during the interaction.
/// </summary>
public class DoorInteractor : MonoBehaviour
{
    [Header("Animator & IK Settings")]
    [Tooltip("The Animator component of the character.")]
    public Animator animator;

    [Header("Door Interaction Targets")]
    [Tooltip("The transform of the door handle where the hand should align.")]
    public Transform doorHandle;

    [Tooltip("Reference to the door script that will be triggered.")]
    public Door doorScript;

    [Header("Interaction Settings")]
    [Tooltip("Delay before the door actually opens (simulating knob turning).")]
    public float knobTurnDelay = 0.5f;

    [Tooltip("Maximum distance to be able to interact with the door.")]
    public float interactionDistance = 2.0f;

    [Tooltip("How quickly the IK weight blends in/out.")]
    public float ikBlendSpeed = 3f;

    [Header("Character Rotation Settings")]
    [Tooltip("How fast the character rotates toward the door.")]
    public float rotationSpeed = 5f;

    [Header("Head Look IK")]
    [Tooltip("Enable head look toward the door during interaction.")]
    public bool enableHeadLook = true;

    [Header("Movement Cancel Settings")]
    [Tooltip("If true, moving will cancel the interaction.")]
    public bool cancelIfMovementDetected = true;

    private bool isOpeningDoor = false;     // True while interacting
    private bool hasTriggeredDoor = false;  // Ensures door opens only once
    private float currentIKWeight = 0f;     // Smooth blending IK weight
    private AvatarIKGoal activeHand = AvatarIKGoal.RightHand; // Hand currently used
    private bool isRotatingToDoor = false;  // True while rotating toward door
    private Vector3 initialPositionDuringInteract; // Track movement start

    void Update()
    {
        // Press E to try interacting
        if (Input.GetKeyDown(KeyCode.E))
        {
            TryOpenDoor();
        }

        // Smoothly decrease IK weight when not interacting
        if (!isOpeningDoor && currentIKWeight > 0f)
        {
            currentIKWeight = Mathf.MoveTowards(currentIKWeight, 0f, Time.deltaTime * ikBlendSpeed);
        }

        // If rotating toward the door, do it smoothly
        if (isRotatingToDoor && doorHandle != null)
        {
            SmoothRotateToward(doorHandle.position);
        }

        // Cancel interaction if the player moves
        if (isOpeningDoor && cancelIfMovementDetected && PlayerMoved())
        {
            CancelInteraction("Player moved, canceling door interaction.");
        }
    }

    /// <summary>
    /// Starts the door opening interaction if all references are valid and within range.
    /// </summary>
    public void TryOpenDoor()
    {
        if (doorHandle == null || doorScript == null)
        {
            Debug.LogWarning("Door handle or door script not assigned!");
            return;
        }

        float dist = Vector3.Distance(transform.position, doorHandle.position);
        if (dist > interactionDistance)
        {
            Debug.Log("Too far from the door to interact.");
            return;
        }

        // Choose which hand is closer to the door handle
        activeHand = GetClosestHand();

        // Save current position to detect movement
        initialPositionDuringInteract = transform.position;

        // Start smooth rotation first
        isRotatingToDoor = true;

        // After rotation is done, IK will begin automatically
        StartCoroutine(StartInteractionAfterRotation());
    }

    /// <summary>
    /// Cancels the door interaction safely.
    /// </summary>
    private void CancelInteraction(string reason)
    {
        Debug.Log(reason);
        isOpeningDoor = false;
        hasTriggeredDoor = false;
        isRotatingToDoor = false;
        // Smooth fade-out of IK will naturally occur in Update()
    }

    /// <summary>
    /// Checks if the player moved significantly during interaction.
    /// </summary>
    private bool PlayerMoved()
    {
        // Simple check: did the player move more than 0.1m from initial position?
        float movementThreshold = 0.1f;
        float movedDistance = Vector3.Distance(transform.position, initialPositionDuringInteract);
        return movedDistance > movementThreshold;
    }

    /// <summary>
    /// Chooses the closest hand to the door handle.
    /// </summary>
    private AvatarIKGoal GetClosestHand()
    {
        Transform rightShoulder = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        Transform leftShoulder = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);

        float distRight = Vector3.Distance(rightShoulder.position, doorHandle.position);
        float distLeft = Vector3.Distance(leftShoulder.position, doorHandle.position);

        return distRight < distLeft ? AvatarIKGoal.RightHand : AvatarIKGoal.LeftHand;
    }

    /// <summary>
    /// Smoothly rotates character toward a target position.
    /// </summary>
    private void SmoothRotateToward(Vector3 targetPos)
    {
        Vector3 direction = (targetPos - transform.position);
        direction.y = 0f; // Ignore vertical
        if (direction.sqrMagnitude < 0.01f) return;

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);

        // Check if almost facing the door
        float angle = Quaternion.Angle(transform.rotation, targetRotation);
        if (angle < 3f) // Close enough
        {
            isRotatingToDoor = false;
        }
    }

    /// <summary>
    /// Wait until rotation is done before starting IK interaction.
    /// </summary>
    private System.Collections.IEnumerator StartInteractionAfterRotation()
    {
        // Wait until rotation finishes
        while (isRotatingToDoor)
            yield return null;

        // If the player already moved, cancel instead of starting
        if (PlayerMoved())
        {
            CancelInteraction("Player moved before interaction started.");
            yield break;
        }

        // Now begin IK interaction
        isOpeningDoor = true;
        hasTriggeredDoor = false;
    }

    void OnAnimatorIK(int layerIndex)
    {
        if (!animator) return;

        // Head look at the door during interaction
        if (enableHeadLook && doorHandle != null)
        {
            float headLookWeight = isOpeningDoor ? 1f : 0f;
            animator.SetLookAtWeight(headLookWeight, 0.4f, 0.6f, 0.6f, 0.5f);
            animator.SetLookAtPosition(doorHandle.position);
        }

        if (isOpeningDoor)
        {
            // Smoothly increase IK weight toward 1
            currentIKWeight = Mathf.MoveTowards(currentIKWeight, 1f, Time.deltaTime * ikBlendSpeed);

            // Apply IK to the chosen hand
            animator.SetIKPositionWeight(activeHand, currentIKWeight);
            animator.SetIKRotationWeight(activeHand, currentIKWeight);
            animator.SetIKPosition(activeHand, doorHandle.position);
            animator.SetIKRotation(activeHand, doorHandle.rotation);

            // Trigger the door after a delay when hand is fully on knob
            if (!hasTriggeredDoor && currentIKWeight >= 0.95f)
            {
                hasTriggeredDoor = true;
                StartCoroutine(OpenDoorAfterDelay(knobTurnDelay));
            }
        }
        else
        {
            // Fade out IK smoothly when not interacting
            animator.SetIKPositionWeight(activeHand, currentIKWeight);
            animator.SetIKRotationWeight(activeHand, currentIKWeight);
        }
    }

    /// <summary>
    /// Delays the door opening to simulate knob turning.
    /// </summary>
private System.Collections.IEnumerator OpenDoorAfterDelay(float delay)
{
    yield return new WaitForSeconds(delay);

    // If the player moved before the delay ended, cancel
    if (PlayerMoved())
    {
        CancelInteraction("Player moved before door could toggle.");
        yield break;
    }

    // ✅ Toggle the door (open if closed, close if open)
    doorScript.ToggleDoor();

    // Stop IK interaction after done
    isOpeningDoor = false;
}
}

