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

## A semicircular meridian

**Complete file:** this example selects a circular span with named endpoints,
closes it along the axis, and revolves that Profile into a sphere. It uses the
ordinary Mechanical schema; there is no `schema Revolve`.

```firmament
schema Mechanical

Model RevolvedSphere {
  units: mm
  Axis mainAxis { origin: [0mm, 0mm, 0mm]; direction: [0, 1, 0] }
  Point2 south { position: [0mm, -20mm] }
  Point2 north { position: [0mm, 20mm] }
  Point2 center { position: [0mm, 0mm] }
  Concept Circle2 meridian { center: center; radius: 20mm }
  Line2 diameter { from: north; to: south }
  Profile sphereSection {
    meridian from south to north as arc
    |> diameter as axisClosure
    |> Close
  }
  Revolve ball { profile: sphereSection; about: mainAxis; angle: full }
}
```

Pipeline endpoint links prefer `from`/`to`, with `From`/`To` retained as aliases.
See [Writing Firmament](syntax.md) for the distinction from fields such as `from:`.

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

## Limits

Revolve materializes a standalone base solid from one validated outer line/arc loop. A one-sided line/arc Profile may produce exact plane, cylinder, cone, sphere, or torus carriers. Full revolutions use periodic seams; partial revolutions have stable `Start` and `End` closure faces. Segment descendants use `RevolvedSpan:<segment>` identity in the construction plan.

Profiles that cross the axis are rejected. A semicircle closed by its diameter produces an exact sphere. A full turn also admits a flat-ended, piecewise-linear solid meridian: exactly one straight axis closure, two radial cap segments, and a strictly axially monotone outer chain with positive radii. Its axis closure creates no material face; the radial segments become planar disk caps, and the outer segments become exact cylinders or cones. The [solid fastener meridian](../../../fixtures/Regression/CodeNativeAssembly/solid-fastener-revolve.firmament) is a complete example. Negative full turns produce the same solid.

Axis-touching partial turns, pointed tips with zero terminal radius, curved outer spans outside the sphere case, radial shoulders, nonmonotone outer chains and Profile inner loops remain unsupported. They reject before export instead of creating degenerate topology. Add/Remove composition of Revolve products, multi-turn revolve, skew-axis projection, sheet/surface revolve, Thread, helix and arbitrary Boolean operations are not supported.

For a part naturally described by cylindrical and conical masses, prefer [primitive Compose](../reference/language/solids-and-features.md#coaxial-primitive-compose) and a named edge finish. Revolve is available when an authored meridian is the clearest design description; it is not a requirement for making a shaft or fastener.

Use `aetheris inspect part.firmament --json` to read `sweepRadians`, `sweepDegrees`, authored alias, direction, full/partial classification, axis, and Profile frame.
