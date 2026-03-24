using System;
using System.Collections.Generic;

namespace FracturedStudios
{
    /// <summary>
    /// TEMPORARY:Might not sctricly need this but is for tracking chapter progression.
    /// also might be useful for other systems. like the save system. TODO: review later.
    /// Minimal chapter/state tracker for narrative progression.
    /// Stores primary chapter + stage, lightweight flags, and optional bitmask for packing.
    /// Persistence/serialization can be added later (save slots, JSON, etc.).
    /// </summary>
    [Serializable]
    public class ChapterState
    {
        // Flag constants: put common string keys here so they're easy to find.
        public const string AbilityFlashFlag = "Ability_Flash";
        //To unlock the flash ability when the owl gives it, call:
        //ChapterStateService.Current.SetAbilityFlash(true);
        //For quick testing in the inspector/console you can toggle it with:
        //ChapterStateService.Current.ToggleAbilityFlash();
        //ChapterStateService.Current.HasAbilityFlash() returns whether the flag is set.
        //ChapterStateService.cs


        public ChapterId CurrentChapter = ChapterId.Intro;
        public int ChapterStage = 0; // per-chapter stage index (use chapter-specific enums for clarity)

        // Human-readable flags (non-packed)
        public HashSet<string> Flags = new HashSet<string>();

        // Optional packed flags for fast checks / save compactness
        public ulong FlagMask = 0;

        // Simple inventory/ability list (IDs only)
        public List<string> AbilitiesOwned = new List<string>();

        public void SetFlag(string flag)
        {
            if (string.IsNullOrWhiteSpace(flag))
                return;
            Flags.Add(flag);
        }

        /// <summary>
        /// Convenience: set or clear the built-in flash ability flag.
        /// Use this to flip the player's access to the flash ability at runtime.
        /// </summary>
        public void SetAbilityFlash(bool enabled)
        {
            // Log changes for debugging
            try
            {
                var st = new System.Diagnostics.StackTrace(1, false);
                var caller = st.FrameCount > 0 ? st.GetFrame(0)?.GetMethod() : null;
                var callerName = caller != null ? $"{caller.DeclaringType?.Name}.{caller.Name}" : "<unknown>";
                UnityEngine.Debug.Log($"[ChapterState] SetAbilityFlash({enabled}) stateId={GetHashCode()} caller={callerName}");
            }
            catch { }
            if (enabled) SetFlag(AbilityFlashFlag);
            else ClearFlag(AbilityFlashFlag);
        }

        /// <summary>
        /// Returns whether the built-in flash ability flag is present.
        /// </summary>
        public bool HasAbilityFlash() => HasFlag(AbilityFlashFlag);

        /// <summary>
        /// Toggle the built-in flash ability flag and return the new state.
        /// </summary>
        public bool ToggleAbilityFlash()
        {
            
            bool now = !HasAbilityFlash();
            SetAbilityFlash(now);
            return now;
        }

        public bool HasFlag(string flag)
        {
            if (string.IsNullOrWhiteSpace(flag))
                return false;
            return Flags.Contains(flag);
        }

        public void ClearFlag(string flag)
        {
            if (string.IsNullOrWhiteSpace(flag))
                return;
            Flags.Remove(flag);
        }

        public void SetMaskBit(ChapterFlagBits bit)
        {
            FlagMask |= (ulong)bit;
        }

        public void ClearMaskBit(ChapterFlagBits bit)
        {
            FlagMask &= ~(ulong)bit;
        }

        public bool HasMaskBit(ChapterFlagBits bit)
        {
            return (FlagMask & (ulong)bit) != 0UL;
        }

        // Cutscene helpers (string IDs so we don't churn enums while story evolves).
        public bool HasPlayedCutscene(string cutsceneId)
        {
            if (string.IsNullOrWhiteSpace(cutsceneId))
                return false;
            return Flags.Contains(cutsceneId);
        }

        public void MarkCutscenePlayed(string cutsceneId)
        {
            if (string.IsNullOrWhiteSpace(cutsceneId))
                return;
            Flags.Add(cutsceneId);
        }
    }

    /// <summary>
    /// String IDs for current intro cutscenes; extend as narrative grows.
    /// </summary>
    public static class CutsceneIds
    {
        public const string Intro_BullyHall = "Intro_BullyHall";           // School bell + bullying beat.
        public const string Intro_VentEntry = "Intro_VentEntry ";       //  the vent cutscene.
        public const string Library_LampExplode = "Library_LampExplode";   // Lamp flicker/explosion (cinematic only).
        public const string Library_BookPortal = "Library_BookPortal";     // Book read -> portal pull.
    }

    public enum ChapterId
    {
        Intro = 0,
        Chapter1 = 1,
        Chapter2 = 2,
        Chapter3 = 3,
        Chapter4 = 4,
    }

    /// <summary>
    /// Example intro-stage enum for clarity; ChapterStage uses int so stages can vary per chapter.
    /// </summary>
    public enum IntroStage
    {
        None = 0,
        BulliedInHall = 1,
        LadderFound = 2,
        LadderPlaced = 3,
        VentEntered = 4,
        LibraryEntered = 5,
        LampExploded = 6,
        BookTaken = 7,
        PortalPulled = 8,
    }

    /// <summary>
    /// Optional packed flags. Keep bits reserved for chapter-agnostic toggles or rare branches.
    /// </summary>
    [Flags]
    public enum ChapterFlagBits : ulong
    {
        None = 0,
        GuardDistracted = 1UL << 0,
        PenPickedUp = 1UL << 1,
        LampSeen = 1UL << 2,
        LampExploded = 1UL << 3,
        BookInspected = 1UL << 4,
        PortalTriggered = 1UL << 5,
    }
}
