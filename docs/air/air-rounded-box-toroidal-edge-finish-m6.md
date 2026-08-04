# AIR-ROUNDED-BOX-M6 — rounded primitive and toroidal top edge finish

## Semantic boundary

`RoundedBox` is a primitive. Its equal `CornerRadius` is part of its two-dimensional silhouette and is lowered as an exact `RoundedRectangleProfile` swept by `LinearSweep` along `+Z`. It is not represented as a sharp box followed by four large fillets.

`EdgeFinish` is a later local modification. In the admitted syntax it selects the complete `+Z` outer boundary and applies one constant-radius `Fillet`:

```firmament
Struct Enclosure {
    RoundedBox Body {
        Size: [120mm, 80mm, 18mm]
        CornerRadius: 12mm
    }

    Modify Body {
        EdgeFinish TopRound {
            Face: +Z
            Target: Boundary
            Kind: Fillet
            Radius: 2mm
        }
    }
}
```

The compiler preserves those as separate feature intents: `RoundedBoxFeature` and `RoundedBoxTopBoundaryFilletFeature`. Source never names B-rep entities.

## Construction and realization

The primitive profile has an ordered closed loop of four exact line segments and four exact quarter-circle arcs. It has stable segment identities and tangent continuity at all eight joins. The linear sweep emits top and bottom planes, four planar side faces, and four cylindrical corner-wall faces.

`RoundedBoxBRepPlan` is the immutable authority before materialization, not a post-emission prediction. Primitive-only topology has 16 vertices, 32 edges, 10 faces, 10 loops, and 48 coedges. The finished top loop has 24 vertices, 48 edges, 18 faces, 18 loops, and 96 coedges. The plan carries stable roles for retained top/bottom, planar sides, rounded cylinder walls, four straight fillets, four toroidal corner fillets, and eight top-boundary segments.

## Complete top-loop fillet

The top boundary is lowered as one ordered eight-segment loop, rather than eight independently stitched operations.

| Boundary support pair | Exact replacement |
| --- | --- |
| top plane + planar side | cylindrical quarter-round face |
| top plane + cylindrical corner wall | toroidal quarter-round face |

For a corner cylinder of radius `Rc`, top fillet `Rf`, corner axis `+Z`, and top height `zt`, the torus uses:

```text
center = corner-cylinder center at z = zt - Rf
axis = +Z
major radius = Rc - Rf
minor radius = Rf
```

At torus minor parameter `v = π/2`, the patch reaches `z = zt` at radial distance `Rc - Rf`: this is the retained top-plane trim. At `v = 0`, it reaches radial distance `Rc` at `z = zt - Rf`: this is the retained cylindrical-corner-wall trim. The same minor quarter circles are shared with the adjacent straight cylindrical fillet faces. Thus each shared boundary is on both supports (G0), and the support normals agree at the top plane and corner cylinder (G1). This is the material-side branch for an outside convex enclosure; no unequal-radius junction rule is involved.

## Admission and preflight

The admitted family is a history-known, axis-aligned positive `+Z` sweep with four equal corner radii and, optionally, exactly one `Face: +Z`, `Target: Boundary`, `Kind: Fillet` edge finish. `Rc < min(width, depth)/2`; when a top fillet exists, `0 < Rf < min(Rc, height)`.

The planner rejects degenerate profiles, oversized corner radius, zero/oversized top radius, and unsupported support-pair/finish selection before it builds topology. `BrepExportPreflightMode.Enforce` checks all shared loop chains, curve endpoints, trim containment on planes/cylinders/tori, and nondegenerate edges. The torus containment formula is explicitly covered by preflight.

Deferred deliberately: unequal/per-corner corner radii, nonrectangular or concave profiles, bottom/both-side finishes, variable radius, imported/no-history bodies, mixed finishes, and a radius which consumes the profile. This milestone makes no claim of arbitrary unequal-radius fillet support.

## Evidence

Canonical command results:

| Fixture | Faces / surfaces | STEP SHA-256 | Reimport |
| --- | --- | --- | --- |
| `rounded_box_basic.firmament` | 10; 6 planes, 4 cylinders, 0 tori; analytic volume `170575.008158 mm³` | `B1A46902EFA998D83F5AB7CD082F845539F7B71370F9E2D40FE1D689EC750F7C` | enclosed, manifold |
| `rounded_box_top_fillet.firmament` | 18; 6 planes, 8 cylinders, 4 tori; analytic volume `170251.739420 mm³` | `02DE113EACDCA270C657B28FF5235BFF3B477B5E64E52013D16A9F0A40372684` | enclosed, manifold |

Both artifacts retain bounds `[-60,-40,-9]..[60,40,9]`; their surface counts and hashes differ, proving the top local finish changed the geometry while preserving the silhouette envelope. The core test matrix also covers `80×50×20 Rc=8 Rf=1`, `150×90×25 Rc=15 Rf=3`, and invalid dimensions/radii.

The reported analytic volume is exact for this admitted family: start with `(W*D - (4-π)*Rc²)*H`, then for `Rf` subtract four straight square-minus-quarter-circle strips and four quarter-annular corner contributions. The generic `aetheris analyze volume` route is not used as evidence here: it currently misclassifies the primitive as a box-with-hole and defers trimmed curved-shell integration for the finished artifact.

CAD Assistant was launched against both hash-tied artifacts. The finished `Rf=2` file reached `Import …: 100%` and reported `Displaying: finished`; the primitive-only file reported `Displaying: finished` but remained at 99% import progress after 20 seconds. This is recorded as an incomplete independent visual smoke for the primitive-only file, not a CAD Assistant pass. Aetheris reimport/preflight is successful for both; re-run the primitive visual smoke in a clean viewer session before claiming independent CAD Assistant acceptance.

## Relationship to rounded web rectangles

The primitive corresponds to an extruded web-style rounded rectangle: its corner radius changes the outer outline. The small top round is closer to a physical enclosure finish: it changes only the post-construction edge neighborhood. Their related curvature does not make them the same compiler feature.
