using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
public class AIVisionBatcher : MonoBehaviour
{
    public static AIVisionBatcher Instance;
    private int PlayerColliderInstanceID;
    private List<AIVision> visionAgents = new List<AIVision>();
    private NativeArray<RaycastCommand> commands;
    private NativeArray<RaycastHit> results;
JobHandle NearestHitsJob;
    private int maxHits = 20;
    private JobHandle raycastJob = default;
    private bool jobScheduled = false;
    private NativeArray<RaycastHit> firstHits ;
    
    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }
    void Start(){
      PlayerColliderInstanceID = GameObject.FindWithTag("Player").GetInstanceID();
      firstHits = new NativeArray<RaycastHit>(GameObject.FindGameObjectsWithTag("AI").Length, Allocator.Persistent,NativeArrayOptions.UninitializedMemory );

    }
    void Destroy(){
      firstHits.Dispose();
    }

    public void Register(AIVision vision)
    {
      visionAgents.Add(vision);
    }

    public void Unregister(AIVision vision)
    {
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

        Debug.Log($"i = {i} start = {start} len = {hits.Length} ");

        firstHits[i] = hits[start];
        if(firstHits[i].colliderInstanceID == 0)
          return;
        NativeArray<RaycastHit> hitSubArray = hits.GetSubArray(start+1,maxHits-1);
        for (int j = 1; j < maxHits-1; j ++){
          if(hitSubArray[j].colliderInstanceID==0)
            return;
          else if (hitSubArray[j].distance < firstHits[i].distance){
            firstHits[i]=hitSubArray[j];
          }
        }

      }
    }
    void Update()
    { 
      Debug.Log("UPDATING");
      if (visionAgents.Count == 0)
            return;
        int count = visionAgents.Count;
        commands = new NativeArray<RaycastCommand>(count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        results = new NativeArray<RaycastHit>(count * maxHits, Allocator.TempJob,NativeArrayOptions.UninitializedMemory);

            QueryParameters parameters = new QueryParameters(
             new LayerMask{
              value =  unchecked((int) 0xFFFFFFFF)
            }, false,
             default, false
             );
        // Build all commands
        //
       // goto Parallel;
        for (int i = 0; i < count; i++)
        {
            AIVision ai = visionAgents[i];
            commands[i] = new RaycastCommand(ai.rayOrigin, ai.rayDirection, parameters, ai.currentViewDistance);
        }
        raycastJob = 
          RaycastCommand.ScheduleBatch(commands, results, 32,raycastJob);

        
        NearestHitsJob = new GetNearestHitJob{
          hits = results,
          firstHits = firstHits,
          maxHits= maxHits
        }.ScheduleParallel(count ,6,raycastJob);
        jobScheduled = true;


        NearestHitsJob.Complete();
        commands.Dispose();
        results.Dispose();
        // Wait for job completion before reading results
        //raycastJob.Complete();
        
        //TODO: yield when nearestHitsJob not complete

    }
    void LateUpdate(){

        for (int i = 0; i < visionAgents.Count; i++)
        {
            if(firstHits[i].collider != null)
              
              visionAgents[i].ProcessVisionResult(firstHits[i],PlayerColliderInstanceID);
        }
        visionAgents.Clear();
    }
}
