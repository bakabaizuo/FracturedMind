using UnityEngine;

namespace FracturedStudios
{
    /// <summary>
    /// Simple interface for world objects that the player can interact with via `PlayerInteract`.
    /// Implementations should return true if the interaction was handled.
    /// </summary>
    public interface IInteractable
    {
        bool Interact(Transform player);
    }
}
