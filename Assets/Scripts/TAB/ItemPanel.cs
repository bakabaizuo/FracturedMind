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
    [SerializeField]
    RectTransform rect;
    void Awake(){
      rect ??= GetComponent<RectTransform>();

    }
    Vector3 resting;
    // Expose label text and sprite for external consumers
    public string LabelText
    {
      get =>  label?.text ?? "UUUUUUUGGGGGGGGGGHHHHHHHHHH";
      set { if (label != null) label.text ??= value; }
    }

    public Sprite CurrentSprite 
    {
      get => spriteHolder?.sprite; 
      set{ if(spriteHolder != null) spriteHolder.sprite = value; } 
    }
    public void SetPosition(float x, float y, float z, float radius, bool reset = false){
      SetPosition(new Vector3(x,y,z) * radius,reset);
    }
    public void SetPosition(Vector3 position, bool reset = false){
      if(reset)
        resting = position;
      if(rect != null)
        rect.anchoredPosition = position;
    }
    public void SetToRest(){
      if(rect != null)
        rect.anchoredPosition = resting;
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
      // return;
      Sprite sprite = atlas is null?null : FracturedStudios.TAB.ResourceLoader.LoadSpriteFromAtlas(item.SpriteName, atlas);
      // Debug.Log(sprite);

        // Try a Resources fallback path (e.g. Resources/UI/Sprites/{name})
      sprite ??= FracturedStudios.TAB.ResourceLoader.GetResourceInPath<Sprite>($"UI/Sprites/{item.SpriteName}");

      if (sprite != null)
        CurrentSprite = sprite;
    }
  }
}
