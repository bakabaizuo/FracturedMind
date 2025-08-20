using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Burst;
using Unity.Mathematics;
using static Unity.Mathematics.math;
[BurstCompile]
public struct Detectors 
{
  //anything that uses this DetectInSphere has adequate attention span (expected) <= buf size
  public static bool DetectInSphere(float3 origin, float radius, ref Collider[] buf, int expected = 1, int layer = 6)
    => expected <= Physics.OverlapSphereNonAlloc(origin,radius, buf,layer);
  public static bool DetectInSphere(float3 offset, float distance) => lengthsq(offset) <= (distance * distance);
  public static bool DetectInSphere(float3 origin, float3 target, float distance)=>
    lengthsq(target-origin) <= distance*distance;

  //you are in a cone if a . b > cos theta
  public static bool DetectInAngle(float3 facing, float3 offset, float theta) =>
    dot(normalize(facing), normalize(offset)) > cos(theta);
  
  public static bool DetectInCone(float3 origin, float3 targetPosition, float3 facing,float range, float theta){
    float3 offset = origin-targetPosition;
    return DetectInSphere(offset,range) && DetectInAngle(facing,offset,theta);
  }    

 }
