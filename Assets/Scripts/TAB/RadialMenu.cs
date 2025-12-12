using UnityEngine;
using System;
public class RadialMenu:MonoBehaviour
{
GameObject Menu;
RadialItem[] items;
void Start(){
   Debug.Log("STARTING TEST");
  // Menu = gameObject.transform.GetChild(0).gameObject;
}
Vector2 pointer;
readonly Vector2 cartesianer = new (Screen.width/2f,Screen.height/2f);
void Select(){
  pointer.x = cartesianer.x-Input.mousePosition.x;
  pointer.y = Input.mousePosition.y-cartesianer.y;
  pointer.Normalize();
  if(pointer == Vector2.zero)
    return ;
  float theta = MathF.Atan2(pointer.y,pointer.x) + MathF.PI * 0.5f;
  if(theta < 0){
    theta += MathF.PI*2f;
  }
  float slice = MathF.PI*2f/items.Length;
  //TODO: do whatever you need from this:
  int selection = (int)MathF.Floor(theta/slice);
  items[selection];



}
void Update(){

  bool active =Input.GetButton("Radial Menu");
  Menu.SetActive(active);
  Cursor.visible = active;
  Cursor.lockState = (active) ? CursorLockMode.Confined:  CursorLockMode.Locked;
  if(active)
    Select();

  
  }
}
