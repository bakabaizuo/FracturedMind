using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.U2D;
using UnityEngine.UI;
using FracturedStudios.UI;

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

       panels = new(items?.Count??0); 
       RadialMenu parent;
       if(!transform.parent.gameObject.TryGetComponent(out parent))
         return;
       slice = parent.slice;
       if(test)
         items = parent.items;
       
       if((items?.Count??0 )< 1)
         return;
       foreach(RadialItem item in items){
         MakeEntry(item);
       }
       Rearrange();
    }
    void MakeEntry(RadialItem item){
      GameObject entry = Instantiate(EntryPrefab, transform);
      FracturedStudios.UI.ItemPanel pane;
      if(!entry.TryGetComponent( out pane)){
        return;
      }
      pane.SetLabel(item.Pseudonym);
      pane.SetSprite(atlas.GetSprite(item.SpriteName));
      panels.Add(pane);
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
