# AIR-FIRMAMENT-X10 — V2 side-hole Y-axis face-pair variation

## Purpose and scope

X10 expands the parser-backed Firmament V2 controlled side-hole path from X-only opposite-face routes to include Y-axis opposite-face routes. This is still a controlled fixture path, not general side-hole support.

## Relationship to X4-X9

X4 introduced direct `+X -> -X` syntax, X5 locked artifacts/parity, X6 varied radius, X7 added face-local center offsets, X8 added aliases, and X9 added `-X -> +X`. X10 adds `+Y -> -Y` and `-Y -> +Y` using the same parser-backed semantic intent and AIR Region golden trace chain.

## Supported route set

* `+X -> -X`
* `-X -> +X`
* `+Y -> -Y`
* `-Y -> +Y`

## Unsupported routes

Z-axis, mixed-axis, same-face, blind, and arbitrary face-pair paths remain unsupported.

## Source syntax examples

Direct `+Y -> -Y`:

```firmament
region sideHole on face(+Y) {
    cut Cylinder { radius: 1 center: [1, 0] through: face(-Y) }
}
```

Direct `-Y -> +Y`:

```firmament
region sideHole on face(-Y) {
    cut Cylinder { radius: 1 center: [1, 0] through: face(+Y) }
}
```

Alias `+Y -> -Y`:

```firmament
expose { face(+Y) => front; face(-Y) => back }
region sideHole on front {
    cut Cylinder { radius: 1 center: [1, 0] through: back }
}
```

## Face-local coordinate convention

* `face(+X): u=+Y, v=+Z`
* `face(-X): u=+Y, v=+Z`
* `face(+Y): u=+X, v=+Z`
* `face(-Y): u=+X, v=+Z`

For Y faces, `[1, 0]` offsets toward `+X`; `[0, 1]` offsets toward `+Z`.

## Route admissibility

Only opposite faces on X or Y are admitted. Same-face routes report same-face unsupported; mixed-axis routes report route unsupported; Z-axis reports axis-not-yet-supported.

## Center/radius clearance rules

For `Box [10, 8, 6]`, X-axis routes use transverse half-extents `Y=4`, `Z=3`: `abs(u)+radius < 4` and `abs(v)+radius < 3`. Y-axis routes use `X=5`, `Z=3`: `abs(u)+radius < 5` and `abs(v)+radius < 3`. The comparison is strict.

## Trace JSON/text route evidence

Trace JSON exposes semantic intent route evidence with axis, direction, attach face, through face, source target kind, center, radius, and center frame. Trace text prints attach/through selectors, alias resolution when present, route direction, and the face-local frame.

## Artifact/manifest behavior

Y-axis valid fixtures emit STEP, trace JSON, trace text, and `manifest.json` using fixture stems such as `side-hole-y-axis-v2`. The manifest includes route evidence, target sources, resolved selectors, radius, center, center frame, `controlledFixtureOnly=true`, and `generalSideHoleSupport=false`.

## Valid and invalid fixtures

Valid fixtures added: direct `side-hole-y-axis-v2`, direct `side-hole-reverse-y-v2`, and alias `side-hole-aliases-y-axis-v2`. Invalid fixtures cover Z-axis unsupported, mixed Y-to-X, Y-axis X/Z clearance boundaries, and alias wrong-through.

## What this proves

The parser-backed controlled side-hole lowering can preserve and report a second controlled axis through the existing AIR Region golden path.

## What this does not prove

No Z-axis, mixed-axis, arbitrary face-pair, arbitrary local frame, blind hole, generic Boolean admission, CIR topology authority, shell, fillet, chamfer, surfacing, pattern, material/FEA, templates/concepts/PMI/where parser, STEP PMI export, or STEP importer/exporter behavior is introduced.

## Tests run

Run the active .NET build/test and targeted Firmament V2 side-hole tests for this milestone.

## Next milestone recommendation

AIR-FIRMAMENT-X11 — controlled V2 side-hole Z-axis face-pair variation. X11 must define `face(+Z)` and `face(-Z)` local frames before implementation; likely `u=+X, v=+Y` for both faces.

## X11 follow-up

AIR-FIRMAMENT-X11 extends the same controlled opposite-face policy to Z-axis routes (`+Z -> -Z` and `-Z -> +Z`) and preserves the X10 Y-axis route evidence and clearance behavior.

## X12 follow-up

AIR-FIRMAMENT-X12 keeps the Y-axis routes unchanged and moves their route/frame/clearance facts into `FirmamentV2SideHoleRoutePolicy`; see `docs/air-firmament-x12-side-hole-route-policy.md`.
