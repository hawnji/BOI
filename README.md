# Hollowcrawl (working title)

An original top-down, room-based roguelike for Windows PC, built in **Unity 6000.6.9f1** with **URP (2D)**.
Fully offline, single-player, keyboard/mouse and controller. All names, characters, items, rooms, art and
audio are original; the game borrows only the *genre structure* of classic room-based roguelikes.

> **Status: Phase 1 (project foundation) in progress.** The project compiles in Unity 6000.6.9f1, passes its tests there and builds for Windows, but the build has not been launched or play-tested yet. See
> [`docs/DEVELOPMENT_CHECKLIST.md`](docs/DEVELOPMENT_CHECKLIST.md) for the honest status of every system and content category.

## Repository layout

```
Assets/
  _Project/
    Scripts/
      Core/        Engine-free gameplay core (no UnityEngine references): RNG & seeds, events, stats,
                   health, status effects, projectile specs, item interaction & synergy engine,
                   procedural floor generation, save system, content loading, input model.
      Runtime/     Unity layer: bootstrap, Input System bindings/rebinding, file saves, platform services.
      Editor/      Editor tools: Floor Previewer, Synergy Debugger, content validation menu.
    Tests/EditMode NUnit tests (run in Unity Test Runner *and* headless via dotnet).
    link.xml       IL2CPP preservation for JSON-deserialized types.
  StreamingAssets/Content/   All game content as JSON (items, synergies, transformations,
                             statuses, room templates, floors).
Packages/manifest.json       Unity package dependencies.
ProjectSettings/ProjectVersion.txt   Pinned editor version (6000.6.9f1). Do not upgrade silently.
Tests/                       Headless .NET harness that compiles Core with Unity's constraints (C# 9, netstandard2.1).
docs/                        Architecture, conventions, decisions, checklist, content authoring guide, bug list.
```

## Opening the project in Unity

1. Install **Unity 6000.6.9f1** with *Windows Build Support (Mono and IL2CPP)*.
2. Open this folder from Unity Hub (Add > project from disk). Unity resolves the packages in `Packages/manifest.json`.
   - If the package manager reports a version mismatch for a core package (URP, uGUI, 2D packages), accept the
     version Unity recommends for 6000.6.9f1. These version numbers could not be verified without the editor.
   - Fallback: create a fresh project from the **Universal 2D** template in Hub with 6000.6.9f1, then copy
     `Assets/`, `Packages/manifest.json` dependencies and this repo's `.gitignore` into it.
3. Menu **Hollowcrawl > Validate All Content** should report 0 errors.
4. Menu **Hollowcrawl > Floor Previewer** generates floors from any seed; **Hollowcrawl > Synergy Debugger** lets you
   toggle items and inspect the resulting shot, synergies and transformations.
5. **Window > General > Test Runner > EditMode > Run All** runs the same tests as the headless harness.

Entering Play Mode in any scene creates the persistent `[Game]` object (services, saves, input, content).
There are no gameplay scenes yet (Phase 2).

## Running the core tests without Unity

Requires the .NET 8 SDK.

```bash
dotnet test Tests/CoreTests/Hollowcrawl.Core.Tests.csproj
```

`Tests/CoreBuild` compiles `Assets/_Project/Scripts/Core` as .NET Standard 2.1 with C# 9, matching Unity's compiler limits,
so engine-free code that builds here should build in Unity.

## Building for Windows

Verified 2026-11-22: the GitHub workflow below compiled the project, passed all EditMode tests in Unity and produced `Hollowcrawl.exe`. The built game has not been launched or play-tested yet.

- In the editor: **Hollowcrawl > Build > Windows (x64)** writes `Builds/Windows/Hollowcrawl.exe`.
- From the command line:
  `Unity -batchmode -quit -projectPath . -executeMethod Hollowcrawl.EditorTools.BuildScript.BuildWindowsCI -buildOutput Builds/Windows`
  (add `-development` for a development build). It exits with code 1 if the build fails.
- On GitHub: run the **unity-windows-build** workflow by hand. It needs `UNITY_LICENSE`, `UNITY_EMAIL` and
  `UNITY_PASSWORD` repository secrets. It runs the EditMode tests inside Unity, builds, and uploads the build as an artifact.

The game needs no scene content: `GameBootstrap` starts everything, and the build script creates an empty
`Assets/_Project/Scenes/Main.unity` the first time.

## Documentation

- [Architecture](docs/ARCHITECTURE.md)
- [Coding conventions](docs/CONVENTIONS.md)
- [Content authoring (items, effects, synergies, room templates)](docs/CONTENT_AUTHORING.md)
- [Development checklist and phase status](docs/DEVELOPMENT_CHECKLIST.md)
- [Architecture decision record](docs/DECISIONS.md)
- [Bug list](docs/BUGS.md)
