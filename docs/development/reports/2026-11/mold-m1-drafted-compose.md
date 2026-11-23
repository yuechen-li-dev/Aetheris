# Mold M1, slices 2 and 3: drafted Compose, holes, slots and Hollow

Second slice of M1 in the [Mold DFM plan](MOLD-DFM-DESIGN.md), after the
[drafted Extrude](mold-m1-drafted-extrude.md). `draft:` now works on `Compose` and its operations, on
`Compose<Hollow>` and on `Extrude<Hollow>`.

## What changed

- **Syntax.**
  - `draft:` on a Compose drafts every Profile operation.
  - `draft:` on a `Base`, `Add`, `Remove`, `Boss` or `Pocket` overrides it, and `0deg` keeps that operation's walls
    vertical.
  - A `Base` also takes `neutral:`.
- **Each operation erodes its own Profile region with height**
  (`PrismaticProfileOperation.DraftErosionAt`).
  - Material narrows upward from its bottom.
  - A removal narrows downward from its mouth, so a Pocket opens toward +Z.
- **`DraftedSectionStack`.**
  - Each slab is composed at its two levels. The two arrangements must correspond loop for loop and curve for
    curve, matched by provenance and rotated into step.
  - The emitter sweeps each pair:
    - **Walls:** a line pair becomes the drafted plane through both lines, an arc pair the right cone through both
      circles.
    - **Edges:** straight transition edges join the corners at the two levels.
    - **Caps:** each transition's caps are the difference between the section below and the section above. Curves
      are split only at points on the same level.
  - Volume is Simpson's rule per slab, which is exact because the section area is quadratic in height.
- **Exactness rules, each refused with its own diagnostic:**
  - **Meeting walls must both be lines** (`compose-draft-curved-junction`). Where two operations' walls meet, their
    meeting line is straight only between two planes. A meeting that involves a cone would be a conic.
  - **No sharp corner on an arc** (`draft-offset-corner-curved`). A sharp corner where an arc meets another curve
    moves along a conic, not a straight line. This also corrects the drafted Extrude, which approximated such an
    edge with a straight line.
  - **Same section shape at both ends of a slab** (`compose-draft-walls-cross`). It fails when walls meet or part
    within a slab.
  - **No overhang** (`compose-draft-overhang`). A Boss wider than the drafted top it stands on would be an undercut.
- **Drafted hollow (`DeriveHollow`).**
  - The cavity is the Base's Profile inset by t / cos α and drafted with the Base. Inner and outer planes are then
    parallel and t apart, and inner and outer cones are coaxial with the same semi-angle.
  - A drafted hollow is built from its Base alone; a Boss or Pocket on it is refused.
  - A drafted `Extrude<Hollow>` now goes through the same route, with an Extrude's neutral at 0.
- **Not drafted yet, each refused with `compose-draft-unsupported`:** holes, slots, material offsets and edge
  finishes, including junction fillets, on a drafted body.

## Evidence

- **`drafted-compose-block`.** An 80 × 50 Base with a Boss and a Pocket, at 2°.
  - All 32 walls are 2° (16 planes, 16 cones).
  - The reimport is enclosed and consistently oriented.
  - The volume matches the per-operation erosion integral to 1e-4. The Mold reports 47515.2 mm³ against
    47516.25 analytic; its figure is measured on a mesh.
  - Mold at `Tooling`: every wall is +pull at exactly 2°, with no findings.
- **Overlapping square Bosses at 1°.** The walls meet along the lines where their planes meet. The volume is exact
  to 1e-9.
- **Mixed angles.** An undrafted Boss keeps its cylinder, and a drafted through-Pocket narrows downward at 3°.
- **Hollow cover (+1.5°, open at the bottom) and tray (−1.5°, open at the top).**
  - All 12 wall planes have a parallel partner exactly 2 mm away (1e-9).
  - All 12 cones have a 1.5° semi-angle.
  - The volumes match to 1e-4.
  - Mold on the cover at `Tooling`: 16 outer walls +pull, 8 inner walls −pull, no findings.
  - The tray passes at `Tooling` with its parting plane at the rim.
- **Refusals** pinned in `DraftedComposeTests`: a hole, curved overlapping bosses, a pocket reaching the wall, a
  stacked boss that overhangs, 50°, a chamfer, a junction fillet, and a hollow with a Boss. A unit test covers a
  sharp corner on an arc.
- **Tests:** `DraftedComposeTests` (11 cases), plus `MoldTests.DraftedComposeBossAndPocketReleaseWithNoFindings` and
  `DraftedHollowCoverReleasesInsideAndOut`. The full lane passes.

## Slice 3: holes, slots, hollow bosses and pockets (M1 complete)

- **Holes and slots.** `Hole<Shaft>`, `Hole<Counterbore>`, `Slot<Capsule>`, `Slot<RoundedRectangle>` and material
  offsets were already Remove operations in the section stack; they now take the Compose's draft (or their own
  `draft:`) and narrow from their entry, as cored. A counterbore's shaft is neutral at the bore's floor.
  Hole wall faces keep the `HoleWallFace` role when drafted (a cone used to mean a countersink).
- **Extrude.** A drafted Extrude's draft becomes the Compose's when it is rewritten (Modify features or `<Hollow>`),
  so `Modify` holes and slots on it are drafted; its neutral stays at the Profile plane.
- **Hollow with Boss and Pocket.** Loops can follow different laws (`DraftLaw`, `LoopDraft`): the Base's cavity is
  cut in bands, and in each band a Pocket's liner (the Pocket grown by t / cos α, drafted as the Pocket) is a hole
  in it, from t below the Pocket's floor. A Boss's cavity is the Boss inset by t / cos α, from the Base's cavity to t
  below its top. Refused: a pocket too deep for a wall under it, a liner that leaves the Base's cavity or meets a
  boss's cavity, features not on the closed top.
- **Fixes.** The overhang check compares a Boss with the exterior beneath it, not with the section (a hollow's
  own cavity under a hollow boss is not a missing support). The stack no longer emits a success marker as a
  diagnostic; parse-time normalizations (the counterbore's host) read it as fatal.
- **Refused by design.** Countersinks (a machined cone), holes on side faces and construction planes (not along the
  pull: M2's side actions), and edge finishes on drafted walls (M3).

Evidence (slice 3):
- **Shaft hole, counterbore and capsule slot at 1°.** Every wall is 1°. The volume matches the per-feature integral
  to 1e-4, and the Mold review at `Tooling` passes.
- **`drafted-housing`.** 100 × 60 at 1.5°, with a hollow Boss and a Pocket. Every wall is 1.5°. The volume matches
  the exterior less both cavities, with the liner left solid, to 1e-4 (27149.9 mm³ against 27151.9 analytic, mesh
  measured). The Mold review at `Tooling` passes with no findings.
- **A drafted Extrude with a Modify hole.** It builds drafted, with the volume matched.
- **A vent hole through the housing's top.** The Mold review reports its parting line stepping from 0 to 23 mm. That
  is a shut-off, correctly left to M2.

## Not done

- **Edge finishes on drafted walls** (M3).
- **Curved walls of different operations meeting.** This needs conic edges.
- **Hollow bosses and pockets off the top, and stacked bosses on a hollow.**

## Verdict

Success for the scope. The ordinary molded shapes now build with exact drafted walls and pass M0 at `Tooling`: a
drafted block with bosses and pockets, and a constant-wall hollow cover or tray. Anything the section stack cannot
draft exactly is refused, naming why.
