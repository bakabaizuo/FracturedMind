using UnityEngine;
using UnityEngine.Events;

namespace FracturedStudios
{
    /// <summary>
    /// Lightweight ladder pickup. First pass hides the ladder while carried
    /// and positions it just in front of the player. Animation/IK can be added later.
    /// </summary>
    [DisallowMultipleComponent]
    public class LadderItem : MonoBehaviour
    {
        [Header("Carry Settings")]
        [SerializeField, Min(0f)] private float carryDistance = 0.9f;
        [SerializeField] private float carryHeightOffset = 0.1f;
        [SerializeField] private bool hideWhileCarried = true;

        [Header("Events")]
        [SerializeField] private UnityEvent onPickedUp;
        [SerializeField] private UnityEvent onPlaced;

        private Transform carrier;
        private Renderer[] renderers;
        private Collider[] colliders;
        private bool isCarried;

        public bool IsCarried => isCarried;

        private void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            colliders = GetComponentsInChildren<Collider>(true);
        }

        private void LateUpdate()
        {
            if (!isCarried || carrier == null)
                return;

            Vector3 targetPos = carrier.position + carrier.forward * carryDistance + carrier.up * carryHeightOffset;
            Quaternion targetRot = Quaternion.LookRotation(carrier.forward, Vector3.up);
            transform.SetPositionAndRotation(targetPos, targetRot);
        }

        public void Pickup(Transform newCarrier)
        {
            if (newCarrier == null)
                return;

            carrier = newCarrier;
            isCarried = true;
            SetCollidersEnabled(false);
            SetRenderersEnabled(!hideWhileCarried);
            onPickedUp?.Invoke();
        }

        public void Drop(Vector3 position, Quaternion rotation)
        {
            carrier = null;
            isCarried = false;
            transform.SetPositionAndRotation(position, rotation);
            SetCollidersEnabled(true);
            SetRenderersEnabled(true);
        }

        public void PlaceAt(Transform snapPoint)
        {
            if (snapPoint == null)
                return;

            Drop(snapPoint.position, snapPoint.rotation);
            onPlaced?.Invoke();
        }

        private void SetRenderersEnabled(bool enabled)
        {
            if (renderers == null) return;
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].enabled = enabled;
        }

        private void SetCollidersEnabled(bool enabled)
        {
            if (colliders == null) return;
            for (int i = 0; i < colliders.Length; i++)
                colliders[i].enabled = enabled;
        }
    }
}
