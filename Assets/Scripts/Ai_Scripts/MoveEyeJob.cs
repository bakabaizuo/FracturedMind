using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;

public struct MoveEyeJob : IJobFor
{
  public NativeArray<Vector3> from;
  public NativeArray<RaycastCommand> rays;
  public NativeArray<Vector3> targets;
  public float range;
    // Start is called before the first frame update
  public void Execute(int i){
   rays[i] = new RaycastCommand{
     direction = targets[i],
     from =from[i],
     distance = range
    };
  }
}
