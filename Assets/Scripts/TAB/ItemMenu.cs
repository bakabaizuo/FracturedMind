using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.U2D;
using UnityEngine.UI;

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
  List<ItemPanel> panels;
  List<RadialItem> items;
  public float slice{get; set;}
    // Start is called before the first frame update
    void Start()
    {
       panels = new(items?.Count??0); 
       RadialMenu parent;
       if(!transform.parent.gameObject.TryGetComponent(out parent))
         return;
       slice = parent.slice;
       items = parent.items;
       if(items == null)
         return;
       foreach(RadialItem item in items){
         MakeEntry(item);
       }
    }
    void MakeEntry(RadialItem item){
      GameObject entry = Instantiate(EntryPrefab, transform);
      ItemPanel pane;
      if(!TryGetComponent( out pane))
        return;
      pane.SetLabel(item.name);
      pane.SetLabel(item.name);
      panels.Add(pane);
    }
    void Rearrange(){
      //Call Rearrange when slice changes?
      int max = items.Count;
      RectTransform m_RectTransform;
      for(int i =0; i < max; i++){
        if(!panels[i].TryGetComponent(out m_RectTransform))
          continue;
        m_RectTransform.anchoredPosition = new(MathF.Sin(slice * i),MathF.Cos(slice*i));
        m_RectTransform.anchoredPosition *= radius;
      }

    }
}
