# Mold M3: wall thickness, inside corners, requirements as Concepts

## What changed

- **Wall thickness** (`MoldAnalysis.WallThickness`, `MoldMeshRays`). Rolling ball: at each face sample, binary search
  for the largest ball tangent to the face there that no other face enters (BVH closest-point queries over the part
  mesh, triangles tagged with their face). Thickness is the ball's diameter.
  - The face's own surface, and neighbours that continue it tangentially (edges under 5°), never stop the ball.
  - A ball whose only contact is a face sharing an edge with the sample's face is in the corner wedge of that edge;
    the sample is dropped. Without this, every convex edge of a solid reads as a near-zero wall.
  - Exact when the contact face is an antiparallel plane, a coaxial cylinder, or a coaxial cone of the same angle,
    and the sampled value is within 5% of the analytic one; `WallStrength` says which.
- **Inside corners** (`MoldAnalysis.InsideCorners`). An edge between two faces whose mesh normals differ by more than
  5° is concave when the second face's outward normal leans over the first. Concave edges are findings against
  `MinimumInsideRadius` (policy field, default 0.5 mm). Always a warning: a part from a drafted Compose cannot yet
  round them, and making them errors at Tooling would fail every hollow fixture for something the language can't fix.
  A `Require` makes it binding.
- **Findings.** `mold-wall-too-thin`, `mold-wall-too-thick` (soft: error only at Tooling), `mold-sharp-inside-corner`
  (warning), `mold-requirement-failed` / `mold-requirement-invalid` (always error).
- **Requirements as Concepts** (`FirmamentRequirements`, new public facade in Kernel.Firmament). A Mold `Require name
  => expr` is evaluated by the existing predicate/bundle Concept checker (`FirmamentConcepts`) against the source's own
  Concepts. Measured names (`wall.minimum`, `draft.minimum`, `corner.sharp`, …) are substituted as quantities before
  parsing, so a requirement means exactly what it would over Static values; failures name the bundle label
  (`HouseCorners.rounded`). No second requirement language.
- **Measurements** (`MoldMeasurements` in the report): minimum draft, min/max/nominal wall, sharp corner count,
  minimum inside radius (smallest concave torus minor radius when there is no sharp edge), undercuts, actions.

## Evidence

| Fixture | Wall min / max / nominal (mm) | Exact faces | Findings |
| --- | --- | --- | --- |
| drafted-hollow-cover (2 mm shell) | 1.959 / 2.288 / 2.000 | 18 of 27 | 8 sharp inside edges (ceiling to inner walls) |
| drafted-housing | 2.000 / 2.307 / 2.000 | 91 | sharp inside edges only |
| drafted-cup (1.5 mm) | 1.473 / 1.651 / 1.500 | 3 of 5 | 1 sharp edge (floor to inner wall) |
| drafted-cover (solid, 12 mm) | – / 12 / – | – | thick (warning at Prototype, error at Tooling) |
| hollow cover at 0.8 mm | nominal 0.8 | – | thin, error at Tooling |

The non-exact values are real geometry, not noise: above the cavity ceiling (z 18–20 mm) the outer wall backs onto
the solid corner of the top slab, where the inscribed ball is ~2.19 mm; the rim at z = 0 sits between two walls
drafted at 1.5°, where the ball tangent to the rim is slightly under 2 mm. The first version measured the top slab's
outer corner cones at 1.369 mm (a false thin wall: the ball was held by the cone's tangent neighbour); treating
tangent neighbours as the face's own surface removed it.

The normal-ray method this replaced reported rims and bottoms as thick as the part's height (20–40 mm) and acute
convex edges as near-zero walls.

Tests: `MoldTests` 29 (wall, corner, Require pass/fail/invalid, thin, thick), `FirmamentRequirementsTests` 3
(substitution, longest-name-first, unknown Concept is invalid rather than failed).

Cost: wall measurement adds roughly 1–2 s per fixture review (about 1.3–3.4 s total for the canonical Mold fixtures).

## Verdict

M3's wall and corner rules are in. Rib and boss rules (rib-to-wall ratio, boss wall) are not; they need feature
intent the read-back STEP does not carry. Existing solid fixtures moved to `Prototype`, where thick sections are
warnings, because they are correctly flagged as thick.
