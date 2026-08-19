# INLINE-STEP-X4 — first bounded semantic replacement

INLINE-STEP-X4 adds the first bounded semantic replacement proof for canonical InlineStep: one recognized imported through-hole region can be claimed by a semantic `hole<shaft>` replacement and exported through the real AP242 exporter.

## Syntax

The bounded syntax is:

```firmament
replace importedPart.region("mountHole") with hole<shaft> mountHole {
    on: importedPart.face("#191")
    center: [0, 0]
    radius: 1
    end: throughAll
    hostSize: [10, 8, 6]
}
```

The replacement target must be an existing `recognize importedPart { region mountHole { kind: holeShaft ... } }` label. X4 does not allow replacing arbitrary face lists.

## Scope and strategy

This is through-hole-only and canonical-input-only. The implemented emission strategy is **Strategy B: bounded rebuild for a simple host**. The imported body is verified as the known simple 10 x 8 x 6 box-with-through-hole class, then rebuilt as a semantic box plus `hole<shaft>` and emitted via `Step242Exporter`.

This is intentionally not full decompilation: no automatic recognition, no raw vendor STEP support, no general residual body surgery, no multiple replacements, and no overlap resolution.

## Fixture

Fixture: `fixtures/Regression/InlineStep/valid/inline-step-v2-replace-through-hole-step-verified.valid.firmfixture`.

Input canonical STEP: `testdata/firmament/inline-step/canonical-through-hole.step`.

Expected volume: `480 - pi * 1^2 * 6 = 461.15044407846125`.

## Verification checks

X4 verifies:

- the inline body exists and is Aetheris-canonical STEP;
- the replacement targets an existing recognized region;
- the recognized region kind is compatible with `hole<shaft>`;
- recognition face references and placement face references resolve through `ImportedStepTopologyMap`;
- radius is positive;
- end condition is `throughAll`;
- the imported canonical body contains exactly one cylindrical face matching the replacement radius;
- rebuilt AP242 output is emitted through the real exporter and can be reimported by existing tests/CLI checks;
- output volume matches the simple fixture formula and does not duplicate the hole.

## Limitations and deferred work

Deferred: automatic recognition, residual body surgery/suppression, multiple replacements, counterbore/countersink/blind replacements, fillet/chamfer/slot replacement, robust geometric signatures beyond this fixture class, graphical PMI, arbitrary vendor STEP, and full decompilation.


## Forward link

INLINE-STEP-X5 adds residual accounting and migration progress metrics for this bounded rebuild path. See `docs/development/implementation/inline-step-x5-residual-accounting.md`.
