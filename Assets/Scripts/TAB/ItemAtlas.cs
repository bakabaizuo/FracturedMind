using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.ComponentModel;
// [CreateAssetMenu(menuName = "ItemAtlas",fileName="ItemAtlas")]
public sealed class ItemAtlas:MonoBehaviour
{
  public static ItemAtlas Instance;
  [SerializeField]
  RadialItem[] itemList;
  Dictionary<string, RadialItem> itemAtlas;
  // [SerializeField]
  // string path;

  void Awake(){
    if( Instance != null){
      Destroy(this);
      return;
    }
    
    Instance = this;
    // itemList = Resources.LoadAll(path) as RadialItem[];
    int siz_t = itemList.Length;
    itemAtlas = new();
    foreach(RadialItem entry in itemList)
      itemAtlas.TryAdd(entry.name,entry);
  }
  RadialItem? GetItem(string name)
    => itemAtlas.TryGetValue(name, out RadialItem item) ? item : null;




}
