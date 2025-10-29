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
    
    private Vector3 origin;
    
    public QueryParameters parameters = new QueryParameters(
       unchecked((int) 0xFFFFFF7F), false,
       default, false
       );
    
    [Header("Crouch Detection")]
    public float crouchDetectionModifier = 0.5f;

    [Header("Memory")]
    readonly static int memoryDuration = 10;
    readonly static int gracePeriod = 20;
//    private Vector3? lastSeenPosition;
//    private Transform lastPlayer;

    [HideInInspector] public Vector3 rayOrigin;
    [HideInInspector] public Vector3 rayDirection;
    [HideInInspector] public float currentViewDistance;
    private Vector3 eyePosition ;
    public AIState state = AIState.Idle;

    Coroutine timer = null;
    void Start(){

     eyePosition = new Vector3(0,eyeHeight,0);

    }

    void OnPlayerLost(){
      switch(state){
        case AIState.Idle: break;
        case AIState.Chase: 
          ticks = memoryDuration;
          state = AIState.Investigate;
          if(scan != null){
            StopCoroutine(scan);
            scan = null;
          }
          timer ??= StartCoroutine(StartTimer());
          search ??= StartCoroutine(ForgetPlayer());
          break;
        case AIState.Investigate: 
          break;
      }
    }
    void OnPlayerSeen(){
      
      switch(state){
        case AIState.Chase:break;
        case AIState.Investigate:
          break;
        case AIState.Idle:
          ticks = gracePeriod;
          state = AIState.Investigate;
          if(search != null){
            StopCoroutine(search);
            search = null;
          }
          timer ??= StartCoroutine(StartTimer());
          scan ??= StartCoroutine(ScanPlayer());
          break;
      }
    }

    int ticks = 0;
    Coroutine scan = null;
    Coroutine search = null;
    IEnumerator StartTimer(){
      for (; ticks > 0; ticks--)
      {
        yield return new WaitForFixedUpdate();
        Debug.Log("Tick");
      }
    }
    IEnumerator ScanPlayer(){
      yield return timer;
      timer = null;
      state = AIState.Chase;
    }
    IEnumerator ForgetPlayer(){
      yield return timer;
      timer = null;
      state = AIState.Idle;
    }
    void OnTriggerExit(Collider other){
      if(!other.CompareTag("Player") )
        return;
      OnPlayerLost();
    }
    void FixedUpdate()
    {
      rayOrigin = transform.position;
      rayOrigin.y += eyeHeight; 
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
        if (AIVisionBatcher.Instance != null)
         goto Batched;
        //goto Serial;
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
        OnPlayerLost();
      

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
