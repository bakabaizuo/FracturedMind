using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UIElements;
using UnityEngine.U2D;

public class ItemPanel : MonoBehaviour
{
  //Refactor in the future. put sprites in one canvas and text in another for better performance
  [SerializeField]
  TextMeshProUGUI label;
  [SerializeField]
  Image spriteHolder;
  public void SetLabel(string text){
    label.text = text;
  }
  public void SetSprite(Texture sprite){
    if(spriteHolder!= null)
    spriteHolder.image ??= sprite;
  }
  public void SetSprite(string name, SpriteAtlas atlas){
    if(spriteHolder != null)
      spriteHolder.sprite ??= atlas.GetSprite(name);
  }
    // Start is called before the first frame update
    void Start()
    {
      SetLabel("Lorem Ipsum Dolor");
      spriteHolder =  GetComponentInChildren<Image>();
    }
}
