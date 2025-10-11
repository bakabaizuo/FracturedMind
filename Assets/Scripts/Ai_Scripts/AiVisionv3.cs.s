using System;
using UnityEngine;

//[RequireComponent(typeof(Collider))]
public class AIVision3 : MonoBehaviour
{
    [Header("Vision Settings")]
    public float viewDistance = 20f;
    public float viewAngle = 120f;
    public float eyeHeight = 1.6f;

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
      Debug.Log("updated");
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
    void OnTriggerEnter(Collider other){
      Debug.Log("Triggered");
      Debug.Log(other.name);
    }
    void OnTriggerStay(Collider other)
    {

      Debug.Log("Still Triggered");
        if (!other.CompareTag("Player")) return;

        var player = other.GetComponent<ThirdPersonBasic>();
        if (player == null) return;

        bool isCrouching = player.isCrouching;
        float detectRange = isCrouching ? viewDistance * crouchDetectionModifier : viewDistance;

        Vector3 origin = transform.position + Vector3.up * eyeHeight;
        Vector3 targetPos = other.transform.position + Vector3.up * (isCrouching ? 0.5f : 1.2f);
        Vector3 dir = (targetPos - origin).normalized;
        float angleToPlayer = Vector3.Angle(transform.forward, dir);
        Debug.Log($"theta = {angleToPlayer}");
        goto oldray;
        if (angleToPlayer <= viewAngle * 0.5f)
        {
            // Store ray for the batcher to process this frame
            rayOrigin = origin;
            rayDirection = dir;
            currentViewDistance = detectRange;
        }
        else
        {
            // Skip — player not within cone
            currentViewDistance = 0f;
        }
        oldray:
        if (Physics.Raycast(origin, dir, out RaycastHit hit, detectRange) && hit.collider.CompareTag("Player"))
        {
            ProcessVisionResult(hit);
            return;// reset memory timer
        }else{
          goto NoHit;
        }
        NewRays:
        NoHit:
        Debug.Log($"[AIVision] Line of sight blocked by {hit.colliderInstanceID}");
    }

    public void ProcessVisionResult(RaycastHit hit)
    {
        Debug.Log(currentViewDistance);
        if (currentViewDistance <= 0f)
            return;
        Debug.Log("CASTING");
        Debug.Log(hit.collider.name);
        if (hit.collider != null && hit.collider.CompareTag("Player"))
        {
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
        Gizmos.DrawRay(origin, transform.forward * viewDistance);

        Vector3 left = Quaternion.Euler(0, -viewAngle / 2f, 0) * transform.forward;
        Vector3 right = Quaternion.Euler(0, viewAngle / 2f, 0) * transform.forward;
        Gizmos.DrawRay(origin, left * viewDistance);
        Gizmos.DrawRay(origin, right * viewDistance);
    }
}
