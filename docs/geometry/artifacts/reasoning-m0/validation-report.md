# M0 validation report

- .NET SDK: 10.0.302.
- Restore: `dotnet restore Aetheris.slnx` succeeded.
- Build: `dotnet build Aetheris.slnx -f net10.0 --no-restore /m:1` succeeded with 0 warnings and 0 errors.
- Full solution tests: 2,708 passed, 0 failed, 0 skipped. `Aetheris.FrictionLab.Tests` contains no discoverable tests and did not fail the run.
- Geometry-focused tests: 11 passed, covering typed domain/plane validation, public evaluation/first jets/normals, identity/provenance, sampled strict sides/crossing/contact candidate, certified polynomial sides/crossing, tangent budget exhaustion, sine/cosine/division intervals, unsupported procedural certification, Panel adapter/CAD clearance, semantic expectations, and deterministic serialization.
- Historical Panel-only CLI fixture: validation is valid (0 fatal, 2 warnings); build now returns a typed unsupported-shell-export diagnostic and emits no STEP artifact.
- Calibration generation: `dotnet run --project tools/Aetheris.Geometry.M0Evidence -- docs/geometry/artifacts/reasoning-m0` succeeded.
- Every calibration's certified-query JSON was serialized twice and produced identical SHA-256 hashes. Sampling and subdivision contain no random choices.

Measured calibration performance is persisted in each fixture JSON. These wall-clock measurements are observational and machine-dependent; classifications, bounds, witness order, statistics, diagnostics, and query hashes are the deterministic contract.
