using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using Unity.Entities;
public partial class TestConeSystem : SystemBase
{
    // Start is called before the first frame update
    // TODO:make this update on timer not everyframe
protected override void OnUpdate(){
  FixedString64Bytes label = new FixedString64Bytes("{0}");
  Entities.ForEach((ref SimpleConeDetector detector) => {
      detector.range += 1.0f * SystemAPI.Time.DeltaTime;
     //string log = $"{detector.range}";
      
      Debug.Log(FixedString.Format(label,detector.range));
      }).ScheduleParallel();
}

}

