# Bug list

| ID | Status | Area | Description | Found | Fixed |
|---|---|---|---|---|---|
| B-001 | Fixed | Content/Items | Effect parameter named `id` was swallowed by the reserved effect `id` key, so `companion` effects never spawned. Parameter renamed to `companion`; reserved keys documented. Caught by `UniqueItemsRefuseDuplicatesAndStackableItemsStack`. | 2026-11-22 | 2026-11-22 |

## Known limitations (not bugs)

- Unity-layer code (Runtime/Editor) has only been type-checked against API stubs, not compiled by Unity. First open in the editor may surface API mismatches.
- `Packages/manifest.json` versions unverified (ADR-008).
