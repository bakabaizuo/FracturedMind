using Unity.Collections;
using UnityEngine;
using Unity.Entities;
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
    public QueryParameters queryParams;
}
public struct ViewBoxJobs: IComponentData{
  //Funny idea to use spatial partitioning to have some sort of squad interaction.
  public DetectorJobs<OverlapBoxCommand,ColliderHit> boxCommands;
}
public struct ViewRayJobs: IComponentData{
  public DetectorJobs<RaycastCommand, RaycastHit> rayCommands;
}
