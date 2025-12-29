# ResourceLoader — Future Integration Ideas

This document collects short, actionable ideas for extending and integrating `FracturedStudios.TAB.ResourceLoader` and `PrefabValidator` into the TAB subsystem.

**Goals**
- Provide a robust, testable runtime API for loading assets (RadialItem, sprites, prefabs).
- Support editor-time validation and runtime fallbacks to avoid null refs.
- Prepare for Addressables or asset-bundle migration.

**API Enhancements**
- Fi/Pi/FType enhancements:
  - **Fi**: Add optional generic overloads that accept fallback paths and a cache TTL.
  - **Pi**: Add `bool tryLoad` variants that return a success flag with out param.
  - **FType**: Cache resolved types (ConcurrentDictionary) and expose a diagnostic method.

- Async support:
  - Add `FiAsync<T>` / `PiAsync<T>` wrappers using `ResourceRequest`/`Addressables`.
  - Provide cancellation tokens and progress callbacks for long loads.

- Sprite/Atlas helpers:
  - Fallback search order: SpriteAtlas -> Resources -> AssetDatabase (editor only).
  - Batch preload API: `PreloadSprites(SpriteAtlas atlas, IEnumerable<string> names)`.

**Prefab & Validation**
- PrefabValidator improvements:
  - Return structured validation results (missing components, unassigned serialized fields).
  - Add an editor menu command to validate all Entry prefabs in a project folder.
  - Add `FindPrefabWithBinding` (already implemented) tests and a small CLI to scan folders.

- Runtime safety:
  - Provide `EnsureEntryPrefab(GameObject prefab)` which auto-adds lightweight fallback components if missing (editor-only modifications guarded by `#if UNITY_EDITOR`).

**Integration Points**
- `ItemMenu.Start()`:
  - Call `PrefabValidator.ValidateEntryPrefab(EntryPrefab)` and early-return with clear log if invalid.
  - Use `ResourceLoader.LoadSpriteFromAtlas` as primary sprite resolver; if null, call `ResourceLoader.Fi<Sprite>(name)` as fallback.

- `ItemPanel`:
  - Add `Populate(RadialItem item, SpriteAtlas atlas)` convenience method to centralize label+sprite logic and fallback sequence.

**Addressables & Build Strategy**
- Abstract the loader behind an interface `IResourceLoader` with implementations for `Resources` and `Addressables`.
- Provide build-time switch (compiler define or DI registration) to use Addressables without changing high-level code.

**Caching & Memory**
- Add a simple in-memory LRU cache for frequently used sprites and scriptable objects.
- Expose a `ClearCache()` method and integrate with scene unload events.

**Editor Tooling & Tests**
- Unit tests for `FType`, caching, and `FindPrefabWithBinding` (use EditMode tests in Unity Test Runner).
- EditorWindow to browse RadialItem registry and preview EntryPrefab visual layout.

**Diagnostics & Logging**
- Add a debug level (None/Errors/Verbose) for `ResourceLoader` and `PrefabValidator` to reduce log noise.
- Add `ResourceLoader.DumpLoadedResources()` for debugging memory/runtime issues.

**Example usage snippets**
- Synchronous Sprite load with fallback:

```csharp
var sprite = ResourceLoader.LoadSpriteFromAtlas(item.SpriteName, atlas)
             ?? ResourceLoader.Fi<Sprite>($"Sprites/{item.SpriteName}");
panel.SetSprite(sprite);
```

- Async load example (future):

```csharp
var sprite = await ResourceLoader.FiAsync<Sprite>("Sprites/hero_idle", ct);
panel.SetSprite(sprite);
```

**Next steps (implementation roadmap)**
1. Add `FiAsync`/`PiAsync` prototypes and tests.
2. Implement `IResourceLoader` abstraction and a simple DI registration strategy.
3. Add `ItemPanel.Populate(...)` convenience method and switch `ItemMenu` to use it.
4. Create Editor validation command and unit tests for `FindPrefabWithBinding`.

---
Document created on 2025-12-29. Replace or extend this file as ideas solidify.
