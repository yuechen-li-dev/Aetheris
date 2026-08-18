# Generic profile modifications (M10)

M10 adds `ProfileDelta` as the semantic substrate below user-programmed profile
features. A delta is a bounded program in one named carrier's local `u/v` frame:

- `Level` names a persistent offset from the carrier.
- `Span` advances at the current named level.
- `Transition` changes level by `Diagonal`, zero-run `Step`, or radius-checked
  exact `Round` geometry. `Concave: true` selects the alternate bounded circular
  solution for an inward/re-entrant profile corner; omission retains the default
  convex solution.
- `Anchor` places the bounded program with `FromStart`, `FromEnd`, or `CenteredAt`.
- `Expose` publishes a named span with explicit capabilities such as
  `FlangeAttachable`.

The exact line descendants are derived by the resolver. Authors do not sequence
anonymous global points or SVG-style commands. Programs must return to the carrier;
impossible levels, open programs, duplicate identities, invalid transitions, corner
consumption conflicts, and overlapping delta domains are rejected before BRep
materialization.

Ordinary typed Firmament templates specialize `ProfileDelta`:

```firmament
Template < P: RecessSpec, Owner: ProfilePath >
ProfileDelta Recess {
    On: Owner;
    Anchor: CenteredAt P.Center;
    Side: Inward;
    Level Carrier { Offset: 0mm; }
    Level Deep { Offset: P.Depth; }
    Transition Enter { Kind: Diagonal; Run: P.Lead; To: Deep; }
    Span Floor { Run: P.Width; At: Deep; }
    Transition Exit { Kind: Diagonal; Run: P.Lead; To: Carrier; }
}

ProfileDelta MyUnknownFeature = Recess < P: Chosen, Owner: Plate.Bottom >
```

`ProfilePath` is a typed template value, not a macro string. Template expansion
still occurs in the existing Firmament specialization pass. The kernel dispatches
only on `ProfileDelta`, levels, spans, and transitions; it contains no branch for
`Recess`, `Tab`, the specialization name, or CTC-03.

`Use Profile.Modifications;` loads the embedded generic `Recess<T>` and `Tab<T>`
library. User-authored templates use the same mechanism and can introduce names the
compiler has never seen. The canonical ordinary-extrusion dogfood is
`fixtures/Canonical/valid/profile-template-delta-extrusion.firmament`;
the Sheet Metal library dogfood is
`fixtures/SheetMetal/m10-profile-delta-recess.firmament`.

Sheet Metal consumes the same resolved delta on a flange `Outer` carrier. Delta,
level, member, exposed attachment, formed-region, and flat-region paths retain stable
correspondence. A child flange may consume an exposed `FlangeAttachable` land; because
that land is already physical owner-boundary geometry, no second release/removal is
created.
