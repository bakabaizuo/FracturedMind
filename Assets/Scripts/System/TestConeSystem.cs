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
    state.RequireForUpdate<BoxJobs>();
    state.RequireForUpdate<RayJobs>();
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
  /*protected override void OnUpdate(){
    FixedString64Bytes label = new FixedString64Bytes("{0}");
    Entities.ForEach((ref SimpleConeDetector detector) => {
        detector.range += 1.0f * SystemAPI.Time.DeltaTime;
       //string log = $"{detector.range}";
        
        Debug.Log(FixedString.Format(label,detector.range));
        }).ScheduleParallel();
  }*/

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
      //TODO:Make a movesystem that updates the Cone
      //TODO:Put in a job specifically for finding which enemies interest in FOV hoepfully in a single array.
      DynamicBuffer<ColliderHit> collisions = boxHits.Reinterpret<ColliderHit>(); 
      DynamicBuffer<OverlapBoxCommand> cmdBuf = viewBoxes.Reinterpret<OverlapBoxCommand>(); 
      OverlapBoxCommand.ScheduleBatch(
        cmdBuf.AsNativeArray(),
        collisions.AsNativeArray(), 
        1,
        1
      ).Complete();
      if(collisions[0].instanceID == 0) return;

      bool alerted = false;
      //TODO: clean rayHits
      //TODO: maybe update raycasts here
      RaycastCommand.ScheduleBatch(
          viewRays.Reinterpret<RaycastCommand>().AsNativeArray(), 
          rayHits.Reinterpret<RaycastHit>().AsNativeArray(),
          2,
          8
      ).Complete();
      
      alertnes.alert = alerted;
//TODO: check if player is found a certain number of times
      //detector.range += dTime;
      //Debug.Log(FixedString.Format(label,detector.range));
    //NativeArray<OverlapBoxCommand> boxCommand = new NativeArray<OverlapBoxCommand>(1,Allocaor.TempJob);

    }
  }
