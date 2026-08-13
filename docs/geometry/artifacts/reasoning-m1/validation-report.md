# AETHERIS-GEOMETRY-REASONING-M1 validation

Validation date: 2026-08-13.

- `dotnet restore Aetheris.slnx`: succeeded; 49 projects accounted for.
- `dotnet build Aetheris.slnx -f net10.0 --no-restore /m:1`: succeeded with 0 warnings and 0 errors.
- `dotnet test Aetheris.slnx -f net10.0 --no-restore --no-build /m:1`: 2,716 passed, 0 failed, 0 skipped across 12 test assemblies. `Aetheris.FrictionLab.Tests` built successfully but contains no discoverable tests.
- `Aetheris.Geometry.Tests`: 19 passed, including 8 M1 curve tests and 11 retained M0 signed-side tests.
- `dotnet run --project Aetheris.CLI -- --help`: succeeded. `modules` inspection reported the expected Surfacing Panel and Piping PipeRoute capabilities. The attempted `modules list` spelling exposed a small CLI friction point: this command lists directly as `modules [--json]`, without a `list` subcommand.
- `git diff --check`: passed.

The durable evidence generator is `tools/Aetheris.Geometry.M1Evidence`. Two consecutive final runs produced the same fixed-output SHA-256, `bec8477b73723b0562f823787f38727d4596992f0ea2d310bcc377663eca7634`. Timing is kept in a separate artifact so machine variation does not affect the determinism hash.

Final Debug/net10.0 smoke measurements over 100,000 operations were approximately 407 ns curve evaluation, 523 ns native first jet, 518 ns expression first jet, 405 ns Panel-edge adapter evaluation, and 465 ns Piping-route adapter evaluation. These are validation measurements, not optimization claims.

The M1 scope deliberately excludes Firmament grammar, piecewise global reparameterization, curve signed-side predicates, second jets, curvature, closest point, distance, generic intersection, contact order, rational B-splines/NURBS, and topology materialization from numerical results.
