using UnityEngine;

namespace FracturedMind.AI
{
    /// <summary>
    /// Attach this to a light that should contribute to librarian perception.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Light))]
    public sealed class AiLightTagger : MonoBehaviour
    {
        [SerializeField] private AiLightProcessor processor;

        private Light _light;

        private void Awake()
        {
            _light = GetComponent<Light>();
        }

        private void OnEnable()
        {
            if (processor == null)
                processor = FindFirstObjectByType<AiLightProcessor>();

            if (processor != null && _light != null)
                processor.Register(_light);
        }

        private void OnDisable()
        {
            if (processor != null && _light != null)
                processor.Unregister(_light);
        }
    }
}
