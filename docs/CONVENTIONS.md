# Coding conventions

- **Language level**: C# 9 (Unity 6 compiler). No file-scoped namespaces, global usings, records structs or other C# 10+ features. `Tests/CoreBuild` enforces this for Core.
- **Namespaces**: `Hollowcrawl.<Area>` matching folders (`Hollowcrawl.Items`, `Hollowcrawl.ProcGen`, ...). Do not name a namespace after a common type (`Random`, `Input` in Unity code with legacy input, etc.) - the RNG namespace is `Hollowcrawl.Rng` for that reason.
- **Assemblies**: rules and data go in Core (no UnityEngine). Unity code goes in Runtime. Editor-only code goes in Editor. Never reference Runtime from Core.
- **Naming**: PascalCase types/members, `_camelCase` private fields, `camelCase` locals/parameters. Content ids are `snake_case` and never change once shipped (saves reference them).
- **Files**: one primary type per file; small related types (enums, structs) may share a file.
- **Determinism**: never use `UnityEngine.Random`, `System.Random`, `Guid` or `string.GetHashCode` for gameplay. Fork a named stream from the run's `SeededRandom`.
- **Allocation**: no LINQ, closures or boxing in per-frame paths (Update, hit dispatch, projectile pipeline). Reuse buffers; pool spawned objects.
- **Data-driven content**: new items/synergies/rooms/floors are JSON. New *mechanics* are new effect classes registered in `BuiltInEffects` (or a module); the engine is not edited for content.
- **Effect parameters**: the keys `effect`, `id` and `condition` are reserved in effect JSON. Use descriptive names (`companion`, `status`, `radius`).
- **Validation**: anything authored by hand gets a validator check and a test (`ContentValidator`, `RoomTemplateValidator`).
- **Error handling**: content and save problems are reported, never crash the game. Effects that throw are logged and isolated by the engine.
- **Comments**: XML doc comments on public types explain *why* and the contract; avoid narrating code.
- **Tests**: NUnit, in `Assets/_Project/Tests/EditMode`; they must pass both headless (`dotnet test Tests/CoreTests/...`) and in the Unity Test Runner.
- **Commits**: push directly to `main` (project owner's instruction); keep commits scoped to one milestone with a descriptive message.
