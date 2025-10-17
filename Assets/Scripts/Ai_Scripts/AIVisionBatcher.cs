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
    private NativeArray<RaycastHit> firstHits ;
    
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
      firstHits = new NativeArray<RaycastHit>(GameObject.FindGameObjectsWithTag("AI").Length, Allocator.Persistent,NativeArrayOptions.UninitializedMemory );

    }
    void Destroy(){
      firstHits.Dispose();
    }

    public void Register(AIVision vision)
    {
     // Debug.Log("REgister");
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
      public NativeArray<RaycastHit> firstHits;
      [ReadOnly]
      public int maxHits;
      public void Execute(int i){
        int start = i * maxHits;
        int end = start + maxHits;
        
        firstHits[i] = hits[start]; 
        if(firstHits[i].colliderInstanceID == 0){
          return;
        }
        for(int j = start+1;j<end;j++){
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
        int count = visionAgents.Count;
        commands = new NativeArray<RaycastCommand>(count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        results = new NativeArray<RaycastHit>(count * maxHits, Allocator.TempJob,NativeArrayOptions.UninitializedMemory);

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
        raycastJob = 
          RaycastCommand.ScheduleBatch(commands, results, 32,raycastJob);

        
        NearestHitsJob = new GetNearestHitJob{
          hits = results,
          firstHits = firstHits,
          maxHits= maxHits
        }.ScheduleParallel(commands.Length,6,raycastJob);
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
              
              visionAgents[i].ProcessVisionResult(firstHits[i]);
        }
        visionAgents.Clear();
    }
}
