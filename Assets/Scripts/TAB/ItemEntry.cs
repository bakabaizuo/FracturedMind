using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.U2D;
using FracturedStudios.UI;

public class ItemEntry : MonoBehaviour
{
  [SerializeField]
  float radius;
  [SerializeField]
  GameObject EntryPrefab;
  [SerializeField]
  SpriteAtlas atlas;
  List<ItemPanel> panels;
  List<RadialItem> items;
  float slice;
    // Start is called before the first frame update
    void Start()
    {
       panels = new();
       RadialMenu parent;
       if(transform.parent.gameObject.TryGetComponent(out parent)){
         slice = parent.slice;
         items = parent.items;
       }

      if((items?.Count ?? 0) < 1)
        return;

      // Create entries for each RadialItem
      foreach(var it in items)
        MakeEntry(it);

      Display();
    }
    void MakeEntry(RadialItem item){
      if (EntryPrefab == null) return;
      GameObject entry = Instantiate(EntryPrefab, transform);
      // Try to find ItemPanel on the instantiated prefab (root or children)
      ItemPanel panel = entry.GetComponentInChildren<ItemPanel>(true);
      if(panel == null)
      {
        Debug.LogWarning($"[ItemEntry] Instantiated entry prefab '{EntryPrefab.name}' has no ItemPanel component.");
        return;
      }

      // Populate panel from RadialItem
      if(item != null)
      {
        panel.SetLabel(item.Pseudonym ?? string.Empty);
        if(atlas != null && !string.IsNullOrEmpty(item.SpriteName))
        {
          var spr = atlas.GetSprite(item.SpriteName);
          if(spr != null)
            panel.SetSprite(spr);
        }
      }

      panels.Add(panel);
    }
    void Display(){
      int max = items.Count;
      RectTransform m_RectTransform;
      for(int i =0; i < max; i++){
        if(panels[i] == null) continue;
        if(panels[i].TryGetComponent(out m_RectTransform))
        {
          m_RectTransform.anchoredPosition = new(MathF.Sin(slice * i),MathF.Cos(slice*i));
          m_RectTransform.anchoredPosition *= radius;
        }
      }

    }
    

    // Update is called once per frame
    void Update()
    {
        
    }
}
