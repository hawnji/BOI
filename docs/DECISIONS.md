# Architecture decision record

Newest last. Each entry: decision, why, consequences.

### ADR-001 Engine-free Core assembly (2026-11-22)
**Decision**: all gameplay rules and data live in `Hollowcrawl.Core` with `noEngineReferences: true`.
**Why**: determinism, unit tests without the editor (CI and cloud sessions), portability to other platforms, and a hard boundary that keeps MonoBehaviours thin.
**Consequences**: Core has its own `Vec2`; Unity code converts at the boundary. World side effects go through `IGameWorld`.

### ADR-002 JSON content in StreamingAssets instead of ScriptableObjects (2026-11-22)
**Decision**: items, synergies, transformations, statuses, rooms and floors are JSON files loaded at runtime from `StreamingAssets/Content`.
**Why**: the same data loads in the game, editor tools and headless tests; diffs review cleanly; hundreds of room templates are easier to author as text; content is additive (future expansions/mods add files).
**Consequences**: art/audio are referenced by string keys (`iconKey`, `visual`, `musicKey`) resolved by catalog assets in the Runtime layer (Phase 9). Desktop platforms read StreamingAssets with `System.IO`; a future Android/WebGL port would need a `UnityWebRequest` loader behind `IPlatformServices.ContentRoot`. IL2CPP stripping is handled by `link.xml`.

### ADR-003 PCG32 with named forks for all randomness (2026-11-22)
**Decision**: one seeded PCG32 per run, forked by stable string hashes per system.
**Why**: reproducible seeds across platforms and builds; adding a roll in one system must not perturb others.
**Consequences**: never use `UnityEngine.Random` in gameplay; tests lock in determinism.

### ADR-004 Effect records + hook interfaces for item interactions (2026-11-22)
**Decision**: every item/synergy/transformation/curse/character effect is an `IEffect` instance created from data by an `EffectRegistry`; the engine discovers hook interfaces and dispatches in deterministic order.
**Why**: new items need no engine or combat changes; synergies can add, rewrite (`proj_convert_status`, delivery priority) or suppress ingredient effects; one place to enforce recursion/spawn/stacking limits and to debug.
**Consequences**: effects must be stateless with respect to other effects and use `EffectContext` for side effects.

### ADR-005 Tree-first room graph with deliberate loops (2026-11-22)
**Decision**: rooms may only touch their parent while growing; loops are added afterwards between combat rooms.
**Why**: guarantees dead ends for special rooms and a single, valid main path; loops stay controlled.
**Consequences**: special rooms are always dead ends; locked rooms never gate the boss.

### ADR-006 Code-built Input System actions (2026-11-22)
**Decision**: the InputActionAsset is created in code (`GameInputActions`) rather than a `.inputactions` asset.
**Why**: bindings are reviewable in one file and need no editor-generated asset or C# wrapper; overrides are saved as JSON in settings.
**Consequences**: the UI rebinding screen (Phase 9) uses `UnityInputRebinder`.

### ADR-007 Health model (2026-11-22)
**Decision**: health counted in half points with three original types: Vigor (refillable, in containers), Ward (temporary shield), Umbral (temporary; breaking a shard damages the room). Defense = 8% per point chance to negate a hit (max 48%).
**Why**: readable, supports "unusual health mechanics" characters (e.g. no containers), avoids flat reduction that would trivialize half-point damage.

### ADR-008 Package versions unverified (2026-11-22)
**Decision**: `Packages/manifest.json` lists plausible versions for 6000.6.9f1; the editor was not available when it was written.
**Consequences**: first open in Unity may adjust core package versions; this must be committed and noted in the checklist.
