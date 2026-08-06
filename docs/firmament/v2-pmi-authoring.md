# Firmament V2 PMI authoring

`Pmi` is a canonical top-level declaration. Its records normalize into the
same V2 PMI AST, binding, validation report, and AP242 export path as the
lowercase compatibility adapter. New source should use the PascalCase form.

```firmament
Pmi {
    Datum A { Target: face(+Z) }
    HoleDiameter MountDiameter {
        Target: Mount
        Value: 8mm
        Tolerance: PlusMinus(0.05mm, 0.02mm)
        DatumRefs: [A]
    }
}
```

`Target` names a semantic hole feature or an admitted face selector; it is not
a raw topology identifier. `HoleDiameter` accepts `Value` (or the existing
`Dimension` compatibility form), and its tolerance may be `PlusMinus(plus,
minus)`. `DatumRefs` is the canonical single-datum reference spelling for the
currently supported dimensional callout route.

Unknown kinds, targets, and fields are fatal typed diagnostics. In particular,
use `Tolerance`, `DatumRefs`, `Target`, and `Value` exactly; misspellings are
not ignored. See
`fixtures/FirmamentV2/Canonical/valid/box-hole-pmi.firmament` for an
exportable example.

Profile/Compose uses this exact same normalized PMI route. A `Datum` may target
the composed host's admitted `face(+Z)` selector, and `HoleDiameter` may target
a composed `Hole<Shaft>` or `Hole<Counterbore>` by feature name. For a
Counterbore, `HoleDiameter` means the shaft diameter; there is no separate
counterbore-diameter PMI kind. See
`fixtures/FirmamentV2/Canonical/valid/profile-compose-l-bracket-counterbore-pmi.firmament`
for the complete accepted example. Datum and HoleDiameter are the currently
supported composed-host PMI kinds.
