using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AIVision : MonoBehaviour
{
    [Header("Vision Settings")]
    public float viewDistance = 20f;
    [Range(0, 360)] public float viewAngle = 180f;
    public float eyeHeight = 1.6f;
    public Transform eyePoint;

    [Header("Crouch Detection Settings")]
    public float crouchDetectionModifier = 0.5f;

    [Header("Memory Settings")]
    public float memoryDuration = 120f; // seconds AI remembers last seen
    private float memoryTimer = 0f;

    // Event callback: (playerTransform, isCrouching)
    public event Action<Transform, bool> OnPlayerDetected;

    // Last seen player info
    private Vector3? lastSeenPosition;
    private Transform lastPlayerTransform;

    private void Update()
    {
        // Memory timer: forget after duration
        if (lastSeenPosition.HasValue)
        {
            memoryTimer += Time.deltaTime;
            if (memoryTimer > memoryDuration)
            {
                Debug.Log("[AIVision] Forgot last seen position");
                lastSeenPosition = null;
                lastPlayerTransform = null;
                memoryTimer = 0f;
            }
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        ThirdPersonBasic player = other.GetComponent<ThirdPersonBasic>();
        if (player == null) return;

        bool isCrouching = player.isCrouching;
        float detectRange = isCrouching ? viewDistance * crouchDetectionModifier : viewDistance;

        Vector3 origin = (eyePoint != null) ? eyePoint.position : transform.position + Vector3.up * eyeHeight;
        Vector3 targetPos = other.transform.position + Vector3.up * (isCrouching ? 0.5f : 1.2f);
        Vector3 dir = (targetPos - origin).normalized;

        float angleToPlayer = Vector3.Angle(transform.forward, dir);
        Debug.Log($"[AIVision] AngleToPlayer={angleToPlayer:0.0}°, MaxAllowed={viewAngle * 0.5f}°");

        if (angleToPlayer < viewAngle * 0.5f)
        {
            if (Physics.Raycast(origin, dir, out RaycastHit hit, detectRange))
            {
                if (hit.collider.CompareTag("Player"))
                {
                    Debug.Log($"[AIVision] Player detected! (Crouching={isCrouching}) Distance={hit.distance:0.0}");
                    OnPlayerDetected?.Invoke(other.transform, isCrouching);

                    // Update last seen info
                    lastSeenPosition = hit.collider.transform.position;
                    lastPlayerTransform = hit.collider.transform;
                    memoryTimer = 0f; // reset memory timer
                }
                else
                {
                    Debug.Log($"[AIVision] Line of sight blocked by {hit.collider.name}");
                }
            }
        }
        else
        {
            Debug.Log("[AIVision] Player outside vision cone");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("[AIVision] Player left awareness zone");
            // Keep last seen position, don’t clear immediately
        }
    }

    // Public methods for AI to access last seen
    public bool HasLastSeenPosition() => lastSeenPosition.HasValue;
    public Vector3 GetLastSeenPosition() => lastSeenPosition ?? transform.position;
    public Transform GetLastSeenPlayerTransform() => lastPlayerTransform;

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 origin = (eyePoint != null) ? eyePoint.position : transform.position + Vector3.up * eyeHeight;

        // Forward direction
        Gizmos.DrawRay(origin, transform.forward * viewDistance);

        // Vision cone boundaries
        Vector3 leftBoundary = Quaternion.Euler(0, -viewAngle / 2, 0) * transform.forward;
        Vector3 rightBoundary = Quaternion.Euler(0, viewAngle / 2, 0) * transform.forward;
        Gizmos.DrawRay(origin, leftBoundary * viewDistance);
        Gizmos.DrawRay(origin, rightBoundary * viewDistance);

        // Last seen position
        if (lastSeenPosition.HasValue)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(lastSeenPosition.Value, 0.3f);
            Gizmos.DrawLine(origin, lastSeenPosition.Value);
        }
    }
}
