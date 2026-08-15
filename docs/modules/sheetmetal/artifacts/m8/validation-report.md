# M8 validation report

## Real-path results

- .NET SDK 10 detected; restore was satisfied during the clean solution build.
- `dotnet build Aetheris.slnx --no-incremental`: pass, zero warnings, zero errors.
- `dotnet test Aetheris.SheetMetal.Tests --no-restore`: pass, 58/58.
- `dotnet test Aetheris.slnx --no-build --no-restore`: pass, 2,880/2,880 discovered tests across test-bearing projects. `Aetheris.FrictionLab.Tests` reports no discoverable tests, as before.
- Final Firmament parses and compiles with the source STEP absent: pass.
- Concept contract, Concept Struct resolution, Concept Paths, semantic constraints, Profiles, formed lowering, flattening, STEP export/reimport, DFM, and CLI inspect/compare: exercised by tests and CLI dogfood.
- Non-CTC semantic-panel fixture: pass with deterministic stable paths.
- `git diff --check`: pass (line-ending notices only; no whitespace errors).

## CLI inspection

Final authored recognition is `Complete`: 15 regions, seven bends, 17 cuts, nine patterns, three datums, one tab, and 18 resolved constraints. Exact flat status is `Valid`; DFM is `Warning` for the two localized front-hole edge-clearance findings documented in the comparison.

One warm observational run on this machine measured:

| Phase | Time |
|---|---:|
| Parse total | 33.2315 ms |
| Semantic + constraint resolution | 19.5383 ms |
| Formed/profile lower | 71.0012 ms |
| Authored flat lower | 61.0412 ms |

These are diagnostic timings, not benchmarks. Constraint resolution is included in `semanticResolve`; exact profile construction is included in `formedLower` and is not separately instrumented.

## Determinism

Repeated generation produced stable paths and these SHA-256 values:

| Artifact | SHA-256 |
|---|---|
| `ctc03-final.firmament` | `B2EBD30F8D6BB2A6045F28D11AC5779C82D75EF82B56C666BB8A8A8310CD54C4` |
| `ctc03-formed.step` | `8F4E1A6FA5E780B4EAC140FBDE63AEFCF3DC5463EFD9C0577DD94928030C409A` |
| `ctc03-flat.step` | `948C3F69090B70E615C3ED60ABB9C6A66B86C88E687870C911FF19E03FA7AC6C` |
| `ctc03-flat.svg` | `9DA3147DC841A5DF629D88A3576368496A78B71C0DB9DCD4E0C67659A1ED832D` |
| Flat IR deterministic hash | `55dd154c6bb063a27f6055bbaeb9e0b29aac41ba99bf8735b21cae54969b71aa` |

## Code quality

Verdict: **acceptable bounded prototype**. The semantic IR, diagnostics, stable paths, non-CTC fixture, and real export/reimport tests are cleanly localized. Debt remains: the parser is regex/block based, `Tab` supports only the bounded outer-edge case, semantic and constraint timing share one bucket, and no general irregular edge-profile MIR exists. There is no CTC-specific compiler branch or generator.
