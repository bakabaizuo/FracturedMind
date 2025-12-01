
using Unity.Mathematics;
using Unity.Burst;
using UnityEngine;
//Unnecessarily BurstCompiled code that acts as an API for AI Vision
[BurstCompile]
public sealed class AIVisionUtils{

   [BurstCompile]
   public static float getViewDistance(
       bool isCrouching,
       float viewDistance,
       float crouchDetectionModifier)
     => 
     isCrouching?
       crouchDetectionModifier*viewDistance : viewDistance ;
   [BurstCompile]
   public static void getTargetDirection(in float3 between, out float3 direction ){
     direction = math.normalize(between);
   }
  [BurstCompile]
  public static void getTargetDirection(
  bool isCrouched,
  float scale,
  in float3 origin, 
  in float3 between, 
  out float3 direction){
    getTargetDirection(between,out direction);
    if(isCrouched)
      direction += (scale-1)* origin;
  }
  [BurstCompile]
  public static bool fovCheck( float fovCosTheta, in float3 left, in float3 right)=>
    math.dot(left,right) > fovCosTheta;
  
  [BurstCompile]
  public static void moveEye(in float3 hostPosition, in float3 eyePosition, out float3 finalPosition){
    finalPosition = hostPosition + eyePosition;
  }
  [BurstCompile]
  public static bool rayCast (int id, in float3 origin, in float3 targetDir, float dist,int mask )=>
  Physics.Raycast(
              origin,
              targetDir,
              out RaycastHit hit,
              dist,
              mask
          )
        &&hit.colliderInstanceID == id;
  

}
