# Semantic Profiles and exact planar contours

Firmament has two deliberately separate profile representations:

- **Semantic Profile MIR** records what an engineering profile means: stable named members, local frame, datums, bounded relationships, and provenance.
- **`PlanarContour2`** records the exact ordered curve topology that downstream geometry consumes.

Primitive curves are lowering details, not the default authoring identity. One semantic member may generate several exact curves. For example:

```firmament
Concept Path Outline {
    Start: Point2(0mm, 0mm)
    Heading: 0deg
    Span BottomLeft { Length: 44mm }
    Tab MountTab { Width: 12mm; Extension: 8mm; Side: Right }
    Span BottomRight { Length: 44mm }
    Span Right { Turn: 90deg; Length: 60mm }
    Span Top { Turn: 90deg; Length: 100mm }
    Close Left
}
Profile Plate From Outline
```

`Plate.MountTab` is the stable semantic identity. It lowers deterministically to three lines named as curve descendants, but callers address the tab rather than relying on curve ordinal. `Plate.MountTab.End` is a derived landmark. Inspection can show `Curve00`–`Curve02` for debugging without promoting those descendants to authoring identity.

## Bounded members

The M1 resolver supports:

| Member | Authored values | Exact lowering |
|---|---|---|
| `Span` | length and optional turn/heading | one line |
| `Arc` | radius and turn | one tangent circular arc |
| `Chamfer` | run, offset, side | one diagonal line |
| `Step` | run, rise, side | two connected lines |
| `Notch` | width, depth, side | three connected lines |
| `Cutback` | run, offset, side | one diagonal line |
| `Tab` | width, extension, side | three connected lines |
| `Close` | profile start | one closing line |

All measurements must be finite and positive. `Side` is `Left` or `Right` relative to the current local heading. Unsupported or contradictory input is rejected before exact contour construction, with the semantic member path in the diagnostic.

## Lowering and identity

```text
Firmament Concept Path / Profile
    -> SemanticProfileIr
    -> bounded constraint resolution and derived landmarks
    -> ResolvedSemanticProfileIr members and curve descendants
    -> ResolvedProfile2D
    -> PlanarContour2
    -> extrusion, composition, sheet metal, drawing projection
```

Each exact descendant carries its semantic member stable ID as provenance. Splitting or offsetting a `PlanarContour2` preserves that ancestry. Stable IDs and contour hashes depend only on normalized semantic input and resolved exact geometry, not BRep edge numbering.

This is intentionally not a general-purpose sketch solver. Relationships are admitted only when their resolution is direct and bounded. M1 includes required-member, equal-size, and paired mirror validation in the MIR API.

## Attached edge profiles

`EdgeProfile` modifies one directed semantic edge without restating its untouched
parts. The owner path supplies the local frame: `u` runs from the edge start to
its end, and positive `v` is the left side of that direction. `Side: Left` and
`Side: Right` select positive and negative `v` respectively.

```firmament
Rect2 PlateBase { Center: [0mm, 0mm]; Size: [100mm, 60mm] }
EdgeProfile PlateBase.Bottom {
    Notch CableNotch { FromStart: 18mm; Width: 8mm; Depth: 4mm; Side: Left }
    Tab MountTab { CenteredAt: 50mm; Width: 12mm; Extension: 6mm; Side: Right }
}
Profile Plate From PlateBase
Extrude Solid { Profile: Plate; From: 0mm; To: 3mm }
```

Each fragment must have exactly one anchor: `FromStart`, `FromEnd`, or
`CenteredAt`. The values are distances in the owner's local `u` coordinate.
Resolution sorts by the resulting interval, rejects overlaps and out-of-bounds
intervals with both semantic paths in the diagnostic, and inserts non-zero
`CarrierNN` members for every gap. Declaration order therefore has no geometric
effect when anchors fully determine placement.

The current bounded attached set is `Tab`, `Notch`, `Step`, `Chamfer`,
`Cutback`, and the multi-transition `SteppedNotch` used by Sheet Metal mounting
flanges. Tabs, notches, and bounded steps lower to three lines; chamfers and
cutbacks lower to two; a `SteppedNotch` lowers to five or seven lines depending
on whether it has an outer chamfer. All return to the owning carrier baseline, which keeps
replacement continuous and makes adjacency unambiguous. Endpoint-consuming
corner fragments and tangent arc attachments are not yet admitted.

Identity remains hierarchical through lowering:

```text
Plate.Bottom
  Carrier00                       generated untouched span
  CableNotch                     authored semantic fragment -> 3 curves
  Carrier01                       generated untouched span
  MountTab                       authored semantic fragment -> 3 curves
  Carrier02                       generated untouched span
```

`Plate.Bottom.MountTab` and its `Curve00` descendants are normal Concept Path
members. The owning edge, authored fragments, and generated carriers all retain
stable provenance in `ResolvedProfile2D` and `PlanarContour2`.
