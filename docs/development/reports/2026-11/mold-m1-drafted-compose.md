# Mold M1, slice 2: drafted Compose and Hollow

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

## Not done

- **Draft on holes, slots and edge finishes**, and **drafted hollows with Boss/Pocket**. The hollow needs the
  band-wise erosion of `PrismaticHollowCavity` done on drafted sections.
- **Curved walls of different operations meeting.** This needs conic edges.

## Verdict

Success for the scope. The ordinary molded shapes now build with exact drafted walls and pass M0 at `Tooling`: a
drafted block with bosses and pockets, and a constant-wall hollow cover or tray. Anything the section stack cannot
draft exactly is refused, naming why.
