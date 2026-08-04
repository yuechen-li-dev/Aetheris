# AIR-FILLET-LOCALIZED-M1 — exact localized tangent blend

This production route is deliberately narrow. It admits one finite, straight, convex
`SharedEdge(+X,+Z)` of a history-known axis-aligned rectangular `Box`, with two
orthogonal planar supports, a positive constant radius, and no neighbouring selected
edge or corner junction. It is not general fillet support.

Firmament Phase 3 expresses the Feature AIR intent with one `EdgeFinish`:

```firmament
Model Localized mm
Box Base { Size: [10mm, 8mm, 6mm] }
Modify Base {
    EdgeFinish RoundedEdge {
        Face: +X
        Target: SharedEdgePlusZ
        Kind: Fillet
        Distance: 1mm
    }
}
```

The source contains semantic face-pair selection, never emitted B-rep edge IDs. The
Feature AIR preserves the body/feature identities, constant radius, material side,
history provenance, and admission result.

## Construction and plan

`LocalizedTangentBlend` is the Construction AIR witness. It owns the two support
planes, selected finite edge, material side, radius, tangent points, two quarter-circle
profiles, cylinder axis, retained planar regions, and explicit endpoint ownership. Its
profile is the exact circle with centre `(maxX-R, y, maxZ-R)`, swept linearly along `Y`.
The replacement is therefore an exact `CylinderSurface`, not facets or a spline.

An authoritative `LocalizedTangentBlend` BRepPlan is constructed before emission. It
preserves the localized chamfer lane's 10 vertices, 15 edges, and 7 faces: six planes
and one cylinder. The two tangent rails and two end arcs bound the cylinder. The emitter
only consumes that immutable topology plan and construction witness; it does not mutate
an existing B-rep or call the legacy bounded-filleting implementation.

The common enforce-mode STEP preflight verifies closed ordered loops, shared coedge
continuity, non-degenerate trims, and trim samples (endpoints plus midpoint) on every
support plane and cylinder. The local route then exports AP242, reimports it, and checks
an enclosed manifold with six planes and one cylinder.

## Analytic evidence and limits

For a 90-degree radius `R` fillet, the removed cross-sectional area is
`R² - πR²/4`: the `R × R` sharp corner square minus the retained quarter-disc. Multiplying
by the selected edge length gives the corresponding volume removal. Tests cover
10×8×6 at R=1 and R=2, plus 12×5×7 at R=1; they also require deterministic STEP bytes,
reimported cylinder radius/axis, tangent endpoint coordinates, and typed failures for
zero/oversized radii, unsupported selection, and two-edge junctions.

Deferred: concave or imported bodies, different support pairs, edge chains, multiple
edges, endpoint/corner interactions, variable/asymmetric radius, and rolling-ball or
toroidal corner resolution. The recommended next milestone is a second straight edge
with an explicit junction Construction AIR witness and deterministic corner policy.
## Status note

This historical tangent-blend evidence is now implemented through the shared [AIR edge-finish consolidation M2](air/air-edge-finish-consolidation-m2.md) topology authority. The exact cylindrical quarter-arc geometry and existing compatibility report remain unchanged.
