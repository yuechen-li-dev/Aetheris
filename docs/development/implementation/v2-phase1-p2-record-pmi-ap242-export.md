# V2 Phase 1 P2 record PMI AP242 export

P2 wires the supported subset of the record-shaped Firmament V2 `pmi` block into the existing semantic STEP/AP242 export path for InlineStep models.

## Supported export records

Implemented for P2 export:

- `datum` over a resolved InlineStep face or recognized `datumPlane` region.
- `diameter` over a resolved InlineStep face or recognized `holeShaft` region.

Export-deferred records remain explicit and are not silently dropped:

- `distance`
- `flatness`
- `parallel`
- `perpendicular`
- `coplanar`

For build/export, P2 rejects export-deferred PMI records with a deterministic `firmament-v2-pmi-export-deferred` diagnostic instead of emitting a partial AP242 file without those records.

## Bridge to semantic PMI

The P1 parser/binder continues to bind record-shaped PMI into `FirmamentV2BoundPmiBlock`. P2 bridges the legacy-compatible `FirmamentV2PmiDecl` records plus bound tolerance metadata into the current `Step242SemanticPmi` payloads consumed by `Step242Exporter`:

- `datum A { target: part.region("baseFace") }` becomes `Step242SemanticPmiDatum`.
- `diameter mountHoleADiameter { target: part.region("mountHoleA") dimension: MountingPattern.holeDiameter }` becomes `Step242SemanticPmiHole`.

The bridge preserves target strings through recognized-region resolution, so AP242 descriptions include the imported canonical face evidence.

## Tolerance handling

Diameter dimensions authored from toleranced `let` values preserve bilateral tolerance metadata. The AP242 exporter emits separate semantic tolerance evidence using `diameter_tolerance:<featureId>` plus `tolerance_plus` and `tolerance_minus` measure items. Diameter records that reference a `dimension:` let without tolerance remain invalid via `firmament-v2-pmi-dimension-missing-tolerance`.

Legacy `value:`/`diameter:` diameter forms remain available for older fixtures, but the V2 Phase 1 P2 authoring path is the toleranced `dimension:` form.

## AP242 evidence checked

The primary P2 fixture asserts these semantic STEP/AP242 strings:

- `SHAPE_ASPECT('firmament-datum:A'`
- `PROPERTY_DEFINITION('datum:A:part'`
- `SHAPE_DIMENSION_REPRESENTATION('diameter:part.mountHoleADiameter'`
- `PROPERTY_DEFINITION('diameter:part.mountHoleADiameter'`
- `SHAPE_DIMENSION_REPRESENTATION('diameter_tolerance:part.mountHoleADiameter'`
- `tolerance_plus`
- `tolerance_minus`

## Fixture and report behavior

Primary fixture:

`fixtures/Regression/InlineStep/valid/inline-step-v2-record-pmi-datum-diameter-step-verified.valid.firmfixture`

Because this fixture imports `canonical-through-hole.step`, its `step-verified` corpus contract includes InlineStep trace metadata for the real through-hole body (`expected-volume: 461.15044407846124`, `expected-topology: faces=7`). Without that metadata, the generic parser-backed trace verifier falls back to the plain 10x8x6 box volume and reports a stale `step-v2-a1-volume-mismatch` even though the AP242 PMI export proof still succeeds.

Invalid/deferred fixtures:

- `fixtures/Compatibility/LegacyAliases/Invalid/InlineStep/inline-step-v2-record-pmi-diameter-missing-tolerance.invalid.firmfixture`
- `fixtures/Compatibility/LegacyAliases/Invalid/InlineStep/inline-step-v2-record-pmi-export-deferred-flatness.invalid.firmfixture`

`aetheris validate <fixture> --json` reports record-shaped PMI status through `firmamentV2Validation.pmi`, including `exportSupport: supported` for datum/diameter and `exportSupport: deferred` for unsupported records.

`aetheris build <fixture> --json` reports AP242 evidence visibility in `pmiExportEvidence`, with datum and diameter entries marked `exportSupport: supported` and `exportEvidence: found` after successful export.

## Non-scope

P2 intentionally does not implement:

- graphical PMI;
- drawing views;
- full GD&T/Y14.5 lowering;
- new modeling behavior;
- STEP geometry changes;
- AP242 lowering for distance, flatness, parallel, perpendicular, or coplanar records.
