using UnityEngine;
using System.Linq;
using UnityEngine.InputSystem;

namespace FracturedStudios
{
    [System.Flags]
    public enum InteractionKind : uint
    {
        None = 0u,
        Ladder = 1u << 0,
        Placement = 1u << 1,
        Default = 1u << 2,
        // Reserve high bits for future context flags
    }

    [DisallowMultipleComponent]
    public class PlayerInteract : MonoBehaviour
    {
        [Header("Filtering")]
        [SerializeField] private LayerMask ignoreHitLayers = 0;
        [SerializeField] private string[] ignoreHitTags = new string[0];
        [SerializeField] private string ladderTag = "Ladder";
        [Header("Debug")]
        [SerializeField] private bool debugPickup = true;
        [SerializeField] private bool debugGizmos = true;

        // runtime debug data for gizmos
        private RaycastHit[] _lastHits;
        private Vector3 _lastRayOrigin;
        private Vector3 _lastRayDirection;
        private Vector3 _lastChosenPoint;
        private bool _hasLastHits = false;
        [SerializeField] private float interactRange = 12f;
        [SerializeField] private LayerMask interactMask = ~0;

        // Optional gameplay guard: only allow ladder interactions while chapter stage <= this value
        [Header("Chapter Guard (optional)")]
        [SerializeField] private ChapterState chapterState;
        [SerializeField] private int maxAllowedChapterStage = (int)IntroStage.VentEntered; // default: allow until vent entered

        private LadderItem carriedLadder;
        private PlayerControlls controls;

        // Exposed for other systems (e.g. VentEntryTrigger needs to know if ladder is carried)
        public LadderItem CarriedLadder => carriedLadder;
        public bool IsCarryingLadder => carriedLadder != null;

        // Context mask kept for future/external orchestration; not used to gate core flow.
        private InteractionKind currentInteractionMask = InteractionKind.Default;
        public void AddInteractionContext(InteractionKind kind) => currentInteractionMask |= kind;
        public void RemoveInteractionContext(InteractionKind kind) => currentInteractionMask &= ~kind;
        public void SetInteractionContext(InteractionKind kind) => currentInteractionMask = kind;

        void Awake()
        {
            controls = new PlayerControlls();
            controls.Player.Interact.started += ctx => OnInteractPressed();
            // By default ignore hits on the player's layer so the player's own colliders
            // don't block camera-origin raycasts.
            ignoreHitLayers |= (1 << gameObject.layer);

            // Also include layers used by any child colliders so all player colliders are ignored
            var childColliders = GetComponentsInChildren<Collider>(true);
            if (childColliders != null)
            {
                foreach (var c in childColliders)
                {
                    if (c == null) continue;
                    ignoreHitLayers |= (1 << c.gameObject.layer);
                }
            }
            // If any child has the Player tag, automatically ignore that tag as well
            if (gameObject.CompareTag("Player"))
            {
                var tags = new System.Collections.Generic.List<string>(ignoreHitTags ?? new string[0]);
                if (!tags.Contains("Player")) tags.Add("Player");
                ignoreHitTags = tags.ToArray();
            }
        }

        void OnEnable()  { controls?.Enable(); }
        void OnDisable() { controls?.Disable(); }

        // ─── Core flow ───────────────────────────────────────────────────────────

        private void OnInteractPressed()
        {
            if (debugPickup) Debug.Log($"PlayerInteract: OnInteractPressed called. carrying={carriedLadder!=null}, interactRange={interactRange}");
            if (chapterState != null && chapterState.ChapterStage > maxAllowedChapterStage)
                return;

            if (carriedLadder != null)
            {
                // Carrying: try to place at a snap point; otherwise drop at feet.
                if (!TryPlaceCarried())
                    DropCarried();
                return;
            }

            // Not carrying: try ladder pickup first, then general IInteractable.
            if (TryPickupLadder())
                return;

            DefaultInteraction();
        }

        // ─── Pickup ──────────────────────────────────────────────────────────────
        /// <summary>Raycast for a LadderItem in the world and pick it up.</summary>
        private bool TryPickupLadder()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                if (debugPickup) Debug.Log("PlayerInteract: Camera.main is null.");
                return false;
            }

            // 1) Try single raycast (fast path)
            if (Physics.Raycast(cam.transform.position, cam.transform.forward, out RaycastHit singleHit, interactRange, interactMask, QueryTriggerInteraction.Ignore))
            {
                _lastRayOrigin = cam.transform.position;
                _lastRayDirection = cam.transform.forward;
                _lastHits = new[] { singleHit };
                _hasLastHits = true;

                if (TryGetLadderFromHit(singleHit, out LadderItem ladderFromSingle))
                {
                    ladderFromSingle.Pickup(transform);
                    carriedLadder = ladderFromSingle;
                    _lastChosenPoint = singleHit.point;
                    SetChapterFlag(nameof(IntroStage.LadderFound), IntroStage.LadderFound);
                    if (debugPickup) Debug.Log($"PlayerInteract: Picked up ladder '{ladderFromSingle.gameObject.name}' via single Raycast.");
                    return true;
                }
            }

            // 2) RaycastAll: scan hits (keeps existing behavior but using out-helpers and filtering)
            Ray ray = new Ray(cam.transform.position, cam.transform.forward);
            _lastRayOrigin = ray.origin;
            _lastRayDirection = ray.direction;
            RaycastHit[] hits = Physics.RaycastAll(ray, interactRange, interactMask, QueryTriggerInteraction.Ignore);
            _lastHits = hits;
            _hasLastHits = hits != null && hits.Length > 0;

            if (hits != null && hits.Length > 0)
            {
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                float forwardDotThreshold = 0.5f; // ~60 degrees
                LadderItem bestInFront = null;
                float bestInFrontDist = float.MaxValue;
                LadderItem bestClosest = null;
                float bestClosestDist = float.MaxValue;

                Vector3 playerForward = transform.forward;
                Vector3 playerPos = transform.position;

                foreach (var h in hits)
                {
                    if (debugPickup) Debug.Log($"PlayerInteract: RaycastAll hit '{h.collider.gameObject.name}' (layer {h.collider.gameObject.layer}) at dist {h.distance}.");

                    // Skip configured layers/tags
                    if ((ignoreHitLayers.value & (1 << h.collider.gameObject.layer)) != 0)
                    {
                        if (debugPickup) Debug.Log($"PlayerInteract: Ignoring hit on ignored layer '{LayerMask.LayerToName(h.collider.gameObject.layer)}' for '{h.collider.gameObject.name}'.");
                        continue;
                    }
                    if (ignoreHitTags != null && ignoreHitTags.Length > 0 && ignoreHitTags.Any(t => !string.IsNullOrEmpty(t) && h.collider.gameObject.CompareTag(t)))
                    {
                        if (debugPickup) Debug.Log($"PlayerInteract: Ignoring hit on ignored tag for '{h.collider.gameObject.name}'.");
                        continue;
                    }

                    // Skip hits that belong to the player (robust child-of-player check)
                    if (h.collider != null && (h.collider.transform == transform || h.collider.transform.IsChildOf(transform)))
                    {
                        if (debugPickup) Debug.Log($"PlayerInteract: Ignoring hit on player collider '{h.collider.gameObject.name}'.");
                        continue;
                    }

                    if (!TryGetLadderFromHit(h, out LadderItem ladder))
                    {
                        if (debugPickup) Debug.Log("PlayerInteract: This hit has no LadderItem in parent.");
                        continue;
                    }
                    if (ladder.IsCarried)
                    {
                        if (debugPickup) Debug.Log("PlayerInteract: Ladder is already carried, continuing search.");
                        continue;
                    }

                    float dist = h.distance;
                    Vector3 toHit = (h.point - playerPos).normalized;
                    float dot = Vector3.Dot(toHit, playerForward);
                    if (dot >= forwardDotThreshold)
                    {
                        if (dist < bestInFrontDist)
                        {
                            bestInFrontDist = dist;
                            bestInFront = ladder;
                        }
                    }

                    if (dist < bestClosestDist)
                    {
                        bestClosestDist = dist;
                        bestClosest = ladder;
                        if (bestClosest == ladder)
                            _lastChosenPoint = h.point;
                    }
                }

                LadderItem chosen = bestInFront != null ? bestInFront : bestClosest;
                if (chosen != null)
                {
                    chosen.Pickup(transform);
                    carriedLadder = chosen;
                    SetChapterFlag(nameof(IntroStage.LadderFound), IntroStage.LadderFound);
                    if (debugPickup) Debug.Log($"PlayerInteract: Picked up ladder '{chosen.gameObject.name}' (inFrontPreferred={(bestInFront!=null)}).");
                    return true;
                }
            }

            // 3) Try heuristic: overlap sphere near the ray endpoint to find ladder objects when ray misses
            if (debugPickup) Debug.Log("PlayerInteract: Ray methods found nothing; running OverlapSphere heuristic.");
            Vector3 sphereCenter = cam.transform.position + cam.transform.forward * Mathf.Min(interactRange, 2f);
            float sphereRadius = 1.2f;
            Collider[] nearby = Physics.OverlapSphere(sphereCenter, sphereRadius, interactMask, QueryTriggerInteraction.Ignore);
            LadderItem bestOverlap = null;
            float bestOverlapDist = float.MaxValue;
            foreach (var c in nearby)
            {
                if (c == null) continue;
                // skip player-owned colliders
                if (c.transform.IsChildOf(transform)) continue;
                if ((ignoreHitLayers.value & (1 << c.gameObject.layer)) != 0) continue;
                if (ignoreHitTags != null && ignoreHitTags.Length > 0 && ignoreHitTags.Any(t => !string.IsNullOrEmpty(t) && c.gameObject.CompareTag(t))) continue;
                if (!TryGetLadderFromCollider(c, out LadderItem ladder)) continue;
                if (ladder.IsCarried) continue;
                float d = Vector3.Distance(cam.transform.position, ladder.transform.position);
                // prefer ones roughly in front of the player
                float dot = Vector3.Dot((ladder.transform.position - transform.position).normalized, transform.forward);
                if (dot < 0f) continue;
                if (d < bestOverlapDist)
                {
                    bestOverlapDist = d;
                    bestOverlap = ladder;
                }
            }
            if (bestOverlap != null)
            {
                bestOverlap.Pickup(transform);
                carriedLadder = bestOverlap;
                _lastChosenPoint = bestOverlap.transform.position;
                SetChapterFlag(nameof(IntroStage.LadderFound), IntroStage.LadderFound);
                if (debugPickup) Debug.Log($"PlayerInteract: Picked up ladder '{bestOverlap.gameObject.name}' via OverlapSphere heuristic.");
                return true;
            }

            // 4) Dot heuristic over all ladders in the scene. This bypasses ray alignment entirely.
            if (TryPickupLadderByDot(out LadderItem dotChosen))
            {
                dotChosen.Pickup(transform);
                carriedLadder = dotChosen;
                _lastChosenPoint = dotChosen.transform.position;
                SetChapterFlag(nameof(IntroStage.LadderFound), IntroStage.LadderFound);
                if (debugPickup) Debug.Log($"PlayerInteract: Picked up ladder '{dotChosen.gameObject.name}' via dot heuristic.");
                return true;
            }

            if (debugPickup) Debug.Log("PlayerInteract: No suitable LadderItem found after all heuristics.");
            return false;
        }

        private bool TryGetLadderFromHit(RaycastHit hit, out LadderItem ladder)
        {
            ladder = null;
            if (hit.collider == null) return false;
            return TryGetLadderFromCollider(hit.collider, out ladder);
        }

        private bool TryGetLadderFromCollider(Collider c, out LadderItem ladder)
        {
            ladder = c.GetComponentInParent<LadderItem>();
            if (ladder != null)
                return true;

            if (!string.IsNullOrEmpty(ladderTag) && c.CompareTag(ladderTag))
            {
                ladder = c.GetComponentInParent<LadderItem>();
                return ladder != null;
            }

            return false;
        }

        private bool TryPickupLadderByDot(out LadderItem ladder)
        {
            ladder = null;

            var ladders = FindObjectsOfType<LadderItem>(true);
            if (ladders == null || ladders.Length == 0)
                return false;

            Vector3 origin = transform.position;
            Vector3 forward = transform.forward;
            float bestDot = float.NegativeInfinity;
            float bestDistance = float.MaxValue;

            foreach (var candidate in ladders)
            {
                if (candidate == null || candidate.IsCarried)
                    continue;

                if (!string.IsNullOrEmpty(ladderTag) && !candidate.CompareTag(ladderTag) && !candidate.gameObject.CompareTag(ladderTag))
                    continue;

                Vector3 toCandidate = candidate.transform.position - origin;
                float distance = toCandidate.magnitude;
                if (distance > interactRange)
                    continue;

                Vector3 dir = toCandidate.normalized;
                float dot = Vector3.Dot(forward, dir);

                // Require the ladder to be at least somewhat in front of the player.
                if (dot < 0.15f)
                    continue;

                if (dot > bestDot || (Mathf.Approximately(dot, bestDot) && distance < bestDistance))
                {
                    bestDot = dot;
                    bestDistance = distance;
                    ladder = candidate;
                }
            }

            if (ladder == null)
                return false;

            if (debugPickup)
                Debug.Log($"PlayerInteract: Dot heuristic selected '{ladder.gameObject.name}' dot={bestDot:0.000} dist={bestDistance:0.000}.");

            return true;
        }

        private void OnDrawGizmosSelected()
        {
            if (!debugGizmos) return;
            // draw the last ray and hit points
            if (_hasLastHits)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(_lastRayOrigin, _lastRayOrigin + _lastRayDirection * interactRange);

                Gizmos.color = Color.red;
                foreach (var h in _lastHits)
                {
                    Gizmos.DrawSphere(h.point, 0.05f);
                    Gizmos.DrawLine(transform.position, h.point);
                }

                Gizmos.color = Color.green;
                Gizmos.DrawSphere(_lastChosenPoint, 0.08f);
                Gizmos.DrawLine(transform.position, _lastChosenPoint);
            }
        }

        // ─── Place ───────────────────────────────────────────────────────────────

        /// <summary>Raycast for a LadderPlacementPoint and place the carried ladder. Returns true on success.</summary>
        private bool TryPlaceCarried()
        {
            if (!Raycast(out RaycastHit hit)) return false;
            var point = hit.collider.GetComponentInParent<LadderPlacementPoint>();
            if (point == null) return false;
            if (!point.TryPlace(carriedLadder)) return false;
            carriedLadder = null;
            SetChapterFlag("LadderPlaced", IntroStage.LadderPlaced);
            return true;
        }

        // ─── Drop ────────────────────────────────────────────────────────────────

        /// <summary>Drop the carried ladder at the player's feet.</summary>
        private void DropCarried()
        {
            if (carriedLadder == null) return;
            Vector3 dropPos = transform.position + transform.forward * 1f + Vector3.up * 0.05f;
            carriedLadder.Drop(dropPos, Quaternion.LookRotation(transform.forward, Vector3.up));
            carriedLadder = null;
        }

        /// <summary>External call: force-drop the ladder (e.g. from VentEntryTrigger).</summary>
        public void ForceDropLadder() => DropCarried();

        // ─── General interact ────────────────────────────────────────────────────

        /// <summary>General IInteractable path (pen distraction, doors, etc.).</summary>
        private void DefaultInteraction()
        {
            if (!Raycast(out RaycastHit hit)) return;
            var interactable = hit.collider.GetComponentInParent<IInteractable>();
            if (interactable != null)
                interactable.Interact(transform);
        }

        // ─── Helpers ─────────────────────────────────────────────────────────────

        private bool Raycast(out RaycastHit hit)
        {
            var cam = Camera.main;
            if (cam == null) { hit = default; return false; }
            return Physics.Raycast(cam.transform.position, cam.transform.forward, out hit, interactRange, interactMask);
        }

        private void SetChapterFlag(string flag, IntroStage stage)
        {
            if (chapterState == null) return;
            chapterState.SetFlag(flag);
            chapterState.ChapterStage = (int)stage;
        }
    }
}
