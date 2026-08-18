# INLINE-STEP-X2 semantic PMI on canonical inline STEP topology

INLINE-STEP-X2 allows a Firmament V2 `InlineStep` body to receive semantic PMI declarations whose targets are faces from the imported Aetheris-canonical STEP file. The supported syntax is intentionally narrow:

```firmament
solid importedPart: InlineStep {
    path: "../testdata/canonical-box-10x8x6.step"
}

pmi {
    datum A {
        target: importedPart.face("#40")
    }

    diameter holeDiameter {
        target: importedPart.face("#191")
        value: 2mm
    }
}
```

Only Aetheris-canonical AP242 exported by Aetheris is accepted as an inline source. Arbitrary vendor STEP remains rejected with `firmament-inline-step-requires-aetheris-canonical-step`; vendor entity IDs are not valid Firmament topology references unless they first pass through canonicalization and are resolved through the canonical inline body.

## Topology identity map

`FirmamentV2InlineStepRecord` now carries an `ImportedStepTopologyMap` with two deterministic face-only dictionaries:

* canonical STEP entity reference (`#40`) to imported face id (`face-1`);
* imported face id (`face-1`) back to canonical STEP entity reference (`#40`).

For X2 the map is built from `ADVANCED_FACE` entities in the canonical STEP text at parse time. It is face-only by design because the supported PMI targets are datum-on-face and diameter-on-face. The build path imports the body through `Step242Importer`, resolves the PMI target through the map, and emits semantic PMI through `Step242Exporter.ExportBody(body, semanticPmi, options)`.

## Fixtures

Valid fixtures:

* `fixtures/InlineStep/valid/inline-step-v2-datum-pmi-on-canonical-face-emits-in-step.valid.firmfixture`
* `fixtures/InlineStep/valid/inline-step-v2-hole-diameter-pmi-on-canonical-face-emits-in-step.valid.firmfixture`

Input canonical STEP fixtures:

* `fixtures/InlineStep/testdata/canonical-box-10x8x6.step`
* `fixtures/InlineStep/testdata/canonical-through-hole.step`

The datum fixture targets box face `#40`. The diameter fixture targets hole cylindrical face `#191` from the checked-in canonical through-hole STEP fixture.

## Semantic PMI evidence

Tests check the existing Aetheris AP242 semantic PMI style:

* datum evidence: `SHAPE_ASPECT('firmament-datum:A'...)` and `PROPERTY_DEFINITION('datum:A:importedPart'...)`;
* diameter evidence: `SHAPE_DIMENSION_REPRESENTATION('diameter:importedPart.holeDiameter'...)` and `PROPERTY_DEFINITION('diameter:importedPart.holeDiameter'...)`.

The output is also re-imported geometrically and volume/topology markers are checked. There is no graphical PMI requirement; X2 does not add `DRAUGHTING_CALLOUT`, annotation planes, leaders, drawing views, or layout.

## Diagnostics

The imported topology target diagnostics added for this milestone are:

* `firmament-inline-step-unknown-body`
* `firmament-inline-step-unknown-face`
* `firmament-pmi-imported-target-not-face`
* `firmament-pmi-imported-target-requires-canonical-step`
* `firmament-pmi-invalid-imported-target`

Invalid fixtures cover unknown inline body, unknown canonical face/entity, and invalid diameter value. Non-canonical inline input continues to reject at the existing canonical gate.

## Deferred features

Deferred: recognized regions, semantic replacement, residual body accounting, robust geometric signatures, arbitrary vendor STEP references, edge/vertex imported topology references, strong AP242 topology association between PMI aspects and exact exported face entities, graphical PMI, drawing views, annotation layout, leader lines, feature control frames, and DisplayIR/frontend PMI.

Cylindrical-face targeting is supported as a resolved imported canonical face with a diameter value. Strong cylindrical face type checking is intentionally deferred until the imported topology map carries durable face surface typing rather than only `ADVANCED_FACE` identity.
