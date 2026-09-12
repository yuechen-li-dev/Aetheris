# Geometric spans

`Span<T>` is a named, bounded, oriented view of existing parameterized geometry. It preserves its parent's semantic identity and does not copy geometry.

The X1 curve lane admits `Span<Line>`, `Span<Arc>`, and `Span<Curve>`. It uses named endpoints, so the bounds remain attached to the parent guide rather than to newly authored helper geometry.

```firmament
Span<Line> MountingEdge {
    On: Stock.Bottom
    From: MountLeft
    To: MountRight
}
```

Use the resulting name wherever a normal pipeline guide is accepted. `Orientation: Reverse` reverses traversal; it does not change the bounded domain. Curve inspection reports the parent, endpoint domain, orientation, length, and provenance through `aetheris inspect-spans part.firmament --json`.

`Span<Plane>` represents a bounded semantic region on an existing planar support. It preserves the parent support identity and constrains where dependent features may apply. Its closed, non-self-intersecting boundary is an ordinary named Profile evaluated in parent-local 2D coordinates.

```firmament
Span<Plane> MountArea {
    On: MountPlane
    Boundary: MountBoundary
}

Hole<Counterbore> MountHole {
    On: MountArea
    Center: [16mm, 9mm]
    Diameter: 8mm
    CounterboreDiameter: 16mm
    CounterboreDepth: 3mm
    End: ThroughAll
}
```

`MountPlane` must be a named `Construction Plane`; a span never finds a nearby BRep face to replace a missing parent. `Hole`, `Boss`, and `Pocket` accept a planar Span on the current prismatic top support. Hole containment checks the complete shaft/counterbore disk, and Boss/Pocket containment checks the actual Profile footprint. Pattern expansion validates every generated feature independently.

Points on the boundary are boundary-valid. Containment uses the same `1e-7 mm` analytic line/arc tolerance as the Profile arrangement engine. Inner Profile loops are excluded and contribute negatively to inspected area. Invalid or missing parents and boundaries fail; consumers never fall back to the whole plane.

A `Span<Plane>` is not automatically a BRep Face. It may describe a region inside one Face, and STEP export continues through the parent body's existing topology without splitting that face merely to carry a name. `inspect-spans --json` reports the boundary, area, inherited normal and frame, consumers, and validity. Feature inspection retains both the Span and its parent support.

The current FEA frontend requires an exact `BoundaryRegionCapability` backed by a continuum or BRep region, and current PMI records target admitted faces, holes, or recognized regions. Planar Span binding for those consumers is not yet qualified; neither silently expands a Span to its parent face.

The constraint-first ladder is:

```text
Concept<T>  what must be true
Span<T>     bounded subset where it is true
Feature     semantic transformation applied against those constraints
```

`Span<T>` is a built-in closed language type, not Template generic programming. Other types—including `Material`, `Body`, and `Feature`—are rejected during binding. A Span is contiguous; use separate named spans for separate intervals.
