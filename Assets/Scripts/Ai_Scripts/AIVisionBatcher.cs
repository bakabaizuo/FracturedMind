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
    readonly int layerMask = unchecked((int) 0xFFFFFFFF);
    private NativeArray<RaycastCommand> commands;
    private NativeArray<RaycastHit> results;
JobHandle NearestHitsJob;
    private int maxHits = 20;
    private JobHandle raycastJob = default;
    private bool jobScheduled = false;

    
    void Awake()
    {
        Debug.Log("Batcher Awake");
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }
    void Start(){
      PlayerColliderInstanceID = GameObject.FindWithTag("Player").GetInstanceID();
    }

    public void Register(AIVision vision)
    {
     // Debug.Log("REgister");
        if (!visionAgents.Contains(vision))
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
    void U()
    {
        if (visionAgents.Count == 0)
            return;

        int count = visionAgents.Count;
        commands = new NativeArray<RaycastCommand>(count, Allocator.TempJob);
        results = new NativeArray<RaycastHit>(count*20, Allocator.TempJob);

        // Build all commands
        //
       // goto Parallel;
        Serial:
        for (int i = 0; i < count; i++)
        {
            AIVision ai = visionAgents[i];
            commands[i] = new RaycastCommand(ai.rayOrigin, ai.rayDirection, ai.currentViewDistance);
            Debug.DrawRay(ai.rayOrigin,ai.currentViewDistance*ai.rayDirection);
            //TODO: ECS THIS FOR INCREASED PARALLELISM & BURST 
        }
        /*Parallel:
          BuildCommands cmd = new BuildCommands{
            rays = commands,
            agents = new NativeArray(
              array = visionAgents.ToArray(),
              allocator = Allocator.TempJob
              )
          };
        raycastJob = cmd.ScheduleParallel(commands.Length,default);*/
        // Schedule all raycasts in parallel
        raycastJob = 
          RaycastCommand.ScheduleBatch(commands, results, 32,raycastJob);

        jobScheduled = true;
    }
    [BurstCompile]
    private struct GetNearestHitJob:IJobFor{
      [ReadOnly]
      public NativeArray<RaycastHit> hits;
      public NativeArray<RaycastHit> firstHits;
      public int maxHits;
      public void Execute(int i){
        int start = i * maxHits;
        int end = start + maxHits;
        
        firstHits[i] = hits[start]; 
        for(int j = start;j<end;j++){
          if(hits[j].colliderInstanceID == 0)
            break;
          else if(hits[j].distance < firstHits[i].distance )
            firstHits[i] = hits[j];

        }

      }
    }
    void Update()
    {        if (visionAgents.Count == 0)
            return;
Debug.Log("batching");
        int count = visionAgents.Count;
        commands = new NativeArray<RaycastCommand>(count, Allocator.TempJob);
        results = new NativeArray<RaycastHit>(count * maxHits, Allocator.TempJob);

        // Build all commands
        //
       // goto Parallel;
        for (int i = 0; i < count; i++)
        {
            AIVision ai = visionAgents[i];
            LayerMask mask = new LayerMask{
              value = layerMask
            };
            QueryParameters parameters = new QueryParameters(
             layerMask, false,
             default, false
             );
            commands[i] = new RaycastCommand(ai.rayOrigin, ai.rayDirection, parameters, ai.currentViewDistance);
        }
        /*Parallel:
          BuildCommands cmd = new BuildCommands{
            rays = commands,
            agents = new NativeArray(
              array = visionAgents.ToArray(),
              allocator = Allocator.TempJob
              )
          };
        raycastJob = cmd.ScheduleParallel(commands.Length,default);*/
        // Schedule all raycasts in parallel
        raycastJob = 
          RaycastCommand.ScheduleBatch(commands, results, 32,raycastJob);

        NativeArray<RaycastHit> firstHits = new NativeArray<RaycastHit>(commands.Length, Allocator.TempJob);
        NearestHitsJob = new GetNearestHitJob{
          hits = results,
          firstHits = firstHits,
          maxHits= maxHits
        }.ScheduleParallel(commands.Length,1,raycastJob);
        jobScheduled = true;

        if (!jobScheduled)
            return;

        // Wait for job completion before reading results
        //raycastJob.Complete();
        NearestHitsJob.Complete();
        jobScheduled = false;
        
        //TODO: yield when nearestHitsJob not complete
        for (int i = 0; i < visionAgents.Count; i++)
        {
            RaycastHit hit = firstHits[i];
            visionAgents[i].ProcessVisionResult(hit);
        }

        commands.Dispose();
        results.Dispose();
        firstHits.Dispose();
    }
}
