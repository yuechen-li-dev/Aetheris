# AIR-FIRMAMENT-X12 — V2 side-hole route policy

## Purpose and scope

AIR-FIRMAMENT-X12 centralizes the controlled Firmament V2 side-hole route, frame, and clearance rules into `FirmamentV2SideHoleRoutePolicy`. Scope is deliberately limited to parser-backed V2 side-hole cuts through `Box` target solids.

## Relationship to X4-X11

X4 introduced the canonical `+X -> -X` route. X5 locked artifact reporting. X6 added radius variation. X7 added center offsets. X8 allowed FaceRef aliases as feature targets. X9, X10, and X11 added reverse-X, Y-axis, and Z-axis opposite-face routes. X12 does not add a seventh route; it consolidates the six routes already proven by X4-X11.

## Why consolidation is needed

Before X12, route support checks, face-local center frame strings, transverse extents, clearance checks, trace facts, and manifest facts were spread across parser and intent/reporting code. X12 makes the route table the source of truth so future route changes do not require duplicating axis-specific conditionals.

## Supported route table

| Route | Axis | Attach face | Through face | u axis | v axis | u half extent | v half extent |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `+X -> -X` | X | `+X` | `-X` | `+Y` | `+Z` | Y/2 | Z/2 |
| `-X -> +X` | X | `-X` | `+X` | `+Y` | `+Z` | Y/2 | Z/2 |
| `+Y -> -Y` | Y | `+Y` | `-Y` | `+X` | `+Z` | X/2 | Z/2 |
| `-Y -> +Y` | Y | `-Y` | `+Y` | `+X` | `+Z` | X/2 | Z/2 |
| `+Z -> -Z` | Z | `+Z` | `-Z` | `+X` | `+Y` | X/2 | Y/2 |
| `-Z -> +Z` | Z | `-Z` | `+Z` | `+X` | `+Y` | X/2 | Y/2 |

For the canonical `Box [10, 8, 6]`, X/2 is 5, Y/2 is 4, and Z/2 is 3.

## Face-local frame table

| Attach face | Center frame |
| --- | --- |
| `+X` | `face(+X):u=+Y,v=+Z` |
| `-X` | `face(-X):u=+Y,v=+Z` |
| `+Y` | `face(+Y):u=+X,v=+Z` |
| `-Y` | `face(-Y):u=+X,v=+Z` |
| `+Z` | `face(+Z):u=+X,v=+Y` |
| `-Z` | `face(-Z):u=+X,v=+Y` |

## Clearance rule

All supported routes use the same strict clearance predicate:

```text
abs(centerU) + radius < uHalfExtent
abs(centerV) + radius < vHalfExtent
```

Equality is rejected. Radius-only clearance is also checked against the smaller transverse half extent.

## Route rejection policy

The policy returns stable diagnostics:

- same attach/through face: `firmament-v2-side-hole-same-face-unsupported`
- mixed-axis or otherwise unsupported route: `firmament-v2-side-hole-route-unsupported`
- radius too large for the route frame: `firmament-v2-side-hole-radius-exceeds-clearance`
- center at or beyond route clearance: `firmament-v2-side-hole-center-exceeds-clearance`

Alias resolution diagnostics remain in parser alias handling because the route policy only receives resolved face facts.

## JudgmentEngine decision

JudgmentEngine is **not used** in X12. The route decision is deterministic lookup in a six-row table plus deterministic clearance predicates. A JudgmentEngine layer would add scoring ceremony without competing interpretations, and AGENTS.md guidance says not to use JudgmentEngine for simple deterministic transformations or ordinary dispatch.

## Trace/manifest route policy evidence

`FirmamentV2SideHoleIntent.RouteEvidence` now exposes normalized policy facts: axis, direction, attach/through faces, center frame, u/v axes, and u/v half extents. Trace JSON serializes this under `firmamentV2.semanticIntent.routeEvidence`; artifact manifests serialize the same evidence under `route`. Existing route, attach face, through face, and center frame fields remain available for compatibility.

## Fixtures/tests covered

The X12 tests cover policy resolution for all six routes, frame and half-extent correctness, same-face rejection, mixed-axis rejection, strict center-boundary rejection, direct X/Y/Z golden paths, alias X/Y/Z golden paths, manifest policy facts, trace JSON policy facts, and prior X4-X11 side-hole fixture success.

## What this proves

X12 proves route/frame/admissibility policy is centralized for controlled Firmament V2 Box side-hole routes and that parser semantic validation, semantic intent, trace JSON, and artifact manifest facts consume the same resolved route policy evidence.

## What this does not prove

X12 does not prove mixed-axis routes, same-face routes, blind holes, oblique holes, non-box target solids, arbitrary center frames, general Boolean admission, multiple cuts, multiple regions, or reusable non-side-hole prismatic through-cut admission.

## Tests run

- `dotnet restore Aetheris.slnx`
- `dotnet build Aetheris.slnx -f net10.0 --no-restore`
- `./scripts/test-active.sh`
- targeted kernel and CLI Firmament V2 side-hole filters
- CLI trace spot checks for X, reverse-X, Y, Z, and mixed-axis invalid fixtures
- artifact spot check for Z-axis side-hole manifest evidence

## Next milestone recommendation

AIR-FIRMAMENT-X13 — transition side-hole route policy into reusable prismatic through-cut policy.
