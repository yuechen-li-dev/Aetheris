# Mold M1, slice 1: drafted Extrude

M1 of the [Mold DFM plan](MOLD-DFM-DESIGN.md) puts draft on the generators, so that M0's draft check can pass on
authored parts. This slice covers `Extrude`. Compose `Base`/`Boss`/`Pocket` and `<Hollow>` come next.

## What changed

- **`draft:` and `neutral:` on Extrude.**
  - The section at local height z is the Profile eroded by (z − neutral) · tan(draft).
  - Positive draft narrows the solid toward `to`.
  - The neutral plane is the Profile's own plane (height 0) unless `neutral:` names another height.
  - `neutral:` without `draft:` is refused.
- **Exact walls.** The extrusion planner builds the two sections as exact offsets of the Profile and sweeps between
  them:
  - **Lines** become planes tilted by the draft.
  - **Arcs and circles** become right cones, with the cone semi-angle equal to the draft.
  - **Tangent junctions** move along their shared normal and stay tangent.
  - **Corners** move to the intersection of their two offset curves and stay sharp.
  - **No fitting and no 3D boolean.** At draft 0 the planner's undrafted path is untouched.
- **Material side per loop.** Traced guides do not promise a hole's winding (a `Rect2 |> TraceLoop` hole runs
  counterclockwise). The offset and every drafted wall's normal therefore take the material side from each loop's
  signed area and its role, not from an assumed winding.
- **Refusals, each with its own diagnostic:**
  - an ellipse or cubic Bézier, which has no exact drafted wall (`draft-unsupported-curve`);
  - an arc, side or gap that closes within the extrusion (`draft-offset-collapse`). Gaps are checked on a
    32-chord-per-arc polyline across all loops;
  - a draft of 45° or more (`draft-angle-invalid`);
  - a drafted Extrude with `Modify` features, `<Hollow>`, an `EdgeFinish` or a Plateau
    (`profile-extrusion-draft-unsupported`). Those routes assume vertical walls; refusing is better than finishing a
    drafted wall as if it were vertical.

## Evidence

- **Rounded rectangle, 2°, 12 mm.**
  - The four side planes and four corner cones each have a draft of 2° (to 1e-9°).
  - The reimported volume matches the analytic integral to 2e-5 relative.
  - M0's review at the `Tooling` stage reports every wall at 2.0000° with exact evidence, and no findings.
- **The test bracket, 1.5°, neutral at the top.** The outline is an L with a concave 4 mm heel arc, five convex
  corner arcs, a round bore and a rectangular window traced counterclockwise.
  - All 17 walls are drafted 1.5°.
  - The reimport raises no `step-orientation-source-mismatch`, and the shell is enclosed and consistently oriented.
  - Volume matches the Steiner integral (A₀ − L·d − 4d², the window's mitred corners giving −4) within the mesh's
    chord error on the bore (2e-4).
  - Before the per-loop material-side fix, the window grew downward instead of shrinking. Its walls faced the wrong
    half, and the volume was 142 mm³ low. The Mold review caught both.
- **−3° about a midplane.** The section at z = 0 is the 40 × 30 rectangle, the top corner sits at 20 + 5·tan 3°, and
  the volume is exact.
- **Mold fixture `drafted-cover`.** A pierced cover at `Tooling` stage gives 9 walls at 1.5°, all +pull, no
  findings. Its projected area is the outline less the bore at its narrowest.
- **Tests.**
  - `DraftedExtrudeTests`: 3 builds and 7 refusals.
  - `MoldTests.DraftedExtrudeReleasesWithExactWallsAndNoFindings`.
  - The full lane passes.

## Not done

- **Compose (`Base`, `Boss`, `Pocket`) and `<Hollow>`.** These are the next slices. A Pocket drafts the opposite way,
  opening toward its pull. A drafted hollow wall stays exact at t / cos α.
- **Draft on ellipses and Béziers.** Their offsets are not in the representation.

## Verdict

Meaningful progression. An authored part can now pass M0's draft check. The walls are exact cones and planes, so
the check's evidence is exact rather than sampled, and an Extrude whose walls cannot be drafted exactly is refused.
