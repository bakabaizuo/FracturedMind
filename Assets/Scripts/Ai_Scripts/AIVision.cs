using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Unity.Burst;
    [BurstCompile]
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
    private float3? lastSeenPosition;
    private Transform lastPlayerTransform;
    public Ray viewRay;
    private void onCreate(){
      viewRay = new Ray();
    }
    private void Update()
    {
        // Memory timer: forget after duration
        if (lastSeenPosition.HasValue)
        {
            memoryTimer += Time.deltaTime;
            if (memoryTimer <= memoryDuration)
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
Debug.Log("Found");
        ThirdPersonBasic player = other.GetComponent<ThirdPersonBasic>();
        if (player == null) return;
        bool isCrouching = player.isCrouching;
        float detectRange = /*isCrouching ? viewDistance * crouchDetectionModifier :*/ viewDistance;
        float3 origin = (eyePoint != null) ? eyePoint.position : transform.position + Vector3.up * eyeHeight;
        float3 targetPos = other.transform.position + Vector3.up /* (isCrouching ? 0.5f : 1.2f)*/;
        float3 dif = targetPos-origin;

        //float angleToPlayer = Vector3.Angle(transform.forward, dir);
        //Debug.Log($"[AIVision] AngleToPlayer={angleToPlayer:0.0}°, MaxAllowed={viewAngle * 0.5f}°");
        float difSqr = math.dot(dif,dif);

//BrowNie: If I have the will, turn this into a spatial partitioning algo.
//Basically make a (static) function that converts coordinates to an int called partition
// and store in a data structure. Just need to a look up for the adjacent partitions in view for player.
    if( (detectRange*detectRange) >= difSqr){
          Debug.Log("Player out of range");
          return;
    }
        //Compiler should be smart enough
        float3 dir = 1.0f/difSqr * dif;


        if (math.dot(dir,transform.forward) > math.cos( viewAngle*0.5f ))
        {
            Debug.Log("[AIVision] Player outside vision cone");
            return;
        }
        RaycastHit hit;
        if (Physics.Raycast(origin, dir, out hit, detectRange) && hit.collider.CompareTag("Player"))
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
