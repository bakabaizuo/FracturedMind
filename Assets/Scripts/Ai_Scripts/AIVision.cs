using System;
using System.Collections;
using Unity.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Burst;
public class AIVision : MonoBehaviour
{

    [Header("Vision Settings")]
    public float viewDistance = 20f;
    public float viewAngle = 120f;
    public float eyeHeight = 1.6f;

    public bool aggro = false;
    [Header("Crouch Detection")]
    public float crouchDetectionModifier = 0.5f;

    [Header("Memory")]
    public float memoryDuration = 60f;

    private float memoryTimer = 0f;
    private Vector3? lastSeenPosition;
    private Transform lastPlayer;

    [HideInInspector] public Vector3 rayOrigin;
    [HideInInspector] public Vector3 rayDirection;
    [HideInInspector] public float currentViewDistance;

    public event Action<Transform, bool> OnPlayerDetected;

    void OnEnable()
    {
        AIVisionBatcher.Instance?.Register(this);
    }

    void OnDisable()
    {
        AIVisionBatcher.Instance?.Unregister(this);
    }

    public bool HasLastSeenPosition() => lastSeenPosition.HasValue;
    public Vector3 GetLastSeenPosition() => lastSeenPosition.Value;
    void Update()
    {
        if (lastSeenPosition.HasValue)
        {
            memoryTimer += Time.deltaTime;
            if (memoryTimer > memoryDuration)
            {
                lastSeenPosition = null;
                lastPlayer = null;
                memoryTimer = 0f;
            }
        }
    }
    void OnTriggerStay(Collider other)
    {

        if (!other.CompareTag("Player")) return;

        var player = other.GetComponent<ThirdPersonBasic>();
        if (player == null) return;

        bool isCrouching = player.isCrouching;
        float detectRange = isCrouching ? viewDistance * crouchDetectionModifier : viewDistance;

        Vector3 origin = transform.position + Vector3.up * eyeHeight;
        Vector3 targetPos = other.transform.position + Vector3.up * (isCrouching ? 0.5f : 1.2f);
        Vector3 dir = (targetPos - origin).normalized;
        float angleToPlayer = Vector3.Angle(transform.forward, dir);
        //Debug.Log($"theta = {angleToPlayer<=viewAngle*0.5f}");
        //goto oldray;
        if (viewDistance > 0f||angleToPlayer <= viewAngle * 0.5f)
        {
            // Store ray for the batcher to process this frame
          rayOrigin = origin;
          rayDirection = dir;
          currentViewDistance = detectRange;
          if (AIVisionBatcher.Instance == null)
            goto Serial;
          Parallel:
          AIVisionBatcher.Instance?.Register(this);
          return;
          Serial:
          Physics.Raycast(origin, dir, out RaycastHit hit, detectRange);
         ProcessVisionResult(hit);
          return;
        }
            // Skip — player not within cone
      currentViewDistance = 0f;
      AIVisionBatcher.Instance?.Unregister(this);
    }

    
    public void ProcessVisionResult(RaycastHit hit)
    {
        

        if ( hit.collider.CompareTag("Player"))
        {
       // Debug.Log($"{this.name} sees: {hit.collider.tag}");
        Debug.DrawRay(rayOrigin, rayDirection*viewDistance);
            OnPlayerDetected?.Invoke(hit.collider.transform, false);
            lastSeenPosition = hit.collider.transform.position;
            lastPlayer = hit.collider.transform;
            memoryTimer = 0f;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 origin = transform.position + Vector3.up * eyeHeight;
        //Gizmos.DrawRay(rayOrigin, rayDirection * viewDistance);

        Vector3 left = Quaternion.Euler(0, -viewAngle / 2f, 0) * transform.forward;
        Vector3 right = Quaternion.Euler(0, viewAngle / 2f, 0) * transform.forward;
        Gizmos.DrawRay(origin, left * viewDistance);
        Gizmos.DrawRay(origin, right * viewDistance);
    }
}
