using System;
using System.Collections;
using Unity.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Burst;
public class AIVision : MonoBehaviour
{
/*    [Header("Vision Settings")]
    public float viewDistance = 120f;
    //[SerializedField]
    private NativeArray<RaycastCommand> viewRays = new NativeArray<RaycastCommand>(1,Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
    private NativeArray<RaycastHit> rayResults = new NativeArray<RaycastHit>(20,Allocator.Persistent,NativeArrayOptions.UninitializedMemory);
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
    private void OnDestroy(){
      viewRays.Dispose();
      rayResults.Dispose();
    }
    private void Start(){
      eyePoint = transform;
      eyePoint.Translate(0f,eyeHeight,0f);
      viewRays[0] = new RaycastCommand(eyePoint.forward,eyePoint.position,QueryParameters.Default, viewDistance);

    }
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
//SOMETHING IS DEEPLY WRONG
    struct PopulateVector3Arrays:IJobFor{
      public NativeArray<Vector3> target;
      public Vector3 value;
      public void Execute(int i){
        target[i] = value;
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
        //Debug.Log($"[AIVision] AngleToPlayer={angleToPlayer:0.0}°, MaxAllowed={viewAngle * 0.5f}°");

        if (angleToPlayer >= viewAngle * 0.5f)
        {
            Debug.Log("[AIVision] Player outside vision cone");
            return;
        }
        
        //goto NewRays;
        OldRays:
        if (Physics.Raycast(origin, dir, out RaycastHit hit, detectRange) && hit.collider.CompareTag("Player"))
        {
            Detected(hit,isCrouching);
            return;// reset memory timer
        }else{
          goto NoHit;
        }
        NewRays:
        NoHit:
        Debug.Log($"[AIVision] Line of sight blocked by {hit.colliderInstanceID}");

        
    }
    private void Detected(in RaycastHit hit, bool isCrouching){
      Debug.Log($"[AIVision] Player detected! (Crouching={isCrouching}) Distance={hit.distance:0.0}");
      OnPlayerDetected?.Invoke(hit.collider.transform, isCrouching);

      // Update last seen info
      lastSeenPosition = hit.collider.transform.position;
      lastPlayerTransform = hit.collider.transform;
      memoryTimer = 0f;

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
    }*/

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
        if (angleToPlayer <= viewAngle * 0.5f)
        {
            // Store ray for the batcher to process this frame
          rayOrigin = origin;
          rayDirection = dir;
          currentViewDistance = detectRange;
          //goto Serial;
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
        if (currentViewDistance <= 0f)
            return;
        Debug.Log($"hit {hit.colliderInstanceID}");
        if (hit.colliderInstanceID != 0 && hit.collider.CompareTag("Player"))
        {
            Debug.Log($"{this.GetInstanceID()} saw Player");
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
        Gizmos.DrawRay(rayOrigin, rayDirection * viewDistance);

        Vector3 left = Quaternion.Euler(0, -viewAngle / 2f, 0) * transform.forward;
        Vector3 right = Quaternion.Euler(0, viewAngle / 2f, 0) * transform.forward;
        Gizmos.DrawRay(origin, left * viewDistance);
        Gizmos.DrawRay(origin, right * viewDistance);
    }
}
