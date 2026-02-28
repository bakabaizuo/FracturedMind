using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using FracturedStudios;

/// <summary>
/// Small helper MonoBehaviour to inspect and toggle `ChapterState` flags at runtime.
/// - Shows a snapshot of current flags via `Refresh()` (Context menu)
/// - Apply `abilityFlash` to the runtime `ChapterState` with `ApplyAbilityFlash()`
/// - Add/Remove arbitrary flags using `AddFlag()`/`RemoveFlag()` context menu actions
/// </summary>
public class ChapterStateToggler : MonoBehaviour
{
    [Header("Quick ChapterState Controls")]
    [SerializeField] private ChapterState cs;

    [Tooltip("Inspector checkmark for Ability_Flash. This value is written to ChapterState in Play mode.")]
    public bool abilityFlash;

    [Tooltip("Token history of recent times the Ability_Flash value was applied (source + timestamp)")]
    public List<string> abilityFlashTokens = new List<string>();

    [Tooltip("When true, changes to `abilityFlash` in Play mode are applied automatically to ChapterState")]
    public bool autoApplyInPlayMode = true;

    [Tooltip("Snapshot of the current ChapterState.Flags (call Refresh from context menu)")]
    public string[] currentFlags = new string[0];

    [Space]
    [Tooltip("Flag string to add when calling AddFlag()")]
    public string flagToAdd;

    [Tooltip("Flag string to remove when calling RemoveFlag()")]
    public string flagToRemove;

    private bool _lastAbilityFlash;
    private bool _hasLastTarget;

    void OnEnable()
    {
        Refresh();
        _lastAbilityFlash = abilityFlash;
        _hasLastTarget = true;
    }

    void Update()
    {
        if (!Application.isPlaying || !autoApplyInPlayMode)
            return;

        if (!_hasLastTarget)
        {
            _lastAbilityFlash = abilityFlash;
            _hasLastTarget = true;
        }

        if (abilityFlash == _lastAbilityFlash)
            return;

        cs = ChapterStateService.Current;
        if (cs == null)
            return;

        bool has = cs.HasAbilityFlash();
        if (abilityFlash != has)
        {
            cs.SetAbilityFlash(abilityFlash);
            Debug.Log($"[ChapterStateToggler] Update-applied Ability_Flash = {abilityFlash}");
            AddAbilityFlashToken("Inspector", abilityFlash);
            Refresh();
        }

        _lastAbilityFlash = abilityFlash;
    }

    void OnValidate()
    {
        if (!Application.isPlaying || !autoApplyInPlayMode)
            return;

        // Avoid applying repeatedly if already tracked
        if (_hasLastTarget && abilityFlash == _lastAbilityFlash)
            return;

        cs = ChapterStateService.Current;
        if (cs == null)
            return;

        bool has = cs.HasAbilityFlash();
        if (abilityFlash != has)
        {
            ChapterStateService.Current.SetAbilityFlash(abilityFlash);
            Debug.Log($"[ChapterStateToggler] OnValidate-applied Ability_Flash = {abilityFlash}");
            AddAbilityFlashToken("InspectorOnValidate", abilityFlash);
            Refresh();
        }

        _lastAbilityFlash = abilityFlash;
        _hasLastTarget = true;
    }

    public void AutoAssignAbilityFlash()
    {
        if (!Application.isPlaying || !autoApplyInPlayMode) return;
        cs = ChapterStateService.Current;
        if (cs == null) return;

        bool has = cs.HasAbilityFlash();
        if (abilityFlash != has)
        {
        Debug.Log($"[ChapterStateToggler] Set Ability_Flash = {abilityFlash}");
        AddAbilityFlashToken("Apply", abilityFlash);
            Debug.Log($"[ChapterStateToggler] Auto-applied Ability_Flash = {abilityFlash}");
            Refresh();
        }

        _lastAbilityFlash = abilityFlash;
        _hasLastTarget = true;
    }
    [ContextMenu("Refresh ChapterState Snapshot")]
    public void Refresh()
    {
        cs = ChapterStateService.Current;
        if (cs == null)
        {
            currentFlags = new string[0];
            abilityFlash = false;
            return;
        }
        abilityFlash = cs.HasAbilityFlash();
        currentFlags = cs.Flags != null ? cs.Flags.ToArray() : new string[0];
        _lastAbilityFlash = abilityFlash;
        _hasLastTarget = true;
    }

    void AddAbilityFlashToken(string source, bool value)
    {
        try
        {
            string entry = $"{System.DateTime.Now:yyyy-MM-dd HH:mm:ss} [{source}] {(value ? "ON" : "OFF")}";
            abilityFlashTokens.Add(entry);
            if (abilityFlashTokens.Count > 200) abilityFlashTokens.RemoveAt(0);
        }
        catch { }
    }

    [ContextMenu("Clear AbilityFlash Tokens")]
    public void ClearAbilityFlashTokens()
    {
        abilityFlashTokens.Clear();
    }
    [ContextMenu("Apply AbilityFlash to ChapterState")]
    public void ApplyAbilityFlash()
    {
        cs = ChapterStateService.Current;
        if (cs == null)
        {
            Debug.LogWarning("[ChapterStateToggler] No ChapterStateService.Current available");
            return;
        }

        ChapterStateService.Current.SetAbilityFlash(abilityFlash);
        Debug.Log($"[ChapterStateToggler] Set Ability_Flash = {abilityFlash}");
        Refresh();
        _lastAbilityFlash = abilityFlash;
        _hasLastTarget = true;
    }

    [ContextMenu("Toggle AbilityFlash")]
    public void ToggleAbilityFlash()
    {
        cs = ChapterStateService.Current;
        if (cs == null)
        {
            Debug.LogWarning("[ChapterStateToggler] ToggleAbilityFlash ignored: ChapterStateService.Current is null");
            return;
        }

        bool before = cs.HasAbilityFlash();
        bool after = !before;
        cs.SetAbilityFlash(after);
        abilityFlash = after;
          AddAbilityFlashToken("Toggle", after);
        Debug.Log($"[ChapterStateToggler] ToggleAbilityFlash before={before} after={after} stateId={cs.GetHashCode()}");
        Refresh();
        _lastAbilityFlash = abilityFlash;
        _hasLastTarget = true;
            Debug.Log($"[ChapterStateToggler] Set Ability_Flash = {abilityFlash}");
      
    }

    [ContextMenu("Add Flag")]
    public void AddFlag()
    {
        if (string.IsNullOrWhiteSpace(flagToAdd)) return;
        ChapterStateService.Current?.SetFlag(flagToAdd);
        Refresh();
    }

    [ContextMenu("Remove Flag")]
    public void RemoveFlag()
    {
        if (string.IsNullOrWhiteSpace(flagToRemove)) return;
        ChapterStateService.Current?.ClearFlag(flagToRemove);
        Refresh();
    }
}
