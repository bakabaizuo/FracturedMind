using UnityEngine;
using UnityEngine.Events;

namespace FracturedStudios
{
    /// <summary>
    /// Snap point under a vent or climb start. Accepts a carried LadderItem and
    /// places it at the configured Transform.
    /// </summary>
    public class LadderPlacementZone : MonoBehaviour
    {
        [Header("Placement")]
        [SerializeField] private Transform snapPoint;
        [SerializeField] private bool requireCarried = true;
        [SerializeField] private bool disableZoneAfterUse = true;

        [Header("Events")]
        [SerializeField] private UnityEvent onPlaced;

        public bool TryPlace(LadderItem ladder)
        {
            if (ladder == null)
                return false;

            if (requireCarried && !ladder.IsCarried)
                return false;

            Transform snap = snapPoint != null ? snapPoint : transform;
            ladder.PlaceAt(snap);
            onPlaced?.Invoke();

            if (disableZoneAfterUse)
                gameObject.SetActive(false);

            return true;
        }
    }
}
