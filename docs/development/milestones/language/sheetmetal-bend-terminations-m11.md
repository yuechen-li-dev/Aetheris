# Sheet Metal bend termination

A profile corner is where two segments of one planar material boundary meet. A
Sheet Metal corner/relief resolves adjacent flange material. A bend termination
is different: it is the stable start or end of a finite bend/root relative to a
neighboring physical boundary. Sheet Metal owns that engineering reason even
though its exact fabrication result is represented by profile contour machinery.

Each authored flange bend exposes `<Flange>Bend.StartTermination` and
`<Flange>Bend.EndTermination` when declared. Treatments are `Natural`, `Trimmed`,
`Rounded`, and `Auto`.

```firmament
Flange Wall {
  From: Deck.Front;
  Height: 16mm;
  Angle: 90deg;
  Radius: 2mm;
  StartTermination: Rounded;
  StartTerminationRadius: 2mm;
  EndTermination: Trimmed;
  EndTerminationSetback: 1mm;
  EndTerminationDepth: 1mm;
}
```

`Natural` keeps the raw finite root. `Trimmed` consumes the declared bounded root
setback. `Rounded` retains an analytic arc in the adjacent exact profile.
`Auto` is deliberately narrow: it resolves only a known bend endpoint, selects
the bounded rounded construction from thickness/radius/available-root constraints,
and refuses when it cannot fit. It never scans or optimizes unrelated geometry.

The lowering is:

```text
SheetBendIr
  ├─ StartTermination: SheetBendTerminationIr
  └─ EndTermination:   SheetBendTerminationIr
          ↓ treatment resolution
  SemanticProfileDeltaIr (stable levels/members/provenance)
          ↓ bounded root/profile adapter
  exact planar contour + exact flat blank
          ↓ refold/materialize
  formed analytic BRep
```

A manually authored `CornerProfile` at the same root conflicts before BRep
construction. A manually authored root `ProfileDelta` is likewise rejected with
both semantic owners named; root ProfileDelta ownership is reserved for bend
termination lowering. Outer-edge ProfileDelta programs remain composable because
they occupy a distinct physical edge.

No `JudgmentEngine` call is used for explicit treatment. Current `Auto` has one
admissible construction family after hard constraints, so deterministic policy is
clearer than utility scoring. Judgment becomes appropriate only when two or more
genuinely valid manufacturing families are implemented.
