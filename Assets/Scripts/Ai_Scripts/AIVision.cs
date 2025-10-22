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
    private  float memoryDuration = 1000f;

    private float memoryTimer = 0f;
    private Vector3? lastSeenPosition;
    private Transform lastPlayer;

    [HideInInspector] public Vector3 rayOrigin;
    [HideInInspector] public Vector3 rayDirection;
    [HideInInspector] public float currentViewDistance;

    public event Action<Transform, bool> OnPlayerDetected;
    public AIState state = AIState.Idle;

    public bool HasLastSeenPosition() => lastSeenPosition.HasValue;
    public Vector3 GetLastSeenPosition() => lastSeenPosition.Value;
    void OnTriggerExit(Collider other){
      if(other.CompareTag("Player")){

        state = AIState.Investigate;
        AIVisionBatcher.Instance?.Unregister(this);
      }
    }
    void Update()
    {

        if (state == AIState.Investigate )
        {
            memoryTimer += Time.deltaTime;
        if (memoryTimer > memoryDuration)
            {
              state = AIState.Idle;
                lastSeenPosition = null;
                lastPlayer = null;
                memoryTimer = 0f;
            }
    }}
    private  QueryParameters parameters = new QueryParameters(
       unchecked((int) 0xFFFFFF7F), false,
       default, false
       );
    struct getNearest:IJob
    {
      [ReadOnly]
       public NativeArray<RaycastHit> hits;
       public NativeArray<RaycastHit>firstHit;
       public void Execute(){
         
        firstHit[0] = hits[0];
        if(firstHit[0].colliderInstanceID == 0)
          return;
        
        for(int i = 1; i < hits.Length; i++){
          if(hits[i].colliderInstanceID == 0)
            return;
          if(firstHit[0].distance<hits[i].distance)
            firstHit[0] = hits[i];
        }
       }

    }
    void OnTriggerStay(Collider other)
    {

        if (!other.CompareTag("Player")) return;

        var player = other.GetComponent<ThirdPersonBasic>();
        int playerID = other.GetInstanceID();
        if (player == null) return;

        bool isCrouching = player.isCrouching;
        float detectRange = isCrouching ? viewDistance * crouchDetectionModifier : viewDistance;

        Vector3 origin = transform.position + Vector3.up * eyeHeight;
        Vector3 targetPos = other.transform.position + Vector3.up * (isCrouching ? 0.5f : 1.2f);
        Vector3 dir = (targetPos - origin).normalized;
        float angleToPlayer = Vector3.Angle(transform.forward, dir);
        //Debug.Log($"theta = {angleToPlayer<=viewAngle*0.5f}");
        //goto oldray;
        if (viewDistance <= 0f||angleToPlayer > viewAngle * 0.5f)
        {
          return;
            // Store ray for the batcher to process this frame
        }
            // Skip — player not within cone

          rayOrigin = origin;
          rayDirection = dir;
          currentViewDistance = detectRange;
          if (AIVisionBatcher.Instance != null){
          
           goto Parallel;
          }
            NativeArray<RaycastCommand> cmds = new NativeArray<RaycastCommand>(1, Allocator.TempJob,NativeArrayOptions.UninitializedMemory);
            NativeArray<RaycastHit> results = new NativeArray<RaycastHit>(20, Allocator.TempJob,NativeArrayOptions.UninitializedMemory);
            cmds[0] = new RaycastCommand(rayOrigin, rayDirection, parameters, currentViewDistance);

            JobHandle jobQueue = RaycastCommand.ScheduleBatch(cmds, results,1,default);
            NativeArray<RaycastHit> firstHit = new NativeArray<RaycastHit>(1,Allocator.TempJob,NativeArrayOptions.UninitializedMemory);
             jobQueue = new getNearest{
              hits = results,
              firstHit = firstHit
            }.Schedule(jobQueue);

            //rayJob.Complete();

            jobQueue.Complete();
            
            cmds.Dispose();
            results.Dispose();

            ProcessVisionResult(firstHit[0]);
            
            firstHit.Dispose();
            
            return;
          Parallel:
          AIVisionBatcher.Instance?.Register(this);
      
    }

    public void ProcessVisionResult(RaycastHit hit){
//       Debug.Log($"ID = {hit.colliderInstanceID} is Null: {hit.collider == null}") ;
      if(hit.collider != null && hit.collider.CompareTag("Player")){

       // Debug.Log($"{this.name} sees: {hit.collider.tag}");
       state = AIState.Chase;
        Debug.DrawRay(rayOrigin, rayDirection*viewDistance);
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
