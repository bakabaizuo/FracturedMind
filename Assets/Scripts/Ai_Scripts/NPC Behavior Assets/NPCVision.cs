using UnityEngine;
using Unity.Mathematics;
[CreateAssetMenu(menuName = "NPC Configuration/Vision",fileName="NPCVision")]
   sealed public class NPCVision : ScriptableObject{
     public float crouchViewDistance()=>viewDistance * crouchDetectionModifier;
     
     [field:SerializeField]
      public float3 eyeOrigin{get; private set;}
    
     [field:SerializeField]
      public  float viewDistance{get; private set;}
     [field:SerializeField]
      public  float fovCosTheta{get; private set;}
     [field:SerializeField]
      public  float crouchDetectionModifier{get; private set;}
     [field:SerializeField]
     public LayerMask targetList{get; private set;}

   }
