# SurfaceMeshIR M7: local planar feature bands

M7 treats a hole or slot as a coherent topological feature, not as an arbitrary
set of polygon vertices. This is topology optimization, not geometric
approximation: a Plane has zero curvature, so chordal error never drives its
interior density.

## Domain and classification

`PlanarDomain` retains one stable Plane-local `(u,v)` chart, authoritative B-rep
sample IDs, source-edge boundary spans, concave vertices, feature loops, and up
to four normalized mechanical directions. Inner loops are classified from exact
curve provenance and projected geometry as `CircularHole`, `Slot`,
`RoundedSlot`, `GeneralConvexInnerLoop`, `GeneralConcaveInnerLoop`, or
`UnknownSimpleInnerLoop`. A slot is recognized from two straight spans joined by
two circular spans; uncertain cases fall back to the general categories.

## Feature-band and bridge plans

`PlanarFeatureBandPlan` records the source loop, exact inner sample count,
simplified outer-guide count, scale-relative width, transition ports, cell
count, collision response, and topology-locality distance. The planner also
creates an inward band for a dense outer trim. This is important on CTC faces 3
and 98: dense mixed outer trims were part of the spiderweb problem.

Guide width is the minimum of a feature-size term, local face-width term, and a
fraction of nearest-loop clearance. The guide is built by Plane-local line
normals and bounded corner miters. It is repeatedly shrunk when simplicity,
domain containment, or band collision cannot be proved. Close features are
clearance-clamped before construction; unsafe cases use the M6 face-local
fallback.

Circular holes use angularly ordered source samples and a 12-position guide.
Slots retain straight-side and rounded-end provenance, use a PCA-stable slot
axis, and expose only a small set of mechanically aligned ports. Unequal sample
counts reuse the same cyclic correspondence principle as M5 unequal annular
loops. A validated sector between one guide edge and one authoritative trim
chain remains one deterministic local n-gon; only a sector that cannot prove
safe lowering receives bounded residual triangles.

`PlanarBridgePlan` is a narrow quad strip between one guide port and one outer
guide edge. At most 128 candidates per feature and eight rows are considered.
The deterministic cost rewards short, direction-aligned connections and
penalizes guide-length skew. Candidates crossing a trim, feature guide, accepted
bridge, or domain exterior are rejected. Accepted bridges turn each hole guide
into a notch in one simple remainder boundary. The M6 convex/simple partitioner
then emits large coarse polygons for the remainder. A bridge is optional when a
hole-bearing remainder can preserve every guide vertex directly.

Cells carry derived provenance: `FeatureBand`, `Bridge`, `CoarseRemainder`, or
`ResidualTransition`. Exceptional triangles also record their reason. This is
debug metadata, not public semantic geometry.

## Quality and bounded failure

Candidate decisions are deterministic and bounded. The cost discourages long
bridges, poor mechanical alignment, skew, crossings, overlap, wide influence,
skinny transitions, and residual fans. No random search, relaxation, global
grid, medial-axis construction, or post-hoc welding is used. If any guide,
bridge, or remainder cannot be proved valid, newly added vertices are rolled
back and the existing M6 decomposition is used for that face with its exact
reason recorded. CTC-01 used this fallback on zero faces.

Safe planar n-gons are retained in SurfaceMeshIR and OBJ. Aetheris proves and
stores their deterministic Plane-local triangulation contract; STL lowering
never delegates the choice to an importer. Quad lowering also rejects a
diagonal that would create a zero-area triangle.

## CTC-01 target-face audit

Face 3 has a 312-sample mixed outer loop, four 36-sample circular holes, a
64-sample slot, and a 68-sample rounded slot. Face 98 has a 475-sample mixed
outer loop, eight circular holes (36 or 42 samples), one 54-sample convex boss
loop, a 64-sample slot, and a 68-sample rounded slot. M6 connected these dense
loops through a global convex partition, which explains their 290 and 494 cells
and the remaining radial patterns.

| Metric | Face 3 M6 | Face 3 M7 | Face 98 M6 | Face 98 M7 |
|---|---:|---:|---:|---:|
| Cells | 290 | 268 | 494 | 397 |
| Quads | 24 | 45 | 42 | 61 |
| Triangles | 213 | 50 | 372 | 77 |
| Safe n-gons | 53 | 173 | 80 | 259 |
| Feature bands / bridges | — | 7 / 6 | — | 12 / 11 |
| Internal edge length | 33,713.79 | 16,499.58 | 55,532.01 | 22,426.66 |
| Longest internal edge | 323.98 | 391.74 | 442.32 | 271.67 |
| Triangle-fan vertices | 21 | 3 | 37 | 3 |
| Maximum feature locality | — | 9.10 | — | 13.03 |

Face 3's longest coarse-remainder edge remains a known weakness, but it is one
large deterministic polygon boundary rather than a fan of feature-driven
diagonals. Its total internal length and fan count still fall materially.

## Full CTC comparison

| Metric | M6 | M7 |
|---|---:|---:|
| Total cells | 9,762 | 9,623 |
| Quads | 8,363 | 8,416 |
| Triangles | 1,120 | 505 |
| Safe n-gons | 279 | 702 |
| Quad percentage | 85.67% | 87.46% |
| Planar cells | 1,156 | 1,017 |
| Planar quads | 81 | 134 |
| Planar triangles | 796 | 181 |
| Planar n-gons | 279 | 702 |
| Internal planar edge length | 110,255.64 | 50,023.50 |
| Longest planar edge | 442.32 | 391.74 |
| Skinny cells (aspect > 12) | 1,055 | 258 |
| Triangle-fan vertices | 75 | 7 |

M7 contains 552 band cells, 52 bridge cells, 123 coarse-remainder cells, and
173 residual-transition cells. The maximum scale-local influence is 13.03 in
the CTC Plane-local units. Feature planning across CTC measured about 0.02 ms
classification, 170 ms band construction, 17 ms bridge selection, 285 ms
remainder decomposition, and 474 ms total inside a roughly 2.3 s full
SurfaceMeshIR build (representative local run; timings are not deterministic
evidence).

The through-hole plate's two caps use 144 planar cells in the prior equal-count
M2/M6 annular path and 90 in M7. The M7 caps retain the 36 exact cylinder samples
only in local rings, preserve a coarse rectangular remainder, and lower to a
watertight outward mesh.

Generic tests cover multiple unequal circular holes, a rounded slot beside
straight boundaries, a mixed hole-plus-slot domain, and close circular features.
Close bands are deterministically clearance-clamped; all repeated plans produce
identical cell and width decisions.

## Evidence and remaining work

The evidence directory contains the topology-preserving OBJ, deterministic STL,
full IR JSON, metrics, focused face plans, generic fixture summaries, hashes,
and the provenance-colored face view. Curved support planners and authoritative
B-rep boundary samples are unchanged.

Remaining weaknesses are bounded: face 3 retains a 391.74-unit coarse polygon
edge; some transition cells have high edge-length aspect ratios because dense
authoritative trim samples can be much shorter than their guide edges; and
feature clustering currently resolves close loops by deterministic width
clamping/shrinking rather than a shared cluster guide.
