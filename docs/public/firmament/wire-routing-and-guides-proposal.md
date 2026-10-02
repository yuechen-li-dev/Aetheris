# Wire routing and Concept guides

Status: [Start/Corner/End point routes](wire-point-routes.md), fixed public endpoint
bindings and a finite Concept Line3 Follow lane are implemented. See the
[qualified syntax and limits](source-owned-guitar-authoring.md). The broader
auto-routing, multi-step and arbitrary-guide contracts below remain proposals.

The intent is an ordered centerline contract: start here, visit these points,
follow this Concept curve interval, and terminate here. The compiler constructs
the connecting spans, checks their admissibility and lowers the accepted path
to ordinary WireForm geometry. Concepts remain erased design scaffolding.

## Suggested syntax

```firmament
Concept Struct HarnessLayout {
 Points<Point3> Anchors {
  Source: Point3(0mm,0mm,0mm);
  CableTie: Point3(30mm,0mm,0mm);
  Terminal: Point3(180mm,20mm,0mm);
 }
 Line3 Groove {
  From: Point3(60mm,20mm,0mm);
  To: Point3(160mm,20mm,0mm);
 }
}

WireRoute SignalLead {
 Diameter: 2mm;
 Material: Standard.Materials.StainlessSteel.304_Annealed;
 MinimumBendRadius: 10mm;
 Routing: Auto;

 Path {
  Start { At: HarnessLayout.Anchors.Source; }
  Via Tie { At: HarnessLayout.Anchors.CableTie; }
  Follow MainGroove {
   Curve: HarnessLayout.Groove;
   From: 0mm;
   Distance: 80mm;
   Direction: Forward;
  }
  End { At: HarnessLayout.Anchors.Terminal; }
 }
}
```

This illustrates the proposed contract, not a qualified route artifact. A Concept
line itself is sufficient as a guide; there is no separate Guide object to create.
`Follow` also accepts admitted Concept Curve2/Curve3 values. A Curve2 must have
an explicit construction plane/frame, using the existing `On` convention; its
local coordinates are lifted through that frame once. The current Concept Curve2
lane derives closed Profile boundaries; open guide curves need a deliberate
extension of that resolver, not an assumption that they already exist.

`From` is arc length from the curve's declared start, in its forward orientation.
`Distance` is positive traced arc length. Forward advances from `From`; Reverse
decreases it and reverses the tangent. For this line, the locked interval runs
from `[60mm,20mm,0mm]` to `[140mm,20mm,0mm]`. Both endpoint tangents are +X.
Omitting Direction means Forward. `Distance: Remaining` may be admitted as an
explicit extent alternative. Out-of-range intervals fail; closed-curve wrapping
is deferred rather than silently clamping or moving the seam.

Start/End may optionally specify Tangent. It always means travel direction along
the wire, including at End; adapters from terminal outward normals must make
that sign conversion explicit. Tangents at Via points are free unless authored.
The declared command order is preserved.

## Three different constraints

| Construct | Meaning |
| --- | --- |
| `Via Tie { At: P; }` | The realized centerline passes through P with a tangent-continuous join. A cable-tie center can be a hard point. |
| `Corner NutBreak { At: P; Radius: 4mm; }` | P is the virtual intersection of untrimmed legs. A circular fillet replaces it; the centerline generally does not pass through P. |
| `Follow Groove { Curve: C; From: S; Distance: L; }` | The whole oriented curve interval is locked, including its endpoint positions and tangents. Only the free spans may change. |

MinimumBendRadius is a centerline radius bound, not a command that every bend use
that exact radius. An authored Corner Radius must meet the minimum. Guide arcs
must also meet it. Lines have infinite curvature radius. A kink or cusp in a
locked guide is invalid; the compiler cannot fillet away part of that guide.
Authors can modify the Concept curve explicitly when smoothing is intended.

For the guitar, preserve the existing 4mm virtual NutBreak first. Its present
WireForm rounds a corner; replacing it with hard `Via` would change the shape.
The new Point2 crossings must be explicitly lifted using an actual nut string
plane published by the neck definition, rather than assuming NutMount denotes
the string height. Example proposed route input:

```firmament
Corner NutBreak {
 At: NutLayout.Crossings.String0;
 On: Guitar.Neck.StringPlane;
 Radius: 4mm;
}
```

`On` here is the proposed Point2-to-spatial route binding. Missing On is a
dimensional error. `StringPlane` is a proposed publication, not an existing port.
Tailpiece starts and tuner string-entry endpoints likewise need definition-owned
publications; routing should not reach into private component geometry.

## What is reusable today

- `Piping/OrthogonalAutoRouter.cs` provides deterministic bounded A*, conservative
  KeepOut inflation, endpoint directions, hard waypoints and local rerouting.
  It currently requires axis-aligned directions and produces 90-degree elbows.
  Its clearance checker and 1000mm elbow penalty are specifically piping policy.
- `Materializer/WireForm.cs` owns forming state, arbitrary-plane circular bends,
  exact cylinder/torus sweeps and conservative self-clearance validation.
- `WireEvaluablePathAir` already provides evaluated centerlines, tangents and
  approximation evidence for coil/knot paths. General guide paths need new
  dispatch and qualification, not just a new subtype with an assumed working
  materializer.
- Keyed Concept points, checked scalar Functions, assembly published frames,
  fixed placement and incremental definition caching provide the input scaffolding.

Extract the existing orthogonal search into a small, geometry-only centerline
search seam when wires need obstacle avoidance. Keep the piping facade and its
fixtures unchanged. Supply actual wire radius and wire cost policy; do not fake
a pipe specification or adopt piping fittings as wire geometry. Reuse suitable
collision bounds, while retaining family-specific realized-curve verification.

## Compiler path

1. Bind an ordered `WireRouteRequestIr`: wire section/material, minimum radius,
   named point/corner constraints, locked guide intervals, optional endpoint
   tangents, route policy and any explicit KeepOut references. Keep stable authored
   command keys and source provenance. Reuse scalar Function evaluation for lengths.
2. Resolve published inputs through the existing assembly placement authority.
   Convert them into the route occurrence's local frame exactly once. Ordinary
   occurrence placement then applies that frame once at display/export. Route
   geometry must not also carry a second world-placement transform.
3. Resolve each guide to a bounded curve value with position, tangent, arc-length
   mapping, regularity and curvature evidence. Derive entry/exit tangent states.
   Retain the immutable guide interval in the planned centerline.
4. Construct a bounded set of free-span candidates: straight, line/arc connectors,
   qualified biarcs and, for admitted axis-aligned cases, the shared orthogonal
   search. Check complete candidate chains, not independently greedy spans:
   a Via tangent and nonlocal collisions can couple neighboring connectors.
   Cap candidates/search and diagnose budget exhaustion separately from having
   no admitted candidate. This is bounded geometric construction and search,
   not a general constraint solver.
5. Use JudgmentEngine to select among competing admitted strategies. Hard point,
   guide, tangent, radius and clearance constraints determine admissibility;
   route length and optional bend cost determine preference, with stable ties.
   Deterministic curve binding and copying a locked guide need no utility scoring.
6. Lower accepted straight/circular spans directly to `WireStraightAir` and
   `WireBendAir`, preserving transported forming frames. General guide curves
   require a qualified `WireGuidePathAir` on the existing evaluable-path seam.
   Extend materializer/inspection dispatch explicitly and reuse tube construction.
7. Validate the realized sweep, including guide joins and nonlocal self-clearance.
   Publish start/end frames, physical centerline length and constraint-to-operation
   associations. Geometry, STEP, USD and camera interaction use the compiled result.

Fixed endpoint placements and projected scaffolds are read dependencies. Reject
cycles where the route helps place a part that supplies its own endpoint, and
defer dynamic-joint endpoints. Incremental cache keys must include bound endpoint
frames, guide geometry/intervals, policy and KeepOut inputs; unchanged routes reuse
their bodies. Moving a camera is never a routing input.

## Exactness and physical meaning

Line/circle guides have analytic length and curvature and can retain exact native
sweeps. For splines, retain the authored curve as the semantic authority, but
arc-length inversion and tube realization may need distinct numerical and shape
tolerances. Report those bounds. Curvature and clearance admission need conservative
evidence; a few favorable samples do not prove the minimum bend radius everywhere.
If the current kernel cannot qualify a curve, stop with a named unsupported-guide
diagnostic rather than silently treating its samples as exact geometry.

A guide denotes the wire centerline. If it comes from a groove floor or support
surface, explicitly derive the centerline at the required wire-radius/clearance
offset first. Surface-normal offsets and spatial curve offsets are different
operations. Following a guide proves no groove width, cable-tie fit or physical
support contact. Guides do not create solids or grant automatic KeepOut exemptions.
X0 obstacles remain conservative declared bounds; a whole-body box cannot prove
that a wire fits inside a groove. Electrical topology, insulation layers, bundle
packing, slack/gravity and string tension remain separate concerns.

## Recommended implementation slices

1. **Point routes:** Start/Corner/End with arbitrary-plane circular bends. Dogfood
   all six guitar strings; remove Python lengths, angles and raw placement bases.
   Compare endpoints, swept lengths, exact cylinder/torus geometry and STEP round-trip.
2. **Guide routes:** Line3 and qualified planar/spatial circular intervals first,
   with tangent entry/exit connectors. Qualify hard Via behavior explicitly; do
   not implement it as a renamed rounded Corner. Add cable-tie/groove witnesses,
   reverse intervals, insufficient transition room and too-tight guide failures.
3. **Obstacle routing:** share the orthogonal search, preserving all piping tests.
   Validate the realized rounded wire envelope against original KeepOut bounds.
4. **General curves:** open Concept Curve2/Curve3 binding, arc-length tolerances,
   curvature qualification and existing evaluable-path tube realization. Admit
   each curve family on evidence; do not make arbitrary splines an X0 prerequisite.

This ships the guitar improvement first while leaving one coherent syntax for
guided harness routing. The guide is an authored geometric constraint, and the
compiler owns the finite transitions around it.
## Implemented first slice

Start/Corner/End point routes are implemented through the existing exact WireForm backend. See [point wire routes](wire-point-routes.md) for the current syntax and explicit frame-binding boundary. The broader guide and auto-routing syntax below remains proposed.


## Owl authoring update

Implemented first guide: Start/Follow/End on one finite literal Concept Line3 interval, automatic minimum-radius entry fillet, forward tangent exit. Placed public endpoint binding is also shipped for fully fixed assemblies. Remaining guide/planning sketches below are proposals. See [source-owned guitar authoring](source-owned-guitar-authoring.md).
