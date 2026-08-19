# INLINE-STEP-X5 residual accounting and migration progress metrics

INLINE-STEP-X5 adds a face-focused migration report for Firmament V2 `InlineStep` strangler-fig workflows. The report is metadata accounting only: it explains how much imported canonical STEP topology is recognized, replaced by the current bounded semantic replacement, and left as raw residual topology.

## Report exposure

`aetheris build --json` now includes `inlineStepMigration` when the build path is an InlineStep re-export or InlineStep replacement. The same report is also available on `FirmamentStepExportResult.InlineStepMigration` for focused tests and implementation callers.

## Fields

`InlineStepMigrationReport` contains:

- `importedBodyName`, `sourcePath`, and `sourceHash` for imported body provenance.
- `originalTopology.faceCount`, plus deferred `edgeCount` and `vertexCount` fields. X5 keeps edge/vertex accounting at `0` because `ImportedStepTopologyMap` is currently face-focused.
- `recognized.regionCount`, `referencedFaceCount`, `duplicateReferencedFaceCount`, and `unresolvedReferenceCount`.
- `replacements.plannedCount`, `verifiedCount`, `emittedCount`, `failedCount`, and `replacedFaceCount`.
- `residual.residualFaceCount` and `unclaimedFaceCount`.
- `coverage.recognizedFaceRatio` and `replacedFaceRatio`.
- `emissionStrategy`, `residualSurgery`, `replacementStates`, and deterministic `diagnostics`.

## Accounting rules

- Original face count is the number of imported face entity references in the canonical `ImportedStepTopologyMap`.
- Recognized face count is the count of unique resolvable imported face refs referenced by recognized regions on the InlineStep body.
- Duplicate recognized refs are counted once for coverage and reported through `duplicateReferencedFaceCount` plus a deterministic diagnostic.
- Unresolved refs are excluded from coverage and reported through `unresolvedReferenceCount` plus a deterministic diagnostic. The normal parser rejects these for fixtures; the report builder still accounts for them defensively for tests and future callers.
- Replaced face count is the unique set of recognized region face refs attached to verified replacements.
- Residual face count is metadata-level accounting: `originalFaceCount - replacedFaceCount`.
- Coverage ratios are deterministic doubles: `recognizedFaceRatio = uniqueRecognizedFaces / originalFaceCount` and `replacedFaceRatio = uniqueReplacedFaces / originalFaceCount`. If original face count is zero, ratios are `0` and a diagnostic is emitted.

## Replacement states

The report uses the INLINE-STEP-A0 state vocabulary where applicable:

- `recognized`
- `replacement-planned`
- `replacement-verified`
- `residual-emitted`
- `hybrid-step-verified`

Recognized-only InlineStep fixtures reach `recognized` and `residual-emitted`. The X4 through-hole replacement fixture reaches `hybrid-step-verified` because the bounded replacement path exports AP242 and the integration test reimports it.

## Bounded rebuild honesty

The X4 through-hole replacement path still uses a bounded rebuild strategy for one recognized `hole<shaft>` through-hole. X5 does not add true residual topology surgery or arbitrary face deletion. For that path the report states:

- `emissionStrategy: "holeShaft-bounded-rebuild"`
- `residualSurgery: false`

The residual counts are therefore migration accounting over the original imported topology, not a claim that the emitted BRep physically suppresses only arbitrary claimed faces.

## Fixture evidence

Primary fixture:

- `fixtures/Regression/InlineStep/valid/inline-step-v2-replace-through-hole-step-verified.valid.firmfixture`

For the canonical through-hole fixture, the X5 report records:

```json
{
  "originalTopology": { "faceCount": 7, "edgeCount": 0, "vertexCount": 0 },
  "recognized": { "regionCount": 1, "referencedFaceCount": 1, "duplicateReferencedFaceCount": 0, "unresolvedReferenceCount": 0 },
  "replacements": { "plannedCount": 1, "verifiedCount": 1, "emittedCount": 1, "failedCount": 0, "replacedFaceCount": 1 },
  "residual": { "residualFaceCount": 6, "unclaimedFaceCount": 6 },
  "coverage": { "recognizedFaceRatio": 0.14285714285714285, "replacedFaceRatio": 0.14285714285714285 },
  "emissionStrategy": "holeShaft-bounded-rebuild",
  "residualSurgery": false,
  "replacementStates": ["recognized", "replacement-planned", "replacement-verified", "residual-emitted", "hybrid-step-verified"]
}
```

Recognized-only fixture coverage is exercised with:

- `fixtures/Regression/InlineStep/valid/inline-step-v2-recognized-face-datum-pmi-emits-in-step.valid.firmfixture`

## Deferred work

- Edge and vertex accounting beyond the current face-focused topology map.
- Multiple replacement families and broader multiple-replacement policy.
- True residual body suppression/surgery.
- Automatic recognition.
- Replacement verification for counterbore, countersink, blind-hole, fillet/chamfer, slot/pocket, or other feature families.
