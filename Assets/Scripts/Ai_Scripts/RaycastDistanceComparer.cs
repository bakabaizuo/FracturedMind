using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public struct RaycastDistanceComparer : IComparer<RaycastHit>
{

  public Vector3 origin;
  public int Compare(RaycastHit left, RaycastHit right)
    =>
    Mathf.FloorToInt((origin-left.point).sqrMagnitude) - Mathf.FloorToInt((origin-right.point).sqrMagnitude);
  
}
