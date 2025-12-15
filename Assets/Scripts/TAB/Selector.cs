using UnityEngine;
using System;

public class Selector : MonoBehaviour
{
  bool test = true;
  Camera eye;
  RadialItem[] items;
  void DoSomething(RadialItem item){
  }
  Vector2 pointer;
  static float halfWidth= Screen.width*0.5f;
  static float halfHeight = Screen.height*0.5f;
  static Vector2 cartesianer = new (halfWidth,halfHeight);
  void Select(){
    if(items == null || items.Length == 0 )
      if(test)
        items = new RadialItem[3];
      else 
        return;
    pointer = cartesianer - (Vector2) Input.mousePosition;
    // pointer.x = halfWidth-Input.mousePosition.x;
    // pointer.y = halfHeight - Input.mousePosition.y;
    if(pointer == Vector2.zero)
      return ;
    pointer.Normalize();
    float theta = MathF.Atan2(pointer.y,pointer.x) + MathF.PI * 0.5f;
    this.transform.rotation = Quaternion.Euler(0, 0, theta * 180f/MathF.PI);
    if(theta < 0){
      theta += MathF.PI*2f;
    }
    Debug.Log($"{theta * 180f/MathF.PI} deg");
    float slice = MathF.PI*2f/items.Length;
    //TODO: do whatever you need from this:
   //item selection is clockwise starting from top right
    int index = items.Length-(int)MathF.Floor(theta/slice) - 1;
    Debug.Log($"ind {index}");
    if(items.Length> index && index > 0);
      DoSomething(items[index]);
  }
  void Update(){
    Select();
  }
    
}
