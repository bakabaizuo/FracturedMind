using UnityEngine;
using System;
using System.Collections.Generic;
public class RadialMenu:MonoBehaviour
{
  public delegate void GetItemCaller(float theta);
  public static event GetItemCaller? GetItem; 
  [SerializeField]
  Transform pointer;
  [SerializeField]
  Canvas menu;
  static float halfWidth = Screen.width*0.5f;
  static float halfHeight = Screen.height*0.5f;


  void Update(){

    if(menu.enabled ^ Input.GetButton("RadialMenu")){
      menu.enabled = !menu.enabled;
      Cursor.visible = menu.enabled;
      Cursor.lockState = (menu.enabled) ? CursorLockMode.Confined:  CursorLockMode.Locked;
    }
    if(!menu.enabled)
      return;
    Vector2 direction = new (halfWidth - Input.mousePosition.x, Input.mousePosition.y - halfHeight);
    if( direction.sqrMagnitude <= 0.01)
      return;
    float theta = MathF.Atan2(direction.x,direction.y) ;
    pointer.rotation = Quaternion.Euler(0f, 0f, theta * Mathf.Rad2Deg);
    GetItem?.Invoke(theta);
  }
}
