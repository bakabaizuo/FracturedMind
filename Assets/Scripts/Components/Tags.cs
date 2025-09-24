
using Unity.Entities;
using Unity.Mathematics;
namespace Tags
{
    
  public struct Movable:IEnableableComponent{
  public float3 previousPosition;
  }

  public struct Enemy:IComponentData{

  }
}


