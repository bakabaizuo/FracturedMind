using UnityEngine;
using UnityEngine.Events;

namespace FracturedStudios
{
    [DisallowMultipleComponent]
    public class VentEntryTrigger : MonoBehaviour
    {
        [Header("Events")]
        [SerializeField] private UnityEvent onVentEntered;

        [Header("Chapter")]
        [SerializeField] private ChapterState chapterState;

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player"))
                return;

            Enter();
        }

        /// <summary>
        /// Programmatic entry handler (also invoked by trigger).
        /// </summary>
        public void Enter()
        {
            // Try to find the placed ladder first, otherwise any ladder instance
            var zone = FindObjectOfType<LadderPlacementPoint>();
            LadderItem ladder = null;
            if (zone != null && zone.PlacedLadder != null)
                ladder = zone.PlacedLadder;

            if (ladder == null)
                ladder = FindObjectOfType<LadderItem>();

            ladder?.HandleVentEntered();

            if (chapterState != null)
            {
                chapterState.SetFlag(nameof(IntroStage.VentEntered));
                chapterState.ChapterStage = (int)IntroStage.VentEntered;
            }

            onVentEntered?.Invoke();
        }
    }
}
