# M8/Profile-M2 validation report

## Real-path results

- .NET SDK 10 detected; restore was satisfied during the clean solution build.
- `dotnet build Aetheris.slnx --no-incremental`: pass, zero warnings, zero errors.
- `dotnet test Aetheris.SheetMetal.Tests --no-restore`: pass, 60/60 in the full run; new Profile-M2 tests also pass after rebuild.
- Full solution test execution completed. All feature suites passed; Kernel.Core exposed timing-test nondeterminism under parallel load. The two failures from the parallel run passed immediately in isolation; a later full Kernel.Core run exposed a different timing assertion, which also passed in isolation. `Aetheris.FrictionLab.Tests` reports no discoverable tests, as before.
- Final Firmament parses and compiles with the source STEP absent: pass.
- Concept contract, Concept Struct resolution, Concept Paths, semantic constraints, Profiles, formed lowering, flattening, STEP export/reimport, DFM, and CLI inspect/compare: exercised by tests and CLI dogfood.
- Non-CTC semantic-panel fixture: pass with deterministic stable paths.
- `git diff --check`: pass (line-ending notices only; no whitespace errors).

## CLI inspection

Final authored recognition is `Complete`: 15 regions, seven bends, 17 cuts, nine patterns, three datums, one tab, two stepped-notch edge fragments, and 18 resolved constraints. Exact flat status is `Valid`; DFM is `Pass` after the mounting-flange profiles restored the real hole-to-edge clearance.

One warm observational run on this machine measured:

| Phase | Time |
|---|---:|
| Parse total | 26.5759 ms |
| Semantic + constraint resolution | 16.0116 ms |
| Formed/profile lower | 70.3482 ms |
| Authored flat lower | 57.5023 ms |

These are diagnostic timings, not benchmarks. Constraint resolution is included in `semanticResolve`; exact profile construction is included in `formedLower` and is not separately instrumented.

## Determinism

Repeated generation produced stable paths and these SHA-256 values:

| Artifact | SHA-256 |
|---|---|
| `ctc03-final.firmament` | `3A8C4A7C93C4750FE9241B4F4071F240108966F5AB0A28F1E81ABF7ABF16CA91` |
| `ctc03-formed.step` | `8E049EC25E0B3F3C57A1F931C4D111C51BD9D840B3B53569A7771F764567F330` |
| `ctc03-flat.step` | `8104D7C13DF87F838DC709171E7BEABCAECFDD3A62943B6D254B68E5DC11B9B5` |
| `ctc03-flat.svg` | `24C95460BF669BA38A78CDFF9BE6E7E1051EE56F39C8E0018249B5BAE00C5B05` |
| Flat IR deterministic hash | `e3ec913e24f65d220839bbdbfa2d909dd8bdeeaed87ecc9ccfc6c1be7a1d297c` |

## Code quality

Verdict: **acceptable bounded prototype**. Generic edge-composition IR, diagnostics, stable paths, non-CTC fixture, and real export/reimport tests are cleanly localized. Debt remains: the Profile adapter is regex/block based, Sheet Metal exposes only the repeated fragment pressure needed so far, semantic and constraint timing share one bucket, and cross-edge corner ownership is not implemented. There is no CTC-specific compiler branch or generator.
