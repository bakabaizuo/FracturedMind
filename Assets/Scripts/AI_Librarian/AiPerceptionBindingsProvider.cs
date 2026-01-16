using UnityEngine;
using UnityEngine.AI;

namespace FracturedMind.AI
{
    /// <summary>
    /// MonoBehaviour bridge to author AiPerceptionBindings in the Inspector.
    /// </summary>
    public sealed class AiPerceptionBindingsProvider : MonoBehaviour
    {
        [Header("Scene refs")]
        [SerializeField] NavMeshAgent navMeshAgent;
        [SerializeField] ThirdPersonBasic playerController;
        [SerializeField] Transform visionOrigin;
        [SerializeField] float visionConeRadius = 1.5f;

        AiPerceptionBindings _bindings;

        void Awake()
        {
            if (navMeshAgent == null) navMeshAgent = GetComponent<NavMeshAgent>();
            if (visionOrigin == null && navMeshAgent != null) visionOrigin = navMeshAgent.transform;
            if (playerController == null) playerController = ThirdPersonBasic.Instance;

            _bindings = new AiPerceptionBindings(navMeshAgent, playerController, visionOrigin, visionConeRadius);
        }

        public AiPerceptionBindings Get() => _bindings;
    }
}
