using System;
using System.Collections;
using Unity.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Burst;
/*
 * close enough, welcome back ecs sharedcomponent
 *
 * -Brownie
*/
//make an array holding different common values

public struct AIVisionSettings{
  public float maxViewDistance;
  public float minViewDistance;
  public float fovAngle;
  public float eyeHeight;

}

public struct AIAttentionSettings{
  public int AttentionSpan;
  public int gracePeriod;
}


public class AIVision : MonoBehaviour
{
  //ECS Would be nice if it had any good pathfinding 
    [Header("Vision Settings")]
    public float viewDistance = 20f;
    public float viewAngle = 120f;
    public float eyeHeight = 1.6f;
    public float crouchDetectionModifier = 0.5f;
    
    [Header("Memory")]
    readonly static int gracePeriod = 100;
    
    public QueryParameters parameters = new QueryParameters(
       unchecked((int) 0xFFFFFF7F), false,
       default, false
       );
    Coroutine timer=null;
    [Header("Memory Tracking")]
    private bool aggro = false; 
    public int ticks = 0;

    [HideInInspector] public Vector3 rayOrigin;
    [HideInInspector] public Vector3 rayDirection;
    [HideInInspector] public float currentViewDistance;
    public AIState state = AIState.Idle;


    void OnPlayerLost(){
      aggro = false;
      switch(state){
        case AIState.Idle: 
          
          break;
        case AIState.Chase: 
          ticks = gracePeriod;
          state = AIState.Investigate;
          break;
        case AIState.Investigate:
          //TODO do not use a magic number
          if(ticks > 0) ticks -= 1;
          else state = AIState.Idle;
          break;
        default: throw new  InvalidOperationException("Reached Impossible State");
      }
    }
    void OnPlayerSeen(){
      aggro = true;
      switch(state){
        case AIState.Chase:break;
        case AIState.Investigate:

          if(ticks < gracePeriod)
            ticks+=1;
          else
            state = AIState.Chase;
          break;
        case AIState.Idle:
          ticks = 0;
          state = AIState.Investigate;
          break;
        default: throw new  InvalidOperationException("Reached Impossible State");
      }
    }

    void FixedUpdate()
    {
          Debug.Log($"{ticks} {aggro}");
      rayOrigin = transform.position;
      rayOrigin.y += eyeHeight; 
      if( aggro || state != AIState.Investigate ){
        return;
      }
      if( ticks > 0)
        ticks -=1;
      else
        state = AIState.Idle;
    }
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
    void StopChase(){
      aggro = false;

      if ( state == AIState.Chase)
      {
        ticks = gracePeriod;
        state = AIState.Investigate;
      }
    }
    void OnTriggerExit(Collider other){
      if(other.CompareTag("Player")){
        StopChase();
      }
    }
    void OnTriggerStay(Collider other)
    {

        if (!other.CompareTag("Player")) return;

        var player = other.GetComponent<ThirdPersonBasic>();
        if (viewDistance <= 0f||player == null) return;

        bool isCrouching = player.isCrouching;
        float detectRange = isCrouching ? viewDistance * crouchDetectionModifier : viewDistance;

        Vector3 targetPos = other.transform.position /*+ Vector3.up * (isCrouching ? 0.5f : 1.2f)*/;
        Vector3 dir = (targetPos - rayOrigin).normalized;
        float angleToPlayer = Vector3.Angle(transform.forward, dir);
        if (angleToPlayer > viewAngle * 0.5f)
        {
          return;
        }
        rayDirection = dir;
        currentViewDistance = detectRange;
        //Problems:
        //Batcher too inconsistent
        //The angle calculations are broken in serial raycasts
        if (AIVisionBatcher.Instance != null)
         goto Batched;
        goto Serial;
        NativeArray<RaycastCommand> cmds = 
          new NativeArray<RaycastCommand>(1, Allocator.TempJob,NativeArrayOptions.UninitializedMemory);
        NativeArray<RaycastHit> results =
          new NativeArray<RaycastHit>(20, Allocator.TempJob,NativeArrayOptions.UninitializedMemory);
        cmds[0] = new RaycastCommand(rayOrigin, rayDirection, parameters, currentViewDistance);

        JobHandle jobQueue = RaycastCommand.ScheduleBatch(cmds, results,1,default);
        NativeArray<RaycastHit> firstHit =
          new NativeArray<RaycastHit>(1,Allocator.TempJob,NativeArrayOptions.UninitializedMemory);
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
          bool detected = Physics.Raycast(rayOrigin,dir, out hit,detectRange, unchecked((int) 0xFFFFFF7F) );
          if(detected)
            ProcessVisionResult(hit);
          return;
        Batched:
        AIVisionBatcher.Instance?.Register(this);
    
    }

    public void ProcessVisionResult(RaycastHit hit){
      if(hit.collider != null && hit.collider.CompareTag("Player"))
        OnPlayerSeen();
      else
        StopChase();

    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        //Gizmos.DrawRay(rayOrigin, rayDirection * viewDistance);

        Vector3 left = Quaternion.Euler(0, -viewAngle , 0) * transform.forward;
        Vector3 right = Quaternion.Euler(0, viewAngle, 0) * transform.forward;
        Gizmos.DrawRay(rayOrigin, left * viewDistance);
        Gizmos.DrawRay(rayOrigin, right * viewDistance);
    }
}
