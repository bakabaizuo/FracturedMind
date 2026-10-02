# Plan: `LibrarianAiStates` — flat switch-based replacement for `LibrarianController`

## Goal

Create a new class `LibrarianAiStates` at
`Assets/Scripts/AI_Librarian/LibrarianAiStates.cs` that replaces
`LibrarianController.cs`. It removes the layered decision abstraction and
replaces it with a single flat dispatch:

```
OnPerceptionUpdate(snap)
        ↓
UpdateAI(snap)
        ↓
switch (mode)
   ├── Guard(snap)
   ├── Caution(snap)
   ├── Investigate(snap)
   └── Pursue(snap)
```

Explicitly **removed**: `DecisionResult` struct, `EvaluateDecision()`,
`ResolveMode()`, `EvaluateGuardMode()`, `EvaluateCautionMode()`,
`EvaluateInvestigateMode()`, `EvaluatePursueMode()`,
`HandleInvestigateMove()`, `HandlePursue()`.

`LibrarianController.cs` is **left untouched** in this pass so the project
keeps compiling and the two can be compared side by side.

---

## Research findings (context that shapes the design)

### `LibrarianPerceptionDriver.cs`

- Owns `public enum LibrarianMode { Passive, Guard, Caution, Investigate, Pursue }`.
- Owns `public struct PerceptionSnapshot` with readonly fields:
  `AlertFlag`, `Belief`, `TargetPosition` (`Vector3?`), `PlayerIsCrouching`,
  `LightLevel`, `Darkness`, `HalfDarkness`.
- Runs in `FixedUpdate()`, so all timers in the consumer must use
  `Time.fixedDeltaTime` (matching current controller behavior).
- Pushes via `controller.OnPerceptionUpdate(new PerceptionSnapshot(...))`.
- Reads `controller._agent == null` directly for a warning log — this is why
  `_agent` must stay a **public field**, not a property.
- Field is typed `[SerializeField] LibrarianController controller;` and
  resolved with `GetComponent<LibrarianController>()`.

### `AiLightProcessor.cs`

- `SampleLightLevel()` returns `Mathf.Clamp01(total)` after smoothing, and
  hard-zeros below `aiMinimumExposureThreshold`.
- `SampleDarkness() = 1f - SampleLightLevel()` → range `0..1`.
- `SampleHalfDarkness() = Clamp01(SampleDarkness() * 0.5f)` → **range `0..0.5`**.

**Important consequence carried into the new class:** the existing
`darknessThreshold = 0.96f` is nearly unreachable and
`halfDarkness >= darknessFallbackThreshold (0.35)` is only reachable in the
top 30% of the half-darkness band. So `IsDark` in practice is driven by
`LightLevel <= lightDarkThreshold` or `Darkness >= darknessDarkThreshold`.
The port preserves the existing `IsDarkValue()` semantics exactly rather than
"fixing" the thresholds, so behavior stays comparable.

### `LibrarianAnimatorDriver.cs`

- Holds `public LibrarianController _controller;`
- Calls `_controller.RetriveLastPosition()` **every `Update()` with no null
  guard** (line 80). Any replacement must expose the same method, and the
  driver must not be left pointing at a null reference.
- Note the existing typo `RetriveLastPosition` — it is preserved for
  drop-in compatibility.

### Debug API signatures confirmed

- `VerboseLogger.SafeLog(string)`
- `DevConsoleBridge.RegisterTrackedValue(string, Func<object>)` / `UnregisterTrackedValue(string)`
- `DevConsoleBridge.RegisterActiveFlagsDetail(string, Func<string>)` / `UnregisterActiveFlagsDetail(string)`
- `DebugDevConsoleUI.Instance.SetActiveFlagBit(int, bool)`

---

## Decisions locked in

| Question | Decision |
|---|---|
| Stub depth | Port existing logic into flat switch style, plus enum fields and basic setup |
| Compat members | Keep `_agent`, `IsPlayerCrouching`, `RetriveLastPosition()`, `SetMode()`, `OnPerceptionUpdate()` |
| Enum ownership | Reuse `LibrarianPerceptionDriver.LibrarianMode`, no duplicate enum |

---

## Class shape

```csharp
using UnityEngine;
using UnityEngine.AI;
using FracturedStudios.UI;

namespace FracturedMind.AI
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class LibrarianAiStates : MonoBehaviour
    {
        // no nested DecisionResult struct
    }
}
```

### Serialized fields (carried over 1:1 so Inspector tuning transfers)

`mode`, `alertThreshold (0.5)`, `investigateAlertThreshold (0.2)`,
`repathInterval (0.25)`, `autoEscalateToPursueOnTarget (true)`,
`baseSpeed (3.5)`, `pursueSpeedMultiplier (1.5)`,
`scaleSpeedWithAlert (true)`, `respectPlayerCrouchInGuard (true)`,
`ignorePlayerCrouchForFollow (true)`, `cautionBeliefThreshold (0.33)`,
`lightDarkThreshold (0.3)`, `darknessDarkThreshold (0.9)`,
`investigateSearchDistance (4)`, `investigateNodeSwitchInterval (2.2)`,
`perception`, `darknessThreshold (0.96)`,
`darknessFallbackThreshold (0.35)`, `darknessAlertMultiplier (0.5)`,
`animator`, `alertParam ("Alert")`, `driveAnimator (true)`.

Plus `const float CautionDuration = 16f;`.

### Public compat surface

```csharp
public NavMeshAgent _agent;                 // public field, driver reads it
public bool IsPlayerCrouching { get; }      // perception != null && perception.IsPlayerCrouched()
public Vector3 RetriveLastPosition();       // transform.position - _investigateDestination
public void SetMode(LibrarianPerceptionDriver.LibrarianMode newMode);
public void OnPerceptionUpdate(LibrarianPerceptionDriver.PerceptionSnapshot snap);
```

### Per-frame state cache

`_repathTimer`, `_cautionTimer`, `_investigateDestination`,
`_hasInvestigateDestination`, `_investigateNodeIndex`, `_investigateNodeTimer`,
`_hasSeenPlayerBefore`, `_alertParamChecked`, `_alertParamExists`,
`_alertParamHash`, and the debug mirrors `_scaledAlert`, `_shouldPursue`,
`_isDark`, `_isFallbackDark`, `_playerCrouched`, `_stateReason`.

The four delta mirrors (`_lastDarknessDelta`, `_lastHalfDarknessDelta`,
`_lastPursueAlertDelta`, `_lastInvestigateAlertDelta`) are kept as plain
fields so the existing dev-console readouts keep working, but they are now
assigned inline in `UpdateAI()` instead of being packed into a struct.

---

## Control flow to implement

### `OnPerceptionUpdate(snap)` — thin entry point

Only normalizes inputs and delegates. No branching on mode here.

1. `_playerCrouched = IsPlayerCrouching || snap.PlayerIsCrouching`
2. `darkness = Clamp01(snap.Darkness)`, `halfDarkness = Clamp01(snap.HalfDarkness)`
3. Attenuate alert (identical math to current controller):
   ```csharp
   float blend = Mathf.Clamp01(Mathf.Max(1f - darkness, 1f - halfDarkness));
   _scaledAlert = snap.AlertFlag * Mathf.Lerp(darknessAlertMultiplier, 1f, blend);
   ```
4. `_isDark = IsDark(snap, darkness)`, `_isFallbackDark = halfDarkness >= darknessFallbackThreshold || snap.LightLevel <= lightDarkThreshold`
5. Cache the four deltas.
6. Latch `_hasSeenPlayerBefore` when `snap.TargetPosition.HasValue && snap.Belief < cautionBeliefThreshold` (preserves the existing—admittedly odd—condition).
7. Call `UpdateAI(snap)`.
8. Call `DriveAnimator()`.

### `UpdateAI(snap)` — mode transition + dispatch

```csharp
void UpdateAI(PerceptionSnapshot snap)
{
    mode = NextMode(snap);          // flattened successor to ResolveMode
    _shouldPursue = false;

    switch (mode)
    {
        case LibrarianMode.Passive:     Passive(snap);     break;
        case LibrarianMode.Guard:       Guard(snap);       break;
        case LibrarianMode.Caution:     Caution(snap);     break;
        case LibrarianMode.Investigate: Investigate(snap); break;
        case LibrarianMode.Pursue:      Pursue(snap);      break;
        default:                        _stateReason = "unknown-mode"; break;
    }

    UpdateActiveFlags();
}
```

`NextMode()` is kept as one compact private method containing the transition
table (Caution countdown/escalation, target-sighted → Investigate,
Guard → Caution on `investigateAlertDelta >= 0`, Investigate persistence,
Guard → Pursue auto-escalation). This is transition logic, not decision
abstraction, so it stays — but it returns a plain enum rather than a
`DecisionResult`.

### The four mode methods — each does its own acting

Critically, each method now **performs its own movement** instead of
returning a verdict for a caller to act on. This is the whole point of the
restructure.

- **`Guard(snap)`** — crouch hold check → dark bail → alert threshold check →
  otherwise `_shouldPursue = true` and repath to target on `_repathTimer`.
- **`Caution(snap)`** — resets path on entry, holds position, sets
  `_stateReason` to `caution-investigate` / `caution-guard` / `caution-stare`.
  Never pursues.
- **`Investigate(snap)`** — no-target / low-belief bail, then the search-node
  logic formerly in `HandleInvestigateMove()`: forward-offset fallback node
  when no target, otherwise alternating `nodeA`/`nodeB` flanking points around
  the anchor, each snapped via `NavMesh.SamplePosition(..., 2f, NavMesh.AllAreas)`.
- **`Pursue(snap)`** — dark bail, alert bail, then speed blend
  (`Lerp(baseSpeed, baseSpeed * pursueSpeedMultiplier, alert)` when
  `scaleSpeedWithAlert`, else flat multiply) plus `SetDestination` on the
  repath timer.

`Passive(snap)` is a trivial `_stateReason = "passive"` so the switch is total.

### Shared helpers (kept small, not an abstraction layer)

- `bool IsDark(snap, darkness)` → `snap.LightLevel <= lightDarkThreshold || darkness >= darknessDarkThreshold`
- `void RepathTo(Vector3 destination)` → timer-gated `_agent.SetDestination`
- `void DriveAnimator()` → one-time `Animator` parameter existence check, then `SetFloat`

---

## Lifecycle & debug wiring

- `Awake()`: resolve `_agent`, `perception`, `animator`; build the debug key
  prefix as `AI.{name}.{GetInstanceID()}.AiStates` (note: **`.AiStates`**, not
  `.Controller`, so both classes can be registered simultaneously without
  key collisions during A/B testing).
- `OnEnable()` / `OnDisable()`: register/unregister the tracked values and the
  active-flags detail block. Same value set as today plus `StateReason`.
- Active flag bits reuse the existing indices: `0` ShouldPursue, `1` IsDark,
  `2` FallbackDark, `3` PlayerCrouched.

---

## Files touched

| File | Action |
|---|---|
| `Assets/Scripts/AI_Librarian/LibrarianAiStates.cs` | **Create** |
| `Assets/Scripts/AI_Librarian/LibrarianController.cs` | Unchanged |
| `Assets/Scripts/AI_Librarian/LibrarianPerceptionDriver.cs` | Unchanged |
| `Assets/Scripts/AI_Librarian/LibrarianAnimatorDriver.cs` | Unchanged |

Unity will generate the `.meta` file on next editor focus.

---

## Verification

1. Let Unity recompile; confirm zero errors in the Console.
2. `LibrarianAiStates` should appear in Add Component under the AI namespace.
3. Since `LibrarianPerceptionDriver.controller` is still typed as
   `LibrarianController`, the new class is **not yet wired in**. It compiles
   and is inspectable but inert — intended for this pass.

---

## Follow-up (explicitly out of scope, listed so it is not forgotten)

To actually swap the classes later, three edits are required:

1. `LibrarianPerceptionDriver`: change the `controller` field type and the
   `GetComponent<>()` call to `LibrarianAiStates`.
2. `LibrarianAnimatorDriver`: change `_controller` type and both `??=` lookups,
   and ideally add the missing null guard before `RetriveLastPosition()`.
3. `_Librarian_v0.1.prefab`: replace the component and re-link references.

Recommend extracting a shared interface (e.g. `ILibrarianBrain`) at that point
so the perception driver does not need to know which brain it is feeding.
