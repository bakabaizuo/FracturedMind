using UnityEngine;
using System;
using System.Collections.Generic;
public class RadialMenu:MonoBehaviour
{
GameObject Menu;
GameObject Knob;
public delegate GetItemCaller(int index);
public event GetItemCaller? GetItem; 
public List<RadialItem> items{get; private set;}
public float slice{ get; private set;}
[SerializeField]
Selector knob;
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
    Menu = gameObject.transform.GetChild(0).gameObject;
    Knob =  gameObject.transform.GetChild(1).gameObject;
    slice = MathF.PI*2f/(items?.Count??1);
  Menu.SetActive(show);
  Knob.SetActive(show);

}
  static float halfWidth= Screen.width*0.5f;
  static float halfHeight = Screen.height*0.5f;
  static Vector2 cartesianer = new (halfWidth,halfHeight);
  bool show = false;
  Vector2 pointer;
void Update(){

  bool active =Input.GetButton("Radial Menu");

  if(show != active)
  {
    Menu.SetActive(active);
    Knob.SetActive(active);
    Cursor.visible = active;
    Cursor.lockState = (active) ? CursorLockMode.Confined:  CursorLockMode.Locked;
    show = active;
  }
  if(!show)
    return;
  pointer = cartesianer - (Vector2) Input.mousePosition;
  if(pointer == Vector2.zero)
    return;
  pointer.Normalize();
  float theta = MathF.Atan2(pointer.y,pointer.x) + MathF.PI * 0.5f;
  knob.Select(theta);
    if(theta < 0){
      theta += MathF.PI*2f;
    }
    if ((items?.Count ?? 0)< 1)
        return;
    GetItem(items.Count-(int)MathF.Floor(theta/slice) - 1);
  
  }
}
