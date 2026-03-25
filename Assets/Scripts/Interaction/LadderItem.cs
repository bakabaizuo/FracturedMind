using UnityEngine;
using UnityEngine.Events;

namespace FracturedStudios
{
    /// <summary>
    /// Lightweight ladder pickup. First pass hides the ladder while carried
    /// and positions it just in front of the player. Animation/IK can be added later.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider), typeof(Rigidbody))]
    public class LadderItem : MonoBehaviour
    {
        [Header("Carry Settings")]
        [SerializeField, Min(0f)] private float carryDistance = 3f;
        [SerializeField] private float carryHeightOffset = 0.25f;
        [SerializeField] private bool hideWhileCarried = false;

        [Header("Carry Spring")]
        [SerializeField, Min(0f)] private float carrySpringStrength = 50f;
        [SerializeField, Min(0f)] private float carryMaxSpeed = 30f;
        [SerializeField, Min(0f)] private float carrySpringDamping = 5f;

        [Header("Carry Rotation")]
        [SerializeField, Min(0f)] private float carryRotationSpeed = 8f;

        [Header("Carry Collision")]
        [SerializeField, Min(0f)] private float carryCheckRadius = 0.2f;  // unused by default; kept for compatibility
        [SerializeField] private LayerMask carryObstacleMask = ~0;        // unused by default; kept for compatibility

        [Header("Pickup Glide")]
        [SerializeField, Min(0f)] private float pickupGlideSpeed = 5f;  // m/s toward carry point
        [SerializeField, Min(0f)] private float glideSnapDistance = 0.06f; // snap once within this distance

        [Header("Events")]
        [SerializeField] private UnityEvent onPickedUp;
        [SerializeField] private UnityEvent onPlaced;
        [SerializeField] private bool destroyOnVentEnter = true; // vent transition cleanup

        private Transform carrier;
        private Transform holdAnchor;
        private Rigidbody rb;
        private bool originalUseGravity;
        private bool originalIsKinematic;
        private RigidbodyInterpolation originalInterpolation;
        private CollisionDetectionMode originalCollisionDetectionMode;
        private Renderer[] renderers;
        private Collider[] colliders;
        private bool isCarried;
        private Vector3 carryVelocity;

        public bool IsCarried => isCarried;

        private void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            colliders = GetComponentsInChildren<Collider>(true);
            rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                originalUseGravity = rb.useGravity;
                originalIsKinematic = rb.isKinematic;
                originalInterpolation = rb.interpolation;
                originalCollisionDetectionMode = rb.collisionDetectionMode;
            }
        }

        private void OnValidate()
        {
            // Editor-time convenience: keep cached component lists up-to-date and ensure the object is visible
            renderers = GetComponentsInChildren<Renderer>(true);
            colliders = GetComponentsInChildren<Collider>(true);
            if (renderers != null && renderers.Length > 0)
            {
                for (int i = 0; i < renderers.Length; i++) renderers[i].enabled = true;
            }
            if (colliders != null && colliders.Length > 0)
            {
                for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = true;
            }
        }

        [ContextMenu("Print Ladder Debug Info")]
        public void PrintDebugInfo()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            colliders = GetComponentsInChildren<Collider>(true);
            Debug.Log($"LadderItem Debug: renderers={ (renderers==null?0:renderers.Length) }, colliders={ (colliders==null?0:colliders.Length) }, isCarried={isCarried}");
            if (renderers != null)
            {
                foreach (var r in renderers) Debug.Log($" Renderer: {r.gameObject.name} enabled={r.enabled}");
            }
            if (colliders != null)
            {
                foreach (var c in colliders) Debug.Log($" Collider: {c.gameObject.name} enabled={c.enabled} isTrigger={c.isTrigger}");
            }
        }

        [ContextMenu("Force Pickup (find Player)")]
        public void ForcePickupForDebug()
        {
            var player = GameObject.FindWithTag("Player");
            if (player == null)
            {
                Debug.LogWarning("ForcePickup: No GameObject found with tag 'Player' in scene.");
                return;
            }
            Pickup(player.transform);
            Debug.Log($"ForcePickup: Picked up by '{player.name}'.");
        }

        private void FixedUpdate()
        {
            if (!isCarried || carrier == null)
                return;

            Transform anchor = holdAnchor != null ? holdAnchor : carrier;
            Vector3 targetPos = anchor.position + anchor.forward * carryDistance + anchor.up * carryHeightOffset;
            Quaternion targetRot = Quaternion.LookRotation(anchor.forward, anchor.up);

            if (rb == null)
            {
                transform.position = Vector3.Lerp(transform.position, targetPos, carrySpringStrength * Time.fixedDeltaTime * 0.02f);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, carryRotationSpeed * Time.fixedDeltaTime * 0.02f);
                return;
            }

            // Pull the rigidbody toward the hold point like a fast magnet/spring.
            Vector3 toHold = targetPos - rb.position;
            Vector3 desiredVelocity = Vector3.ClampMagnitude(toHold * carrySpringStrength, carryMaxSpeed);
            rb.velocity = Vector3.MoveTowards(rb.velocity, desiredVelocity, carrySpringDamping * Time.fixedDeltaTime * 10f);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRot, carryRotationSpeed * Time.fixedDeltaTime));

            // Mild damping so it doesn't oscillate wildly.
            rb.drag = carrySpringDamping;
        }

        public void Pickup(Transform newCarrier)
        {
            if (newCarrier == null)
                return;

            carrier = newCarrier;
            holdAnchor = ResolveHoldAnchor(newCarrier);
            isCarried = true;
            carryVelocity = Vector3.zero;
            if (rb != null)
            {
                rb.useGravity = false;
                rb.isKinematic = false;
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }
            SetCollidersEnabled(false);
            SetRenderersEnabled(true);
            onPickedUp?.Invoke();
        }

        public void Drop(Vector3 position, Quaternion rotation)
        {
            carrier = null;
            holdAnchor = null;
            isCarried = false;
            carryVelocity = Vector3.zero;
            if (rb != null)
            {
                rb.useGravity = originalUseGravity;
                rb.isKinematic = originalIsKinematic;
                rb.interpolation = originalInterpolation;
                rb.collisionDetectionMode = originalCollisionDetectionMode;
            }
            transform.SetPositionAndRotation(position, rotation);
            SetCollidersEnabled(true);
            SetRenderersEnabled(true);
        }

        public void PlaceAt(Transform snapPoint)
        {
            if (snapPoint == null)
                return;

            Drop(snapPoint.position, snapPoint.rotation);
            // Ensure renderers/colliders are enabled after placement (defensive - prevents staying invisible).
            SetCollidersEnabled(true);
            SetRenderersEnabled(true);
            gameObject.SetActive(true);
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

        private Transform ResolveHoldAnchor(Transform newCarrier)
        {
            if (newCarrier == null)
                return null;

            ThirdPersonBasic controller = newCarrier.GetComponent<ThirdPersonBasic>();
            if (controller == null)
                controller = newCarrier.GetComponentInParent<ThirdPersonBasic>();

            if (controller != null && controller.InteractionAnchor != null)
                return controller.InteractionAnchor;

            return newCarrier;
        }
    }
}
