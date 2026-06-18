# AIR-FIRMAMENT-X5 — V2 side-hole artifacts and parity lock

## Purpose and scope

AIR-FIRMAMENT-X5 locks the parser-backed Firmament V2 side-hole source path as a generated-on-demand artifact workflow. The scope is exactly the controlled V2 side-hole fixture: STEP artifact, trace JSON artifact, trace text artifact, manifest artifact, and structural parity with the existing AIR-REGION-X13 controlled side-hole path.

This milestone does not add geometry support or broaden region support.

## Relationship to X4

AIR-FIRMAMENT-X4 proved that the parser-backed V2 source can lower to the existing AIR Region golden trace chain:

```text
stage: region-parent-integrated
parentIntegration: Integrated
shellClosure: Closed
stepSmoke: Succeeded
```

X5 keeps that real path and adds persistent, reproducible compiler artifacts when `--out-dir` is supplied.

## Relationship to AIR-REGION-X13

AIR-REGION-X13 locked the earlier metadata-controlled side-hole fixture as generated-on-demand artifacts. X5 gives the parser-backed V2 fixture its own clearly labeled artifact names and compares stable semantic facts against the X13 path instead of requiring STEP byte equality.

## V2 fixture path

```text
fixtures/FirmamentV2/Region/valid/side-hole-v2.valid.firmfixture
```

## Artifact generation command

```bash
dotnet run --project Aetheris.CLI -f net10.0 -- trace \
  --fixture fixtures/FirmamentV2/Region/valid/side-hole-v2.valid.firmfixture \
  --out-dir artifacts/air-firmament-x5/side-hole-v2
```

## Expected output paths

```text
artifacts/air-firmament-x5/side-hole-v2/side-hole-v2.step
artifacts/air-firmament-x5/side-hole-v2/side-hole-v2.trace.json
artifacts/air-firmament-x5/side-hole-v2/side-hole-v2.trace.txt
artifacts/air-firmament-x5/side-hole-v2/manifest.json
```

The V2 names deliberately do not reuse `side-hole.step`, `side-hole.trace.json`, or `side-hole.trace.txt` from the X13 metadata path.

## Generated-on-demand artifact policy

Artifacts are generated on demand and should not be committed as golden blobs. When `--out-dir` is present, the CLI creates the directory, writes stable files, reports artifact paths in trace JSON and trace text, writes `manifest.json`, and leaves the files on disk. Without `--out-dir`, normal trace output remains unchanged and no persistent artifacts are required.

## Parity checks

The parity lock compares stable structural facts shared by the V2 parser-backed path and the X13 controlled side-hole path:

- both reach `region-parent-integrated`;
- parent integration is `Integrated`;
- shell closure is `Closed`;
- STEP smoke is `Succeeded`;
- entry face is `+X`;
- exit / through face is `-X`;
- radius is `1`;
- tool is `Cylinder`;
- `CutEntryLoop`, `CutExitLoop`, and `CutWallFace` evidence is present;
- `RegionIntegrationPatch` is consumed/materialized in the parent integration evidence;
- blockers are absent;
- CIR remains analysis-only;
- Boolean is unused / not generally admitted.

## Deliberately not compared

X5 does not require byte-for-byte STEP equality. The artifact is a stable generated inspection artifact, but parity is locked on normalized compiler facts and STEP smoke success rather than incidental file bytes.

## What the V2 artifact proves

The parser-backed Firmament V2 source for the controlled side-hole can reach the controlled AIR Region golden path and can emit a STEP/text/JSON/manifest artifact set on demand.

## What it does not prove

- No general side-hole support.
- No arbitrary face/axis support.
- No generic region support.
- No Boolean general admission.
- No CIR topology authority.
- No shell, fillet, chamfer, surfacing, pattern, material, or FEA implementation.
- No templates, concepts, PMI, or `where` parser support.
- No STEP PMI export.

## Tests run

X5 validation should include the focused CLI artifact/parity tests plus the usual trace commands:

```bash
dotnet build Aetheris.CLI/Aetheris.CLI.csproj -f net10.0
dotnet run --project Aetheris.CLI -f net10.0 -- --help
dotnet run --project Aetheris.CLI -f net10.0 -- trace --help
dotnet run --project Aetheris.CLI -f net10.0 -- trace --fixture fixtures/FirmamentV2/Region/valid/side-hole-v2.valid.firmfixture
dotnet run --project Aetheris.CLI -f net10.0 -- trace --fixture fixtures/FirmamentV2/Region/valid/side-hole-v2.valid.firmfixture --json
dotnet run --project Aetheris.CLI -f net10.0 -- trace --fixture fixtures/FirmamentV2/Region/valid/side-hole-v2.valid.firmfixture --out-dir artifacts/air-firmament-x5/side-hole-v2
dotnet run --project Aetheris.CLI -f net10.0 -- trace --fixture fixtures/Firmament/Region/valid/side-hole-face-attached-region.valid.firmfixture
dotnet test Aetheris.CLI.Tests/Aetheris.CLI.Tests.csproj --filter "FirmamentV2SideHoleArtifacts|FirmamentV2Parser|FirmamentV2SideHole|FirmamentV2Region|FirmamentSemanticRefs|FirmamentExpose|FirmamentFatArrow|FirmamentWith|FirmamentRecordDerivation|FirmamentV2|FirmamentFixtureCorpus|FirmFixture|Fixture|Trace|Region|SideHole|Primitive|ParserBacked|NotImplemented|Invalid"
```

## Next milestone recommendation

Recommended next milestone: add a thin explicit V2 side-hole-to-AIR-region adapter object so future parser-backed region milestones avoid trace-probe coupling. Do not generalize arbitrary faces, axes, Boolean admission, or generic region topology until that adapter has its own narrow tests.
