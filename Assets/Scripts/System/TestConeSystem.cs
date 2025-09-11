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
    Debug.Log("SimpleConeDetector system running");

  }
    // Start is called before the first frame update
  /// <summary>
  /// update SimpleConeDetector components 
    // TODO:make this update on timer not everyframe
  /// </summary>
  /// <param name="state">[TODO:description]</param>
  public void OnUpdate(ref SystemState state){
//TODO: check for change of position then transform forward component here.
//TODO: call OverlapBoxCommand.ScheduleBatch here
//TODO: check results. if results not empty, do a raycast. make sure to clear both later
//No need to worry. make it an archetype.
      string fString = "Range is {0} m";
      new ConeDetectionJob{
        dTime = SystemAPI.Time.DeltaTime,
        label = fString
      }.ScheduleParallel();
    //Check up on ComponentGroup and see if i can somehow use it to batch viewboxes better

  }
  /*protected override void OnUpdate(){
    FixedString64Bytes label = new FixedString64Bytes("{0}");
    Entities.ForEach((ref SimpleConeDetector detector) => {
        detector.range += 1.0f * SystemAPI.Time.DeltaTime;
       //string log = $"{detector.range}";
        
        Debug.Log(FixedString.Format(label,detector.range));
        }).ScheduleParallel();
  }*/
  public partial struct ConeDetectionJob: IJobEntity{
    public float dTime;
    public FixedString64Bytes label;
    public void Execute(ref BoxJobs viewBoxes, ref RayJobs viewRays, TagEnemyComponent isEnemy, ref RayHits rayHits, ref BoxHits boxHits){
      OverlapBoxCommand.ScheduleBatch(viewBoxes.asNativeArray(), boxHits.asNativeArray(),1,1);
      if(boxHits[0].instanceID == 0){
        return;
      }
      RaycastCommand.ScheduleBatch(viewRays.asNativeArray(), rayHits.asNativeArray(),2,8);
//TODO: check if player is found a certain number of times
      //TODO:Make a movesystem
    
      //detector.range += dTime;
      //Debug.Log(FixedString.Format(label,detector.range));
    //NativeArray<OverlapBoxCommand> boxCommand = new NativeArray<OverlapBoxCommand>(1,Allocaor.TempJob);

    }
  }

}

