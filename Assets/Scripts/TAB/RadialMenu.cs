using UnityEngine;
using System;
using System.Collections.Generic;
public class RadialMenu:MonoBehaviour
{
public delegate void GetItemCaller(int index);
public event GetItemCaller? GetItem; 
public List<RadialItem> items{get; private set;}
public float slice{ get; private set;}
[SerializeField]
Selector selector;
[SerializeField]
bool test = true;
void Start(){
  if(test)
  {
    items = new(3);
    RadialItem[] test = new RadialItem[5];
    for(int i = 0; i < test.Length; i++){
      items.Add(test[i]);
    }
  }
    foreach (Transform child in transform)
      child.gameObject.SetActive(show);
    slice = fullCircle/(items?.Count??1);

}
  static float halfWidth = Screen.width*0.5f;
  static float halfHeight = Screen.height*0.5f;
  bool show = false;
  Vector2 pointer;
  static float fullCircle = MathF.PI *2f;
  Vector2 old;
void Update(){

  bool active =Input.GetButton("RadialMenu");

  if(show != active)
  {
    show = active;
    foreach (Transform child in transform)
      child.gameObject.SetActive(show);
    Cursor.visible = show;
    Cursor.lockState = (show) ? CursorLockMode.Confined:  CursorLockMode.Locked;
  }
  if(!show)
    return;
  pointer = new (halfWidth - Input.mousePosition.x, Input.mousePosition.y - halfHeight);
  if(old == pointer || pointer == Vector2.zero)
    return;
  float theta = MathF.Atan2(pointer.x,pointer.y) ;
  selector.Select(theta);
  old = pointer;
  if(theta < 0)
    theta += fullCircle;
  int index = (int)MathF.Floor(theta/slice);
  GetItem?.Invoke( (index < (items?.Count?? -1)) ? index : 0 );
  }
}
