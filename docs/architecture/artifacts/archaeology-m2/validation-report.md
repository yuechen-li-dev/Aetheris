# Validation report

- `dotnet restore Aetheris.slnx`: passed.
- `dotnet build Aetheris.slnx -f net10.0 --no-restore /m:1`: passed.
- Active Firmament suite: 1,115 passed.
- Opt-in V1 compatibility suite (`AETHERIS_RUN_LEGACY_TESTS=1`): 1,734 passed.
- Focused V1 codec, legacy loader, and V2 assembly firewall tests: 24 passed.
- CLI assembly routing tests: 6 passed.
- Server routing tests: 46 passed.
- Forge Host tests: 10 passed.

The historical `TriangleHexPrismProfileParityLabTests` were not implicated or modified.
