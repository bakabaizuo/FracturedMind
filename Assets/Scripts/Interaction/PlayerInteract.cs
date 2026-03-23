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
        }

        void OnEnable()  { controls?.Enable(); }
        void OnDisable() { controls?.Disable(); }

        // ─── Core flow ───────────────────────────────────────────────────────────

        private void OnInteractPressed()
        {
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
            if (!Raycast(out RaycastHit hit)) return false;
            var ladder = hit.collider.GetComponentInParent<LadderItem>();
            if (ladder == null || ladder.IsCarried) return false;
            ladder.Pickup(transform);
            carriedLadder = ladder;
            SetChapterFlag(nameof(IntroStage.LadderFound), IntroStage.LadderFound);
            return true;
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
