using System;
using UnityEngine;

namespace FracturedStudios
{
    /// <summary>
    /// Minimal static holder for the runtime ChapterState.
    /// Other systems can read/set flags through this service.
    /// </summary>
    public static class ChapterStateService
    {
        // Publicly accessible current chapter state. Initialized to a new instance by default.
        public static ChapterState Current { get; private set; } = new ChapterState();

        // Replace the current state (useful for loading / testing)
        public static void SetCurrent(ChapterState state) => Current = state ?? new ChapterState();

        // Convenience accessor for the common 'Ability_Flash' string flag
        public static bool IsFlashAbilityUnlocked() => Current != null && Current.HasAbilityFlash();
    }
}
