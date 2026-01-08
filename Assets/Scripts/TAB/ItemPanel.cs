using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.U2D;
using FracturedStudios.TAB;
using FracturedStudios.UI;
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
    get =>  label?.text ?? string.Empty;
    set { if (label != null) label.text = value; }
  }

  public Sprite CurrentSprite 
  {
    get => spriteHolder?.sprite; 
    set{ if(spriteHolder != null) spriteHolder.sprite ??= value; } 
  }

  public Image SpriteHolder => spriteHolder;
  
  /// <summary>
  /// Populate the panel from a RadialItem and an optional SpriteAtlas.
  /// Uses ResourceLoader to resolve sprites with fallbacks.
  /// </summary>
  public void Populate(RadialItem item, SpriteAtlas atlas = null)
  {
    if (item == null)
    {
      LabelText = "FUCK";
      return;
    }

    LabelText = item.Pseudonym ?? "NULL";

    if(string.IsNullOrEmpty(item.SpriteName))
      return;
    
    Sprite sprite = null;
    if (atlas != null )
      sprite = FracturedStudios.TAB.ResourceLoader.LoadSpriteFromAtlas(item.SpriteName, atlas);

      // Try a Resources fallback path (e.g. Resources/UI/Sprites/{name})
    sprite ??= FracturedStudios.TAB.ResourceLoader.GetResourceInPath<Sprite>($"UI/Sprites/{item.SpriteName}");

    if (sprite != null)
      CurrentSprite = sprite;
  }
  // public void SetSprite(Texture sprite){
  //   spriteHolder.image ??= sprite;
  // }
  // public void SetSprite(string name, SpriteAtlas atlas){
  //   SetSprite(atlas.GetSprite(name));
  // }
    // Start is called before the first frame update
    // void Start()
    // {
    //   SetLabel("Lorem Ipsum Dolor");
    // }
    void OnEnable(){
      Debug.Log(transform.position);
    }
}
}
