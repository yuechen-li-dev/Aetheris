# AIR-FIRMAMENT-X9 — controlled V2 side-hole reverse-X face-pair variation

## Purpose and scope

X9 extends the parser-backed Firmament V2 side-hole path from the canonical `face(+X) -> face(-X)` route to the controlled reverse route `face(-X) -> face(+X)`. This milestone proves the side-hole semantic intent and golden trace path are not hardcoded to attach only to `+X`.

## Relationship to X4-X8

- X4 introduced direct-selector V2 side-hole syntax for `+X -> -X`.
- X5 locked generated artifacts and parity for the canonical V2 route.
- X6 added radius variation.
- X7 added face-local center offsets.
- X8 added semantic alias targets for attach and through faces.
- X9 keeps those paths intact and adds only the controlled reverse-X pair.

## Supported route set

- `+X -> -X`
- `-X -> +X`

## Unsupported routes

Y-axis, Z-axis, mixed-axis, and same-face routes remain unsupported. Blind holes and arbitrary face pairs are not admitted.

## Source syntax examples

Direct reverse-X:

```firmament
region sideHole on face(-X) {
    cut Cylinder {
        radius: 1
        center: [1, 0]
        through: face(+X)
    }
}
```

Alias reverse-X:

```firmament
expose {
    face(-X) => left
    face(+X) => right
}

modify base {
    region sideHole on left {
        cut Cylinder {
            radius: 1
            center: [1, 0]
            through: right
        }
    }
}
```

## Face-local coordinate convention

- `face(+X)`: `u=+Y`, `v=+Z`.
- `face(-X)`: `u=+Y`, `v=+Z`.

For a `[10, 8, 6]` box, clearance remains strict: `abs(u) + radius < 4` and `abs(v) + radius < 3`.

## Route admissibility

The parser admits only opposite X-face routes in either direction. Same-face, mixed-axis, Y-axis, Z-axis, unknown aliases, aliases resolving to non-face refs, and aliases resolving to unsupported faces are rejected with stable diagnostics.

## Trace JSON/text route evidence

Trace JSON preserves attach and through target sources, target kinds, resolved faces, route direction (`+X->-X` or `-X->+X`), radius, center, and center frame. Trace text prints the original target, alias resolution when applicable, route direction, and the face-local center frame.

## Artifact/manifest behavior

Reverse-X fixtures emit STEP, trace JSON, trace text, and manifest artifacts using reverse-X stems. Manifest data includes route direction, attach face, through face, target source facts, resolved selectors, radius, center, center frame, `controlledFixtureOnly=true`, and `generalSideHoleSupport=false`.

## Valid and invalid fixtures

Valid fixtures added:

- `fixtures/FirmamentV2/Region/valid/side-hole-reverse-x-v2.valid.firmfixture`
- `fixtures/FirmamentV2/Region/valid/side-hole-aliases-reverse-x-v2.valid.firmfixture`

Invalid fixtures added:

- `fixtures/FirmamentV2/Region/invalid/side-hole-same-face-x-v2.invalid.firmfixture`
- `fixtures/FirmamentV2/Region/invalid/side-hole-mixed-axis-x-to-y-v2.invalid.firmfixture`
- `fixtures/FirmamentV2/Region/invalid/side-hole-y-axis-not-yet-supported-v2.invalid.firmfixture`
- `fixtures/FirmamentV2/Region/invalid/side-hole-alias-reverse-x-wrong-through-v2.invalid.firmfixture`

## What this proves

The parser-backed side-hole lowering path is not hardcoded to attach `+X` only; both direct and alias reverse-X fixtures reach the controlled golden trace path.

## What this does not prove

X9 does not add arbitrary face-pair support, Y/Z axes, arbitrary local frames beyond controlled X faces, blind holes, generic Boolean admission, CIR topology authority, shell/fillet/chamfer/surfacing/pattern/material/FEA features, templates/concepts/PMI/where parsing, STEP PMI export, STEP importer behavior changes, or production route replacement.

## Tests run

Use the active suite and targeted parser/CLI tests for reverse-X, aliases, radius, center offsets, artifacts, traces, and invalid diagnostics.

## Next milestone recommendation

AIR-FIRMAMENT-X10 — controlled V2 side-hole Y-axis face-pair variation. X10 must define `face(+Y)` and `face(-Y)` local frames before implementation; a possible policy is `u=+X`, `v=+Z` for both Y faces.


Update: AIR-FIRMAMENT-X10 extends the controlled side-hole route set to Y-axis opposite faces; see `docs/air-firmament-x10-v2-side-hole-y-axis.md`.
