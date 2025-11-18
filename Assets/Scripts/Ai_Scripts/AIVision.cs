using System;
using System.Collections;
using Unity.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Burst;
using static AIConfigs.targettingList;
/*
 * close enough, welcome back ecs sharedcomponent
 *
 * -Brownie
*/
//make an array holding different common values



public class AIVision : MonoBehaviour
{
  //ECS Would be nice if it had any good pathfinding 
  RaycastCommand cmd;
  //TODO: make this a config probably using the above structs
    [Header("Vision Settings")]
      public    BaseEnemyConfiguration config;
    // public float viewDistance = 2f;
    // public float eyeHeight = 1.6f;
    // public float crouchDetectionModifier = 0.5f;
    // the cosine of planned viewangle
    // public float fovCosTheta = 0.5f;

    [Header("Memory")]
    readonly static int gracePeriod = 10;
    
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
          state = AIState.Investigate;
          break;
        case AIState.Investigate:
          //TODO Do not use magic number
          if(ticks > 0) {ticks -= 1;Debug.Log($"{this.name} cooling");}
          else {
            Debug.Log($"{this.name}:IForgor");
            state = AIState.Idle;
            // AIVisionBatcher.Instance?.Unregister(this);
          }
          break;
        default: throw new  InvalidOperationException("Reached Impossible State");
      }
    }

    void Start(){
      SphereCollider ESPTrigger;
      bool hasTrigger = this.TryGetComponent(out ESPTrigger);
      if(!hasTrigger)
        ESPTrigger = gameObject.AddComponent(typeof(SphereCollider)) as SphereCollider;
      ESPTrigger.radius = config.visionSettings.viewDistance;
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
      Debug.Log($"{this.name} {aggro}");
      if(aggro)
        OnPlayerSeen();
      else
        OnPlayerLost();
      rayOrigin = transform.position;
      rayOrigin.y += config.visionSettings.eyeHeight; 
    }
    void OnTriggerExit(Collider other){
      if(other.CompareTag("PlayerCollider")){
        aggro = false;
        // AIVisionBatcher.Instance?.Unregister(this);
      }
    }
    void OnTriggerStay(Collider other)
    {
      if (!other.CompareTag("PlayerCollider")) return;
// less lines and kinder to my laptop with editor

      bool hasPlayer = other.TryGetComponent(out ThirdPersonBasic player);

      if (!hasPlayer || player == null) {aggro = false; return;}

      bool isCrouching = player.isCrouching;
      currentViewDistance = isCrouching ? config.visionSettings.viewDistance * config.visionSettings.crouchDetectionModifier : config.visionSettings.viewDistance;

      LayerMask layerMask = 
        config.npcTargets.layerMask;
        // unchecked((int) 0xFFFFFEFF);
      Vector3 targetPos = other.transform.position + Vector3.up * (isCrouching ? 0.5f : 1.2f);
      rayDirection = (targetPos - rayOrigin).normalized;
      float angleToPlayer = Vector3.Dot(transform.forward, rayDirection);
      if (angleToPlayer < config.visionSettings.fovCosTheta) {aggro = false; return;}
      Debug.Log(AIVisionBatcher.Instance?.isActiveAndEnabled ?? false);
      if (AIVisionBatcher.Instance?.isActiveAndEnabled ?? false){
        cmd = new RaycastCommand(rayOrigin, rayDirection, rayTargeting, currentViewDistance);
        AIVisionBatcher.Instance?.Register(this);
      }else{
          Debug.DrawLine(rayOrigin, currentViewDistance * rayDirection, Color.black);
        aggro = 
          Physics.Raycast(
              rayOrigin,
              rayDirection,
              out RaycastHit hit,
              currentViewDistance,
              layerMask
              // config.npcTargets.query.layerMask
          ) &&
          hit.collider.CompareTag("PlayerCollider");
        
      }
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
      float viewAngle =Mathf.Acos(config.visionSettings.fovCosTheta) * converter;
        Gizmos.color = Color.yellow;
        //Gizmos.DrawRay(rayOrigin, rayDirection * config.visionSettings.viewDistance);

        Vector3 left = Quaternion.Euler(0, -viewAngle , 0) * transform.forward;
        Vector3 right = Quaternion.Euler(0, viewAngle, 0) * transform.forward;
        Gizmos.DrawRay(rayOrigin, left * config.visionSettings.viewDistance);
        Gizmos.DrawRay(rayOrigin, right * config.visionSettings.viewDistance);
    }
}
