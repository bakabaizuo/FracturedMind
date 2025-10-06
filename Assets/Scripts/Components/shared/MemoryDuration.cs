
using Unity.Entities;
public struct MemoryDuration:ISharedComponentData{
  public float timer;
  public MemoryDuration(float length){
    timer = length;
  }
  public static implicit operator MemoryDuration(float length) => new MemoryDuration(length);

}
