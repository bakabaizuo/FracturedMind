using UnityEngine;
using System;
public class RadialMenu:MonoBehaviour
{
GameObject Menu;
GameObject Selector;
void Start(){
   Debug.Log("STARTING TEST");
    Menu = gameObject.transform.GetChild(0).gameObject;
    Selector =  gameObject.transform.GetChild(1).gameObject;
}
void Update(){

  bool active =Input.GetButton("Radial Menu");
  Menu.SetActive(active);
  Selector.SetActive(active);
  Cursor.visible = active;
  Cursor.lockState = (active) ? CursorLockMode.Confined:  CursorLockMode.Locked;

  
  }
}
