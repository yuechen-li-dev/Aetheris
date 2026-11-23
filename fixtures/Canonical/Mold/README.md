# Mold canonical fixtures

Each file is an ordinary part `Model` plus one `Mold` block that reads it. Build any of them with
`aetheris build <file>`: the part STEP and a `.mold.json` report are written side by side.

- `drafted-cup.firmament`: a 2° cup at the `Tooling` stage; releases, with one sharp inside corner (a warning) where
  the floor meets the inner wall.
- `drafted-cover.firmament`: a solid pierced cover extruded with `draft: 1.5deg` at the `Prototype` stage; every
  wall, the bore included, releases into the cavity half; it is 12 mm through, so its faces back onto thick sections
  (warnings).
- `tray-concept.firmament`: a hollow tray with vertical walls at the `Concept` stage; every wall is short of draft
  (warnings), nothing is undercut.
- `cup-textured-policy.firmament`: a typed policy Static and a textured −pull half raise the required draft.
- `drafted-compose-block.firmament`: a solid drafted Compose with a boss and a pocket at the `Prototype` stage;
  releases, with thick sections and sharp inside edges (warnings).
- `drafted-hollow-cover.firmament`, `drafted-housing.firmament`: drafted hollow Compose shells that release at the
  `Tooling` stage with an even 2 mm wall; the only findings are sharp inside edges (warnings).
- `cover-requirements.firmament`: the hollow cover held to a house standard: two Concepts applied by `Require`
  over the review's wall, draft and undercut measurements.
- `side-action-latch.firmament`: a pin hole drilled from the side is an undercut formed by the side action
  `pinCore`, which withdraws along −Y; cut into a core, a cavity and the slide's pin.
- `rod-split.firmament`: a rod molded across its axis; the parting plane splits its cylinder along the silhouette,
  and the inserts are cut after the rod's faces are split at the plane.
- `block-mid-parting.firmament`: a straight-walled pierced block parted at mid-height; its walls, corners and bore
  are cut at the plane for the inserts, and the bore's section is a shut-off.
- `housing-inserts.firmament`: the drafted housing with a vent, cut into `.cavity.step` and `.core.step` inserts;
  the vent is a shut-off.

The invalid fixtures (`fixtures/Invalid/Mold`) show undercuts (a bottle shoulder, a pulley groove, a side hole
without its action), an action that releases nothing, an unknown material, and a `Require` that fails at its
Concept's `rounded` check.
