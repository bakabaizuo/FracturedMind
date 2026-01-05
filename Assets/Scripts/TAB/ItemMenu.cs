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
   float radius;
  [SerializeField]
  GameObject EntryPrefab;
  [SerializeField]
  SpriteAtlas atlas;
  //Make panels an ObjectPool
  [SerializeField]
  List<FracturedStudios.UI.ItemPanel> panels;
  // public List<Radia
  // public float slice{get; set;}
    // Start is called before the first frame update
    void Start()
    {
      if (EntryPrefab == null)
        //load it in
        ;
      int len = items.Count;
      if(len> 0)
        slice = MathF.PI* (2/len);
      ItemMenuEnable();
      RadialMenu.GetItem+= GetItem;
    
    }
    private void ItemMenuEnable()
    {
       // If EntryPrefab was already assigned in inspector, initialize immediately.
       if (EntryPrefab == null || (items?.Count ?? 0) < 1)
         // Initialize();
         ;
       MakePanels();
       
    }
    FracturedStudios.UI.ItemPanel CreateEntry(RadialItem item)
    {
      FracturedStudios.UI.ItemPanel pane = null;
      bool instantiated = EntryPrefab != null && (Instantiate(EntryPrefab, transform)?.TryGetComponent( out pane)??false);
      if(instantiated)
        pane!.Populate(item, atlas);
      return pane;
    }
    void MakePanels(){
      int max = items.Count;
      if(max < 1)
        return;
      panels = new(items.Count);
      for(int i = 0; i < max; i++){
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
      EntryPrefab ??= entryPrefab;
      atlas ??= spriteAtlas;
      items ??= itemList;

    }
    void Rearrange(){
      //Call Rearrange when slice changes?
      int max = panels.Count;
      if(max < 1)
        return;
      for(int i =0; i < max; i++){

          Vector2 placement = new Vector2(MathF.Cos(slice * i),MathF.Sin(slice*i)) * radius;
          Debug.Log(placement);
          panels[i]?.SetPosition(placement);


      }

    }
   void GetItem(float theta){
    if((items?.Count ?? 0) < 1)
      return;
    int index = (int) MathF.Floor(theta/slice);
    if (index >= items.Count)
      index %= items.Count;
    if(panels[index] != null)
      panels[index].transform.position= Vector3.zero;
    else
      Debug.Log("FICK");
  }
}
