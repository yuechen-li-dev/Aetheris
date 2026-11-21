# Mold canonical fixtures

Each file is an ordinary part `Model` plus one `Mold` block that reads it. Build any of them with
`aetheris build <file>`: the part STEP and a `.mold.json` report are written side by side.

- `drafted-cup.firmament`: a 2° cup at the `Tooling` stage; releases with no findings.
- `drafted-cover.firmament`: a pierced cover extruded with `draft: 1.5deg` at the `Tooling` stage; every wall,
  the bore included, releases into the cavity half with no findings.
- `tray-concept.firmament`: a hollow tray with vertical walls at the `Concept` stage; every wall is short of draft
  (warnings), nothing is undercut.
- `cup-textured-policy.firmament`: a typed policy Static and a textured −pull half raise the required draft.

The invalid fixtures (`fixtures/Invalid/Mold`) show undercuts (a bottle shoulder, a pulley groove) and an unknown
material.
