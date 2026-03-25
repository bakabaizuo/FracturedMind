using UnityEngine;
using UnityEngine.Events;
using FracturedStudios.Components;

namespace FracturedStudios
{
    [DisallowMultipleComponent]
    public class LadderPlacementPoint : MonoBehaviour
    {
        [Header("Placement")]
        [SerializeField] private Transform snapPoint;

        [Header("Rotation Snap")]
        [Tooltip("If true, placed ladder will be oriented to match the snap point with up axis compensation.")]
        [SerializeField] private bool alignOnPlace = true;

        [Tooltip("Up axis used to align ladder when 'alignOnPlace' is enabled.")]
        [SerializeField] private Vector3 placeUpAxis = Vector3.up;

        [Tooltip("Euler offset applied to the placed ladder after alignment. Use this to correct model rotation offsets (e.g., 90 degrees yaw).")]
        [SerializeField] private Vector3 placeRotationOffset = Vector3.zero;

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
        [Header("Trigger Auto Place")]
        [Tooltip("If true, the placement zone will attempt AutoPlace when player enters trigger while carrying a ladder.")]
        [SerializeField] private bool autoPlaceOnTriggerEnter = true;

        [Tooltip("Does this placement point ignore the player carrying state and place any ladder that enters trigger?")]
        [SerializeField] private bool autoPlaceOnLadderEnter = false;

        public bool TryPlace(LadderItem ladder, bool allowUncarriedPlacement = false)
        {
            if (ladder == null)
                return false;

            if (requireCarried && !allowUncarriedPlacement && !ladder.IsCarried)
                return false;

            var target = snapPoint != null ? snapPoint : transform;
            ladder.PlaceAt(target, alignOnPlace, placeUpAxis, placeRotationOffset);
            PlacedLadder = ladder;
            onLadderPlaced?.Invoke();

            if (disablePointAfterUse)
                gameObject.SetActive(false);

            return true;
        }

        private void OnTriggerEnter(Collider other)
        {
            VerboseLogger.SafeLog($"LadderPlacementPoint.OnTriggerEnter: other={other.gameObject.name}, autoPlaceOnLadderEnter={autoPlaceOnLadderEnter}, autoPlaceOnTriggerEnter={autoPlaceOnTriggerEnter}");
            if (!gameObject.activeInHierarchy)
            {
                VerboseLogger.SafeLog("LadderPlacementPoint.OnTriggerEnter: zone inactive");
                return;
            }

            // Ladder object enters trigger (via collider or attached rigidbody) - primary path.
            if (autoPlaceOnLadderEnter)
            {
                var ladder = other.GetAny<LadderItem>()
                             ?? other.attachedRigidbody?.GetAny<LadderItem>();
                VerboseLogger.SafeLog($"LadderPlacementPoint.OnTriggerEnter: candidate ladder={(ladder==null?"null":ladder.gameObject.name)}");

                if (ladder != null)
                {
                    VerboseLogger.SafeLog($"LadderPlacementPoint.OnTriggerEnter: climbing into ladder path: isCarried={ladder.IsCarried}");
                    if (ladder.IsCarried)
                    {
                        var carrierPlayer = ladder.CarrierPlayerInteract;
                        VerboseLogger.SafeLog($"LadderPlacementPoint.OnTriggerEnter: carried ladder carrierPlayer={(carrierPlayer==null?"null":carrierPlayer.gameObject.name)}");
                        if (carrierPlayer != null && carrierPlayer.IsCarryingLadder)
                        {
                            bool placed = carrierPlayer.AttemptPlaceCarried(this);
                            VerboseLogger.SafeLog($"LadderPlacementPoint.OnTriggerEnter: carrierPlayer.AttemptPlaceCarried result={placed}");
                          //  return;
                        }
                    }

                    bool placedDirect = TryPlace(ladder, true);
                    VerboseLogger.SafeLog($"LadderPlacementPoint.OnTriggerEnter: TryPlace(ladder, allowUncarriedPlacement=true) result={placedDirect}");
                    return;
                }
            }

            // Optional player carrying path (fallback). 
            if (!autoPlaceOnTriggerEnter)
                return;

            var playerInteract = other.GetComponentInParent<PlayerInteract>();
            if (playerInteract != null && playerInteract.IsCarryingLadder)
            {
                playerInteract.AttemptPlaceCarried(this);
            }
        }
    }
}
