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
  
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void Select(float theta){
    this.transform.GetChild(0).rotation = Quaternion.Euler(0, 0, theta * Mathf.Rad2Deg);
   //  if(theta < 0){
   //    theta += MathF.PI*2f;
   //  }
   //  if (items == null || items.Count < 1)
   //      return null;
   //  //TODO: do whatever you need from this:
   // //item selection is clockwise starting from top right
   //  return items.Count-(int)MathF.Floor(theta/slice) - 1;
  }
  // [MethodImpl(MethodImplOptions.AggressiveInlining)]
  // void Update(){
  //   Debug.Log(Select());
  // }

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
    if(parent != null)
      
    Debug.Log(items?.Count);
  }
    
}
