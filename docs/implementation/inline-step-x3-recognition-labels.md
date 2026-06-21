# INLINE-STEP-X3 recognition labels

INLINE-STEP-X3 adds metadata-only recognition labels under a canonical inline STEP body. A label says that imported faces probably represent a semantic candidate region; it does not replace geometry, suppress regions, compute residual bodies, or perform automatic recognition.

## Syntax

```firmament
recognize importedPart {
    region topFace {
        kind: datumPlane
        faces: ["#40"]
        confidence: high
    }

    region mountHole {
        kind: holeShaft
        faces: ["#191"]
        confidence: high
    }
}
```

PMI can target labels with `importedPart.region("name")`.

## Supported scope

- Canonical Aetheris AP242 inline STEP only; arbitrary vendor STEP remains rejected.
- Face-only topology references through the existing imported `ADVANCED_FACE` entity map.
- Recognition kinds: `datumPlane` and `holeShaft` (`hole<shaft>` normalizes to `holeShaft`).
- Confidence values: `low`, `medium`, `high`, `certain`; omitted confidence defaults to `medium`.
- Datum PMI accepts `datumPlane`; diameter PMI accepts `holeShaft`.

## Fixtures and AP242 evidence

- `fixtures/FirmamentV2/InlineStep/valid/inline-step-v2-recognized-face-datum-pmi-emits-in-step.valid.firmfixture` checks semantic datum evidence such as `SHAPE_ASPECT('firmament-datum:A')` and `PROPERTY_DEFINITION('datum:A:importedPart')`.
- `fixtures/FirmamentV2/InlineStep/valid/inline-step-v2-recognized-hole-diameter-pmi-emits-in-step.valid.firmfixture` checks semantic diameter evidence such as `SHAPE_DIMENSION_REPRESENTATION('diameter:importedPart.mountHoleDiameter')` and `PROPERTY_DEFINITION('diameter:importedPart.mountHoleDiameter')`.

No graphical PMI markers, leader lines, drawing views, or annotation layout are required.

## Deferred

Automatic recognition, semantic replacement, residual accounting, edge/vertex regions, robust geometric signatures, hybrid body emission, arbitrary vendor STEP references, and graphical PMI remain deferred.
