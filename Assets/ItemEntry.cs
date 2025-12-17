using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class ItemEntry : MonoBehaviour
{
  [SerializeField]
  float radius;
  [SerializeField]
  GameObject EntryPrefab;
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
    }
    void MakeEntry(){
      GameObject entry = Instantiate(EntryPrefab, transform);
      ItemPanel panel;
      if(!TryGetComponent( out panel))
        return;
      panels.Add(panel);

    }
    void Display(){
      int max = items.Count;
      RectTransform m_RectTransform;
      for(int i =0; i < max; i++){
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
