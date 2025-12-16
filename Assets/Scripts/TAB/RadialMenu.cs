using UnityEngine;
using System;
using System.Collections.Generic;
public class RadialMenu:MonoBehaviour
{
GameObject Menu;
GameObject Selector;
public List<RadialItem> items{get; private set;}
public float slice{ get; private set;}
void Start(){
    items = new(3);
    Menu = gameObject.transform.GetChild(0).gameObject;
    Selector =  gameObject.transform.GetChild(1).gameObject;
    slice = MathF.PI*2f/(items?.Count??1);

}
// void UpdateChildren(){
  // Menu.TryGetComponent();
// }
void Update(){

  bool active =Input.GetButton("Radial Menu");

  // if(!active)
  //   return;
  Menu.SetActive(active);
  Selector.SetActive(active);
  Cursor.visible = active;
  Cursor.lockState = (active) ? CursorLockMode.Confined:  CursorLockMode.Locked;

  
  }
}
