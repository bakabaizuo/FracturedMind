
using Unity.Collections;
using UnityEngine;
using Unity.Entities;
using Unity.Burst;
[BurstCompile]
//TODO:Decide if making it generic is a good idea and won't cause funny problems later
public struct BoxJobs:IBufferElementData{
 
    public OverlapBoxCommand viewBox;
    public static implicit operator OverlapBoxCommand(BoxJobs b)=> b.viewBox;
    public static implicit operator BoxJobs(OverlapBoxCommand b) => new BoxJobs{viewBox = b};
}

public struct BoxHits:IBufferElementData{
 
  public ColliderHit hits;
  public static implicit operator ColliderHit(BoxHits b) => b.hits;
    
    public static implicit operator BoxHits(ColliderHit b) => new BoxHits{hits = b};

}

