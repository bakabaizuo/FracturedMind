using UnityEngine;
using UnityEngine.InputSystem;

namespace FracturedStudios
{
    [System.Flags]
    public enum InteractionKind : ulong
    {
        None = 0UL,
        Ladder = 1UL << 0,
        Placement = 1UL << 1,
        Default = 1UL << 2,
        // Reserve high bits for future context flags
    }

    [DisallowMultipleComponent]
    public class PlayerInteract : MonoBehaviour
    {
        [SerializeField] private float interactRange = 3f;
        [SerializeField] private LayerMask interactMask = ~0;

        // Optional gameplay guard: only allow ladder interactions while chapter stage <= this value
        [Header("Chapter Guard (optional)")]
        [SerializeField] private ChapterState chapterState;
        [SerializeField] private int maxAllowedChapterStage = (int)IntroStage.VentEntered; // default: allow until vent entered

        private LadderItem carriedLadder;
        private PlayerControlls controls;

        // Current interaction context mask. Can be updated by triggers, chapter orchestration, or position checks.
        private InteractionKind currentInteractionMask = InteractionKind.Default;

        public void AddInteractionContext(InteractionKind kind) => currentInteractionMask |= kind;
        public void RemoveInteractionContext(InteractionKind kind) => currentInteractionMask &= ~kind;
        public void SetInteractionContext(InteractionKind kind) => currentInteractionMask = kind;

        void Awake()
        {
            controls = new PlayerControlls();
            controls.Player.Interact.started += ctx => OnInteractPressed();
        }

        void OnEnable()
        {
            controls?.Enable();
        }

        void OnDisable()
        {
            controls?.Disable();
        }

        private void OnInteractPressed()
        {
            // Top-level entry on button press. Use the currentInteractionMask to decide behavior.
            if (chapterState != null && chapterState.ChapterStage > maxAllowedChapterStage)
                return; // guarded by chapter

            // Try ladder-oriented interactions first if flagged
            if ((currentInteractionMask & InteractionKind.Ladder) == InteractionKind.Ladder)
            {
                if (TryPerformLadderInteraction())
                    return;
            }

            if ((currentInteractionMask & InteractionKind.Placement) == InteractionKind.Placement)
            {
                if (TryPerformPlacementInteraction())
                    return;
            }

            if ((currentInteractionMask & InteractionKind.Default) == InteractionKind.Default)
            {
                var resultTag = DefaultInteraction();
                // resultTag can be used by orchestration systems; for now log it
                if (!string.IsNullOrEmpty(resultTag))
                    Debug.Log($"[Interact] Default result tag: {resultTag}");
            }
        }

        private void HandleLadderPickup(LadderItem ladder)
        {
            ladder.Pickup(transform);
            carriedLadder = ladder;
            if (chapterState != null)
            {
                chapterState.SetFlag(nameof(IntroStage.LadderFound));
                chapterState.ChapterStage = (int)IntroStage.LadderFound;
            }
        }

        private void HandleLadderPlacement(LadderPlacementPoint point)
        {
            if (carriedLadder == null)
            {
                var placed = point.PlacedLadder;
                if (placed != null && !placed.IsCarried)
                {
                    placed.Pickup(transform);
                    carriedLadder = placed;
                    if (chapterState != null)
                    {
                        chapterState.SetFlag(nameof(IntroStage.LadderFound));
                        chapterState.ChapterStage = (int)IntroStage.LadderFound;
                    }
                }
            }
            else
            {
                if (point.TryPlace(carriedLadder))
                {
                    carriedLadder = null;
                    if (chapterState != null)
                    {
                        chapterState.SetFlag("LadderPlaced");
                        chapterState.ChapterStage = (int)IntroStage.LadderFound;
                    }
                }
            }
        }

        private bool TryPerformLadderInteraction()
        {
            var cam = Camera.main;
            if (cam == null) return false;
            Ray ray = new Ray(cam.transform.position, cam.transform.forward);
            if (!Physics.Raycast(ray, out RaycastHit hit, interactRange, interactMask))
                return false;

            var ladder = hit.collider.GetComponentInParent<LadderItem>();
            if (ladder != null && !ladder.IsCarried)
            {
                HandleLadderPickup(ladder);
                return true;
            }

            return false;
        }

        private bool TryPerformPlacementInteraction()
        {
            var cam = Camera.main;
            if (cam == null) return false;
            Ray ray = new Ray(cam.transform.position, cam.transform.forward);
            if (!Physics.Raycast(ray, out RaycastHit hit, interactRange, interactMask))
                return false;

            var placement = hit.collider.GetComponentInParent<LadderPlacementPoint>();
            if (placement != null)
            {
                HandleLadderPlacement(placement);
                return true;
            }

            return false;
        }

        /// <summary>
        /// DefaultInteraction: general-purpose interaction path.
        /// Returns the 'tag' of the hit object (or empty) so orchestration systems can react.
        /// </summary>
        private string DefaultInteraction()
        {
            var cam = Camera.main;
            if (cam == null) return string.Empty;
            Ray ray = new Ray(cam.transform.position, cam.transform.forward);
            if (!Physics.Raycast(ray, out RaycastHit hit, interactRange, interactMask))
            {
                // fallback: if carrying ladder, drop
                if (carriedLadder != null)
                {
                    Vector3 dropPos = transform.position + transform.forward * 1f + Vector3.up * 0.1f;
                    carriedLadder.Drop(dropPos, Quaternion.LookRotation(transform.forward, Vector3.up));
                    carriedLadder = null;
                    return "LadderDropped";
                }
                return string.Empty;
            }

            var interactable = hit.collider.GetComponentInParent<IInteractable>();
            if (interactable != null)
            {
                if (interactable.Interact(transform))
                    return hit.collider.gameObject.tag ?? string.Empty;
            }

            // Default: return the tag of what we hit so external systems can route behavior
            return hit.collider != null ? hit.collider.gameObject.tag ?? string.Empty : string.Empty;
        }
    }
}
