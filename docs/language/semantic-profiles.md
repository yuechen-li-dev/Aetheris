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
