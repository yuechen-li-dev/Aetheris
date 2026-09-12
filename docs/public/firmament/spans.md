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

`Span<Plane>` is the bounded surface lane. Its boundary is a named Profile and it remains a semantic view: X1 does not create an independent face, infer a boundary from BRep edges, accept holes, or support arbitrary trimmed/freeform surfaces.

```firmament
Span<Plane> MountArea {
    On: MountPlane
    Boundary: MountBoundary
}
```

`MountPlane` must be a named `Construction Plane`; a span never finds a nearby BRep face to replace a missing parent.

`Span<T>` is a built-in closed language type, not Template generic programming. Other types—including `Material`, `Body`, and `Feature`—are rejected during binding. A Span is contiguous; use separate named spans for separate intervals.
