using UnityEngine;
  [CreateAssetMenu(menuName = "NPC Configuration/Vision Behavior",fileName="NPCVisionBehavior")]
  sealed public class NPCVisionBehavior:ScriptableObject{
    public LayerMask layerMask;
    [SerializeField]
    public bool hitBackFace;
    [SerializeField]
    public  bool hitMultipleFaces;
    [SerializeField]
    public QueryTriggerInteraction howTriggers;
    // public QueryParameters query => new(layerMask, hitMultipleFaces, howTriggers, hitBackFace);
    public QueryParameters query{get; private set;}
    void OnEneable(){
      query = new(layerMask, hitMultipleFaces, howTriggers, hitBackFace) ;

    }
  }

