using UnityEngine;
using UnityEngine.Events;

namespace FracturedStudios
{
    /// <summary>
    /// Lightweight ladder pickup. First pass hides the ladder while carried
    /// and positions it just in front of the player. Animation/IK can be added later.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public class LadderItem : MonoBehaviour
    {
        [Header("Carry Settings")]
        [SerializeField, Min(0f)] private float carryDistance = 0.9f;
        [SerializeField] private float carryHeightOffset = 0.1f;
        [SerializeField] private bool hideWhileCarried = true;

        [Header("Carry Collision")]
        [SerializeField, Min(0f)] private float carryCheckRadius = 0.2f;  // sphere radius for geometry check along carry axis
        [SerializeField] private LayerMask carryObstacleMask = ~0;        // layers that block the carry position

        [Header("Pickup Glide")]
        [SerializeField, Min(0f)] private float pickupGlideSpeed = 5f;  // m/s toward carry point
        [SerializeField, Min(0f)] private float glideSnapDistance = 0.06f; // snap once within this distance

        [Header("Events")]
        [SerializeField] private UnityEvent onPickedUp;
        [SerializeField] private UnityEvent onPlaced;
        [SerializeField] private bool destroyOnVentEnter = true; // vent transition cleanup

        private Transform carrier;
        private Renderer[] renderers;
        private Collider[] colliders;
        private bool isCarried;
        private bool isGliding;

        public bool IsCarried => isCarried;

        private void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            colliders = GetComponentsInChildren<Collider>(true);
        }

        /// <summary>
        /// Resolves the carry position along the player→carryPoint axis.
        /// If geometry blocks the ideal carry distance, the position is pulled back
        /// toward the player to the hit surface. Returns the safe carry position.
        /// </summary>
        private Vector3 ResolveCarryPosition(Vector3 origin, Vector3 carryDir, float desiredDistance, float heightOffset)
        {
            // Cast from just behind the ideal origin outward along the carry axis.
            Vector3 castOrigin = origin + carrier.up * heightOffset;
            if (Physics.SphereCast(castOrigin, carryCheckRadius, carryDir, out RaycastHit hit,
                                   desiredDistance, carryObstacleMask, QueryTriggerInteraction.Ignore))
            {
                // Pull back to hit surface minus the check radius so we don't clip
                float safeDistance = Mathf.Max(0f, hit.distance - carryCheckRadius);
                return castOrigin + carryDir * safeDistance;
            }
            return castOrigin + carryDir * desiredDistance;
        }

        private void LateUpdate()
        {
            if (!isCarried || carrier == null)
                return;

            Vector3 targetPos = ResolveCarryPosition(carrier.position, carrier.forward, carryDistance, carryHeightOffset);
            Quaternion targetRot = Quaternion.LookRotation(carrier.forward, Vector3.up);

            if (isGliding)
            {
                // Pull along the axis toward the carry point, slerp rotation in parallel.
                Vector3 axis = targetPos - transform.position;
                float dist = axis.magnitude;
                if (dist <= glideSnapDistance)
                {
                    isGliding = false;
                    transform.SetPositionAndRotation(targetPos, targetRot);
                }
                else
                {
                    transform.position = Vector3.MoveTowards(transform.position, targetPos, pickupGlideSpeed * Time.deltaTime);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, pickupGlideSpeed * Time.deltaTime / dist);
                }
            }
            else
            {
                transform.SetPositionAndRotation(targetPos, targetRot);
            }
        }

        public void Pickup(Transform newCarrier)
        {
            if (newCarrier == null)
                return;

            carrier = newCarrier;
            isCarried = true;
            isGliding = pickupGlideSpeed > 0f; // start glide only if speed is set
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

        /// <summary>
        /// Call when the player enters the vent and this ladder is no longer needed.
        /// A separate trigger should invoke this; destruction is optional via flag.
        /// </summary>
        public void HandleVentEntered()
        {
            if (!destroyOnVentEnter)
                return;

            Destroy(gameObject);
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
