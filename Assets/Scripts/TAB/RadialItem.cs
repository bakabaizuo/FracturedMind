using UnityEngine;
  [CreateAssetMenu(menuName = "RadialItem",fileName="RadialItemConfiguration")]
public sealed class RadialItem:ScriptableObject
{
  public string Pseudonym;
  public string SpriteName;
  public string Key;
  public string FlavorText;
  public bool owned;

}
