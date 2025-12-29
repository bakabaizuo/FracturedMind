using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.U2D;
namespace FracturedStudios.UI
{
public class ItemPanel : MonoBehaviour
{
  //Refactor in the future. put sprites in one canvas and text in another for better performance
  [SerializeField]
  TextMeshProUGUI label;
  [SerializeField]
  Image spriteHolder;
  
  // Expose label text and sprite for external consumers
  public string LabelText
  {
    get => label != null ? label.text : string.Empty;
    set { if (label != null) label.text = value; }
  }

  public Sprite CurrentSprite => spriteHolder != null ? spriteHolder.sprite : null;

  public Image SpriteHolder => spriteHolder;
  public void SetLabel(string text){
    label.text = text;
  }
  // public void SetSprite(Texture sprite){
  //   spriteHolder.image ??= sprite;
  // }
  public void SetSprite(string name, SpriteAtlas atlas){
    SetSprite(atlas.GetSprite(name));
  }
  public void SetSprite(Sprite sprite){
    if(spriteHolder == null){
      return;
    }
    spriteHolder.sprite ??= sprite;
  }
    // Start is called before the first frame update
    void Start()
    {
      SetLabel("Lorem Ipsum Dolor");
    }
    void OnEnable(){
      Debug.Log(transform.position);
    }
}
}