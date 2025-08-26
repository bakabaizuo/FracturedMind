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
}
