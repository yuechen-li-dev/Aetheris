# CLI reference

Primary public commands:

| Command | Purpose |
|---|---|
| `validate <file> [--json]` | Parse, bind, check units/targets/static semantics without geometry |
| `build <file> [--output path] [--json]` | Materialize and export exact STEP AP242 |
| `inspect <firmament-or-step> [--json]` | Inspect source semantics or route STEP to analysis |
| `analyze <step> [--json]` | Reinspect topology, surfaces, bounds, manifold state, and PMI |
| `analyze section <step> ((--xy\|--xz\|--yz) --offset value\|--origin x,y,z --normal x,y,z) [--out section.svg] [--json]` | Extract trim-aware analytic section loops in a principal or arbitrary plane and optionally render deterministic SVG evidence |
| `analyze compound <step> [--json]` | Inspect every disconnected rigid root without silently selecting or merging one |
| `view <file>` | Open in bundled Cadmata |
| `fea <file> --out-dir dir [--json]` | Compile and solve the bounded linear-elastic analysis |
| `sheetmetal ...` | Inspect, recognize, recover, compare, or flatten Sheet Metal |
| `sculpture build <source> ...` | Build the bounded non-manufacturing Sol 1 AP242 artwork, evidence, and SVG preview |
| `section-chain <build\|inspect\|validate> <file.firmament\|flagship\|twist\|two-profile> ...` | Lower authored profiles into SectionChain IR and report frames, spans, correspondence, pcurves, conservative intersection qualification, STEP, and reimport evidence |
| `wireframe <model.step> [--out preview.svg] [--view iso\|front\|top\|right] [--density N] [--json]` | Render deterministic exact-edge and trim-clipped surface-isoline SVG evidence |
| `asm ...` | Inspect/execute/import/export assemblies |

Run `aetheris <command> --help` for exact options. JSON is a public automation surface, but its root is command-specific: process exit status is authoritative, `validate` reports `firmamentV2Validation.status`, and materializing/analysis commands report `success` where documented. Diagnostics are structured; artifact paths are explicit; wireframe reports trim/pcurve coverage and a deterministic SVG hash; Sheet Metal reports regions, bends, and lowered features separately; FEA reports requested/result artifacts; sculpting builds report every `BodyState`, `GeometricDelta`, validation evidence, surface-family inventory, and the sibling `.delta.json` path; other builds report emitted features and PMI export evidence. Counts describe semantic items at the stated layer, not arbitrary STEP entity counts.

Generic STEP analysis reports analytic placement and size information where it is present: planes include origin and normal; bounded cylinders and cones include axis, axial extent, and circular-boundary angular extent; spheres include center and radius; and circles include center, axis, radius, sweep, orientation, and their actual trimmed endpoints. Circular-edge bounds and section output are computed from those trims rather than from the supporting full circle. `analyze section` returns normalized closed loops with deterministic orientation and nesting metadata; `--out` is intended as compact geometry evidence, not a substitute for the JSON record.

## Assembly display meshes

`aetheris mesh <assembly.firmament|assembly.firmasm> --format assembly-json --output <file.mesh.json> --json` exports shared local definition meshes and stable world-space occurrences. Failed geometry is not omitted. The default destination is `artifacts/local/mesh/<name>.mesh.json`. See [editable mechanisms](../firmament/editable-mechanisms.md) for the schema and a working consumer.
