# Legacy JSON `.firmasm` map

## Identity boundary

| Identity | Syntax/model | Status |
|---|---|---|
| current `.firmasm` | Firmament V2 Assembly document profile; ordinary V2/Assembly declarations and exactly one exported/root `Assembly` | current, canonical authoring/interchange profile |
| `LegacyFirmasmJson` | JSON object with `manifest`, `assembly`, `parts`, `instances`; transform-first flat occurrence model | deprecated compatibility/serialization input |

The extension is not deprecated. Only the historical JSON-shaped syntax is deprecated.

## Current implementation graph

```text
legacy JSON file
  -> FirmasmManifestLoader.Parse -> FirmasmManifest
  -> LoadFromFile
       -> resolve part paths
       -> Firmament part reference: path wrapper only
       -> STEP part: Step242Importer.ImportBody
       -> FirmasmLoadedAssembly

Compatibility route A (current assembly tooling):
  FirmamentAssemblyDocumentCompiler
    -> LegacyFirmasmMigration.GenerateCurrentSource
    -> temporary current .firmasm source with Placement LegacyExplicit
    -> AssemblyM1Pipeline -> AssemblyIR / geometry

Compatibility route B (old direct CLI):
  aetheris asm exec
    -> FirmasmAssemblyExecutor
    -> STEP-only instances, rigid transforms, composed multi-shell body

Compatibility route C (old outbound package):
  aetheris asm export
    -> FirmasmAssemblyRoundtripExporter
    -> per-instance STEP + roundtrip.package.json
```

## Component inventory

| Component | Role | Future disposition |
|---|---|---|
| `FirmasmManifestContracts.cs` | legacy normalized schema/DTO | `KEEP_AS_SERIALIZATION` |
| `FirmasmManifestLoader.Parse` | strict JSON schema/version/field parser | `KEEP_AS_COMPATIBILITY_READER` |
| `FirmasmManifestLoader.LoadFromFile` | path resolution, STEP import, Firmament reference loading | reader plus import bridge; split I/O from codec in M2 |
| `LegacyFirmasmMigration.GenerateCurrentSource` | deterministic compatibility migration into current Assembly source | `KEEP_AS_COMPATIBILITY_READER`; preserve `LegacyExplicit` authority |
| `FirmamentAssemblyDocumentCompiler` JSON detection | current `.firmasm` profile dispatcher | retain; replace shape sniffing with named format detection in M2 |
| `FirmasmAssemblyExecutor` | direct legacy transform-first STEP occurrence execution | `DEPRECATE`, but retain for Preview 3 compatibility |
| `FirmasmAssemblyRoundtripExporter` | outbound per-instance STEP package | compatibility exporter, not same-format writer; freeze |
| `AssemblyM2Interop` / `Step242FirmasmPackageImporter` | imports AP242 assembly into current V2 `.firmasm` package | current V2/interchange producer; not legacy JSON |

## Schema and data limits

The JSON schema contains:

- `manifest.version`;
- `assembly.name` and `assembly.units`;
- named parts with `kind` (`firmament` or `step`) and relative `source`;
- flat instances with `id`, part reference, translation, and optional XYZ Euler rotation.

It contains no Interface, Role, Mate, hierarchy semantics, constraint intent, tolerance stack, semantic occurrence relationship, or authored reason for a transform. Migration correctly emits `Placement LegacyExplicit`; it must not infer Mates or mark imported transforms as `MateDerived`.

Direct `FirmasmAssemblyExecutor` supports only loaded STEP parts. A `Firmament` part reference can be loaded by the manifest loader but is rejected by the direct executor. The current migration/Assembly pipeline is therefore the more capable compatibility path.

## Live callers

| Caller | Path | Requirement |
|---|---|---|
| CLI `asm inspect` | `.firmasm` -> `FirmamentAssemblyDocumentCompiler`; JSON is migrated | **must retain read + migration** |
| server `AssemblyDisplayService` | same compiler/profile route | **must retain read + migration** |
| CLI `asm export-ap242` | same compiler/profile route then `AssemblyIrAp242Exporter` | **must retain export through current AssemblyIR** |
| CLI `asm exec` | direct legacy loader/executor | **could freeze/deprecate**, retain for Preview 3 |
| CLI `asm export` | direct executor then per-instance STEP package | **could freeze/deprecate**, retain for Preview 3 |
| tests under `Assembly/Firmasm*` and CLI assembly tests | loader, migration, authority, direct execution/export | compatibility contract |

M1 CLI evidence: `asm inspect` migrated the OCCT AS1 JSON fixture to AssemblyIR with part placements marked `LegacyExplicit`; `asm exec` produced 18 bodies and the expected deprecation warning.

## Round-trip status

| Route | Status | Notes |
|---|---|---|
| legacy JSON -> `FirmasmManifest` -> legacy JSON | **unsupported** | No manifest JSON writer exists. |
| legacy JSON -> current V2 `.firmasm` | **lossy but intentionally safe** | Parts, flat instance IDs, references, and transforms are represented; comments/order/JSON representation and manifest version are not round-tripped. Missing semantic intent stays missing. |
| legacy JSON -> executed BRep -> per-instance STEP package | **geometry/interchange stable for admitted STEP subset, not format round-trip** | Output is STEP plus package JSON, not the input format. |
| current V2 `.firmasm` -> AP242 -> current package | bounded current interchange | Owned by Assembly M2/M3, separate from legacy JSON serialization. |

## Conflated locations and corrections

| Location | Issue | Disposition |
|---|---|---|
| `docs/development/history/firmament/preview2-reference/language-reference.md` former “`.firmasm` is a Legacy compatibility format” sentence | contradicted the same page and implementation | corrected in M1 to distinguish current profile from legacy JSON syntax |
| `FirmasmAssemblyRoundtripExporter` package field `nativeAuthority = ".firmasm"` | ambiguous when input is legacy JSON | update in M2 to a named legacy format identity or current AssemblyIR authority |
| CLI command names `asm exec` / `asm export` | accept only the legacy JSON direct path despite general `.firmasm` wording | document/deprecate or rename as legacy in M2; keep aliases for compatibility |
| `fixtures/Assembly/LegacyImports` READMEs call JSON manifests `.firmasm` contracts without consistently saying legacy | historical ambiguity | update labels in M2; retain fixtures |
| historical assembly artifact docs | correctly record transform-first limitations but sometimes predate current profile | retain as historical and add context, do not rewrite history |

## Proposed M2 ownership

Use `Aetheris.Kernel.Firmament.Serialization.LegacyFirmasm` (initially in the existing assembly) with:

- explicit `LegacyFirmasmJson` detection and version validation;
- parse-only DTO separated from part I/O;
- deterministic migration result carrying original IDs, paths, transforms, and provenance;
- no new authoring fields;
- no inference of Interfaces/Mates;
- read support and the existing current-V2 migration route as the primary compatibility implementation;
- direct executor/exporter retained behind a clearly legacy facade until Preview 3 removal policy is decided.

Same-format write support is not currently required by a production caller. Do not build it in M2 unless a Preview 3 compatibility contract is found outside this repository.
