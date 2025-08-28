using Unity.Collections;
using UnityEngine;
//TODO:BurstCompile this
public struct DetectorJobs<T,U> 
  where T : struct
  where U : struct
{
 
  //TODO: Solve dilemma of needing to update collider positions
  //possible solution/s: do it in the system
    public NativeArray<T> colliders;
    public NativeArray<U> collisions;
    public int minJobs;
    public int maxHits;
    public QueryParameters query_params;
}
public struct ViewBoxJobs: IComponentData{
  public DetectorJobs<OverlapBoxCommand,ColliderHit> box_commands;
}
public struct ViewRayJobs: IComponentData{
  public DetectorJobs<RaycastCommand, RaycastHit> ray_commands;
}
