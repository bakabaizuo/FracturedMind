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
    
    // Expose label text and sprite for external consumers
    public string LabelText
    {
      get =>  label?.text ?? string.Empty;
      set { if (label != null) label.text ??= value; }
    }

    public Sprite CurrentSprite 
    {
      get => spriteHolder?.sprite; 
      set{ if(spriteHolder != null) spriteHolder!.sprite ??= value; } 
    }
    public void SetPosition(float x, float y, float z, float radius){
      SetPosition(new Vector3(x,y,z) * radius);
    }
    public void SetPosition(Vector3 position){
      Debug.Log(position);
      rect.anchoredPosition= position;

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
      return;
      
      Sprite sprite = null;
      if (atlas != null )
        sprite = FracturedStudios.TAB.ResourceLoader.LoadSpriteFromAtlas(item.SpriteName, atlas);

        // Try a Resources fallback path (e.g. Resources/UI/Sprites/{name})
      sprite ??= FracturedStudios.TAB.ResourceLoader.GetResourceInPath<Sprite>($"UI/Sprites/{item.SpriteName}");

      if (sprite != null)
        CurrentSprite = sprite;
    }
  }
}
