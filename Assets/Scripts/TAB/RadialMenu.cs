using UnityEngine;

public class RadialMenu:MonoBehavior
{
  RadialItem[] Items;
  float PieTheta;
  void Start(){
    PieTheta= 360f/Items.Length;

  }
    
}
