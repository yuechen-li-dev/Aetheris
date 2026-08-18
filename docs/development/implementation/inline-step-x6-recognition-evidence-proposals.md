# INLINE-STEP-X6 recognition evidence and semantic proposals

INLINE-STEP-X6 extends Firmament V2 inline STEP recognition regions with metadata-only evidence and semantic proposal records.

## Chosen syntax

The parser accepts optional nested blocks inside an existing recognized region:

```firmament
recognize importedPart {
    region mountHole {
        kind: hole<shaft>
        faces: ["#191"]
        confidence: high

        evidence {
            surfaceFamily: cylindrical
            radius: 1mm
            axis: +Z
            through: true
        }

        proposes hole<shaft> {
            on: importedPart.face("#51")
            center: [0mm, 0mm]
            radius: 1mm
            end: throughAll
        }
    }
}
```

`hole<shaft>` is normalized to the existing internal `holeShaft` label.

## Model additions

`FirmamentV2RecognizedRegion` now carries optional `Evidence` and `Proposal` records. Evidence stores known surface families plus optional radius, axis, center, through flag, and notes. Proposal stores the normalized proposal kind, feature name, optional placement target, center, radius, and end condition.

## Validation now

Evidence validation is intentionally bounded:

- radius must be positive when present;
- axis must be one of `+X`, `-X`, `+Y`, `-Y`, `+Z`, or `-Z` when present;
- surface family must be known (`cylindrical` or `planar` currently);
- `through` is parsed as a boolean.

Proposal validation for X6 is limited to `hole<shaft>`:

- proposal kind must match the recognized region kind;
- proposal radius must be positive when present;
- `on` must resolve to an imported canonical STEP face when provided;
- end condition must be `throughAll` when provided.

## Metadata-only relationship to replacement

Evidence and proposals do not mutate geometry and do not create `replace` declarations. A proposal becomes construction authority only in a later explicit replacement path that verifies and emits replacement geometry.

## Migration report changes

`InlineStepRecognizedAccounting` includes:

- `EvidenceCount`;
- `ProposalCount`;
- `ProposalVerifiedCount` (always `0` in X6);
- `ProposalUnverifiedCount`.

X6 counts proposal presence but does not claim replacement verification.

## Fixture

The recognized-only fixture is:

`fixtures/InlineStep/valid/inline-step-v2-recognized-hole-proposal-report.valid.firmfixture`

It imports the canonical through-hole STEP body, recognizes the cylindrical wall face, attaches cylindrical/radius/axis/through evidence, proposes a future `hole<shaft>`, attaches diameter PMI to the recognized region, and re-exports canonical AP242 without replacement.

## Example report excerpt

Expected recognized-only accounting:

```text
recognized.regionCount = 1
recognized.evidenceCount = 1
recognized.proposalCount = 1
recognized.proposalVerifiedCount = 0
replacements.plannedCount = 0
replacements.emittedCount = 0
residual.residualFaceCount = original.faceCount
emissionStrategy = canonical-reexport
residualSurgery = false
```

## Deferred features

- automatic recognition;
- proposal scoring/JudgmentEngine selection;
- proposal-to-replacement automation;
- robust geometric signatures;
- multiple proposals per region;
- edge/vertex evidence;
- counterbore/countersink/fillet/slot proposals.


## Forward link

INLINE-STEP-X7 adds review-only proposal-to-replacement assist for these X6 proposal records; see `docs/development/implementation/inline-step-x7-proposal-replacement-assist.md`.
