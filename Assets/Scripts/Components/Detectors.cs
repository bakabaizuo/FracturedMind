using Unity.Collections;
using UnityEngine;
public struct Detector<T,U> 
  where T : struct
  where U : struct
{
 
    public NativeArray<T> colliders;
    public NativeArray<U> collisions;
    public int minCommands;
    public int maxHits;
    public QueryParameters query_params;
}
public struct ViewBoxCommands: IComponentData{
  public Detector<OverlapBoxCommand,ColliderHit> box_commands;
}
public struct ViewRayCommands: IComponentData{
  public Detector<RaycastCommand, RaycastHit> ray_commands;
}
