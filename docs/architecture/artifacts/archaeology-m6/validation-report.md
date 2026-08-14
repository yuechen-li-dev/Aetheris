# M6 validation report

- `dotnet restore Aetheris.slnx`: succeeded; all projects up to date.
- `dotnet build Aetheris.slnx -f net10.0 --no-restore /m:1`: succeeded with 0 warnings and 0 errors.
- Default solution lane (`Category!=SlowCorpus`): 2,789 passed after the two final codec contract cases were added. Project totals: Core 940; Firmament 1,120; CLI 364; Continuum/SurfaceMeshIR 148; Server 54; Forge.Host 10; FEA 12; Geometry 58; Modules 37; Reconstruction 24; Semantics 9; Collaboration 5; sample 8.
- Full Core lane, including Recipe, Surgery, Boolean compatibility, and STEP AP242 contracts: 959 passed, 0 failed.
- Opt-in Firmament V1 lane: 1,739 passed, 0 failed.
- Focused Boolean/CIR FrictionLab: 40 passed, 0 failed.
- Full opt-in FrictionLab: 394 passed, 5 failed, 399 total.
- The five failures are exactly `TriangleHexPrismProfileParityLabTests`; each throws `ArgumentOutOfRangeException: End must be finite` from `ParameterInterval` through `ProfileExtrusionBRepPlan.cs:128`. They are the documented pre-existing unrelated baseline and no M6 path appears in the stacks.
- Focused V1 codec + malformed-V2 firewall: 12 passed, 0 failed.
- CLI ground truth: top-level and build help clearly separate V2, V1 compatibility, and legacy `.firmasm`; a real V1 file build emitted exactly one `FirmamentV1.Compatibility` warning and produced STEP before the temporary artifact was removed.
- `git diff --check`: clean. Final `git status --short` contains only the intended M6 source, test, and Markdown changes; test-generated STEP artifacts were removed and tracked fixture outputs restored.
