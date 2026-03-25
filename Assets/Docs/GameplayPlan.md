# FracturedStudios Gameplay Notes (Working Log)
_Last audited: March 23, 2026_

---

## Implementation Status

| System | Script(s) | Status |
|---|---|---|
| Player locomotion | Controllers/ThirdPersonBasic.cs | ✅ Done |
| Animator FSM | Controllers/StringscriptAnimator.cs | ✅ Done |
| Input driver (crouch/sprint/dodge) | Controllers/isCrouching.cs | ✅ Done |
| Sprint+crouch lock | ThirdPersonBasic + StringscriptAnimator | ✅ Done |
| Slide / anti-drift | StringscriptAnimator (moveInputEpsilon) | ✅ Done |
| Look sensing | Controllers/LampVisionSensor.cs | ✅ Done |
| Lamp logic | Environment/LibraryLampBehavior.cs | ✅ Done |
| PlayerData / moveSpeed | DataContainers/PlayerData.cs | ✅ Done |
| Event/data bus | EventInvokers/WorldBridgeSystem.cs | ✅ Done |
| Interaction interface | Interaction/IInteractable.cs | ✅ Exists |
| Ladder pickup + carry collision + glide | Interaction/LadderItem.cs | ✅ Done |
| Ladder placement zone | Interaction/LadderPlacementZone.cs | ✅ Exists |
| Player interact (pickup/drop/re-pickup) | Interaction/PlayerInteract.cs | ✅ Done |
| Vent entry trigger + debug | Interaction/VentEntryTrigger.cs | ✅ Done |
| AI Librarian guard | AI_Librarian/ (LibrarianController + PerceptionDriver + NpuVm) | ✅ Exists |
| Chapter state data | DataContainers/ChapterState.cs | ✅ Exists |
| Chapter progression service | Managers/ChapterStateService.cs | ✅ Exists |
| Cutscene ID registry | ChapterState.cs → CutsceneIds (static class) | ✅ Exists |
| Chapter/flag bitmask | ChapterState.cs → ChapterFlagBits enum | ✅ Exists |
| Chapter state dev tool | DevTools/ChapterStateToggler.cs | ✅ Exists |
| Orb / Flash ability rig | RigLayers/OrbHandRigLayer.cs | ✅ Exists |
| Ladder/vent in-scene wired | Scene prefabs / triggers connected | 🟡 Needs scene place |
| Vent → library transition | VentEntryTrigger onVentEntered hookup | 🟡 Needs scene place |
| Library lamp cutscene | Timeline / LibraryLampBehavior wiring | 🟡 Needs Timeline setup |
| Guard obstruction layers | LibrarianPerceptionDriver layermask | 🟡 Needs tuning |

| LampVisionSensor critique in cutscene | ❌ Not wired yet | Note: Defered till trigger ready. Cutscene and Animation Dependent. Not Gameplay dependent. Decide what do to with it after cutscene is set up. or Defer to Cutcene Controller semantics |

---

## Scope Focus (near-term)

- Single scene for now: school → vent → library in one cell to simplify iteration.
- Ladder-to-vent beat: pick up ladder, place under vent, enter vent to exit school.
- Core movement/interaction: keep ThirdPersonBasic + StringscriptAnimatior as-is; add minimal interaction scripts only.
- Library lamp beat (cutscene): lamp flicker/explosion is cinematic only; no player-driven lamp gameplay. Use it to set up the guard distraction and portal beat.

---

## Current Systems (Full)

### Movement & Input
- `Assets/Scripts/Controllers/ThirdPersonBasic.cs` — locomotion, sprint, crouch lock, jump, gravity; moveSpeed sourced from PlayerData via WorldBridgeSystem.
- `Assets/Scripts/Controllers/StringscriptAnimator.cs` — animation FSM; owns crouch/dodge/sprint state; slide epsilon guard.
- `Assets/Scripts/Controllers/isCrouching.cs` — Input System router → StringscriptAnimatior; ability flash hook.
- `Assets/Scripts/Controllers/SprintTapController.cs` — tap-sprint boost (ability-style multiplier).
- `Assets/Scripts/Controllers/TorsoAimController.cs` — upper-body aim override.
- `Assets/Scripts/Controllers/LookTargetLerper.cs` — smooth look-target utility.

### Sensing
- `Assets/Scripts/Controllers/LampVisionSensor.cs` — cone + raycast LOS to lamps; auto-added by ThirdPersonBasic. 


### Environment
- `Assets/Scripts/Environment/LibraryLampBehavior.cs` — look counting, flicker, explosion hooks.

### Interaction
- `Assets/Scripts/Interaction/IInteractable.cs` — interface.
- `Assets/Scripts/Interaction/LadderItem.cs` — pickup/carry/place; `[RequireComponent(Collider)]`; carry collision via `SphereCast` along forward axis (pulls back if geometry blocks); pickup glide via `MoveTowards` + `Slerp`; invisible while carried.
- `Assets/Scripts/Interaction/LadderPlacementZone.cs` (`LadderPlacementPoint`) — snaps carried ladder to a `SnapPoint` Transform; exposes `PlacedLadder`; disables itself after use.
- `Assets/Scripts/Interaction/PlayerInteract.cs` — carry-state-driven E-key flow: carrying → `TryPlaceCarried()` or `DropCarried()`; not carrying → `TryPickupLadder()` or `DefaultInteraction()`; `ForceDropLadder()` for external callers; `InteractionKind` context mask kept for future use.
- `Assets/Scripts/Interaction/VentEntryTrigger.cs` — `OnTriggerEnter` (Player tag): calls `ForceDropLadder()`, destroys placed ladder via `HandleVentEntered()`, sets `IntroStage.VentEntered` flag, fires `onVentEntered` UnityEvent; `debugVentEntry` toggle logs entry timestamp + zone name.

### Data / Events
- `Assets/Scripts/DataContainers/PlayerData.cs` — HP, XP, level, attributes, moveSpeed property.
- `Assets/Scripts/DataContainers/DataContainerBase.cs` — base class; death state, damage/heal.
- `Assets/Scripts/DataContainers/NPCData.cs` — NPC stats container.
- `Assets/Scripts/DataContainers/ChapterState.cs` — chapter unlock flags, stage index, `HashSet<string>` flags, `ulong` bitmask, abilities list, cutscene-played tracking. Contains: `ChapterId` enum, `IntroStage` enum, `ChapterFlagBits` bitmask enum, `CutsceneIds` static string registry (`Intro_BullyHall`, `Intro_VentEntry`, `Library_LampExplode`, `Library_BookPortal`).
- `Assets/Scripts/EventInvokers/WorldBridgeSystem.cs` — singleton data bus; holds `PlayerData data` ref.
- `Assets/Scripts/EventInvokers/DynamicDictionaryInvoker.cs` — dynamic key/method event dispatch.
- `Assets/Scripts/EventInvokers/EventData.cs` — typed event wrappers.
- `Assets/Scripts/EventInvokers/PlayerCaseController.cs` — player-specific case/event routing.
- `Assets/Scripts/Managers/ChapterStateService.cs` — static singleton-style service; `ChapterStateService.Current` returns the live `ChapterState`; `SetCurrent()` replaces it (load/test); `IsFlashAbilityUnlocked()` convenience accessor. Call `ChapterStateService.Current.SetAbilityFlash(true)` to grant the flash ability.

### AI (Librarian Guard)
- `Assets/Scripts/AI_Librarian/LibrarianController.cs` — guard movement/actuation.
- `Assets/Scripts/AI_Librarian/LibrarianPerceptionDriver.cs` — overlap sphere scan, NPU vision register.
- `Assets/Scripts/AI_Librarian/NpuVm.cs` — lightweight neural-program-unit VM for guard belief.
- `Assets/Scripts/AI_Librarian/NpuProgramBuilder.cs` — builds sample detection programs.
- `Assets/Scripts/AI_Librarian/NpuAgentRunner.cs` — drives NPU tick loop.
- `Assets/Scripts/AI_Librarian/AiPerceptionBindings.cs / Provider` — binding helpers.
- `Assets/Scripts/AI_Librarian/_Librarian_v0.1.prefab` — guard prefab.

### Chapter State & Progression
- `Assets/Scripts/DataContainers/ChapterState.cs` — data container (plain C# class, `[Serializable]`):  
  - `CurrentChapter` (`ChapterId` enum), `ChapterStage` int  
  - `Flags` `HashSet<string>` — human-readable named flags; `SetFlag / ClearFlag / HasFlag`  
  - `FlagMask` `ulong` — packed bitmask; `SetMaskBit / ClearMaskBit / HasMaskBit` via `ChapterFlagBits` enum  
  - `AbilitiesOwned` `List<string>` — ability ID registry  
  - Cutscene helpers: `HasPlayedCutscene(string) / MarkCutscenePlayed(string)`  
  - Flash ability shortcuts: `SetAbilityFlash(bool) / HasAbilityFlash() / ToggleAbilityFlash()`  
  - Constant: `AbilityFlashFlag = "Ability_Flash"`
- `Assets/Scripts/DataContainers/ChapterState.cs` → `CutsceneIds` (static class) — canonical cutscene ID strings:  
  `Intro_BullyHall`, `Intro_VentEntry`, `Library_LampExplode`, `Library_BookPortal`
- `Assets/Scripts/DataContainers/ChapterState.cs` → `ChapterId` enum — `Intro / Chapter1-4`
- `Assets/Scripts/DataContainers/ChapterState.cs` → `IntroStage` enum — per-chapter stage clarity helper
- `Assets/Scripts/Managers/ChapterStateService.cs` — static handler / service interface:  
  - `ChapterStateService.Current` — live state instance (auto-inits to `new ChapterState()`)  
  - `SetCurrent(ChapterState)` — replace state (load from save / test override)  
  - `IsFlashAbilityUnlocked()` — reads `Current.HasAbilityFlash()`  
  - **Usage**: `ChapterStateService.Current.SetAbilityFlash(true);` to grant flash
- `Assets/Scripts/DevTools/ChapterStateToggler.cs` — inspector/runtime dev tool:  
  - Inspector checkbox `abilityFlash` auto-applies to `ChapterState` in Play mode  
  - Context-menu `Refresh()` — snapshots `Current.Flags` to inspector array  
  - `autoApplyInPlayMode` toggle; token history of each flag change with timestamp + source

### Abilities / Rig
- `Assets/Scripts/AbilityCaster.cs` — cast ability by flag.
- `Assets/Scripts/RigLayers/OrbHandRigLayer.cs` — orb/flash hand rig layer, flash trigger.
- `Assets/Scripts/FlashBang.cs` — flash effect.

---

## Minimal Implementation Plan

1) **Ladder beat** — `LadderItem` + `LadderPlacementPoint` scripts complete. Status: Done.
   - Carry collision, pickup glide, RequireComponent(Collider) all done in code.
   - Scene: place `LadderItem` prefab between storage racks; place `LadderPlacementPoint` under vent with a `SnapPoint` child Transform.
 uses Tag "Player" for trigger detection; `carryObstacleMask` should exclude Player + Ladder layers.
2) **PlayerInteract** — carry-state-driven pickup/drop/re-pickup complete. Status: Done.
   - Add to player GameObject; Input Action `Player/Interact` must be bound (E key).
   Note: LadderItem Snaps to Position and lerps in a while loop and disables its gravity RB.
   Works.
   - Assign `ChapterState` ref if chapter gating is needed.

3) **Vent entry** — `VentEntryTrigger.cs` complete. Status: Defer to Cutscene `onVentEntered` wired to scene transition. - Wire `onVentEntered` → fade/load/teleport to library. <-- needed. 
   
   
4) **Library lamp slice (cutscene)** — `LibraryLampBehavior.cs` exists. Status: ❌ Timeline drive not yet set up. Defer until Library is Built in scene. WIP. 3/23/2026.
   - Drive flicker/explode via Timeline, not player gaze.
   - Disable `LampVisionSensor` during sequence.


   Next steps: Librarian setup and Scene setup. Lamp is Cutscene and Animation dependent, not Gameplay dependent. Defer until cutscene is set up, then decide how to handle the LampVisionSensor critique (disable during cutscene or leave as-is).

---

## Narrative & Story Structure

### INTRO — Summary
Xiona, a nerdy goth girl, is bullied daily at school. Fed up, she decides to fight back not with fists but psychologically. She heads to the library to research tactics. While browsing near the restricted section, she notices a lamp flickering in the distance — glances at it, looks away, it flickers again; on her third look-away it explodes. Curiosity takes over. At the restricted entrance she finds a librarian sitting guard. She distracts her and slips inside. The final corridor is pitch-dark except for a growing red glow emanating from a book: _Ancient Forbidden Witchcraft_. She finds the chapter "How to ban species from your world", thinks of her bullies, and reads the spell aloud. A portal tears open and swallows her whole.

---

### INTRO — Detailed Script

#### Cutscene — `Intro_BullyHall` ~
- School bell rings. _"Time to go to class!"_
- Xiona closes her locker — bullies waiting right beside her, the last students in the hallway.
- Bullies taunt: _"hahahahaaha ofc, you don't have anything in life except for your parents and your silly best friend who's worthless! Hahahaahaha!"_
- Xiona smiles back: _"well, I'm way smarter than all you combined, and laugh all you want, I will reach my goal in my life. What will you guys ever become? Hmmmm… maybe jail would be the best future for you guys!"_
- A bully shoves her to the ground. Another empties a soda can on her. They drag her to the nearby storage room and lock her inside.
- Xiona cries sitting against the wall. Looking up, she spots a **vent hatch** in the ceiling.

#### Gameplay — School Storage Room
- Find the small ladder hidden between the storage racks.
- Place ladder directly beneath the vent hatch (`LadderItem` → `LadderPlacementZone`).
- Enter the ventilation system (`VentEntryTrigger`). <- animation dependent.
- Xiona's internal monologue while crawling: _"I've had it with these bullies, just you wait! I'm going to the library and I'm going to give them a good psychological kicking!"_ <- UI wireing
- Navigate vents to exit through the school's main hallway side (exit is nearby).
- `ChapterId.Intro` / `IntroStage` advances here.

#### Transition — Loading Screen / Brief Black Screen
> _"?LOADING SCREEN or just a sec black screen?"_ — decision pending.

#### Cutscene — Library Arrival ~
- Xiona arrives at the library.
- She searches the science section for psychology books, near the biology shelves — adjacent to the restricted area entrance (librarian visible at desk on the other side).
- She catches the flickering lamp out of the corner of her eye in the distance. Glances at it; thinks nothing of it; looks away to continue searching.
- The moment she looks away — **the lamp explodes** (`Library_LampExplode`).
- _"well, that's a goner…"_ — notices the explosion came from inside the restricted area.
- Observes: WARNING sign at the entrance; librarian seated at desk beside it.
- _"I really want to have a look inside, maybe I should distract her."_
- She spots an **old pen** on a nearby bookshelf. (Interact → distraction item.)

#### Gameplay — Library Restricted Area (Stealth)
- **Goal**: enter the restricted area as fast as possible while sneaking.
- Once past the entrance at a certain depth, Xiona whispers: _"I think the librarian is coming…"_
- Sneak toward the dark end of the restricted section while the librarian patrols.
- **Hide**: duck behind racks and museum-style cabinets to break line-of-sight (`LibrarianPerceptionDriver`).
- **Detection → Game Over**: librarian chases Xiona on sight; caught = game over; restart from restricted area entry.
- **Safe zone**: dark aisle around the exploded lamp — the librarian **will not** enter the red-glow area.
- As Xiona approaches, darkness deepens, red glow intensifies corridor by corridor.
- At the far end: a book radiating the red glow. **Interact with book** → triggers cutscene.

#### Cutscene — `Library_BookPortal` ~
- Camera focuses on the book in Xiona's hands. Red glow fades away.
- Camera cuts to full-body view: she holds **_"Ancient Forbidden Witchcraft"_**.
- She opens it — confirms the title.
- Swipes to table of contents. Reads: _"How to ban species from your world."_
- She smiles: _"maybe I should try this on those bullies, but yet again, this is all just mythological. Meh, what could possibly go wrong?"_
- She turns straight to that page, eyes wide: _"Look at this! So I need to keep the bullies in mind to perform…"_
- Can't hold her laugh — reads aloud:  
  _"You who are regarded as mortals but who abuse it, we shall ban you from this realm. **Iranomora Restinda Xeridious!**"_
- A portal rips open. Gravitational pull so strong she's sucked in before she can respond.
- `ChapterState.MarkCutscenePlayed(CutsceneIds.Library_BookPortal)`

---

### Chapter 1: A Forsaken Abyss

#### Cutscene — Waking Up ~
- Xiona wakes. A **luminous owl** hovers above her. She panics and scrambles backwards.
- Owl: _"I thought you were dead — I'm glad to have been wrong."_
- She asks why he can talk and why he glows. He explains he is one of the ancient creatures.
- _"Ancient creatures? Where am I?"_ — She looks at her hands: **long black nails, black fur emitting black smoke**. Feels her face — full panic.
- Owl: _"Take a deep breath."_ Two deep breaths. She calms slightly.
- _"You're out of this world, is that right?"_ — her monstrous form is a standard reaction to someone who has crossed worlds.
- Much has happened in the last few centuries; he is very happy to see a living person — most others are just **wanderers**.
- She walks to a puddle and looks at her reflection — clearly disappointed she's not in her real form/shape.
- Owl: _"Don't be sad, I'll help you find the way back to your home world."_

#### Backstory — Overno & the 7 Ancient Creatures
- Owl introduces himself: **Overno** the ancient. _"But you can call me Overno."_
- Xiona: _"My name is Xiona."_
- Overno: there used to be **7 ancient creatures** that could grant powers to those bound to them. He doesn't know if any of the others are still alive.
- This world was once full of life — now **completely extinct**. Dark creatures devour everything in their path.
- _"But who knows, we might find more information about what happened through our adventure."_

#### Gameplay — Flash Ability Tutorial
- Overno grants Xiona the **flash ability**: burst of light from her hand, radius ~5–10 m.  
  _"This will certainly help you to find your way."_
- Tutorial objective: use the flash to **activate a nearby floating stone**.
- `ChapterStateService.Current.SetAbilityFlash(true)` ← flash unlocked here.
- Stone illuminated → clears a section of the area near a forest with swamp and thick fog.

#### Gameplay — First Encounter (Dark Creature)
- Xiona walks through the woods with Overno at her side.
- A dark creature lunges from the bushes. **Time slows.**
- Player must choose: **Run** or **Hide**.
  - **Hide** (creature is close): complete a button-press sequence to successfully go undetected.
  - **Run**: must get far enough to shake the creature — or retreat back to the illuminated stone safe zone.

---

---

## Recommendations (keep minimal)
- Animator: avoid adding RigLayer/IK for ladder until the basic pickup/place loop works.
- Namespaces: prefer `namespace FracturedStudios` on new scripts; legacy mismatches can be cleaned later.
- Serialization: keep fields `[SerializeField]` for tuning in inspector; avoid hard-coding values.
- Layers/masks: set LampVisionSensor obstructionMask to include walls/shelves, exclude FX helpers; set ladder/interaction layers as needed.

---

## Open Questions
- Which camera alignment to use for vision: body forward vs. camera forward? (recommend camera forward for lamp gaze).
- Do we need a carry pose/animation for the ladder, or is a hidden mesh acceptable in first pass?
- Should vent entry fade to black or quick load? (`onVentEntered` slot ready for either).
- ~~Does WorldBridgeSystem.Instance.data get assigned at runtime?~~ **Resolved** — `PlayerData.Awake()` assigns `WorldBridgeSystem.Instance.data = this`.
- What layers should `carryObstacleMask` exclude? (Player, Ladder, FX/triggers at minimum).

---

## Notes
- Ladder currently invisible when carried and stays in front of the player; sockets/IK to be added later if needed.
- ThirdPersonBasic moveSpeed now reads from PlayerData via WorldBridgeSystem; fallback to `_fallbackMoveSpeed` if data is null.
- MoveSpeed zeroed on zero-input to prevent physics sliding.
- Sprint blocked while crouched in both ThirdPersonBasic and StringscriptAnimator.

---

## Done
- ✅ Lamp vision + lamp behavior scaffolds.
- ✅ ThirdPersonBasic auto-adds LampVisionSensor.
- ✅ moveSpeed sourced from PlayerData.
- ✅ Sprint/crouch state lock (no run while crouched).
- ✅ Slide / idle epsilon fix in StringscriptAnimator.
- ✅ WorldBridgeSystem.Instance.data wired — PlayerData.Awake() self-assigns.
- ✅ LadderItem: RequireComponent(Collider), carry collision (SphereCast), pickup glide (MoveTowards + Slerp).
- ✅ PlayerInteract: carry-state-driven flow (pickup / place / drop / re-pickup), ForceDropLadder() API.
- ✅ VentEntryTrigger: direct placementZone ref, ForceDropLadder on entry, debug logging, FindFirstObjectByType.
- ✅ AI Librarian guard stack: PerceptionDriver + NpuVm + Controller.
- ✅ ChapterState + unlock service.

---

## Next Actions
1. **Scene — Storage Room**: place `LadderItem` prefab between racks; place `LadderPlacementPoint` under vent with `SnapPoint` child; add `PlayerInteract` to player; set `carryObstacleMask` (exclude Player + Ladder layers); test full pickup → carry → place → enter loop.
2. **Scene — Vent Trigger**: add `BoxCollider (isTrigger)` at vent mouth; assign `placementZone`; wire `onVentEntered` → fade/load/teleport; verify `[VentEntryTrigger]` debug log fires.
3. **Library lamp cutscene**: set up Timeline for flicker → explode; disable `LampVisionSensor` on cutscene start event.
4. **AI guard**: tune `LibrarianPerceptionDriver` layermask for library shelves; block guard from entering red-glow aisle.
5. **Wire lamp VFX/SFX**: guard avoidance of dark aisle post-explosion; set up red-glow intensity gradient as player approaches.
