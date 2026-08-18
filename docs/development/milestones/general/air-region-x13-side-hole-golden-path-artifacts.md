# AIR-REGION-X13 — Side-hole golden path artifacts

## Purpose and scope

AIR-REGION-X13 locks the controlled side-hole AIR Region path as a reproducible artifact workflow. It is artifact/corpus discipline for one fixture only, not a new geometry milestone.

## Relationship to AIR-REGION-X12

AIR-REGION-X12 proved the controlled side-hole trace could reach parent integration `Integrated`, shell closure `Closed`, and STEP smoke `Succeeded`. X13 preserves that evidence and adds a stable command that writes persistent artifacts for inspection.

## Fixture

```text
fixtures/Region/valid/side-hole-face-attached-region.valid.firmfixture
```

## Generate artifacts

```bash
dotnet run --project Aetheris.CLI -f net10.0 -- trace \
  --fixture fixtures/Region/valid/side-hole-face-attached-region.valid.firmfixture \
  --out-dir artifacts/air-region-x13/side-hole
```

## Expected artifact paths

```text
artifacts/air-region-x13/side-hole/side-hole.step
artifacts/air-region-x13/side-hole/side-hole.trace.json
artifacts/air-region-x13/side-hole/side-hole.trace.txt
artifacts/air-region-x13/side-hole/manifest.json
```

## Artifact policy

Generated-on-demand only. The STEP file is deliberately not committed as a corpus blob; the CLI command above regenerates the controlled fixture artifact and leaves it on disk when `--out-dir` is provided.

## Manual STEP inspection

Generate the artifacts, then open:

```text
artifacts/air-region-x13/side-hole/side-hole.step
```

The STEP artifact is a controlled golden-path inspection artifact for the known +X/-X/radius-1 side-hole fixture.

## What the golden path proves

- The controlled AIR Region side-hole fixture can reach closed parent BRep evidence.
- The controlled path preserves +X entry loop, -X exit loop, cylindrical cut wall, and consumed `RegionIntegrationPatch` evidence.
- Parent integration is `Integrated`.
- Shell closure is `Closed`.
- STEP smoke is `Succeeded`.

## What it does not prove

- No general side-hole support.
- No arbitrary face/axis support.
- No imported/no-history route.
- No CIR topology authority; CIR remains analysis-only.
- No Boolean general admission; Boolean remains unused for this path.

## Tests run

Recommended checks for this milestone:

```bash
dotnet build Aetheris.CLI/Aetheris.CLI.csproj -f net10.0
dotnet run --project Aetheris.CLI -f net10.0 -- --help
dotnet run --project Aetheris.CLI -f net10.0 -- trace --help
dotnet run --project Aetheris.CLI -f net10.0 -- trace --fixture fixtures/Region/valid/side-hole-face-attached-region.valid.firmfixture
dotnet run --project Aetheris.CLI -f net10.0 -- trace --fixture fixtures/Region/valid/side-hole-face-attached-region.valid.firmfixture --json
dotnet run --project Aetheris.CLI -f net10.0 -- trace --fixture fixtures/Region/valid/side-hole-face-attached-region.valid.firmfixture --out-dir artifacts/air-region-x13/side-hole
dotnet test Aetheris.CLI.Tests/Aetheris.CLI.Tests.csproj --filter "Trace|Fixture|FirmFixture|Region|AirRegion|GoldenPath|Artifact|SideHole|ParserBacked|Firmament|AIR|Air|CIR|Cir|Step"
```

## Recommended next milestone

AIR-REGION-X14 — Controlled side-hole parameter variation. Start with the same box and the same +X/-X through direction, varying only radius or center offset.

## AIR-FIRMAMENT-A1 corpus status

AIR-FIRMAMENT-A1 promotes the controlled side-hole fixture into the language-level Firmament corpus taxonomy. The fixture remains at `fixtures/Region/valid/side-hole-face-attached-region.valid.firmfixture` with explicit metadata for source validity, implementation state, expected lowering stage, integration status, shell closure, STEP smoke, and artifact evidence. This is still the same controlled golden path only: it does not admit general side-hole support, arbitrary face/axis support, production Boolean fallback, CIR topology authority, STEP exporter/importer changes, or BRep topology behavior changes.

## Firmament V2 syntax note

AIR-FIRMAMENT-A2 documents a V2 side-hole region syntax as design-level source shape. That snippet is not parser-backed yet and does not expand the controlled X13 golden path into general side-hole support, arbitrary face/axis support, production Boolean fallback, CIR topology authority, STEP exporter/importer behavior changes, or BRep topology behavior changes.

## AIR-FIRMAMENT-X5 V2 parity note

AIR-FIRMAMENT-X5 now gives the parser-backed Firmament V2 side-hole fixture its own generated-on-demand artifact path at `artifacts/air-firmament-x5/side-hole-v2/`. The V2 artifact uses clearly labeled `side-hole-v2.*` filenames and is structurally parity-checked against this X13 controlled side-hole path for stage, parent integration, shell closure, STEP smoke, +X entry, -X exit, radius-1 Cylinder cut evidence, loop/wall/patch materialization evidence, no blocker, CIR analysis-only status, and no Boolean general admission.
