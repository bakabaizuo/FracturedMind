using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FlashBang : MonoBehaviour
{
    [SerializeField]
    GameObject BlindScreen;
    Image Tint;
    IEnumerator Dim(){
      for(float alpha = 1f; alpha > 0.01f; alpha -= 0.1f){
        Tint.color = new Color(1f,1f,1f,alpha);
        yield return new WaitForFixedUpdate();
      }
      BlindScreen.SetActive(false);
    }
    public void Flash(){
      BlindScreen.SetActive(true);
      var dim = Dim();
      StartCoroutine(dim);

    }
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
