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
//    private Vector3? lastSeenPosition;
//    private Transform lastPlayer;

    [HideInInspector] public Vector3 rayOrigin;
    [HideInInspector] public Vector3 rayDirection;
    [HideInInspector] public float currentViewDistance;
    private Vector3 eyePosition ;
    public AIState state = AIState.Idle;

    //public bool HasLastSeenPosition() => lastSeenPosition.HasValue;
    //public Vector3 GetLastSeenPosition() => lastSeenPosition.Value;
    void Start(){

     eyePosition = new Vector3(0,eyeHeight,0);

    }
    void OnTriggerExit(Collider other){
      if(other.CompareTag("Player")){

        state = AIState.Investigate;
        AIVisionBatcher.Instance?.Unregister(this);
      }
    }
    void FixedUpdate()
    {
      origin = 
          transform.position + eyePosition;



        if (state == AIState.Investigate )
        {
            memoryTimer += Time.deltaTime;
        if (memoryTimer > memoryDuration)
        


        {
              state = AIState.Idle;
                //lastSeenPosition = null;
                //lastPlayer = null;
                memoryTimer = 0f;
            }
    }}
    private  QueryParameters parameters = new QueryParameters(
       unchecked((int) 0xFFFFFF7F), false,
       default, false
       );
    Vector3 origin;
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
        if (player == null) return;

        bool isCrouching = player.isCrouching;
        float detectRange = isCrouching ? viewDistance * crouchDetectionModifier : viewDistance;

        Vector3 targetPos = other.transform.position /*+ Vector3.up * (isCrouching ? 0.5f : 1.2f)*/;
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
          
           goto Batched;
          }
          //goto Serial;
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
            Serial:
              RaycastHit hit;
              Physics.Raycast(origin,dir, out hit,detectRange, unchecked((int) 0xFFFFFF7F) );
            ProcessVisionResult(hit);
              return;
          Batched:
          AIVisionBatcher.Instance?.Register(this);
      
    }

    public void ProcessVisionResult(RaycastHit hit){
//       Debug.Log($"ID = {hit.colliderInstanceID} is Null: {hit.collider == null}") ;
      if(hit.collider != null && hit.collider.CompareTag("Player")){

       // Debug.Log($"{this.name} sees: {hit.collider.tag}");
       state = AIState.Chase;
        //Debug.DrawRay(rayOrigin, rayDirection*viewDistance);
          //  lastSeenPosition = hit.collider.transform.position;
            //lastPlayer = hit.collider.transform;
            memoryTimer = 0f;
      }else
      {
          state = AIState.Investigate;
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
