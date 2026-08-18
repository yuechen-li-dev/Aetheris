# AIR-FIRMAMENT-X7 — V2 side-hole center offset

## Purpose and scope

X7 generalizes the parser-backed Firmament V2 controlled side-hole path from radius-only variation to a bounded face-local center-offset parameter.

## Relationship to X4, X5, and X6

- X4 introduced the parser-backed V2 `sideHole` source shape and lowered it to the existing AIR Region golden trace chain.
- X5 locked generated-on-demand V2 side-hole artifacts and X13 parity.
- X6 proved the same path is parameterized by radius.
- X7 keeps that same controlled path and adds only center offset evidence.

## Controlled center-offset-only variation

The supported shape remains one Box solid named `base`, one `modify base`, one `region sideHole on face(+X)`, one `cut Cylinder`, and `through: face(-X)`.

## Supported source syntax

`cut Cylinder` accepts optional `center: [u, v]`. If omitted, center defaults to `[0, 0]` and is marked non-explicit in semantic intent.

## Face-local coordinate convention

For `face(+X)`, `u=+Y` and `v=+Z`. The stable frame label is `face(+X):u=+Y,v=+Z`.

## Valid center fixtures

- `fixtures/Region/valid/side-hole-center-y1-v2.valid.firmfixture`
- `fixtures/Region/valid/side-hole-center-z1-v2.valid.firmfixture`
- `fixtures/Region/valid/side-hole-center-y1-z1-v2.valid.firmfixture`

## Invalid center fixtures

- `fixtures/Region/invalid/side-hole-center-y-boundary-v2.invalid.firmfixture`
- `fixtures/Region/invalid/side-hole-center-z-boundary-v2.invalid.firmfixture`
- `fixtures/Region/invalid/side-hole-center-arity-one-v2.invalid.firmfixture`
- `fixtures/Region/invalid/side-hole-center-arity-three-v2.invalid.firmfixture`

## Center/radius admissibility and clearance rule

For Box `[10, 8, 6]`, the transverse half extents are `Y=4` and `Z=3`. X7 admits only strict clearance:

```text
abs(u) + radius < YHalfExtent
abs(v) + radius < ZHalfExtent
```

Tangent/equality cases are rejected with `firmament-v2-side-hole-center-exceeds-clearance`.

## Trace JSON/text center evidence

Trace JSON preserves `centerU`, `centerV`, `centerExplicit`, and `centerSelectorFrame` in semantic intent. AIR Region trace evidence carries the same center into the circle profile. Text output includes the center and the face-local frame.

## Artifact behavior for offset-specific fixtures

Generated-on-demand artifacts derive offset-specific stems such as `side-hole-center-y1-v2.step`, `side-hole-center-y1-v2.trace.json`, and `side-hole-center-y1-v2.trace.txt`, plus `manifest.json`. The manifest records radius, center, tool, and through selector.

## What this proves

V2 side-hole lowering can carry a face-local 2D coordinate parameter through source, semantic intent, AIR Region trace evidence, and generated artifact manifests.

## What this does not prove

X7 does not add arbitrary face pairs, arbitrary local frames beyond +X, blind holes, general side-hole support, generic Boolean admission, or CIR topology authority.

## Tests run

Run the active .NET build/test commands for X7 changes, including targeted Firmament V2 side-hole center tests and CLI trace/artifact tests.

## Next milestone recommendation

AIR-FIRMAMENT-X8 — controlled V2 side-hole selector aliases using X3 exposures, for example `face(+X) => right` and `face(-X) => left`.



X10 note: controlled Y-face frames are now `face(+Y): u=+X, v=+Z` and `face(-Y): u=+X, v=+Z`; X-face frames remain unchanged.

## X11 center-frame extension

AIR-FIRMAMENT-X11 adds Z-face local frames: `face(+Z): u=+X, v=+Y` and `face(-Z): u=+X, v=+Y`. Existing X/Y center-frame conventions remain unchanged.

## X12 follow-up

AIR-FIRMAMENT-X12 keeps the strict center-clearance rule and centralizes the face-local center frames and transverse half extents in `FirmamentV2SideHoleRoutePolicy`; see `docs/development/milestones/general/air-firmament-x12-side-hole-route-policy.md`.
