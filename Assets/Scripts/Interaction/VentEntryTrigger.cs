using UnityEngine;
using UnityEngine.Events;

namespace FracturedStudios
{
    [DisallowMultipleComponent]
    public class VentEntryTrigger : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The placement zone under this vent. Assign in Inspector.")]
        [SerializeField] private LadderPlacementPoint placementZone;

        [Header("Events")]
        [SerializeField] private UnityEvent onVentEntered;

        [Header("Debug")]
        [Tooltip("Log to console when vent entry is triggered")]
        [SerializeField] private bool debugVentEntry = true;

        [Header("Chapter")]
        [SerializeField] private ChapterState chapterState;

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player"))
                return;

            // Force-drop if player somehow enters the vent still carrying the ladder,
            // so PlayerInteract.carriedLadder is cleared before the ladder is destroyed.
            var interact = other.GetComponentInParent<PlayerInteract>();
            interact?.ForceDropLadder();

            Enter();
        }

        /// <summary>
        /// Programmatic entry handler (also invoked by trigger).
        /// </summary>
        public void Enter()
        {
            // Prefer the directly assigned placement zone; fall back to scene search.
            LadderItem ladder = placementZone != null ? placementZone.PlacedLadder : null;

            if (ladder == null)
                ladder = FindFirstObjectByType<LadderItem>();

            ladder?.HandleVentEntered();

            if (chapterState != null)
            {
                chapterState.SetFlag(nameof(IntroStage.VentEntered));
                chapterState.ChapterStage = (int)IntroStage.VentEntered;
            }

            if (debugVentEntry)
                Debug.Log($"[VentEntryTrigger] Vent entered at {Time.time:F2}, placementZone={(placementZone!=null?placementZone.name:"none")}");

            onVentEntered?.Invoke();
        }
    }
}
