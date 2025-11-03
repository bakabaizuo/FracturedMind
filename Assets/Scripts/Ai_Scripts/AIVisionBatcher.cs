using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
public class AIVisionBatcher : MonoBehaviour
{
    public static AIVisionBatcher Instance;
    private List<AIVision> visionAgents ;
    private NativeList<RaycastCommand> commands;
    private NativeArray<RaycastHit> results;
    JobHandle NearestHitsJob;
    private readonly int maxHits = 20;
    private JobHandle raycastJob = default;
    private HashSet<AIVision> registration = new HashSet<AIVision>();
    int playerID;
    //private bool jobScheduled = false;
    
    QueryParameters parameters = new QueryParameters(
       new LayerMask{
        value =  unchecked((int) 0xFFFFFF7F)
      }, false,
       default, false
     );
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
      commands = new NativeList<RaycastCommand>(len,Allocator.Persistent);
      playerID = GameObject.FindWithTag("Player").GetComponent<Collider>().GetInstanceID();
    }
    void OnDestroy(){
      commands.Dispose();
    }

    public IEnumerator Register(AIVision vision)
    {
      
      yield return new WaitForFixedUpdate();
      visionAgents.Add(vision);
      RaycastCommand cmd = vision.GetCommand();
      if(commands.Length < commands.Capacity){
        commands.AddNoResize(cmd);
      }
      else
      {
        commands.Add(cmd);
      }

    }

    public IEnumerator Unregister(AIVision vision)
    {
      yield return new WaitForFixedUpdate();
      visionAgents.Remove(vision);
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
      public void Execute(int i){
        
        int start = i * maxHits;
        if(hits[start].colliderInstanceID == 0){
          goodHits.Set(i,false);
          return;
        }
        RaycastHit firstHit = hits[start];
        for (int j = start+1; j < start+maxHits; j++){
          if(hits[j].colliderInstanceID==0)
            break;
          else if (hits[j].distance < firstHit.distance){
            firstHit=hits[j];
          }
        }
        goodHits.Set(i,firstHit.colliderInstanceID== playerID);

      }
    }
    void FixedUpdate()
    { 
      if (visionAgents.Count == 0)
        return;
      int count = visionAgents.Count;
      results = 
        new NativeArray<RaycastHit>(count * maxHits, Allocator.TempJob,NativeArrayOptions.UninitializedMemory);
      NativeBitArray goodHits = 
        new NativeBitArray(count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
      raycastJob = 
        RaycastCommand.ScheduleBatch(commands.AsArray(), results, 1,raycastJob);

      
      NearestHitsJob = new GetNearestHitJob{
        hits = results,
        maxHits= maxHits,
        goodHits = goodHits,
        playerID= playerID,
      }.ScheduleParallel(count,6,raycastJob);

      NearestHitsJob.Complete();
      commands.Clear();
      results.Dispose();
      for (int i = 0; i < visionAgents.Count; i++){
        visionAgents[i].ProcessVisionResult(goodHits.IsSet(i));
      }
      goodHits.Dispose();
      visionAgents.Clear();

    }
}
