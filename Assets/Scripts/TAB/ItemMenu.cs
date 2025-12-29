using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.U2D;
using UnityEngine.UI;
using FracturedStudios.UI;
using FracturedStudios.TAB;

//TODO: Rename to ItemMenu
public class ItemMenu : MonoBehaviour
{
  [SerializeField]
  float radius;
  [SerializeField]
  GameObject EntryPrefab;
  [SerializeField]
  SpriteAtlas atlas;
  //Make panels an ObjectPool
  [SerializeField]
  List<FracturedStudios.UI.ItemPanel> panels;
  [SerializeField]
  bool test;
  public List<RadialItem> items;
  public float slice{get; set;}
    // Start is called before the first frame update
    void Start()
    {
    
    }
    private void ItemMenuEnable()
    {
         panels = new(items?.Count??0);
       RadialMenu parent;
       if(!transform.parent.gameObject.TryGetComponent(out parent))
         return;
       slice = parent.slice;
       if(test)
         items = parent.items;

       // If EntryPrefab was already assigned in inspector, initialize immediately.
       if (EntryPrefab != null && (items?.Count ?? 0) > 0)
       {
         Initialize(EntryPrefab, atlas, items);
       }
    }
    FracturedStudios.UI.ItemPanel CreateEntry(RadialItem item)
    {
      if (EntryPrefab == null) return null;
      GameObject entry = Instantiate(EntryPrefab, transform);
      FracturedStudios.UI.ItemPanel pane;
      if(!entry.TryGetComponent( out pane)){
        return null;
      }
      pane.Populate(item, atlas);
      return pane;
    }

    /// <summary>
    /// Initialize the menu with an entry prefab, atlas and item list.
    /// Safe to call at runtime after discovery/validation.
    /// </summary>
    public void Initialize(GameObject entryPrefab, SpriteAtlas spriteAtlas, List<RadialItem> itemList)
    {
      if (entryPrefab == null || itemList == null) return;
      EntryPrefab = entryPrefab;
      atlas = spriteAtlas;
      items = itemList;

      panels = new(items.Count);
      foreach(RadialItem item in items)
      {
        var p = CreateEntry(item);
        if (p != null) panels.Add(p);
      }

      Rearrange();
    }
    void Rearrange(){
      //Call Rearrange when slice changes?
      int max = panels.Count;
      Debug.Log(max);
      if(max < 1)
        return;
      RectTransform m_RectTransform;
      for(int i =0; i < max; i++){
        if(!panels[i].TryGetComponent(out m_RectTransform))
          continue;
        m_RectTransform.anchoredPosition = 
          // Vector2.zero;
          (new(MathF.Sin(slice * i),MathF.Cos(slice*i)));
        m_RectTransform.anchoredPosition *= radius;
      }

    }
}
