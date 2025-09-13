
using Unity.Collections;
using UnityEngine;
using Unity.Entities;
using Unity.Burst;
[BurstCompile]
//TODO:Decide if making it generic is a good idea and won't cause funny problems later
public struct BoxJobs:IBufferElementData{
 
    public OverlapBoxCommand viewBox;
}

public struct BoxHits:IBufferElementData{
 
  public ColliderHit hits;

}

