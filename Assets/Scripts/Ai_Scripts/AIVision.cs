using System;
using System.Collections;
using Unity.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Burst;
using static targettingList;
/*
 * close enough, welcome back ecs sharedcomponent
 *
 * -Brownie
*/
//make an array holding different common values

public struct AIVisionSettings{
  public readonly float maxViewDistance;
  public readonly float minViewDistance;
  public readonly float fovAngle;
  public readonly float eyeHeight;

}

public struct AIAttentionSettings{
  public readonly int detectSpeed;
  public readonly int forgetSpeed;
  public readonly int gracePeriod;
}
//Bastardized "sharedcomponent" for QueryParameters
public ref struct targettingList{
  public static readonly QueryParameters[] targetLists=  {
    new (
       unchecked((int) 0xFFFFFF7F), false,
       default, false
       ),

  } ;
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
    public float CosFOV = 0.5f;

    [Header("Memory")]
    readonly static int gracePeriod = 100;
    
    public QueryParameters rayTargeting = targetLists[0];
    [Header("Memory Tracking")]
    public bool aggro = false; 
    public int ticks = 0;

    [HideInInspector] public Vector3 rayOrigin;
    [HideInInspector] public Vector3 rayDirection;
    [HideInInspector] public float currentViewDistance;
    public AIState state = AIState.Idle;


    [BurstCompile]
    void OnPlayerLost(){
      switch(state){
        case AIState.Idle: 
          
          break;
        case AIState.Chase: 
          ticks = gracePeriod;
          // state = AIState.Investigate;
          break;
        case AIState.Investigate:
          //TODO Do not use magic number
          if(ticks > 0) ticks -= 1;
          else state = AIState.Idle;
          break;
        default: throw new  InvalidOperationException("Reached Impossible State");
      }
    }

    void Start(){
      SphereCollider ESPTrigger;
      bool hasTrigger = this.TryGetComponent(out ESPTrigger);
      if(!hasTrigger)
        ESPTrigger = gameObject.AddComponent(typeof(SphereCollider)) as SphereCollider;
      ESPTrigger.radius = viewDistance;
    }
    [BurstCompile]
    void OnPlayerSeen(){
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

    void FixedUpdate() {
      if(aggro)
        OnPlayerSeen();
      else
        OnPlayerLost();
      rayOrigin = transform.position;
      rayOrigin.y += eyeHeight; 
    }
    void OnTriggerExit(Collider other){
      if(other.CompareTag("Player")){
        aggro = false;
        AIVisionBatcher.Instance?.Unregister(this);
      }
    }
    void OnTriggerStay(Collider other)
    {
      if (!other.CompareTag("Player")) goto Fail;
// less lines and kinder to my laptop with editor

      bool hasPlayer = other.TryGetComponent(out ThirdPersonBasic player);
      Debug.Log($"plyerID{GameObject.FindWithTag("Player").GetComponent<Collider>().GetInstanceID()} == detect{other.GetInstanceID()}");

      if (!hasPlayer || player == null) goto Fail;

      bool isCrouching = player.isCrouching;
      currentViewDistance = isCrouching ? viewDistance * crouchDetectionModifier : viewDistance;

      Vector3 targetPos = other.transform.position /*+ Vector3.up * (isCrouching ? 0.5f : 1.2f)*/;
      rayDirection = (targetPos - rayOrigin).normalized;
      float angleToPlayer = Vector3.Dot(transform.forward, rayDirection);
      if (angleToPlayer < CosFOV) goto Fail;
      if (AIVisionBatcher.Instance != null){
        cmd = new RaycastCommand(rayOrigin, rayDirection, rayTargeting, currentViewDistance);
        AIVisionBatcher.Instance.Register(this);
      }else{
          //Debug.DrawLine(rayOrigin, currentViewDistance* rayDirection, Color.black);
        aggro = 
          Physics.Raycast(
              rayOrigin,
              rayDirection,
              out RaycastHit hit,
              currentViewDistance,
              unchecked((int) 0xFFFFFF7F)
          );
        aggro = aggro && hit.collider.CompareTag("Player");
      }
      return;
      Fail:
        AIVisionBatcher.Instance?.Unregister(this);
    }
    public void ProcessVisionResult(bool hit){
      aggro = hit;
    }

    public void ProcessVisionResult(RaycastHit hit){
      aggro = hit.collider?.CompareTag("Player") ?? false;
    }

    void OnDrawGizmosSelected()
    {
      //For converting rad to deg. derived from 180 * 113 /355 up to 8 significant figures
      const float converter = 57.2957746f;
      float viewAngle =Mathf.Acos(CosFOV) * converter;
        Gizmos.color = Color.yellow;
        //Gizmos.DrawRay(rayOrigin, rayDirection * viewDistance);

        Vector3 left = Quaternion.Euler(0, -viewAngle , 0) * transform.forward;
        Vector3 right = Quaternion.Euler(0, viewAngle, 0) * transform.forward;
        Gizmos.DrawRay(rayOrigin, left * viewDistance);
        Gizmos.DrawRay(rayOrigin, right * viewDistance);
    }
}
