using System;
using UnityEngine;
using Unity.Mathematics;



public class AIVision : MonoBehaviour
{
  //TODO: make this a config probably using the above structs
    [Header("Vision Settings")]
      public  NPCConfiguration config;
    // public float viewDistance = 2f;
    // public float eyeOrigin = 1.6f;
    // public float crouchDetectionModifier = 0.5f;
    // the cosine of planned viewangle
    // public float fovCosTheta = 0.5f;

    [Header("Memory")]
    [SerializeField]
    int gracePeriod = 10;
    
    LayerMask rayTargeting;
    [Header("Memory Tracking")]
    public bool aggro = false; 
    public int ticks = 0;

    [HideInInspector] public Vector3 rayOrigin;
    // [HideInInspector] public Vector3 rayDirection;
    [HideInInspector] public float currentViewDistance;
      public AIState state = AIState.Idle;


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

      SphereCollider ESPTrigger;
    void Start(){
      bool hasTrigger = this.TryGetComponent(out ESPTrigger);
      if(!hasTrigger)
        ESPTrigger = gameObject.AddComponent(typeof(SphereCollider)) as SphereCollider;
      ESPTrigger.radius = config.visionSettings.viewDistance;

    }
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
