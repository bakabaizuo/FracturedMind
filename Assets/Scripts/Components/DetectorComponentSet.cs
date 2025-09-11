using Unity.Entities;
using Unity.Collections;
using Unity.Burst;
[BurstCompile]
public struct DetectorComponentSet{
  public static readonly ComponentTypeSet ComponentSet= new ComponentTypeSet(
    new FixedList128Bytes<ComponentType>{
    typeof(FrontComponent),typeof(RangeComponent),typeof(ViewBoxJobs),typeof(ViewRayJobs)
  }
);
  
}
