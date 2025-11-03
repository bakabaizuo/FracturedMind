using System.Collections;
using System.Collections.Generic;
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
    private NativeArray<RaycastHit> firstHits ;
    JobHandle NearestHitsJob;
    private readonly int maxHits = 20;
    private JobHandle raycastJob = default;
    private HashSet<AIVision> registration = new HashSet<AIVision>();
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
    }
    void OnDestroy(){
      //firstHits.Dispose();
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

   /* private struct BuildCommands:IJobFor{
      public NativeArray<RaycastCommand> rays;
      public NativeArray<AIVision> agents;
      public void Execute(int i){

        AIVision ai = agents[i];
        commands[i] = new RaycastCommand(ai.rayOrigin, ai.rayDirection, ai.currentViewDistance);
      }
    }*/
    [BurstCompile]
    private struct GetNearestHitJob:IJobFor{
      [ReadOnly]
      public NativeArray<RaycastHit> hits;
      [NativeDisableParallelForRestriction]
      public NativeArray<RaycastHit> firstHits;
      [ReadOnly]
      public int maxHits;
      public void Execute(int i){
        if(i >= firstHits.Length){
          return;
        }
        int start = i * maxHits;

      //  Debug.Log($"i = {i} start = {start} len = {hits.Length} ");

        firstHits[i] = hits[start];
        if(firstHits[i].colliderInstanceID == 0)
          return;
        for (int j = start+1; j < start+maxHits; j++){
          if(hits[j].colliderInstanceID==0)
            return;
          else if (hits[j].distance < firstHits[i].distance){
            firstHits[i]=hits[j];
          }
        }

      }
    }
    void FixedUpdate()
    { 
      if (visionAgents.Count == 0)
            return;
        int count = visionAgents.Count;
        firstHits = new NativeArray<RaycastHit>(count, Allocator.TempJob,NativeArrayOptions.UninitializedMemory );
        results = new NativeArray<RaycastHit>(count * maxHits, Allocator.TempJob,NativeArrayOptions.UninitializedMemory);

        // Build all commands
        //
       // goto Parallel;
/*        for (int i = 0; i < count; i++)
        {
            AIVision ai = visionAgents[i];
            commands[i] = new RaycastCommand(ai.rayOrigin, ai.rayDirection, parameters, ai.currentViewDistance);
        }
        */
        raycastJob = 
          RaycastCommand.ScheduleBatch(commands.AsArray(), results, 1,raycastJob);

        
        NearestHitsJob = new GetNearestHitJob{
          hits = results,
          firstHits = firstHits,
          maxHits= maxHits
        }.ScheduleParallel(count,6,raycastJob);
        //jobScheduled = true;

        NearestHitsJob.Complete();
       // commands.Dispose();
        results.Dispose();
        // Wait for job completion before reading results
        //raycastJob.Complete();
        //TODO:yield when nearestHitsJob not complete
        for (int i = 0; i < visionAgents.Count; i++)
        {
            if(firstHits[i].colliderInstanceID != 0)
              
              visionAgents[i].ProcessVisionResult(firstHits[i]);
        }
        firstHits.Dispose();
        visionAgents.Clear();
        commands.Clear();

    }
    private IEnumerator GetHits(){
      yield return new WaitForFixedUpdate();
        NearestHitsJob.Complete();
       // commands.Dispose();
        results.Dispose();
        // Wait for job completion before reading results
        //raycastJob.Complete();
        //TODO:yield when nearestHitsJob not complete
        for (int i = 0; i < visionAgents.Count; i++)
        {
            if(firstHits[i].colliderInstanceID != 0)
              
              visionAgents[i].ProcessVisionResult(firstHits[i]);
        }
        visionAgents.Clear();
        commands.Clear();
     //   firstHits.Dispose();

    }
}
