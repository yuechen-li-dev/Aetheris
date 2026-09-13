# Revolve

`Revolve` sweeps one bounded 2D `Profile` around an explicit semantic `Axis` through a bounded angle. Use it when the engineering intent is “take this section and turn it around this axis.” It is a base construction, not public Boolean authoring.

```firmament
Axis MainAxis {
    Origin: [0mm, 0mm, 0mm]
    Direction: [0, 1, 0]
}

Rect2 Section { Center: [15mm, 0mm]; Size: [10mm, 20mm] }
Profile RingSection { Loop Outer { Section |> TraceLoop } }

Revolve Ring {
    Profile: RingSection
    About: MainAxis
    Angle: full
}
```

The axis must preexist, have a finite nonzero direction, and lie in the Profile construction plane. The Profile frame fixes the zero-angle radial direction; partial revolutions do not use a hidden world-axis orientation. Arbitrary 3D axes are supported when they are coplanar with the Profile.

## Angles

Radians are dimensionless numeric values. Degree literals use `deg`. The lowercase semantic aliases `quarter`, `half`, and `full` mean π/2, π, and 2π. All forms lower through one angle path.

```firmament
Angle: 1.5707963267948966
Angle: 90deg
Angle: quarter
Angle: half
Angle: full
Angle: -90deg
```

The safe domain is `0 < |Angle| <= full`. A negative angle reverses direction. Zero and multiple turns are compile-time errors.

## X1 support boundary

X1 materializes a standalone base solid from one validated outer line/arc loop. A one-sided line/arc Profile may produce exact plane, cylinder, cone, sphere, or torus carriers. Full revolutions use periodic seams; partial revolutions have stable `Start` and `End` closure faces. Segment descendants use `RevolvedSpan:<segment>` identity in the construction plan.

Profiles that cross the axis are rejected. A semicircle closed by its diameter is the admitted axis-touching sphere case. Other axis-touching loops and Profile inner loops are explicitly deferred rather than sent to degenerate topology. Add/Remove composition, multi-turn revolve, skew-axis projection, sheet/surface revolve, Thread, helix, and arbitrary Boolean operations are not X1 capabilities.

Use `aetheris inspect part.firmament --json` to read `sweepRadians`, `sweepDegrees`, authored alias, direction, full/partial classification, axis, and Profile frame.
