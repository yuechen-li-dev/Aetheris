# PMI constraint projection (X1)

Preview 1 keeps engineering intent in one authoritative place: Concept/static facts declare it, `Require` validates it against a semantic feature, and `Pmi` explicitly projects the successfully validated constraint into AP242.

```firmament
Require MountDiameterConstraint {
    Actual: Mount.Diameter
    Expected: 8mm
    Tolerance: PlusMinus(0.05mm, 0.02mm)
}
Pmi {
    Datum A { Target: face(+Z) }
    HoleDiameter MountDiameterCallout {
        From: MountDiameterConstraint
        As: HoleDiameter
        DatumRefs: [A]
    }
}
```

`From` is a named, dimensional `Require`; `As` is required and currently admits only `HoleDiameter`. The target (`Mount`), nominal, and tolerance come from the constraint. A projected callout must not specify `Target`, `Value`, `Diameter`, `Dimension`, or `Tolerance`; this is rejected as `firmament-v2-pmi-projected-field-must-not-override-source-constraint`.

The Require normalizes to a semantic constraint with source identity, subject/property, nominal length, structural tolerance, validation status, source span, and expected-value provenance. It validates before PMI is available: a missing hole/property or an actual diameter outside its tolerance fails the Require and prevents export.

Projected PMI lowers to the existing canonical PMI record and the existing `BuildV2SemanticPmi` AP242 exporter. There is no second STEP path. Inspection JSON reports `projectionSource`, `sourceConstraintKind`, `sourceSubject`, `validationStatus`, and `provenance` on projected PMI.

Direct `Concept -> PMI` projection is deliberately deferred. Preview 1 projection requires a checked Require, so exported PMI is validated semantic intent rather than an independent annotation language.

Current limit: only `Mount.Diameter`-style semantic hole diameter equality and `HoleDiameter` projection are admitted. Other Require forms remain their existing boolean/static form.

## Documentation-only authoring check

A clean authoring pass used the example above plus a canonical `Record`/`Static` fact. First attempt succeeded with no syntax retries: one static nominal and tolerance drove the Hole, named Require, and projected PMI. Inspection showed the static member provenance and AP242 build succeeded. Deliberately adding `Value:` to the projected PMI produced `firmament-v2-pmi-projected-field-must-not-override-source-constraint`; using an unknown `From:` produced `firmament-v2-pmi-projection-unknown-require`. The bounded `As: HoleDiameter` rule is explicit in the example, so no PMI-kind inference or parser archaeology was required.
