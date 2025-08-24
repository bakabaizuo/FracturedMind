using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using Unity.Entities;
using Unity.Burst;
  [BurstCompile]
public partial struct TestConeSystem : ISystem
{
  public void OnCreate(ref SystemState state){
    state.RequireForUpdate<SimpleConeDetector>();
  }
    // Start is called before the first frame update
    // TODO:make this update on timer not everyframe
  /// <summary>
  /// [TODO:description]
  /// </summary>
  /// <param name="state">[TODO:description]</param>
  public void OnUpdate(ref SystemState state){

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
  public partial struct ConeDetectionJob: IJobEntity{
    public float dTime;
    public FixedString64Bytes label;
    public void Execute(ref SimpleConeDetector detector){
      detector.range += dTime;
      Debug.Log(FixedString.Format(label,detector.range));
    //NativeArray<OverlapBoxCommand> boxCommand = new NativeArray<OverlapBoxCommand>(1,Allocaor.TempJob);

    }
  }

}

