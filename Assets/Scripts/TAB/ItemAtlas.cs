using UnityEngine;
[CreateAssetMenu(menuName = "ItemAtlas",fileName="ItemAtlas")]
public sealed class ItemAtlas:ScriptableObject{
  [SerializeField]
  RadialItem[] itemList;
  public RadialItem this[int i]{
    get => itemList[i];
  }
  public int Length{
    get=> itemList.Length;
  }




}
