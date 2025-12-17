using UnityEngine;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

public class Selector : MonoBehaviour
{
  bool test = true;
  Camera eye;
  List<RadialItem> items;
  Vector2 pointer;
  GameObject parent;
  RadialMenu menu;
  float slice;
  void DoSomething(RadialItem item){
  }
  static float halfWidth= Screen.width*0.5f;
  static float halfHeight = Screen.height*0.5f;
  static Vector2 cartesianer = new (halfWidth,halfHeight);
  
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  int? Select(){
    pointer = cartesianer - (Vector2) Input.mousePosition;
    // pointer.x = halfWidth-Input.mousePosition.x;
    // pointer.y = halfHeight - Input.mousePosition.y;
    if(pointer == Vector2.zero)
      return null;
    pointer.Normalize();
    float theta = MathF.Atan2(pointer.y,pointer.x) + MathF.PI * 0.5f;
    this.transform.rotation = Quaternion.Euler(0, 0, theta * Mathf.Rad2Deg);
    if(theta < 0){
      theta += MathF.PI*2f;
    }
    if (items == null || items.Count < 1)
        return null;
    //TODO: do whatever you need from this:
   //item selection is clockwise starting from top right
    return items.Count-(int)MathF.Floor(theta/slice) - 1;
  }
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  void Update(){
    Select();
  }

  void Start(){
    parent = transform.parent.gameObject;
    if(parent.TryGetComponent(out menu));
    {
      items = menu.items;
      slice = menu.slice;
    }
  }
  void OnEnable(){
    items = menu?.items ?? null;
    slice = menu?.slice ?? 0f;
    Debug.Log(items?.Count);
  }
    
}
