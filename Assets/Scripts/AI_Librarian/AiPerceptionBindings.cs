using UnityEngine;
using UnityEngine.AI;
using AIConfigs;

namespace FracturedMind.AI
{
    /// <summary>
    /// Local binding container for perception/navigation; distinct name to avoid clashes.
    /// Not a MonoBehaviour: create via code or the provided provider component.
    /// </summary>
    public sealed class AiPerceptionBindings
    {
        public readonly NavMeshAgent navMeshAgent;
        public readonly ThirdPersonBasic playerController;
        public readonly Transform visionOrigin;
        public readonly float visionConeRadius;

        public AiPerceptionBindings(NavMeshAgent navMeshAgent, ThirdPersonBasic playerController, Transform visionOrigin, float visionConeRadius)
        {
            this.navMeshAgent = navMeshAgent;
            this.playerController = playerController;
            this.visionOrigin = visionOrigin != null ? visionOrigin : navMeshAgent != null ? navMeshAgent.transform : null;
            this.visionConeRadius = visionConeRadius;
        }

        public static AiPerceptionBindings FromLegacy(AIAgentBindings legacy)
        {
            if (legacy == null) return null;
            return new AiPerceptionBindings(legacy.navMeshAgent, legacy.playerController as ThirdPersonBasic, legacy.visionOrigin, legacy.visionConeRadius);
        }
    }
}
