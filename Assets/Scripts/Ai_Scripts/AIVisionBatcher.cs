using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using System.Threading.Tasks;

public class AIVisionBatcher : MonoBehaviour
{
    public static AIVisionBatcher Instance;
    private List<AIVision> visionAgents ;
    JobHandle NearestHitsJob;
    private readonly int maxHits = 2;
    private JobHandle raycastJob = default;
    int playerID;
    NativeBitArray goodHits;
    
    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }
    void Start(){

      int len =GameObject.FindGameObjectsWithTag("AI").Length;
      visionAgents = new List<AIVision>(len);
      playerID = GameObject.FindWithTag("PlayerCollider").GetComponent<Collider>().GetInstanceID();
      goodHits = new NativeBitArray(64, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
    }
    void OnDisable(){
      Instance = null;
    }
    void OnDestroy(){
      goodHits.Dispose();
    }

    public void Register(AIVision vision)
    {
      
      if(this.isActiveAndEnabled && !(visionAgents?.Contains(vision)??false))
        visionAgents?.Add(vision);

    }

    public void Unregister(AIVision vision)
    {
      visionAgents?.Remove(vision);
    }


    [BurstCompile]
    private struct GetNearestHitJob:IJobFor{
      // [ReadOnly]
      // public NativeArray<int> offsets;
      [ReadOnly]
      public NativeArray<RaycastHit> hits;
      [ReadOnly]
      public int maxHits;
      [ReadOnly]
      public int playerID;
      [NativeDisableParallelForRestriction]
      public NativeBitArray goodHits;
      [NativeDisableParallelForRestriction]
      public NativeArray<RaycastHit> firstHits;

      public void Execute(int i){
        
        // if(offsets[i] == 0 ){
        //   goodHits.Set(i,false);
        //   return;
        // }
        NativeArray<RaycastHit> results = hits.GetSubArray(i*maxHits,maxHits);
        
        firstHits[i]=results[0];
        if(firstHits[i].colliderInstanceID == 0){
          goodHits.Set(i,false);
          return;
        }
        for (int j = 1; j < maxHits; j++){
          if(results[j].colliderInstanceID == 0)
            break;
          if (results[j].distance < results[0].distance){
            firstHits[i]=results[j];
          }
        }

        // Debug.Log($" iter:{i} id:{firstHits[i].colliderInstanceID} id:{firstHits[i].colliderInstanceID==playerID} x:{firstHits[i].point.x} y:{firstHits[i].point.y} z:{firstHits[i].point.z}");
        goodHits.Set(i,firstHits[i].colliderInstanceID == playerID);

      }
    }
    [BurstCompile]
    private struct DebugHits:IJobFor{
      [ReadOnly]
      public NativeArray<RaycastHit> firstHits;
      [ReadOnly]
      public NativeArray<RaycastCommand> cmds;

      public void Execute(int i){
        var hit = firstHits[i];
        if(firstHits[i].colliderInstanceID!=0)
        {
          Debug.DrawLine(cmds[i].from, firstHits[i].point, Color.black);
          // Debug.Log($"point {hit.colliderInstanceID} {hit.point.x} {hit.point.y} {hit.point.y}");
          
        
        }
      }

    }
    void FixedUpdate()
    { 

      if (visionAgents.Count == 0)
        return;
      string how = "howtf";
      int count = visionAgents.Count;
      NativeArray<RaycastCommand> cmds =
        new NativeArray<RaycastCommand>(count, Allocator.TempJob,NativeArrayOptions.UninitializedMemory);
      NativeArray<RaycastHit> results = 
        new NativeArray<RaycastHit>(count * maxHits, Allocator.TempJob);
      for(int i = 0;i<count; i++){
        cmds[i] = visionAgents[i].GetCommand();
      }
      raycastJob = 
        RaycastCommand.ScheduleBatch(cmds, results, 2,raycastJob);
      NativeArray<RaycastHit> hits = 
        new NativeArray<RaycastHit>(count, Allocator.TempJob,NativeArrayOptions.UninitializedMemory);
      // raycastJob.Complete();
      // for(int i = 0; i < count; i++){
      //   int start = i*maxHits;
      //   hits[i] = results[start];
      //   if(results[start].collider == null){
      //     goodHits.Set(i,false);
      //     continue;
      //   }
      //   for(int offset = 1; offset<maxHits; offset++){
      //     int index = start+offset;
      //     if(results[index].collider==null)
      //       break;
      //     else if(results[index].distance< results[start].distance)
      //       results[start]= results[index];
      //   }
      //   goodHits.Set(i,results[start].collider.CompareTag("PlayerCollider"));
      //   hits[i] = results[start];
      //
      //
      // }
      // NativeArray<int> hack = new(count,Allocator.TempJob,NativeArrayOptions.UninitializedMemory);
      // for(int i = 0; i < count; i++){
      //   int start = i*maxHits;
      //   int offset = 0;
      //
      //   while(offset < maxHits && results[start+offset].collider != null){
      //     Debug.Log(results[start+offset].collider);
      //     offset++;
      //   }
      //   hack[i] = offset;
      //   Debug.Log($"{i} : {hack[i]}");
      //
      // }
      NearestHitsJob = new GetNearestHitJob{
        hits = results,
        maxHits= maxHits,
        goodHits = goodHits,
        playerID= playerID,
        firstHits= hits,
        // offsets=hack
      }.ScheduleParallel(count,4,raycastJob);

      NearestHitsJob.Complete();
      // hack.Dispose();
      // JobHandle debug = new DebugHits{
      //   cmds = cmds,
      //   firstHits = hits
      // }.ScheduleParallel(count, 1, NearestHitsJob);
      // debug.Complete();
      // for(int i = 0; i < hits.Length; i++){
      //   if(goodHits.IsSet(i))
      //     Debug.Log($"id:{hits[i].colliderInstanceID} == {results[i*maxHits].colliderInstanceID} d:{hits[i].distance} {hits[i].point} {hits[i].collider?.name ?? how}");
      // }
      results.Dispose();
      hits.Dispose();
      cmds.Dispose();
      for(int i =0; i < count; i ++) {
        visionAgents[i].aggro = goodHits.IsSet(i);
      }
    }
}
