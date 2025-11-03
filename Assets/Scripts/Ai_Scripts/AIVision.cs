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
  RaycastCommand cmd;
    [Header("Vision Settings")]
    public float viewDistance = 20f;
    public float eyeHeight = 1.6f;
    public float crouchDetectionModifier = 0.5f;
    // the cosine of planned viewangle
    readonly float cosine = 0.5f;
    public float viewAngle =Mathf.Acos(cosine);

    
    [Header("Memory")]
    readonly static int gracePeriod = 100;
    
    public QueryParameters parameters = new QueryParameters(
       unchecked((int) 0xFFFFFF7F), false,
       default, false
       );
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
          //TODO Do not use magic number
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
          //TODO Do not use magic number
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


    public RaycastCommand GetCommand() => cmd;
    void FixedUpdate()
    {
      if(aggro)
        OnPlayerSeen();
      else
        OnPlayerLost();
      rayOrigin = transform.position;
      rayOrigin.y += eyeHeight; 
    }
    void OnTriggerExit(Collider other){
      if(other.CompareTag("Player"))
        OnPlayerLost();
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
      float angleToPlayer = Vector3.Dot(transform.forward, dir);
      if (angleToPlayer > cosine) return;
      rayDirection = dir;
      currentViewDistance = detectRange;
      //Problems:
      //Batcher too inconsistent
      //The angle calculations are broken in serial raycasts
      if (AIVisionBatcher.Instance == null){
        bool detected = Physics.Raycast(rayOrigin,dir, out RaycastHit hit,detectRange, unchecked((int) 0xFFFFFF7F) );
        aggro = detected && hit?.CompareTag("Player") ?? false;
      }
      else{
        cmd = new RaycastCommand(rayOrigin, dir, parameters, detectRange);
        AIVisionBatcher.Instance?.Register(this);

      }
      
    
    }

    public void ProcessVisionResult(RaycastHit hit){
      aggro = hit?.CompareTag("Player")??false;

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
