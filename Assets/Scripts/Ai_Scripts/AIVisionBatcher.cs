using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;

public class AIVisionBatcher : MonoBehaviour
{
    public static AIVisionBatcher Instance;

    private List<AIVision> visionAgents = new List<AIVision>();

    private NativeArray<RaycastCommand> commands;
    private NativeArray<RaycastHit> results;

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

    public void Register(AIVision vision)
    {
      Debug.Log("REgister");
        if (!visionAgents.Contains(vision))
            visionAgents.Add(vision);
        Debug.Log($"registerd{vision.transform.position}");
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

    void Update()
    {        if (visionAgents.Count == 0)
            return;
Debug.Log("batching");
        int count = visionAgents.Count;
        commands = new NativeArray<RaycastCommand>(count, Allocator.TempJob);
        results = new NativeArray<RaycastHit>(count * 8, Allocator.TempJob);

        // Build all commands
        //
       // goto Parallel;
        Serial:
        for (int i = 0; i < count; i++)
        {
            AIVision ai = visionAgents[i];
            commands[i] = new RaycastCommand(ai.rayOrigin, ai.rayDirection, default, ai.currentViewDistance);
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

        if (!jobScheduled)
            return;

        // Wait for job completion before reading results
        raycastJob.Complete();
        jobScheduled = false;
        
        for(int i = 0; i < results.Length; i++){
          Debug.Log($"Name:{results[i].colliderInstanceID}");
        }
        for (int i = 0; i < visionAgents.Count; i++)
        {
          Debug.Log("PASSING");
            RaycastHit hit = results[i];
            visionAgents[i].ProcessVisionResult(hit);
        }

        commands.Dispose();
        results.Dispose();
    }
}
