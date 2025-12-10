using UnityEngine;
public class RadialMenu:MonoBehaviour
{
void Update(){
bool active = Input.GetButton("Radial Menu");
// gameObject.SetActive(active);
gameObject.transform.GetChild(0).gameObject.SetActive(active);
// Debug.Log(active);
}
}
