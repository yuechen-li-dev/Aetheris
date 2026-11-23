# Mold M2: undercuts, side actions, split faces, shut-offs, and core/cavity inserts

M2 of the [Mold DFM plan](MOLD-DFM-DESIGN.md) (undercut detection, `Action`, parting split for straddling faces),
plus the core/cavity split, done by assembly rather than by Boolean.

## What changed

- **Undercuts (`MoldAnalysis`).**
  - Undercut faces that share an edge form one undercut.
  - Each undercut is ray-tested along the four directions across the pull, on a ray grid built per direction.
  - The finding names the directions that release it ("declare an Action with that pull"), or says that none does:
    it needs a lifter, a collapsing core or another pull.
- **`Action name { pull; near }` in the Mold block.**
  - An undercut belongs to an action when every one of its samples faces the action's pull (or runs parallel to it)
    and its ray along that pull is free. Where several actions qualify, `near` picks the closest.
  - Action faces are drafted against the action's pull (exact on planes, and on cylinders and cones about it).
  - They leave the halves' side tally, the projected area and the parting line.
  - An action that releases nothing is an error that lists the undercuts and their directions.
- **Split faces.**
  - A straddling face's silhouette height is exact for a cylinder across the pull (its axis) and for a sphere (its
    centre); otherwise it is the samples nearest parallel.
  - When the face releases and the parting plane passes through the silhouette, it is split there and is no
    finding.
  - Otherwise the finding says where its silhouette is and where the plane is.
  - The projected area counts the upward half of a split face.
- **Parting loops.**
  - Parting edges are chained into loops, each classified:
    - **Parting:** on the plane.
    - **ShutOff:** flat, across the pull, ringing an opening. The test point inside the loop is outside the part
      by ray parity.
    - **Stepped:** anything else.
  - Shut-offs are no longer reported as the parting line leaving the plane. The off-plane finding lists the stepped
    loops and off-plane silhouettes.
- **Core/cavity inserts (`MoldInserts`, `inserts: <margin>`).** Each insert is assembled, not intersected:
  - the part's faces of its half, reversed, on the part's own edges and exact surfaces;
  - the parting face: the block's section less the part's outline. Its inner loop takes that half's own coedge
    directions on the parting edges, which also fixes the loop convention for the new faces.
  - a shut-off face across each opening;
  - five block faces.
  - The CLI writes `<part>.cavity.step` and `<part>.core.step`.
  - Inserts are refused, with the reason, for actions, faces neither half forms alone, or stepped parting.
- **Fix.** Faces removed from the side map defaulted to `Positive` (the enum's first value) in the projected-area
  sum.

## Evidence

- **Side-hole latch (pin hole drilled from the front).**
  - Without an action: one undercut of 2 faces (the cylinder and its drill-point cone), releasing along −Y.
  - With `Action pinCore { pull: -Y }`: the action forms the undercut, with no undercut or straddle findings. Its
    draft along −Y is exactly 0°, which is reported. The projected area is 40 × 30 exactly.
  - With `pull: +X`: `mold-action-releases-nothing`.
- **Pulley groove:** one undercut of 3 faces that releases along no direction across the pull.
- **Rod across the pull.**
  - With the parting plane through its axis: the silhouette is at 0 exactly, with no straddle or off-plane finding.
    The projected area is 640 mm² to 1e-3.
  - With the plane moved to 3 mm: "silhouette at 0 mm, off the parting plane at 3 mm".
- **Inserts, all enclosed-manifold on re-import; block = cavity + core + part:**

| Part | Cavity | Core | Part | Block | Shut-offs |
|---|---|---|---|---|---|
| Drafted hollow cover | 131921.5 | 131675.5 | 16403.0 | 280000 | 0 |
| Housing with vent | 247061.7 | 215409.1 | 27129.2 | 489600 | 1 |
| Drafted compose block | 162484.8 | 70000.0 | 47515.2 | 280000 | 0 |
| Tray (vertical walls) | 332063.5 | 248574.4 | 35362.1 | 616000 | 0 |

  Volumes are in mm³ and measured on meshes. The sums match to the printed 0.1 mm³.
- **Tests:** `MoldTests` adds 8 tests: undercut groups and directions, an action forming its undercut, an action
  that releases nothing, split faces on and off the plane, inserts with a shut-off and their volume identity, and
  inserts refused with actions. The full lane passes.

## Not done

- **Inserts for tools with actions.** A slide body would be cut the same way, from its undercut faces and a parting
  face across the action's pull.
- **Stepped and contoured parting surfaces**, and splitting a straddling face's B-rep at its silhouette for inserts.
  Its edge is exact for cylinders and spheres.
- **Lifters,** for undercuts that release along no direction across the pull.

## Verdict

Success. The review now says how to tool an undercut, not only that there is one. A drafted part with a flat
parting line becomes a two-plate tool's core and cavity in one build, with the volumes balancing exactly by
construction.
