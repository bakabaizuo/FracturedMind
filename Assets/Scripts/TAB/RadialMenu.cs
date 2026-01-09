using UnityEngine;
using System;
using System.Collections.Generic;
public class RadialMenu:MonoBehaviour
{
  public delegate void GetItemCaller(float theta);
  public static event GetItemCaller GetItem; 
  [SerializeField]
  Transform pointer;
  [SerializeField]
  Canvas menu;
  readonly float halfWidth = Screen.width*0.5f;
  readonly float halfHeight = Screen.height*0.5f;

  readonly float  menuSensitivity = 0.01f;

  void Update(){


    menu.enabled = Input.GetButton("RadialMenu");
    if(!menu.enabled)
      return;
    if(Input.GetAxis("Mouse X") == 0f && Input.GetAxis("Mouse Y") == 0f){
      GetItem?.Invoke(-1f);
      return;
    }
    Vector2 direction = new ( Input.mousePosition.x - halfWidth,  halfHeight - Input.mousePosition.y );
    if( direction.sqrMagnitude <= menuSensitivity)
      return;
    float theta = MathF.Atan2(direction.x,direction.y)+ MathF.PI;
    pointer.rotation = Quaternion.Euler(0f, 0f, theta * Mathf.Rad2Deg);
    GetItem?.Invoke(theta);
  }
}
