# AIR-EDGE-FINISH-JUNCTION-M3

## Scope and result

M3 admits exactly one two-edge junction: a history-known axis-aligned rectangular box with
`SharedEdge(+X,+Z)` and `SharedEdge(+Y,+Z)`, which meet at `(+X,+Y,+Z)`, and two equal-distance
planar chamfers. The compiler lowers the two Feature AIR intents to one `LocalizedEdgeJunctionConstruction`,
then emits one authoritative `LocalizedEdgeJunction` BRep plan. It does not stitch two independently emitted bodies.

The canonical construction is **Direct**. One candidate was materialized and one is hard-valid; there is no
utility scoring.

## Exact corner construction

For half-extents `(hx, hy, hz)` and equal distance `d`, the replacement planes are:

```text
x + z = hx + hz - d
y + z = hy + hz - d
```

Their valid common boundary is the line segment from `(hx, hy, hz-d)` to
`(hx-d, hy-d, hz)`. It is the only closure of the two finite planar replacement regions.
It is recorded as `MiteredReplacementBoundary`: one exact owned shared edge with opposite coedges in
the two chamfer loops. It is not a planar triangular face. Adding a triangular corner face would overlap
the material already bounded by the two planes, so it is not an alternative valid policy.

The Construction AIR owns both replacement boundaries, the removed shared endpoint, both remote endpoint
transitions, retained `+X`, `+Y`, and `+Z` regions, material side, the miter boundary, and provenance.

## Authoritative topology plan

The combined plan contains 11 vertices, 17 edges, 8 loops/coedges faces, and 34 coedges:

- 3 retained incident supports (`+X`, `+Y`, `+Z`), each exactly trimmed;
- 2 remote-endpoint transition supports (`-Y` for edge A and `-X` for edge B);
- 1 unaffected `-Z` support;
- 2 planar replacement faces, with a shared miter edge;
- one shell and body.

Every edge is a line and all eight face supports are planes. `EmitPlanarPolyhedron` is the shared
plan-consuming localized-edge emitter; it receives ordered plan loops and does not infer or repair topology.

## Admission and deferral

Hard admission requires known box history, the ordered canonical pair, finite positive equal distances,
and a distance below all three incident extents. Failures occur before emission:

- unequal parameters: `localized-junction-parameter-mismatch`;
- zero/invalid or oversized distance;
- noncanonical/nonsharing selection;
- more than two selected edges;
- mixed finish families;
- two fillets: `localized-junction-unsupported-finish-combination:fillet-shared-patch-surface-required`.

The last is deliberate. A two-fillet corner needs an independently derived bounded blend support and
tangency proof; M3 does not guess a spherical or toroidal patch. Concave, imported/no-history, curved,
non-orthogonal, chain, loop, and three-edge cases remain outside this route.

## Fixture evidence

| Fixture | Result | STEP SHA-256 | Reimport/topology |
| --- | --- | --- | --- |
| `10 x 8 x 6`, `d=1` | Direct miter | `7B5A9289D7741F56C3EBF90056C0520463E869DF9248D01CC46847A5DF7C026F` | 11V / 17E / 8F, enclosed manifold |
| `10 x 8 x 6`, `d=2` | Direct miter | `D6D0431E95C3547B7F8E80A7F41008F6E0AC48CD2517E92CF420DA52B650A6CF` | 11V / 17E / 8F, enclosed manifold |
| `12 x 5 x 7`, `d=1` | Direct miter | `597EDDF4D44848698B57CA7714D0967A28E5867633AA699C2DA95B603B0487BB` | 11V / 17E / 8F, enclosed manifold |

The exact retained volume is `W*D*H - d^2*(W+D)/2 + d^3/3`; the final term restores the overlap of
the two independent prism removals. Tests also verify both miter endpoints lie on their intended replacement
and support planes, bounds remain unchanged, and all supports are planar.

Both artifacts were opened in CAD Assistant in this environment. CAD Assistant reported import completion
for `m3-junction.step` and `m3-junction-variation.step`; the displayed solids had a clean changed corner,
without a reader error, missing face, or visible gap. This evidence is independent of Aetheris STEP reimport.

## Preflight and legacy evidence

The production export uses `BrepExportPreflightMode.Enforce`; direct preflight, STEP export, and Aetheris
reimport all pass. The older two- and three-edge direct-BRep experiments remain negative evidence only:
they are not routed here and do not acquire this Construction AIR, combined authoritative plan, or CAD
Assistant acceptance by association.

## Future seam

The seam is reusable: individual replacements provide exact boundaries, the junction construction resolves
their shared closure, and one plan owns topology. It is suitable for a future exact fillet junction once a
bounded tangent patch primitive is proven; it can also host cove, roundover, ogee/profile-sweep, and trim
work without changing Feature AIR ownership. M4 should derive and validate the equal-radius fillet corner
surface before admitting it. General chains and valence-three corners remain deferred.
