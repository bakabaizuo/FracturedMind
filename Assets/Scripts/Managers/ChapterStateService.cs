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
        public static void SetCurrent(ChapterState state)
        {
            Current = state ?? new ChapterState();
            try
            {
                var count = Current.Flags != null ? Current.Flags.Count : 0;
                Debug.Log($"[ChapterStateService] SetCurrent called. StateId={Current.GetHashCode()} FlagsCount={count}");
            }
            catch { }
        }

        // Convenience accessor for the common 'Ability_Flash' string flag
        public static bool IsFlashAbilityUnlocked()
        {
            var cs = Current;
            var has = cs != null && cs.HasAbilityFlash();
            try
            {
                var flags = cs?.Flags;
                var flagsCount = flags != null ? flags.Count : 0;
                var flagsStr = flags != null ? string.Join(",", flags) : "<null>";
                var stateId = cs != null ? cs.GetHashCode().ToString() : "null";
                UnityEngine.Debug.Log($"[ChapterStateService] IsFlashAbilityUnlocked -> {has} | Current={(cs==null?"null":"ok")} | StateId={stateId} | FlagsCount={flagsCount} | Flags=[{flagsStr}]");
            }
            catch { }
            return has;
        }
    }
}
