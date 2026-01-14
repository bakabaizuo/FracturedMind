using System;
using UnityEngine;
using Unity.Mathematics;



public class AIVision : MonoBehaviour
{
    [Header("Vision Settings")]
      public  NPCConfiguration config;

    [Header("Memory")]
    [SerializeField]
    int gracePeriod = 10;
    
    LayerMask rayTargeting;
    [Header("Memory Tracking")]
    public bool aggro = false; 
    public int ticks = 0;

    [HideInInspector] public Vector3 rayOrigin;
    [HideInInspector] public float currentViewDistance;
      public AIState state = AIState.Idle;


    //TODO: Remove. put the logic in AIController
    void OnPlayerLost(){
      if(ticks<=0){
        state = AIState.Idle;
        return;
      }
      switch(state){
        case AIState.Idle: 
          break;
        case AIState.Chase: 
          ticks = gracePeriod;
          state = AIState.Investigate;
          break;
        case AIState.Investigate:
          //TODO Do not use magic number
          ticks -= 1;
          break;
       default: throw new  InvalidOperationException("Reached Impossible State");
      }
    }

    [SerializeField]
      SphereCollider ESPTrigger;
    void Start(){
      if(ESPTrigger != null)
        goto SetTrigger;
      bool hasTrigger = this.TryGetComponent(out ESPTrigger);
      if(!hasTrigger)
        ESPTrigger = gameObject.AddComponent(typeof(SphereCollider)) as SphereCollider;
      SetTrigger:
        ESPTrigger.radius = config.visionSettings.viewDistance;
        ESPTrigger.isTrigger = true;

    }
    //TODO: ditto line 33
    void OnPlayerSeen(){
      if(gracePeriod<=ticks){

        state = AIState.Chase;
        return;
      }
      switch(state){
        case AIState.Chase:break;
        case AIState.Investigate:
          
          //TODO Do not use magic number
          ticks+=1;
          break;
        case AIState.Idle:
          ticks = 0;
          state = AIState.Investigate;
          break;
        default: throw new  InvalidOperationException("Reached Impossible State");
      }
    }



    //TODO: ditto line 33
    void FixedUpdate() {
      if(!aggro && AIState.Idle != state)
        OnPlayerLost();
    }
    void OnTriggerExit(Collider other){
      if(other.CompareTag("PlayerCollider"))
        aggro = false;
    }
    float getViewDistance(bool isCrouching)=> isCrouching ? config.visionSettings.viewDistance * config.visionSettings.crouchDetectionModifier : config.visionSettings.viewDistance;

    void OnTriggerStay(Collider other)
    {
      if (!other.CompareTag("PlayerCollider")) return;
      var player = ThirdPersonBasic.Instance;
      aggro = player !=null;
      if (!aggro) return;
      
      float3 pos= transform.position;
      float3 posOther= other.transform.position;

      bool isCrouching = player.isCrouching ;
      isCrouching=false;
      currentViewDistance = AIVisionUtils.getViewDistance(isCrouching,config.visionSettings.viewDistance, config.visionSettings.crouchDetectionModifier);
      ESPTrigger.radius = currentViewDistance;

      float3 targetDir;
      AIVisionUtils.getTargetDirection(isCrouching, config.visionSettings.crouchDetectionModifier,config.visionSettings.eyeOrigin, posOther - pos,out targetDir);
      float3 front = transform.forward;
      aggro = 
        AIVisionUtils.fovCheck(config.visionSettings.fovCosTheta,front,targetDir) &&
        AIVisionUtils.rayCast(
            other.GetInstanceID(),
            pos + config.visionSettings.eyeOrigin,
            targetDir,
            currentViewDistance,
            config.visionSettings.targetList
            );
      if(aggro && state != AIState.Chase)  
        OnPlayerSeen();
    }

    void OnDrawGizmosSelected()
    {
      float viewAngle =Mathf.Acos(config.visionSettings.fovCosTheta) * Mathf.Rad2Deg;
        Gizmos.color = Color.yellow;
        //Gizmos.DrawRay(rayOrigin, rayDirection * config.visionSettings.viewDistance);

        Vector3 left = Quaternion.Euler(0, -viewAngle , 0) * transform.forward;
        Vector3 right = Quaternion.Euler(0, viewAngle, 0) * transform.forward;
        Gizmos.DrawRay(rayOrigin, left * config.visionSettings.viewDistance);
        Gizmos.DrawRay(rayOrigin, right * config.visionSettings.viewDistance);
    }
}
