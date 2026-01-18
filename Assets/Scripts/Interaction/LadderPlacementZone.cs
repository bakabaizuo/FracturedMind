using UnityEngine;
using UnityEngine.Events;

namespace FracturedStudios
{
    [DisallowMultipleComponent]
    public class LadderPlacementPoint : MonoBehaviour
    {
        [Header("Placement")]
        [SerializeField] private Transform snapPoint;

        [Tooltip("If true, placement requires the ladder to be carried by the player.")]
        [SerializeField] private bool requireCarried = true;

        [Tooltip("If true, the placement point will disable itself after a successful placement.")]
        [SerializeField] private bool disablePointAfterUse = true;

        [SerializeField] private UnityEngine.Events.UnityEvent onLadderPlaced;

        public LadderItem PlacedLadder { get; private set; }
        public bool IsOccupied => PlacedLadder != null;

        /// <summary>
        /// Try to place a ladder into this zone. Returns true on success.
        /// If <see cref="requireCarried"/> is set, the ladder must be currently carried.
        /// </summary>
        public bool TryPlace(LadderItem ladder)
        {
            if (ladder == null)
                return false;

            if (requireCarried && !ladder.IsCarried)
                return false;

            var target = snapPoint != null ? snapPoint : transform;
            ladder.PlaceAt(target);
            PlacedLadder = ladder;
            onLadderPlaced?.Invoke();

            if (disablePointAfterUse)
                gameObject.SetActive(false);

            return true;
        }
    }
}
