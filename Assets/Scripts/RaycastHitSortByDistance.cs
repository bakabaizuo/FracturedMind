using Unity.Collections;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
[BurstCompile]
public struct RaycastHitSortDistance:IComparer<RaycastHit>{

  public Vector3 origin;
  public int Compare(RaycastHit left, RaycastHit right){
    Vector3 rightOperand = (origin - right.point);
    Vector3 leftOperand = origin - left.point;
    int leftSqr = Mathf.FloorToInt(leftOperand.sqrMagnitude);
    int rightSqr = Mathf.FloorToInt(rightOperand.sqrMagnitude);
    return leftSqr - rightSqr;
  }

}
