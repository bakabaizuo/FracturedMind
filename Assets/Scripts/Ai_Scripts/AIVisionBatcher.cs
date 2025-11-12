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
      
      if(this.isActiveAndEnabled && !visionAgents.Contains(vision))
        visionAgents?.Add(vision);

    }

    public void Unregister(AIVision vision)
    {
      visionAgents?.Remove(vision);
      Debug.Log ($"Removed{vision.name}");
      
    }


    [BurstCompile]
    private struct GetNearestHitJob:IJobFor{
      // [ReadOnly]
      // public NativeArray<int> offsets;
      [ReadOnly]
      public NativeArray<RaycastHit> hits;
      public int maxHits;
      public int playerID;
      [NativeDisableParallelForRestriction]
      public NativeBitArray goodHits;
      public int totalHits;

      public void Execute(int i){
        
        int start = i*maxHits;
        if(hits[start].colliderInstanceID == 0){
          goodHits.Set(i,false);
          return;
        }
        int end = start+maxHits;

        if(end<totalHits) 
          end = totalHits;
        
        int indexOfMin = start;

        for (int j = start;hits[j].colliderInstanceID != 0 && j < end; j++)
          if (hits[j].distance < hits[indexOfMin].distance)
            indexOfMin = j;
        goodHits.Set(i,hits[indexOfMin].colliderInstanceID == playerID);

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
          Debug.DrawLine(cmds[i].from, firstHits[i].point, Color.black);
      }

    }
    void FixedUpdate()
    { 


      if (visionAgents.Count == 0)
        return;

      int count = visionAgents.Count;
      Debug.Log($"Len:{count} {System.DateTime.Now}");
      NativeArray<RaycastCommand> cmds =
        new NativeArray<RaycastCommand>(count, Allocator.TempJob,NativeArrayOptions.UninitializedMemory);
      NativeArray<RaycastHit> results = 
        new NativeArray<RaycastHit>(count * maxHits, Allocator.TempJob);
      for(int i = 0; i < count; i++){
        cmds[i] = visionAgents[i].GetCommand();
      }
      raycastJob = 
        RaycastCommand.ScheduleBatch(cmds, results, 2,raycastJob);
      NearestHitsJob = new GetNearestHitJob{
        hits = results,
        maxHits= maxHits,
        goodHits = goodHits,
        playerID= playerID,
        totalHits = results.Length
      }.ScheduleParallel(count,4,raycastJob);

      NearestHitsJob.Complete();
      results.Dispose();
      cmds.Dispose();
      for(int i= 0; i<count; i++) {
        visionAgents[i].aggro = goodHits.IsSet(i);
      }
      visionAgents.Clear();
    }
}
