using UnityEngine;
[CreateAssetMenu(menuName = "NPC Configuration/Enemy Configuration/Base Enemy",fileName="BaseEnemyConfiguration")]

sealed public class BaseEnemyConfiguration: ScriptableObject{
  public NPCVision visionSettings;
  public EnemyAttention attentionSettings;

  public NPCVisionBehavior npcTargets;


}
[CreateAssetMenu(menuName = "NPC Configuration/Enemy Configuration/Base Enemy Vision",fileName="BaseEnemyVision")]
 sealed public class NPCVision : ScriptableObject{
   [field:SerializeField]
    public  float viewDistance{get; private set;}
   // [field:SerializeField]
   //  public  float minViewDistance{get; private set;}
   [field:SerializeField]
    public  float fovCosTheta{get; private set;}
   [field:SerializeField]
    public  float eyeHeight{get; private set;}
   [field:SerializeField]
    public  float crouchDetectionModifier{get; private set;}

 }

[CreateAssetMenu(menuName = "NPC Configuration/Enemy Configuration/Base Enemy Attention",fileName="BaseEnemyAttention")]
 sealed public class EnemyAttention : ScriptableObject{
   [field:SerializeField]
  public int alertDuration{get; private set;}
   [field:SerializeField]
  public int detectDuration{get;private set;}
 } 
    
[CreateAssetMenu(menuName = "NPC Configuration/Enemy Configuration/Base Enemy Vision Behavior",fileName="BaseNPCVisionBehavior")]
sealed public class NPCVisionBehavior:ScriptableObject{
  public LayerMask layerMask;
  [SerializeField]
  private bool hitBackFace;
  [SerializeField]
  private  bool hitMultipleFaces;
  [SerializeField]
  private QueryTriggerInteraction howTriggers;
  private QueryParameters parameters;
  public QueryParameters query {get => parameters; 
   private set {
     parameters= new(layerMask, hitMultipleFaces, howTriggers, hitBackFace);
   }
  }
}

