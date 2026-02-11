# DebugDevConsoleUI — Enhancement Roadmap

**Purpose:**
- Outline how to expand `DebugDevConsoleUI` to collect and display flags, `ChapterState`, and other debug info across secondary panels while keeping the main console command-focused.
- Provide a trackable, actionable checklist and example wiring (including note about `ChapterState.SetFlag`).

**Goals**
- Keep main console focused (commands & logs).
- Add modular panels for: Flags / ChapterState, Performance / Stats, Input/Bindings, and Scene/Object inspectors.
- Provide a simple API to register tracked values (type + ToString()) and backplane/panel switching.

**Key Concepts**
- Main Console: command entry + output history (minimal change).
- Panels: UI containers (GameObjects) shown/hidden by index or parent; switch via keys (`<`/`,` and `>`/`.`) or `panel` command.
- Backplane images: array of `Image` components switched by index to change visual context.
- Tracked values: runtime-registered name + getter (`Func<object>`) displayed in a panel when enabled.

**ChapterState integration**
- `ChapterState` contains human-readable flags (`HashSet<string> Flags`) and packed `FlagMask`.
- Important: `ChapterState.SetFlag(string flag)` is used to add a named flag. When systems call `SetFlag(...)` they mutate chapter-level flags the console can display.
- Strategy: have the game systems continue using `ChapterState.SetFlag`, and register a tracked value in the console that reads the current `ChapterState.Flags` (or a formatted view) to show live flag state.

Example: register a tracked viewer for chapter flags
```csharp
// somewhere during initialization (e.g., GameManager or where ChapterState is created)
DebugDevConsoleUI.Instance?.RegisterTrackedValue("ChapterFlags", () => {
    var cs = myChapterStateReference; // your ChapterState instance
    if (cs == null) return "<no ChapterState>";
    return string.Join(", ", cs.Flags);
});
```

Or to register the packed mask
```csharp
DebugDevConsoleUI.Instance?.RegisterTrackedValue("FlagMask", () => myChapterStateReference.FlagMask);
```

**Recommended console commands (examples)**
- `flags` — toggle the flags panel (already implemented in current code).
- `backplane <index>` — switch background/backplane image.
- `panel list` — list available debug panels.
- `panel <index>` — switch to panel index.
- `track <name>` — register a simple tracked value (see advanced below).
- `untrack <name>` — unregister.
- `tracked` — list tracked items.

**Tracked-value registration patterns**
- Short-lived ephemeral values: register once with a getter that reads a static/service value.
- Long-lived observers: register once and ensure getter is resilient (try/catch inside) to avoid exceptions breaking display.
- Avoid heavy computation inside getters; cache where appropriate.

**Panel design guidelines**
- Panels should be independent prefabs (or child GameObjects). The console exposes `debugPanels` array or `debugPanelsParent` to auto-discover them.
- Each panel should own its own refresh/update logic, or provide a `RegisterTrackedValue` hookup to the console.
- Limit UI updates per-frame; prefer per-second updates for expensive displays.

**Safety & performance**
- Wrap tracked getters in try/catch and return safe fallback strings if they throw.
- Limit number/length of strings in UI.
- When showing many flags, consider a scrollable list rather than a single text block.

**Checklist (trackable)**
- [ ] Create documentation (this file) — `Assets/Docs/DebugDevConsoleUI_Enhancements.md` (done)
- [ ] Add `KeyCode` fields to expose/change panel keys in inspector
- [ ] Add console command `tracked` to list registered tracked values
- [ ] Add `track` / `untrack` console commands (simple CLI helpers)
- [ ] Auto-register `ACTIVEFLAGS` and `ChapterState` viewers at runtime (optional)
- [ ] Add example wiring in a bootstrap location (GameManager) showing `RegisterTrackedValue` usage
- [ ] Add safe-refresh interval for tracked-values (configurable, default ~0.25s)
- [ ] Add visual design: Panel prefabs + optional backplane images
- [ ] Add unit/integration test plan (manual QA checklist)

**Actionable implementation notes**
1. Tracked-values refresh frequency
   - Add a configurable `trackedRefreshInterval` (float seconds) and update only when elapsed.
2. Registering ChapterState
   - Pass a `ChapterState` reference to the bootstrap and `RegisterTrackedValue("ChapterFlags", ()=> string.Join(",", cs.Flags))`.
   - For single-flag changes, consider `RegisterTrackedValue("Has_Foo", ()=> cs.HasFlag("Foo"))`.
3. Console commands API
   - The `DevConsoleBridge` helper can expose `RegisterCommand` so game systems can add custom commands.
4. Panel discovery
   - If `debugPanels` is empty, build from `debugPanelsParent` or children of the console to avoid manual wiring.
5. Backplane images
   - Use `SetBackplaneIndex(int)` and ensure all backplanes are inactive when not selected.

**Example: auto-register `ACTIVEFLAGS` and ChapterState viewer**
```csharp
void Start()
{
    // example: auto-register ACTIVEFLAGS
    DebugDevConsoleUI.Instance?.RegisterTrackedValue("ACTIVEFLAGS", () => DebugDevConsoleUI.Instance?.ACTIVEFLAGS ?? 0);

    // example: register ChapterState if accessible
    var cs = FindObjectOfType<GameManager>()?.ChapterState; // adapt to your project
    DebugDevConsoleUI.Instance?.RegisterTrackedValue("ChapterFlags", () => cs == null ? "<none>" : string.Join(",", cs.Flags));
}
```

**Manual QA checklist**
- [ ] `panel list` enumerates correct panels
- [ ] `panel <index>` selects panel and deactivates others
- [ ] `backplane <index>` toggles backplane images safely
- [ ] `flags` toggles tracked flag display
- [ ] Registered tracked values display correct ToString() and type name
- [ ] Trackers don't throw or block frame (slow getters flagged)

**Next steps (suggested priority)**
1. Add `tracked` / `track` / `untrack` commands to `DebugDevConsoleUI` (low complexity, high value).
2. Add `trackedRefreshInterval` and switch to interval-based updates.
3. Add example wiring in `GameManager` or bootstrap code to register `ACTIVEFLAGS` and `ChapterState` viewers.
4. Provide prefabs for panels and backplane images; document expected hierarchy.

**References / code locations**
- Console implementation: `Assets/Scripts/DevTools/DebugDevConsoleUI.cs`
- Bridge/helper: `Assets/Scripts/DevTools/DevConsoleBridge.cs`
- ChapterState: `Assets/Scripts/DataContainers/ChapterState.cs` (see `SetFlag(string)` method)

---

If you want I can: 
- Implement `track`/`untrack` commands and `tracked` listing now, or
- Add `trackedRefreshInterval` and change `UpdateTrackedValuesDisplay()` to use interval-based updates.

Pick one and I will make the code changes and update the checklist accordingly.