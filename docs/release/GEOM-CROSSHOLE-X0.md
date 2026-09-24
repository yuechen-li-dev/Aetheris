# GEOM-CROSSHOLE-X0 — qualification record

**Executive verdict: Blocked (honest stop).** Aetheris does not currently construct an exact diametral cylindrical cross-hole through solid or Hollow cylindrical stock. The supplied clevis pin is a valid reference, but importing it is not an authored reconstruction. No CrossHole feature, ClevisPin template, barrel-vent fixture, or STEP export is claimed by this record. A subsequent [intersection-curve qualification](GEOM-INTERSECTION-CURVE-X0.md) added exact curve authority and a certified finite 3D representation; BRep cutting and STEP qualification remain open.

## Supported case requested

The intended X0 primitive is a through-diameter cut by an analytic cylinder whose axis passes through and is perpendicular to the host cylinder axis. An admitted host needs an analytic axis, radius and axial bounds; a Hollow host also needs an analytic inner radius. The hole radius must be positive and smaller than the relevant host radius, with enough axial clearance from each end. Eccentric, tangent, oversize, end-intersecting and oblique placements require explicit diagnostics. Multiple radial holes may be admitted only after the one-hole topology and seams work.

## Exact intersection and topology requirement

For a host of radius `R` about `Z`, a perpendicular cutter of radius `r` about `X`, and hole center `z0`, the two surfaces meet where

```text
x² + y² = R²
y² + (z - z0)² = r²
```

One exact parameterization of each side of the outer host wall is

```text
y(t) = r cos(t)
z(t) = z0 + r sin(t)
x(t) = ±sqrt(R² - r² cos²(t)),  0 ≤ t < 2π
```

The host pcurve is `(atan2(y,x), z)` in a compatible cylinder frame; the cutter pcurve follows from its own angular frame and `x(t)`. Both must preserve the same 3D edge, sense and periodic equivalence at seams. A Hollow tube additionally intersects the cutter with its inner cylinder, yielding separate inner and outer opening loops on both sides of the diameter. These are equations for the required construction, **not implemented BRep edges**.

This unequal-radius intersection is not a line, circle, ellipse, hyperbola or finite polynomial B-spline segment. The reference has `R = 0.79375 mm` and `r = 0.5953125 mm`. Fitting sampled points to a spline, or tessellating the loops, would not meet the requested exactness. STEP's `intersection_curve` describes an intersection associated with two surfaces but still requires a `curve_3d` representation; naming that entity alone does not fill the missing exact curve representation. See the [ISO 10303-42 geometry schema](https://ap238.org/SMRL_v8_final/data/resource_docs/geometric_and_topological_representation/sys/4_schema.htm) and the [AP242 entity description](https://www.steptools.com/stds/stp_aim/html/t_intersection_curve.html).

## Current Aetheris boundary

`BrepBooleanSafeCompositionGraphValidator.TryValidateCylinderRootSubtract` admits only world-Z aligned root and tool cylinders, reporting `BrepBoolean.AnalyticHole.AxisNotAligned` for a transverse tool. `CurveGeometry` can now retain exact cylinder-intersection authority beside a certified finite B-spline, while `PcurveGeometryKind` still has no analytic cross-cylinder relation or certified finite realization. There is no builder for the required cross-hole topology, and `Step242Exporter` has no qualified cross-hole edge to serialize. The existing cylindrical Perforation lowering therefore reports `perforation-cylindrical-topology-unavailable`, consistently with `docs/public/firmament/perforation.md`.

Making the bounded primitive real now requires integrating the exact intersection authority and its certified finite representation with BRep edges, finite pcurves on both analytic surfaces, seam-aware topology validation, STEP export/import and display tessellation. The current route cannot meet the requested exactness by changing only the Boolean admission check. Adding an unqualified feature or wrapping sampled curves in analytic cylindrical faces would create an invalid success signal, so no production CrossHole path exists.

## Supplied clevis pin reference

`Aetheris.CLI analyze` imported the user-supplied `98306A316_Carbon Steel Clevis Pin.STEP` as one enclosed manifold with 19 faces, 48 edges and 30 vertices. Analytic face inspection gives these dimensions in the imported millimeter coordinates:

| Measurement | Reference |
| --- | ---: |
| Shank diameter | 1.5875 mm |
| Head diameter | 3.175 mm |
| Head thickness | 1.5875 mm |
| Overall axial length | 30.95625 mm |
| Tip-to-head underside | 29.36875 mm |
| Transverse hole diameter | 1.190625 mm |
| Hole center from shank tip | 1.7859375 mm |

The reference also has 4 B-spline faces and 20 B-spline curves; its source topology is not an exact-construction substitute. The CLI reports millimeters but marks its STEP unit basis as assumed, because import does not yet preserve the source unit declaration. The STEP file declares inch units; the converted dimensions above are consistent with that declaration. These measurements would be suitable parameters for a **McMaster clevis pin witness**, but do not establish coverage of a clevis-pin standard family.

Reproduce the read-only inspection with:

```text
dotnet run --project Aetheris.CLI -- analyze "<path to supplied clevis-pin STEP>" --json
```

## Qualification status

There is no authored solid cross-hole, Hollow cross-hole, ClevisPin template, cylindrical Perforation witness, or cross-hole STEP round trip to test. The 1/8/32-hole performance measurements are consequently unavailable. Planar Perforation remains separately qualified in `GEOM-PERFORATION-X0.md`; that qualification does not imply cylindrical support. A future acceptance run must verify exact curves and pcurves on both surfaces, deterministic seam crossings, manifold topology, semantic wall/source identity, solid and Hollow cases, controlled invalid-placement diagnostics, production STEP reimport, and a dimensional comparison to this reference.

On the 2026-09-23 local Windows .NET 10 run, `dotnet build Aetheris.slnx --no-restore -m:1` passed with seven WebAssembly warnings. The serial `dotnet test Aetheris.slnx --no-build --no-restore -m:1 --verbosity quiet` run passed every project suite except one existing kernel tessellation assertion. `SurfaceMeshIrTests.ThroughHolePlate_UsesMultiLoopPlanarBands_AndLowersToWatertightMesh` expected 90 planar cells and got 87; the isolated test reproduced the same failure. This was present in the prior `GEOM-PERFORATION-X0` qualification and does not exercise a cross-hole implementation. The full solution test result is therefore **not green**.
