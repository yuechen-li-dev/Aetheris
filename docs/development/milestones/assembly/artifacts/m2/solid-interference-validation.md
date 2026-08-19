# Assembly solid-interference validation

The original `fixtures/Canonical/Assembly/template-block-pair.firmament` seated two top faces together. Its 20 × 20 × 10 mm Fixed solid occupied world Z `[0, 10]`; its 12 × 12 × 15 mm Moving solid was translated to world Z `[-5, 10]`. The solids therefore shared positive volume from Z `0` through `10`, and their coplanar top faces produced visible depth-buffer fighting in Cadmata.

The fixture now exposes both `Seat = Bounds.Face(+Z)` and `Base = Bounds.Face(-Z)`. `Moving.Base` is coincident with `Fixed.Seat`; the 12 × 12 × 8 mm Moving solid occupies world Z `[10, 18]`. The parts have legal zero-volume face contact and a positive 2 mm dimensional transition.

Compilation now checks resolved occurrence BReps after exact materialization. The admitted proof lane:

1. uses exact vertex bounds only to reject disjoint or touching-only pairs;
2. requires closed convex planar bodies;
3. orients each exact face plane toward the solid interior;
4. intersects the combined half-spaces;
5. requires a full-dimensional interior witness and a positive-volume contained tetrahedron.

Reverting the fixture to top-to-top seating produces a fatal diagnostic before Cadmata:

```text
assembly-solid-volume-interference:
Part occurrences 'TemplateBlockPair.Fixed' and 'TemplateBlockPair.Moving'
occupy overlapping solid volume. The assembly is physically invalid and cannot
be materialized. Face/edge contact is allowed, but positive-volume overlap is not.
```

The check never treats AABB overlap as collision proof. Curved or non-convex pairs outside this bounded exact lane remain unsupported rather than receiving a false collision claim. Core tests cover positive-volume boxes, face contact, and unsupported curved geometry; Firmament and CLI tests exercise the real post-materialization failure path.
