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
        VentEntered = 3,
        LibraryEntered = 4,
        LampExploded = 5,
        BookTaken = 6,
        PortalPulled = 7,
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
