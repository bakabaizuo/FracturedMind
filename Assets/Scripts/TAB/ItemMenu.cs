using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.U2D;
using UnityEngine.UI;
using FracturedStudios.UI;
using FracturedStudios.TAB;
using System.Collections.Specialized;

//TODO: Rename to ItemMenu
public class ItemMenu : MonoBehaviour
{
  [SerializeField]
  // public List<RadialItem> items;
  public ItemAtlas items;
  public float slice{ get; private set;}
  [SerializeField]
   float radius;
  [SerializeField]
  GameObject EntryPrefab;
  [SerializeField]
  SpriteAtlas atlas;
  [SerializeField]
  bool test;
  [SerializeField]
  int owned;
  BitVector32 inventory;

  [Flags]
  public enum Inventory:short{

  }
  //Make panels an ObjectPool
  [SerializeField]
  List<FracturedStudios.UI.ItemPanel> panels;
  // public List<Radia
  // public float slice{get; set;}
    // Start is called before the first frame update
    void Start()
    {
      if(test){
        inventory = new BitVector32(3);
      }
      Debug.Log($"data {inventory.Data}");
      if (EntryPrefab == null)
        //load it in
        ;
      ItemMenuEnable();
      int len = panels.Count;
      if(len> 0)
        slice = MathF.PI*2f/len;
      RadialMenu.GetItem+= GetItem;
    
    }
    private void ItemMenuEnable()
    {
       // If EntryPrefab was already assigned in inspector, initialize immediately.
       if (EntryPrefab == null || (items?.Length ?? 0) < 1)
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
      int max = items.Length;
      if(max < 1)
        return;
      panels = new(max);
      Debug.Log(max);
      Debug.Log($"data {inventory.Data}");
      for(int i = 0; i < max; i++)
      { int mask = 1 << (31-i);
        Debug.Log($"{i}{inventory[mask]}");
        if(!inventory[mask])
          continue;
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
      if (items == null || entryPrefab == null || itemList == null) return;
      EntryPrefab = entryPrefab;
      atlas = spriteAtlas;
      // items = itemList;

    }
    void Rearrange(){
      //Call Rearrange when slice changes?
      int max = panels.Count;
      if(max < 1)
        return;
      for(int i =0; i < max; i++){

          Vector3 placement = new Vector3(MathF.Cos(slice * i),MathF.Sin(slice*i), 0f) * (radius);
          Debug.Log($"{this} {placement}");
          panels[i]?.SetPosition(placement, true);


      }

    }
   void GetItem(float theta){
    if((items?.Length ?? 0) < 1)
      return;
    int index = -1;
    if(theta >= 0f){
      index = (int) MathF.Floor(theta/slice);
      if (index >= items.Length)
        index %= items.Length;
    }
    for(int i = 0; i < (panels?.Count ?? -1); i++){
      if(i == index){
        panels[i]?.SetPosition(Vector3.up * 10);
      }else{
        panels[i]?.SetToRest();
      }
    }
  }
}
