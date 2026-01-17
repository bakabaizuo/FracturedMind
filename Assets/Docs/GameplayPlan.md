# FracturedStudios Gameplay Notes (Working Log)

## Scope Focus (near-term)

- Single scene for now: school → vent → library in one cell to simplify iteration.
- Ladder-to-vent beat: pick up ladder, place under vent, enter vent to exit school.
- Core movement/interaction: keep ThirdPersonBasic + StringscriptAnimatior as-is; add minimal interaction scripts only.
- Library lamp beat (cutscene): lamp flicker/explosion is cinematic only; no player-driven lamp gameplay. Use it to set up the guard distraction and portal beat.
## Current Systems
- Player locomotion: Assets/Scripts/Controllers/ThirdPersonBasic.cs (auto-adds LampVisionSensor).
- Look sensing: Assets/Scripts/Controllers/LampVisionSensor.cs (cone/raycast LOS to lamps).
- Lamp logic: Assets/Scripts/Environment/LibraryLampBehavior.cs (look counting, flicker, explosion hooks).
- Input driver: Assets/Scripts/Controllers/isCrouching.cs (crouch/dodge/sprint -> StringscriptAnimatior).
- StringscriptAnimator: Animatior FSM/Switch Assets/Scripts/Controllers/StringscriptAnimator.cs
- Ladder: Assets/Scripts/Interaction/LadderItem.cs (pickup/carry/place, invisible while carried), Assets/Scripts/Interaction/LadderPlacementZone.cs (snap under vent).

## Minimal Implementation Plan
1) Ladder beat
   - Create LadderItem (pickup) and LadderPlacementZone (under vent). When carrying, interact to place and enable climb/vent entry trigger.
   Ladder beat: "Currently In Progress"
   - No animator layer changes yet; optional bool `isCarryingLadder` later for pose swap.
   - Attach point: optional hand/hip socket; otherwise hide ladder mesh when carried (current default) and show placed prefab when dropped.
2) Vent entry
   - Simple trigger that disables ladder carry, snaps player to entry point, and loads vent crawl segment (short corridor) or teleports to library scene/area.
3) Library lamp slice (cutscene)
   - Treat lamp flicker/explode as Timeline/cutscene. If using LibraryLampBehavior, drive via animation/timeline events (not player gaze).
   - Keep guard/desk colliders for blocking shots as needed; occlusion masks optional for this cinematic.
   - If LampVisionSensor stays on player, disable/ignore during this cutscene.
4) Interaction input
   - Add a lightweight `PlayerInteract` (new) that raycasts from camera and invokes `IInteractable` on LadderItem/LadderPlacementZone/vent trigger.

## Gameplay Notes (from narrative boards)
- School hallway: bullied, finds ladder, climbs into vent to escape.
- Library: restricted section with guard at desk; player must sneak, hide behind racks/cabinets, and reach the exploded-lamp aisle.
- Lamp: flickers on look-away; after a few looks it explodes, revealing a dark/red-glow aisle the guard avoids.
- Book: red glow source; reading triggers portal pull (banishment spell) that sucks her in.

## Recommendations (keep minimal)
- Animator: avoid adding RigLayer/IK for ladder until the basic pickup/place loop works.
- Namespaces: prefer `namespace FracturedStudios` on new scripts; legacy mismatches can be cleaned later.
- Serialization: keep fields `[SerializeField]` for tuning in inspector; avoid hard-coding values.
- Layers/masks: set LampVisionSensor obstructionMask to include walls/shelves, exclude FX helpers; set ladder/interaction layers as needed.

## Open Questions
- Which camera alignment to use for vision: body forward vs. camera forward? (recommend camera forward for lamp gaze).
- Do we need a carry pose/animation for the ladder, or is a hidden mesh acceptable in first pass?
- Should vent entry fade to black or quick load?

## Notes
- Ladder currently invisible when carried and stays in front of the player; sockets/IK to be added later if needed.

## Done
- Lamp vision + lamp behavior scaffolds.
- ThirdPersonBasic auto-adds LampVisionSensor.

## Next Actions
- Implement LadderItem/LadderPlacementZone prefabs + simple PlayerInteract.
- Place vent trigger and hook transition to library.
- Wire lamp VFX/SFX for the cutscene and guard obstruction layers as needed for the shot; disable LampVisionSensor during the sequence.
