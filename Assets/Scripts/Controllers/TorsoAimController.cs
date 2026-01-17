using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Collections;

using UnityEngine;

/// <summary>
/// Player torso/head IK aiming toward camera center, with limited yaw range and animation checks.
/// Works with Cinemachine FreeLook.
/// </summary>
[RequireComponent(typeof(Animator))]
public class PlayerIKLookLimited : MonoBehaviour
{
    [Header("References")]
    public Transform mainCamera;      // Assign Cinemachine FreeLook MainCamera
    public Transform lookTarget;      // Empty GameObject as LookAt target
    public string enemyTag = "LookTarget"; // Tag to auto-find targets (returns null-safe when not found)
    public StringscriptAnimatior animStateMachine; // Reference to your state machine script
    [Header("Auto-find")]
    public LookTargetLerper lookTargetLerper; // component on the placeholder socket that lerps the look target

    [Header("IK Settings")]
    public float aimDistance = 10f;    // How far from camera to place the target
    [Range(0f, 1f)] public float bodyWeight = 0.5f; 
    [Range(0f, 1f)] public float headWeight = 1f;  
    [Range(0f, 1f)] public float eyesWeight = 0.3f;  
    public float blendSpeed = 5f;      // Smoothness of blend

    [Header("Rotation Limits")]
    public float maxYawAngle = 90f;    // Max left/right twist

    private Animator animator;
    private float currentIKWeight = 0f;

    void Start()
    {
        animator = GetComponent<Animator>();

        // Auto-find the look target by tag first (tag name: LookTarget).
        if (lookTarget == null)
        {
            try
            {
                var tagged = GameObject.FindWithTag("LookTarget");
                if (tagged != null) lookTarget = tagged.transform;
            }
            catch { /* tag may not exist; fall through */ }
        }

        // If not found by the explicit tag search above, try again using the configured enemyTag.
        if (lookTarget == null)
        {
            try
            {
                var tagged = GameObject.FindWithTag(enemyTag);
                if (tagged != null) lookTarget = tagged.transform;
            }
            catch { /* tag may not exist; ignore */ }
        }
    }

    void Update()
    {
        // If a lerper is present, ensure our `lookTarget` reference points to it.
        if (lookTargetLerper != null)
        {
            lookTarget = lookTargetLerper.transform;
        }
        GameObject enemyObj = null;
        try
        {
            if (!string.IsNullOrEmpty(enemyTag))
                enemyObj = GameObject.FindWithTag(enemyTag);
        }
        catch { enemyObj = null; }

       

        // If a LookTargetLerper is present, it drives the socket position; do not overwrite it here.
        if (lookTargetLerper == null)
        {
            if (enemyObj != null && lookTarget != null)
            {
                lookTarget.position = enemyObj.transform.position;
            }
            else
            {
                if (mainCamera != null && lookTarget != null)
                {
                    Vector3 camForward = mainCamera.forward;
                    camForward.Normalize();
                    lookTarget.position = mainCamera.position + camForward * aimDistance;
                }
            }
        }

        // Check if we should IK aim
        bool shouldAim = Input.GetMouseButton(1);

        // Block torso twisting for certain animations
        string currentAnim = animStateMachine != null ? animStateMachine.GetCurrentState() : "";
        if (currentAnim.Contains("Dodge") || currentAnim.Contains("Crouch"))
        {
            shouldAim = false; // override
        }

        // Smoothly blend toward target weight
        float targetWeight = shouldAim ? 1f : 0f;
        currentIKWeight = Mathf.Lerp(currentIKWeight, targetWeight, Time.deltaTime * blendSpeed);
    }

    void OnAnimatorIK(int layerIndex)
    {
        if (!animator) return;

        if (currentIKWeight > 0.01f)
        {
            // Check yaw difference to limit torso rotation
            Vector3 charForward = transform.forward;
            Vector3 camForward = mainCamera != null ? mainCamera.forward : transform.forward;
            camForward.y = 0f; // ignore pitch
            camForward.Normalize();

            float yawAngle = Vector3.SignedAngle(charForward, camForward, Vector3.up);

            if (Mathf.Abs(yawAngle) <= maxYawAngle)
            {
                // Within allowed range → apply IK LookAt
                animator.SetLookAtWeight(currentIKWeight, bodyWeight, headWeight, eyesWeight, 0.5f);
                var targetTransform = lookTargetLerper != null ? lookTargetLerper.transform : null;
                if (targetTransform != null)
                    animator.SetLookAtPosition(targetTransform.position);
            }
            else
            {
                // Out of range → smoothly return
                animator.SetLookAtWeight(Mathf.Lerp(currentIKWeight, 0f, Time.deltaTime * blendSpeed));
            }
        }
        else
        {
            // No aiming → reset IK
            animator.SetLookAtWeight(0f);
        }
    }
}

// Utility helpers removed — using `enemyTag` lookup only.
