
using Unity.Collections;
using UnityEngine;
using Unity.Entities;
//TODO:Decide if making it generic is a good idea and won't cause funny problems later
public struct RayJobs:IBufferElementData

{
 
  //TODO: Solve dilemma of needing to update collider positions
  //possible solution/s: do it in the system
  public    RaycastCommand viewRays;

}

public struct RayHits:IBufferElementData

{
 
  //TODO: Solve dilemma of needing to update collider positions
  //possible solution/s: do it in the system
  public  RaycastHit result;

}

