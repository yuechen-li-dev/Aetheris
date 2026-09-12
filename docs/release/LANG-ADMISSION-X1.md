# LANG-ADMISSION-X1 — unified Firmament admission

## Verdict

**Meaningful progression.** B03 is fixed: canonical V2 symbol binding now applies the same namespace doctrine to the profile/Compose route used by the Boss-stack witness. Build, validate, and inspect agree that the original source is admitted; lowering remains a subsequent command concern.

The wider "one admission result before every domain route" objective remains blocked by a concrete boundary: established profile-edge-finish, blind-drill, WireForm, and Sweep sources are accepted by their owned production parsers but are not yet representable by the generic V2 parser. A temporary global build gate correctly exposed that mismatch but rejected 27 existing Firmament tests, so it was removed rather than breaking those routes. Converging that requires an explicit multi-domain admission adapter, not a conditional around the generic parser.

## Root cause and correction

Before X1, build and validate reached the profile/Compose adapter, which treated construction profiles and materialized bosses as distinct semantic entities. Generic inspect also ran the canonical symbol binder, but that binder placed `Profile Pad` and `Boss Pad` in one flat name map and rejected the program as `firmament-v2-symbol-duplicate`.

The canonical symbol table now preserves both typed symbols. Profiles are construction geometry; Bosses and Pockets are materialized feature identities. A `Boss`/`Pocket` `Profile:` operand is typed, so same display names are not ambiguous. Other cross-family collisions remain rejected, and ordinary lookup retains the construction profile for existing unqualified profile consumers; kind-qualified lookup disambiguates structured consumers.

## Admission doctrine

```
Firmament recognition -> parser and semantic bind -> canonical symbol admission -> command/domain routing -> lowering or inspection
```

Admission covers source shape, semantic declarations, typed bindings, and namespace validity. Geometry support, STEP export, and specialized domain execution happen afterward. Current domain-specific syntaxes continue to own their execution paths; this X1 change does not collapse them into generic build.

## Parity evidence

| Source | Admit | Build | Validate | Inspect |
|---|---|---|---|---|
| original colliding Boss stack | accepted | success | valid | success |
| renamed Boss stack | accepted | success | valid | success |
| Profile/Compose name collision | rejected | n/a | invalid | invalid |

The original Boss stack retains its existing Z=0..21 mm realization. Evidence JSON and STEP outputs are in `artifacts/local/admission-x1/`.

## Namespace policy

- `Profile` and `Boss`/`Pocket` are separate typed namespaces and may share a display name.
- `Profile`, `Compose`, bodies, static declarations, selections, and peer feature declarations otherwise reject accidental shadowing.
- Typed references, such as `Boss ... Profile: Pad`, resolve the expected declaration kind deterministically.
- Template/record and compatibility behavior remains unchanged by this bounded repair.

## Validation

Release builds, focused parser/binder tests, Boss/Pocket tests, and command parity replay pass. `git diff --check` passes.
