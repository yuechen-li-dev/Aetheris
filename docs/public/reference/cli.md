# CLI reference

Primary public commands:

| Command | Purpose |
|---|---|
| `validate <file> [--json]` | Parse, bind, check units/targets/static semantics without geometry |
| `build <file> [--output path] [--json]` | Materialize and export exact STEP AP242 |
| `inspect <firmament-or-step> [--json]` | Inspect source semantics or route STEP to analysis |
| `analyze <step> [--json]` | Reinspect topology, surfaces, bounds, manifold state, and PMI |
| `view <file>` | Open in bundled Cadmata |
| `fea <file> --out-dir dir [--json]` | Compile and solve the bounded linear-elastic analysis |
| `sheetmetal ...` | Inspect, recognize, recover, compare, or flatten Sheet Metal |
| `asm ...` | Inspect/execute/import/export assemblies |

Run `aetheris <command> --help` for exact options. JSON is a public automation surface: `success` reflects the command result; diagnostics are structured; artifact paths are explicit; Sheet Metal reports regions, bends, and lowered features separately; FEA reports requested/result artifacts; builds report emitted features and PMI export evidence. Counts describe semantic items at the stated layer, not arbitrary STEP entity counts.
