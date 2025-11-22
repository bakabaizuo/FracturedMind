using UnityEngine;
    
  [CreateAssetMenu(menuName = "NPC Configuration/NPC",fileName="NPCConfiguration")]

  sealed public class NPCConfiguration: ScriptableObject{
    public NPCVision visionSettings;
    public EnemyAttention attentionSettings;

    public NPCVisionBehavior npcTargets;


  }
