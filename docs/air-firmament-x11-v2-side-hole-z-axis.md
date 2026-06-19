# AIR-FIRMAMENT-X11 — controlled Firmament V2 side-hole Z-axis routes

## Purpose and scope

AIR-FIRMAMENT-X11 completes the controlled opposite-face Firmament V2 side-hole route set for Box targets by adding the two Z-axis face pairs: `+Z -> -Z` and `-Z -> +Z`. This remains controlled fixture support, not general side-hole support.

## Relationship to X4-X10

X4 introduced parser-backed direct `+X -> -X` side-hole syntax. X5 locked artifacts and parity. X6 added radius variation. X7 added face-local center offsets. X8 added aliases. X9 added reverse X. X10 added both Y-axis directions. X11 adds the controlled Z-axis directions without broadening the feature surface.

## Supported route set after X11

- `+X -> -X`
- `-X -> +X`
- `+Y -> -Y`
- `-Y -> +Y`
- `+Z -> -Z`
- `-Z -> +Z`

## Unsupported routes

Mixed-axis paths, same-face paths, blind holes, arbitrary face pairs, oblique holes, non-box target solids, multiple regions/cuts, LoopRef/EdgeRef/VertexRef feature targets, and feature-output aliases remain unsupported.

## Source syntax examples

Direct `+Z -> -Z`:

```firmament
model SideHoleZAxisV2 {
    units mm
    solid base: Box { size: [10, 8, 6] }
    modify base { region sideHole on face(+Z) { cut Cylinder { radius: 1 center: [1, 0] through: face(-Z) } } }
}
```

Direct `-Z -> +Z`:

```firmament
model SideHoleReverseZV2 {
    units mm
    solid base: Box { size: [10, 8, 6] }
    modify base { region sideHole on face(-Z) { cut Cylinder { radius: 1 center: [1, 0] through: face(+Z) } } }
}
```

Alias `+Z -> -Z`:

```firmament
model SideHoleAliasZAxisV2 {
    units mm
    solid base: Box { size: [10, 8, 6] expose { face(+Z) => top face(-Z) => bottom } }
    modify base { region sideHole on top { cut Cylinder { radius: 1 center: [1, 0] through: bottom } } }
}
```

## Face-local coordinate convention

- `face(+X): u=+Y, v=+Z`
- `face(-X): u=+Y, v=+Z`
- `face(+Y): u=+X, v=+Z`
- `face(-Y): u=+X, v=+Z`
- `face(+Z): u=+X, v=+Y`
- `face(-Z): u=+X, v=+Y`

For Z faces, `[0, 0]` is the attach-face center, `[1, 0]` offsets along +X, and `[0, 1]` offsets along +Y.

## Route admissibility

The parser admits only opposite faces on the same principal axis. Same-face routes are rejected with the same-face diagnostic; mixed-axis routes are rejected with the route-unsupported diagnostic after direct selector or alias resolution.

## Center/radius clearance rules

For Box `[10, 8, 6]`, strict clearance is required:

- X-axis routes: `abs(u) + radius < 4`, `abs(v) + radius < 3`.
- Y-axis routes: `abs(u) + radius < 5`, `abs(v) + radius < 3`.
- Z-axis routes: `abs(u) + radius < 5`, `abs(v) + radius < 4`.

## Trace JSON/text route evidence

Trace JSON preserves semantic intent and route evidence with axis `Z`, direction `+Z->-Z` or `-Z->+Z`, attach face, through face, center, radius, source target kind, and center frame. Text trace prints the attach target, resolved alias targets when present, through target, route, and center frame such as `face(+Z) local u=+X, v=+Y`.

## Artifact/manifest behavior

The existing trace artifact flow emits STEP smoke, trace JSON, trace text, and `manifest.json` for Z-axis fixtures. The manifest includes syntax version, fixture path, stage, parent integration, shell closure, STEP smoke, route axis/direction, attach/through faces, source and resolved targets, radius, center/frame, `controlledFixtureOnly = true`, and `generalSideHoleSupport = false`.

## Valid and invalid fixtures

Valid fixtures added:

- `fixtures/FirmamentV2/Region/valid/side-hole-z-axis-v2.valid.firmfixture`
- `fixtures/FirmamentV2/Region/valid/side-hole-reverse-z-v2.valid.firmfixture`
- `fixtures/FirmamentV2/Region/valid/side-hole-aliases-z-axis-v2.valid.firmfixture`

Invalid fixtures added:

- `fixtures/FirmamentV2/Region/invalid/side-hole-mixed-axis-z-to-x-v2.invalid.firmfixture`
- `fixtures/FirmamentV2/Region/invalid/side-hole-z-center-x-boundary-v2.invalid.firmfixture`
- `fixtures/FirmamentV2/Region/invalid/side-hole-z-center-y-boundary-v2.invalid.firmfixture`
- `fixtures/FirmamentV2/Region/invalid/side-hole-alias-z-wrong-through-v2.invalid.firmfixture`

## What this proves

Controlled opposite-face side-holes now work across all three principal axes for Box fixtures in the parser-backed V2 trace path.

## What this does not prove

This does not prove mixed-axis support, same-face support, blind holes, oblique holes, non-box target solids, arbitrary local frames, generic Boolean admission, CIR topology authority, multiple regions/cuts, feature-output aliases, shell/fillet/chamfer/surfacing/pattern/material/FEA behavior, templates/concepts/PMI/where parsing, STEP PMI export, STEP importer behavior, or a Firmasm redesign.

## Tests run

- `dotnet build Aetheris.slnx -f net10.0 --no-restore`
- `./scripts/test-active.sh`
- Focused CLI/kernel test filters for Firmament V2 side-hole and trace coverage.

## Next milestone recommendation

AIR-FIRMAMENT-X12 should consolidate controlled opposite-face side-hole route policy into a central route/frame/admissibility table rather than adding more feature surface area.
