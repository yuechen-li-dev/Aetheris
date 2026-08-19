# INLINE-STEP-X7 proposal-to-replacement assist

INLINE-STEP-X7 adds review-only proposal-to-replacement assist for Firmament V2 inline STEP recognized regions. It evaluates semantic proposals recorded on recognized regions and reports whether a proposal is compatible with the current explicit INLINE-STEP-X4 replacement path.

The assist is not automatic replacement: it does not mutate geometry, create `replace` declarations, execute replacement, perform residual topology surgery, or claim full decompilation.

## Supported proposal kind

X7 supports exactly one proposal family:

- recognized region kind: `hole<shaft>` / normalized `holeShaft`
- proposal kind: `hole<shaft>` / normalized `holeShaft`
- end condition: `throughAll`

## Compatibility checks

A proposal is replacement-ready only when:

1. the recognized region has a semantic proposal;
2. the recognized region kind matches the proposal kind;
3. the proposal kind is `holeShaft`;
4. the proposal placement target resolves to an imported STEP face;
5. the proposal radius is positive and finite;
6. the end condition is `throughAll`;
7. center, radius, target, feature name, and end condition are enough to print X4 syntax;
8. evidence radius matches proposal radius within `1e-6` when both are present;
9. evidence `through: false` blocks `throughAll` assist;
10. evidence surface families must include `cylindrical` when supplied.

The current assist intentionally stays at metadata/topology-reference level; broader geometric proof and multi-proposal scoring are deferred.

## Report model and build JSON

`InlineStepReplacementAssistReport` contains `assists`, `readyCount`, and `blockedCount`. Each assist records `bodyName`, `regionName`, `proposalKind`, `replacementReady`, optional `suggestedReplacementText`, optional `suggestedReplacementModel`, deterministic `reasons`, and `diagnostics`.

`aetheris build --json` now includes `inlineStepReplacementAssist` next to `inlineStepMigration`. The migration recognized accounting also exposes `proposalAssistReadyCount` and `proposalAssistBlockedCount`; these are separate from replacement planned/verified/emitted counts.

## Suggested replacement text

Ready assists produce stable X4-compatible text:

```firmament
replace importedPart.region("mountHole") with hole<shaft> mountHole {
    on: importedPart.face("#51")
    center: [0mm, 0mm]
    radius: 1mm
    end: throughAll
}
```

This text is review support only. The source document remains recognized/proposed-only unless a human adds an explicit `replace` declaration.

## Fixture and geometry proof

The primary fixture is:

```text
fixtures/Regression/InlineStep/valid/inline-step-v2-recognized-hole-proposal-report.valid.firmfixture
```

For that fixture, build JSON reports one ready assist and zero blocked assists, while replacement planned/verified/emitted counts remain zero. The emitted STEP remains the canonical inline STEP re-export; AP242 analyze volume remains `461.15044407846125`, proving the assist did not mutate geometry.

The X4 replacement fixture remains the regression for actual replacement execution and bounded rebuild verification.

## Relationship to X4

X4 defines the explicit bounded replacement syntax and execution path. X7 only determines whether a recognized-region proposal has enough compatible metadata to suggest that syntax. Replacement authority still comes only from an explicit `replace` declaration.

## Deferred features

- automatic replacement;
- multi-proposal ranking;
- JudgmentEngine scoring;
- counterbore, countersink, fillet, slot, and pocket proposals;
- true residual surgery;
- raw vendor STEP inline support;
- graphical PMI and frontend changes;
- Forge concept registry integration.
