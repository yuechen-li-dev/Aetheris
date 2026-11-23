# Mold canonical fixtures

Each file is an ordinary part `Model` plus one `Mold` block that reads it. Build any of them with
`aetheris build <file>`: the part STEP and a `.mold.json` report are written side by side.

- `drafted-cup.firmament`: a 2° cup at the `Tooling` stage; releases with no findings.
- `drafted-cover.firmament`: a pierced cover extruded with `draft: 1.5deg` at the `Tooling` stage; every wall,
  the bore included, releases into the cavity half with no findings.
- `tray-concept.firmament`: a hollow tray with vertical walls at the `Concept` stage; every wall is short of draft
  (warnings), nothing is undercut.
- `cup-textured-policy.firmament`: a typed policy Static and a textured −pull half raise the required draft.
- `drafted-compose-block.firmament`, `drafted-hollow-cover.firmament`, `drafted-housing.firmament`: drafted Compose
  parts (bosses, pockets, hollow shells) that release at the `Tooling` stage with no findings.
- `side-action-latch.firmament`: a pin hole drilled from the side is an undercut formed by the side action
  `pinCore`, which withdraws along −Y.
- `rod-split.firmament`: a rod molded across its axis; the parting plane splits its cylinder along the silhouette.
- `housing-inserts.firmament`: the drafted housing with a vent, cut into `.cavity.step` and `.core.step` inserts;
  the vent is a shut-off.

The invalid fixtures (`fixtures/Invalid/Mold`) show undercuts (a bottle shoulder, a pulley groove, a side hole
without its action), an action that releases nothing, and an unknown material.
