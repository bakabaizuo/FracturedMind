using UnityEngine;
using Unity.Burst;
using Unity.Mathematics;
  [CreateAssetMenu(menuName = "NPC Configuration/NPC",fileName="NPCConfiguration")]
[BurstCompile]
  sealed public class NPCConfiguration: ScriptableObject{
    public NPCVision visionSettings;
    public EnemyAttention attentionSettings;
     [BurstCompile]
     public static float getViewDistance(bool isCrouching, float viewDistance, float crouchDetectionModifier)
       =>
       isCrouching? viewDistance: crouchDetectionModifier*viewDistance;
     [BurstCompile]
     public static void getTargetDirection(in float3 between, out float3 direction ){
       direction = math.normalize(between);
     }
[BurstCompile]
public static void getTargetDirection(bool isCrouched, float scale,  in float3 origin, in float3 between, out float3 direction){
  getTargetDirection(between,out direction);
if(isCrouched)
  direction += (scale-1)* origin;
}
  }
