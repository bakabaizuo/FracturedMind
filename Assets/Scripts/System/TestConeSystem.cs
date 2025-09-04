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
    state.RequireForUpdate<SimpleConeDetector>();
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
//AWWW FUCK. shoulda stuck with OOP but i have commited to DOTS.
//No need to worry. make it an archetype.
      string fString = "Range is {0} m";
    ConeDetectionJob task = new ConeDetectionJob{
      dTime = SystemAPI.Time.DeltaTime,
      label = fString
    };
    task.ScheduleParallel();

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
    public float dTime;
    public FixedString64Bytes label;
    public void Execute( SimpleConeDetector detector){
    
      //detector.range += dTime;
      //Debug.Log(FixedString.Format(label,detector.range));
    //NativeArray<OverlapBoxCommand> boxCommand = new NativeArray<OverlapBoxCommand>(1,Allocaor.TempJob);

    }
  }
