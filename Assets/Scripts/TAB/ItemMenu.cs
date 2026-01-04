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
  public List<RadialItem> items;
  public float slice{ get; private set;}
  [SerializeField]
  readonly float radius;
  [SerializeField]
  GameObject EntryPrefab;
  [SerializeField]
  SpriteAtlas atlas;
  //Make panels an ObjectPool
  [SerializeField]
  List<FracturedStudios.UI.ItemPanel> panels;
  [SerializeField]
  readonly bool test;
  // public List<RadialItem> items;
  // public float slice{get; set;}
    // Start is called before the first frame update
    void Start()
    {
      RadialMenu.GetItem+= GetItem;
      int len = items.Count;
if (EntryPrefab == null)
  //load it in
  ;
      if(len> 0)
        slice = MathF.PI* (2/len);
      ItemMenuEnable();
    
    }
    private void ItemMenuEnable()
    {
       // If EntryPrefab was already assigned in inspector, initialize immediately.
       if (EntryPrefab == null || (items?.Count ?? 0) < 1)
         // Initialize();
         ;
       Populate();
       
    }
    FracturedStudios.UI.ItemPanel CreateEntry(RadialItem item)
    {
      if (EntryPrefab == null) return null;
      GameObject entry = Instantiate(EntryPrefab, transform);
      if(entry.TryGetComponent( out FracturedStudios.UI.ItemPanel pane)){
        pane.Populate(item, atlas);
        return pane;
      }
        return null;
    }
    void Populate(){
      int max = items.Count;
      if(max < 1)
        return;
      panels = new(items.Count);
      for(int i = 0; i < max; max++)
      {
        var p = CreateEntry(items[i]);
        if (p != null) panels?.Add(p);
      }
      Rearrange();
    }

    /// <summary>
    /// Initialize the menu with an entry prefab, atlas and item list.
    /// Safe to call at runtime af
    /// er discovery/validation.
    /// </summary>
    public void Initialize(GameObject entryPrefab, SpriteAtlas spriteAtlas, List<RadialItem> itemList)
    {
      if (entryPrefab == null || itemList == null) return;
      EntryPrefab = entryPrefab;
      atlas = spriteAtlas;
      items = itemList;

    }
    void Rearrange(){
      //Call Rearrange when slice changes?
      int max = panels.Count;
      if(max < 1)
        return;
      for(int i =0; i < max; i++){
        if(panels[i]?.TryGetComponent(out RectTransform m_RectTransform)??false)
          m_RectTransform.anchoredPosition = new Vector2(MathF.Sin(slice * i),MathF.Cos(slice*i)) * radius;
        // m_RectTransform.anchoredPosition *= radius;
      }

    }
       void GetItem(float theta){
         Debug.Log(slice);
         if(items.Count < 1)
           return;
    int index = (int)MathF.Floor(theta/slice);
        // panels[index].transform.localScale *= 5;
      }
}
