# Blend recognition by the rolling-ball certificate

**Goal.** Recognize blend faces by what they are, not by surface kind: another kernel's B-spline
fillet (Fusion today, SolidCraft's certified nets tomorrow) must read the same as an analytic
torus. Choose which neighbours a face spans as an explicit judgment. Graduated into Core
(`Aetheris.Kernel.Core.Brep.Blends`), additive: nothing existing calls it except the blend
clean-up probe.

Idea taken from solidcore's generic-blend design (rails are where the ball touches each face;
see `claude/solidcore-blend-takeaways.md` in the project); the certificate, the judgment and
the anchor rules are ours.

## Method

- **Certificate (round).** For sample points p of the face with normal n, the ball centre
  p ∓ r·n is at distance r from both anchors. r is solved per sample from the better-conditioned
  anchor, the median taken, then every sample is checked at that r. The residual is the worst
  miss. Representation-free: samples come from the display mesh; anchors need a distance
  function (plane, cylinder, cone, sphere, torus; `BlendSupport`).
- **Certificate (chamfer).** Each rail keeps a constant distance to the other anchor; the face
  leans towards both anchors; convexity from the rails.
- **Judgment.** Every reading (anchor pair × round/chamfer × convex/concave) is measured first,
  then a `JudgmentModel` (policy `blend-recognition/1`) admits and ranks them. Hard rules carry
  measured vs limit: anchors analytic, coverage, size (policy limit), residual, offset not
  collapsed (an anchor of the ball's own radius, within 1%), centre path is a curve (a ball that
  does not move is a corner), anchors turn (1°..179°: no full rounds), chamfer leans.
- **Anchors stay.** A strip read against another blend (a transition patch rolling on a fillet of
  its own radius) is judged again without the blends, to a fixed point. A face some strip spans is
  never a corner patch. Joiners: smooth all round, meeting a strip, spanning nothing.
- **Normals.** Exact support normals for analytic faces (the nearest display vertex can be a row
  away: 18.7° on a coarse cylinder); freeform faces are measured on the edge through its trim
  curve.

## Results

Core tests (8): all five rounded Platonic solids read every edge as a convex strip at the exact
radius (residual < 1e-9) and every corner as a joiner; plain solids read nothing; a wrong-side
reading is refused with its measured miss; a 3 mm round under a 2 mm limit refuses naming 3;
SO-101 certifies analytic and freeform blends with no strip read against a blend.

Blend clean-up reach (imported corpus, probe anchors from the certificate instead of shape
heuristics):

| | heuristic (E2b) | certificate |
|---|---|---|
| removed, valid, round-trips, volume plausible | 754 | **802** |
| of which full pcurve round trip | 578 | **693** |

STC-07's 20 B-spline corner patches and SO-101's 10 now read as joiners; FTC-10 +12, SO-101 +8.

**Volume against the certificate** (new verifier): each removed strip predicts its volume change
as section area (r²(tan(θ/2) − θ/2), or s_a·s_b/(2 sin θ)) times its length. Strip-only regions:
61 faces within 2%, 125 within 10%, 45 off. It flags FTC-06 R16 (−3,344 mm³ measured, −237
predicted), the source-tessellation error found by hand before. Most of the 45 are ring strips
(FTC-08, constant ratio 1.34): the length is the rails' mean, not the section centroid's
(Pappus); a limitation of the prediction.

## Open

- Freeform anchors (no distance function yet): `freeform-neighbour-not-extendable` 186 faces.
- Point-contact strips (the chimera sphere; item 2 of the plan) read as joiners for now.
- Pappus length for curved strips in the volume prediction.

## Point contacts: the ball's triangle

A fillet as large as the arc it follows (the chimera's 4 mm fillet on a 4 mm convex arc) is a
piece of the ball: its centre is fixed on the arc's axis, and it touches the top at one vertex.
solidcore names it the ball's triangle (#2403).

- **Recognition.** An anchor's offset collapses when the ball's centres sit on its core (a
  cylinder's axis, a sphere's centre, a torus's tube circle), measured, not inferred from equal
  radii: on the outside of a reflex arc of the same radius the ball rolls (the chimera's torus 24
  was wrongly refused before this). A reading whose centre does not move and whose one collapsed
  anchor runs along an edge is admitted as the ball's triangle (`BlendRecognition.BallTriangle`).
  A box corner's sphere never qualifies: its candidate anchors are sibling strips.
- **Keep set.** `Recognize(body, options, keep)`: faces the caller keeps are never blends, so
  they are anchors. The chimera's profile arcs are geometrically blends between the side walls;
  a caller removing only the rim finish keeps them. Replaces the test-side anchor workaround.
- **Removal.** The contact vertex joins no cluster and disappears; each strip ending at it maps
  it along its own end edge to its own corner; corners include the anchors of the strips ending
  there; every sharp edge is built before the loops, and a loop gap is closed by any new edge on
  that face (the top gets the arc across the contact).

The chimera now comes back as the authored sharp part (19 faces, every vertex within 1e-6 mm,
volume, export and reimport); `FilletAsLargeAsTheArc_Refuses` is replaced by that ground truth and
`FilletAsLargeAsItsArc_ReadsAsTheBallsTriangle`. Reach unchanged (802; 697 full pcurve): the
corpus has no ball's triangle.
