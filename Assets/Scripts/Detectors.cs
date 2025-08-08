using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Detectors 
{
  public static bool DetectInSphere(Vector3 offset, float distance) => offset.sqrMagnitude <= (distance * distance);
  public static bool DetectInSphere(Vector3 origin, Vector3 target, float distance)=>
    DetectInSphere(target-origin, distance);

  //you are in a cone if a . b > cos theta
  public static bool DetectInAngle(Vector3 facing, Vector3 offset, float theta) =>
    Vector3.Dot(facing.normalized, offset.normalized) > Mathf.Cos(theta);
  

}
