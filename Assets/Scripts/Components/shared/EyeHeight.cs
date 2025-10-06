using Unity.Entities;
public struct EyeHeight:ISharedComponentData{
  public float eyeHeight;
  public EyeHeight(float height){
    eyeHeight = height;
  }
  public static implicit operator EyeHeight(float height) => new EyeHeight(height);

}
