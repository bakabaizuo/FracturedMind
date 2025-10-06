using Unity.Entities;
public struct ViewRange:  ISharedComponentData{
  public float range;
  public ViewRange(float r){
    range = r;
  }
  public static implicit operator ViewRange(float r) => new ViewRange(r);
}
public struct FieldOfView:  ISharedComponentData{
  public float theta;
  public FieldOfView(float fov){
    theta = fov;
  }
  public static implicit operator FieldOfView(float fov) => new FieldOfView(fov);
}
public class ViewCone{
public ViewRange range;
public FieldOfView fov;

}
public class ViewConeTypes{
  //TODO: make a table of this and future types and an operator that implicitly maps keys (from an enum) to values
  public static ViewCone BaseEnemyCone = new ViewCone{
  range = (ViewRange) 120,
  fov = (FieldOfView)70
};


}
