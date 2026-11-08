# Bracket corner finishes and mounting holes

Use full-length longitudinal edge finishes to round an inside bend or break
outside corners on a profile-extruded bracket, together with separated mounting
holes. Each finish names one original straight-sided profile corner.

The complete [finished mounting bracket](../../../fixtures/Regression/OrientedComposeHoles/bracket-finished-mounts.firmament)
builds a 30 × 40 × 3 mm L section, extruded 20 mm on XZ. It has a 2 mm inside
fillet, a 0.5 mm outside chamfer, a 0.5 mm outside fillet, a counterbored foot
mount and a countersunk upright mount. Build that file with `aetheris build`.

The corner requests in that example are:

```text
EdgeFinish Bend {
  Face: Longitudinal
  Target: section.Outer.Vertex3_Vertex4.Start
  Kind: Fillet
  Radius: 2mm
}
EdgeFinish FootTop {
  Face: Longitudinal
  Target: section.Outer.Vertex2_Vertex3.Start
  Kind: Chamfer
  Distance: 0.5mm
}
```

Place them inside `Modify bracket` as in the complete fixture. The compiler
replaces the named profile corner with an exact tangent arc or a straight bevel,
then extrudes it through the full width. Inside finishes add material; outside
finishes remove it. Different original corners may use different finishes.

`Radius` for Fillet and `Distance` for Chamfer must be at least 0.01 mm. At least
0.01 mm must remain on each adjacent straight leg and between a new boundary
and other boundaries or finishes. Repeating a finish on the same corner,
consuming a shared edge, and intersecting or tangent boundaries are rejected.

Source-side through shafts, counterbores and countersinks may share this single
profile host. Hole coordinates remain in the untrimmed construction face frame;
changing a corner does not relocate an existing mounting hole. Admission checks
the complete cavity envelope against the remaining planar supports, other
cavities and intervening faces. Move the hole or reduce the finish when the
required 0.01 mm clearance is unavailable. The conservative finite corridor
proof can refuse interactions that need a more general intersection planner.

This route requires one polygonal outer profile extrusion. It does not admit
inner profile loops, curved original corners, multiple Base/Add/Remove regions,
imported bodies or mixed end-face and longitudinal finishes. Full-edge cap
fillets and tangent-chain selection need different planners. In Helios, select
one edge in Iso/Edges, inspect the automatic gold preview, and Apply; a refused
preview preserves the accepted part and source.

See [profile boundary edits](profile-boundary-edits.md) and the
[language reference](../reference/language/README.md).
