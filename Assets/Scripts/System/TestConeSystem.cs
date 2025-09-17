using Unity.Collections;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Burst;
  [BurstCompile]
public partial struct TestConeSystem : ISystem
{
  /// <summary>
  /// Creates the System that operates on SimpleConeDetector components (only if they exist)
  /// </summary>
  /// <param name="state">[TODO:description]</param>
  public void OnCreate(ref SystemState state){

    state.RequireForUpdate<Tags.Enemy>();
//     state.RequireForUpdate<DynamicBuffer<BoxJobs>>();
    //state.RequireForUpdate<RayJobs>();
    Debug.Log("SimpleConeDetector system running");
  }
    // Start is called before the first frame update
  /// <summary>
  /// update SimpleConeDetector components 
    // TODO:make this update on timer not everyframe
  /// </summary>
  /// <param name="state">[TODO:description]</param>
  public void OnUpdate(ref SystemState state){
      new ConeDetectionJob{
      }.ScheduleParallel();

  }

}

  public partial struct ConeDetectionJob: IJobEntity{
    public void Execute(
        DynamicBuffer<BoxJobs> viewBoxes, 
        DynamicBuffer<BoxHits> boxHits,
        DynamicBuffer<RayJobs> viewRays,
        DynamicBuffer<RayHits> rayHits,
        Tags.Enemy isEnemy,
        ref EnemyAlertComponent alertnes
    ){
      if(alertnes.alert)
        return;
      NativeArray<ColliderHit> collisions = boxHits.Reinterpret<ColliderHit>().AsNativeArray(); 
      NativeArray<OverlapBoxCommand> cmdBuf = viewBoxes.Reinterpret<OverlapBoxCommand>().AsNativeArray(); 

     OverlapBoxCommand.ScheduleBatch(
        cmdBuf,
        collisions,
        1,
        1
      ).Complete();

      if(collisions[0].instanceID == 0) return;

      bool alerted = false;
      //TODO: clean rayHits
      RaycastCommand.ScheduleBatch(
          viewRays.Reinterpret<RaycastCommand>().AsNativeArray(), 
          rayHits.Reinterpret<RaycastHit>().AsNativeArray(),
          2,
          8
      ).Complete();
      int hits = 0;
      for(int i = 0; i < rayHits.Length; i++){
        //SOME MAGIC NUMBER WHICH IS THE ID FOR THE PLAYER MESH/COLLIDER
        if (rayHits[i].result.colliderInstanceID == /*player collider id*/ 0) hits++;
      }
      
      alertnes.alert = hits > 4;


    }
  }
