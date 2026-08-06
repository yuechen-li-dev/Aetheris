# Firmament V2 Concept Path (M1)

`Concept Path` is the concise frontend for an ordered, contiguous local-2D chain of line and tangent-arc guides. It lowers to the same named points, guides, ordinary `Profile` loops, and `ResolvedProfile2D` route as `Rect2`, `Line2`, `Circle2`, and explicit `Segment` declarations.

It is a **Path**, not a Profile Loop: a Path may be open and has no opinion about closure, winding, material boundaries, or intersections. Those checks remain with `Profile`.

```firmament
Concept Path Outline {
    Start: Point2(-10mm, -5mm)
    Heading: 0deg
    Line South { Length: 20mm }
    Line East { Turn: 90deg; Length: 10mm }
    Line North { Heading: 180deg; Length: 20mm }
    Close West
}
Profile Plate From Outline
```

Coordinates and headings are local to the enclosing construction frame. `Heading` sets the initial or absolute local heading; `Turn` is relative to the current heading and happens before a line advances. A line with neither continues the current heading. `Line { To: Start }` emits a straight guide to the Path start, updates the heading to that line direction, and cannot be mixed with `Length`, `Turn`, or `Heading`. `Close West` is exactly that line-to-start sugar; it emits the ordinary named guide `Outline.West` and endpoint `Outline.West.End`.

Every step emits a guide and endpoint: `Outline.Start`, `Outline.South`, and `Outline.South.End` are all available to normal low-level profile segments.

```firmament
Arc InnerFillet { Radius: 8mm; Turn: 90deg }
```

An arc is an exact circular arc. Its entry point and tangent come from the current Path state; a positive turn is counterclockwise in the local XY frame and a negative turn is clockwise. Radius and turn must be nonzero; full-circle sweeps are not admitted in M1.

The profile shorthand creates one outer loop from every Path guide in declaration order. The expandable form permits holes:

```firmament
Profile Plate {
    Loop Outer From OuterOutline
    Loop Inner From InnerCutout
}
```

Use explicit `Segment { Trace; From; To }` when selecting part of a guide or when the desired boundary is not simply the complete Path. There is no `MoveTo`, branching, subpath, automatic reversal, `Arc To`, spline, or path-level geometry validation in M1.

Canonical examples are maintained under `fixtures/FirmamentV2/Canonical/valid/concept-path-*.firmament`.

Path steps become stable resolved Profile segment identities. For example, a `South` step in a Path consumed by `Profile Bracket From Outline` can be targeted later as `Bracket.Outer.South` in an M1 Profile-boundary chamfer; this remains source binding, not BRep-edge lookup.
