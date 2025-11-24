
using UnityEngine;  
[CreateAssetMenu(menuName = "NPC Configuration/Vision",fileName="NPCVision")]

   sealed public class NPCVision : ScriptableObject{
     [field:SerializeField]
      public  float viewDistance{get; private set;}
     [field:SerializeField]
      public  float fovCosTheta{get; private set;}
     [field:SerializeField]
      public  float eyeHeight{get; private set;}
     [field:SerializeField]
      public  float crouchDetectionModifier{get; private set;}
     [field:SerializeField]
     public LayerMask targetList{get; private set;}

   }
