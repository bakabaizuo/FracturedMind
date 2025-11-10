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
    private readonly int maxHits = 30;
    private JobHandle raycastJob = default;
    int playerID;
    NativeBitArray goodHits;
    //private bool jobScheduled = false;
    
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
      //It's small enough not to be a tax to memory and allocations slow it down anyway.
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
      // yield return new WaitForFixedUpdate();
      visionAgents?.Remove(vision);
    }

    [BurstCompile]
    private struct GetNearestHitJob:IJobFor{
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
        
        int start = i * maxHits;
        RaycastHit firstHit = hits[start];
        firstHits[i]=firstHit;
        if(!Resources.InstanceIDIsValid(hits[start].colliderInstanceID)){
          goodHits.Set(i,false);
          return;
        }
        for (int j = start+1; j < start+maxHits; j++){
          Debug.Log($"{i} {j} ");
          if(!Resources.InstanceIDIsValid(hits[j].colliderInstanceID))
            break;
          if (hits[j].distance < firstHit.distance){
            firstHit=hits[j];
          }
        }
        firstHits[i]=firstHit;

        Debug.DrawLine(new Vector3(1f,1f,1f), firstHit.point, Color.black);
        goodHits.Set(i,firstHit.colliderInstanceID == playerID);

      }
    }
    [BurstCompile]
    private struct DebugHits:IJobFor{
      [ReadOnly]
      public NativeArray<RaycastHit> firstHits;
      [ReadOnly]
      public NativeArray<RaycastCommand> cmds;

      public void Execute(int i){
        if(firstHits[i].colliderInstanceID!=0)
          Debug.DrawLine(cmds[i].from, firstHits[i].point, Color.black);
      }

    }
    void FixedUpdate()
    { 

      if (visionAgents.Count == 0)
        return;
      int count = visionAgents.Count;
      // if( count != commands.Length){
      //   commands.ResizeUninitialized(count);
      // }
      NativeArray<RaycastCommand> cmds =
        new NativeArray<RaycastCommand>(count, Allocator.TempJob,NativeArrayOptions.UninitializedMemory);
      NativeArray<RaycastHit> results = 
        new NativeArray<RaycastHit>(count * maxHits, Allocator.TempJob,NativeArrayOptions.UninitializedMemory);
      Parallel.For(0,count, i =>{
        cmds[i] = visionAgents[i].GetCommand();
      });
      raycastJob = 
        RaycastCommand.ScheduleBatch(cmds, results, 2,raycastJob);
      NativeArray<RaycastHit> hits = 
        new NativeArray<RaycastHit>(count, Allocator.TempJob,NativeArrayOptions.UninitializedMemory);
      NearestHitsJob = new GetNearestHitJob{
        hits = results,
        maxHits= maxHits,
        goodHits = goodHits,
        playerID= playerID,
        firstHits= hits
      }.ScheduleParallel(count,4,raycastJob);

      // NearestHitsJob.Complete();
      JobHandle debug = new DebugHits{
        cmds = cmds,
        firstHits = hits
      }.ScheduleParallel(count, 4, NearestHitsJob);
      debug.Complete();
      string how = "howtf";
      for(int i = 0; i < hits.Length; i++){
        Debug.Log($"{hits[i].colliderInstanceID} {hits[i].distance} {hits[i].collider?.name ?? how}");
      }
      hits.Dispose();
      cmds.Dispose();
      results.Dispose();
      for(int i =0; i < count; i ++) {
        visionAgents[i].aggro = goodHits.IsSet(i);
      }
      // visionAgents.Clear();
    }
}
