# M3 validation report

| Check | Result |
|---|---|
| `dotnet restore Aetheris.slnx` | pass; all projects current |
| `dotnet build Aetheris.slnx -f net10.0 --no-restore /m:1` | pass; 0 warnings, 0 errors |
| Kernel Core full | 948 passed |
| BRep Boolean focused | 132 passed |
| STEP AP242 focused | 273 passed |
| SurfaceMeshIR + StandardLibrary focused | 15 passed |
| Firmament full (includes materializers and M2 compatibility) | 1115 passed |
| Firmament materializer/compatibility focused | 97 passed |
| FrictionLab Boolean/generic-CIR/stepped focused with `AETHERIS_RUN_LEGACY_TESTS=1` | 61 passed |
| Surgery primitives + canonical recipe parity | 8 passed |
| CLI ground-truth path | `dotnet run --project Aetheris.CLI -- --help` passed |
| `git diff --check` | pass |

An intentionally broad first FrictionLab filter also selected the pre-existing unrelated `TriangleHexPrismProfileParityLabTests.TriangleAndHex_ValidRows_ProduceBodiesAndNo3DBoolean`; it failed at `ParameterInterval` with non-finite `end`, matching the milestone's known failure. The corrected focused Boolean/history selection passed 61/61. M3 did not touch that path.
