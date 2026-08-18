# M4 validation report

Environment: .NET SDK `10.0.302`, Windows x64.

## Tests and builds

- `dotnet test Aetheris.SheetMetal.Tests/Aetheris.SheetMetal.Tests.csproj`: 33 passed.
- focused Firmament contour/Concept Path tests: 16 passed.
- `dotnet build Aetheris.slnx --no-restore`: succeeded, 0 warnings, 0 errors.
- `dotnet test Aetheris.slnx --no-build --no-restore`: 2,851 passed across discovered test projects; `Aetheris.FrictionLab.Tests` contains no discoverable tests.
- CLI real paths exercised: `sheetmetal paths`, `sheetmetal inspect`, `sheetmetal flatten --step --svg`, `build`, and `sheetmetal compare`.

## Performance samples (warm CLI process measurements, milliseconds)

| Fixture | Parse | Formed lower | Initial flat lower | Reflatten | Exact single blank | DFM |
|---|---:|---:|---:|---:|---|---|
| L bracket | 8.98 | 53.47 | 56.51 | 0.49 | yes | Pass |
| M3 electronics tray (four reliefs) | 9.32 | 59.24 | 59.02 | 1.53 | no | Fail |
| CTC-03 | 9.41 | 59.95 | 58.46 | 2.43 | no | Fail |

No optimization campaign was needed; exact arrangement is small for these fixtures.

## Determinism

- L bracket: `de2501e789274aa295a7d778c7e8a88a8281cf0d6b29b79e33f06f58572502b4`
- M3 tray: `450fc03780a32a440b697fa6e7567dfe0e85aec8a6c03e1bb16ee4b59d2e2117`
- CTC-03: `dfeb1228747e38e88bdd9e6e0149fbc79db7474fc335035b3a0d65976d9c2ef8`
- PSU enclosure: `da104ebd017d4915b3d10e3a372d59794823caa8aa4d96def08fbb1cd85f5d91`

Repeated flattening retains hashes. Concept Path shape is stable across dimension/radius/cut changes and tray template specializations. Segment and relief ordering is ordinal/stable-ID based.

## DXF seam

The flat IR now separates exact cut contours, exact relief contours, bend lines with up/down direction and policy, and semantic IDs. Future DXF can map cut, bend-up, bend-down, and mark/etch layers as serialization. Full DXF is intentionally not implemented.
