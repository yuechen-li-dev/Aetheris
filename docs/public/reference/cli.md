# CLI reference

Primary public commands:

Commands use `aetheris` unless a separate executable is shown. The optional `aetheris-3dm` tool includes native Rhino3dm; [installation and source usage](3dm-recovery.md).

| Command | Purpose |
|---|---|
| `validate <file> [--json]` | Check admission; generic mechanical validation includes in-memory geometry preflight |
| `frontend-shadow <file> [--json] [--ast] [--validate] [--benchmark]` | Developer-only original-source structural evidence and bounded production comparison |
| `build <file> [--output path] [--json]` | Materialize and export exact STEP AP242 |
| `inspect <firmament-or-step> [--json]` | Inspect source semantics or route STEP to analysis |
| `aetheris-3dm inspect <file.3dm> [--json]` | Inventory Rhino/OpenNURBS metadata, units, BRep topology, and unsupported objects |
| `aetheris-3dm recover <file.3dm> [--body N [--edge N\|--face N]] [--json]` | Measure provisional analytic/non-rational recovery candidates; see [3DM recovery evidence](3dm-recovery.md) |
| `analyze <step> [--json]` | Reinspect topology, surfaces, bounds, manifold state, and PMI |
| `analyze section <step> ((--xy\|--xz\|--yz) --offset value\|--origin x,y,z --normal x,y,z) [--out section.svg] [--json]` | Extract trim-aware analytic section loops in a principal or arbitrary plane and optionally render deterministic SVG evidence |
| `analyze compound <step> [--json]` | Inspect every disconnected rigid root without silently selecting or merging one |
| `view <file>` | Open in bundled Cadmata |
| `fea <file> --out-dir dir [--json]` | Compile and solve the bounded linear-elastic analysis |
| `sheetmetal ...` | Inspect, recognize, recover, compare, or flatten Sheet Metal |
| `sculpture build <source> ...` | Build the bounded non-manufacturing Sol 1 AP242 artwork, evidence, and SVG preview |
| `section-chain <build\|inspect\|validate> <file.firmament\|flagship\|twist\|two-profile> ...` | Lower authored SectionChain, `Loft`, or `Loft<Hollow>` into SectionChain IR and report frames, spans, correspondence, pcurves, conservative intersection qualification, STEP, and reimport evidence |
| `wireframe <model.step> [--out preview.svg] [--view iso\|front\|top\|right] [--density N] [--json]` | Render deterministic exact-edge and trim-clipped surface-isoline SVG evidence |
| `asm ...` | Inspect/execute/import/export assemblies |

Generic mechanical `validate` now runs the existing engineering compilation in memory, including selection binding, capability checks and geometry/STEP preflight. It writes no output file. Its report includes `validationLevel` (`Built`, `BuildRejected`, or `ParsedNotBuilt`) and developer-only `parserRoute` metadata. `--explain` prints the route, admission level and available instance counts. `--parse-only` explicitly requests static analysis and reports `parsed-not-built` on success. Drawing, Analysis, PlasticShell, Sculpting, Plateau and Piping currently lack that separate static admission path: they reject this option with `firmament-validation-option-unsupported` and `NotValidated`, rather than silently materializing geometry. Structural and Garment reuse their existing static analyzers. External `--forge-pack` validation is also labelled `parsed-not-built`: those descriptor providers are not installed into normal build. Domain-specific validators retain their separate scope; inspect their reports. This is not a promise that unchanged source can never encounter a later environment or kernel failure.

Mechanical Model and material-bearing Struct documents now use the unified lexer/AST admission path (`UnifiedMechanical`). The ignored Compose Fillet, erased Rect2 field and unused Slot2 route-perturbation cases are repaired. Profile/Compose build execution consumes admitted engineering inputs. Other dedicated domains retain their existing owners; see the [production migration qualification](../../development/reports/2026-10/FIRMAMENT-MECHANICAL-FRONTEND-MIGRATION.md) for its scope and evidence.

`validate file.firmament --shadow --explain --json` opts into the developer
envelope: `shadow` contains original-source syntax status, AST summary, original
diagnostics and a bounded comparison against the generic production projection;
`productionValidation` contains the ordinary validation command's unchanged
report. `Parsed` means `StructuralOnlyUnbound`, not engineering validity. The
comparison explicitly reports unqualified declarations and fields. `--ast` adds
the detailed source tree and trivia only when requested. `frontend-shadow`
without `--validate` collects syntax/projection evidence without geometry.

`frontend-shadow --corpus fixtures` writes JSON and Markdown under ignored
`artifacts/local/frontend-x1/corpus` by default. It includes invalid, legacy and
speculative sources; exit zero means collection completed, while `qualified:
false` remains explicit. Per-file exit one means structural rejection or, with
`--validate`, production rejection. Normal mechanical compilation uses the same syntax parser through its production
AST binder; the developer comparison does not control admission. `--benchmark` measures twenty warm structural parses and current
thread allocations, excluding file IO, production parsing, comparison and geometry.

Run `aetheris <command> --help` for exact options. JSON is a public automation surface, but its root is command-specific: process exit status is authoritative, `validate` reports `firmamentV2Validation.status`, and materializing/analysis commands report `success` where documented. Diagnostics are structured; artifact paths are explicit; wireframe reports trim/pcurve coverage and a deterministic SVG hash; Sheet Metal reports regions, bends, and lowered features separately; FEA reports requested/result artifacts; sculpting builds report every `BodyState`, `GeometricDelta`, validation evidence, surface-family inventory, and the sibling `.delta.json` path; other builds report emitted features and PMI export evidence. Counts describe semantic items at the stated layer, not arbitrary STEP entity counts.

Generic STEP analysis reports analytic placement and size information where it is present: planes include origin and normal; bounded cylinders and cones include axis, axial extent, and circular-boundary angular extent; spheres include center and radius; and circles include center, axis, radius, sweep, orientation, and their actual trimmed endpoints. Circular-edge bounds and section output are computed from those trims rather than from the supporting full circle. `analyze section` returns normalized closed loops with deterministic orientation and nesting metadata; `--out` is intended as compact geometry evidence, not a substitute for the JSON record.

## Assembly display meshes

`aetheris mesh <assembly.firmament|assembly.firmasm> --format assembly-json --output <file.mesh.json> --json` exports shared local definition meshes and stable world-space occurrences. Failed geometry is not omitted. The default destination is `artifacts/local/mesh/<name>.mesh.json`. See [editable mechanisms](../firmament/editable-mechanisms.md) for the schema and a working consumer.

## Generate engineering drawings

`aetheris drawing generate <part.firmament|model.step> --out-dir <directory> --json` creates PDF, SVG, editable PPTX, Drawing IR and validation evidence. The default automatic path requires a complete nominal dimension plan; currently rectangular plates/boxes with disjoint Z-through holes are qualified. `--linear-tolerance`, `--angular-tolerance` and `--material` are explicit drawing requirements. `--reference-only` emits clearly incomplete visualization. See [drawing generation](../drawings.md) for admission, reconstruction qualification and manufacturing limits. Use `drawing compile` for authored drawings and assembly BOMs.
