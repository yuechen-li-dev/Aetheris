# Semantic Profile edge composition M2 report

## Verdict

**Meaningful progression.** Generic single-edge attachment is implemented and
used by ordinary Profiles and Sheet Metal. It materially improves CTC-03's
front/rear mounting-flange outlines. CTC-03 remains `NeedsReview` because
corner-consuming wall tapers and right-wall transitions span two owning edges;
M2 deliberately does not disguise those as single-edge features.

## Why M1 stopped short

M1 resolves semantic members with a moving cursor. A member knows the endpoint
of the preceding member, but has no independent interval on a named owner.
Consequently authors had to state every carrier segment, manually order
features, and manually transform them into the profile frame. M2 adds the
missing interval/owner layer:

```text
named directed edge + independently anchored fragments
  -> validate bounds and conflicts
  -> sort by resolved u interval
  -> generate CarrierNN gaps
  -> lower each semantic member one-to-many
  -> exact replacement chain
  -> ResolvedProfile2D / PlanarContour2
```

The implementation types are `SemanticEdgeProfileIr`,
`SemanticEdgeFragmentIr` and its typed records, `SemanticEdgeAnchorIr`,
`ResolvedSemanticEdgeMemberIr`, `ResolvedSemanticEdgeProfileIr`, and
`SemanticEdgeProfileResolver`.

## Authoring and local frame

```firmament
Concept Struct Layout On XY {
    Rect2 PlateBase { Center: [0mm, 0mm]; Size: [100mm, 60mm] }
    EdgeProfile PlateBase.Bottom {
        Notch CableNotch { FromStart: 18mm; Width: 8mm; Depth: 4mm; Side: Left }
        Tab MountTab { CenteredAt: 50mm; Width: 12mm; Extension: 6mm; Side: Right }
    }
}
Profile Plate From PlateBase
```

`u=0` is the directed owner start and `u=length` its end. Positive `v` is
left of the owner tangent for ordinary Profiles. Exactly one of `FromStart`,
`FromEnd`, or `CenteredAt` is required. Anchors resolve to occupied intervals;
source order is not geometry order. Equal endpoints are allowed, zero-length
carriers are omitted, and overlaps name both fragment paths.

The bounded generic fragment records are `Tab`, `Notch`, `Step`, `Chamfer`,
`Cutback`, and `SteppedNotch`. Tabs/notches/steps lower to three lines,
chamfers/cutbacks to two, and stepped notches to five or seven. These operations
return to the carrier baseline. Arc transition remains an M1 cursor member and
is not yet an independently attached fragment.

Generated paths look like:

```text
Plate.Bottom.Carrier00
Plate.Bottom.CableNotch
Plate.Bottom.MountTab
Plate.Bottom.MountTab.Curve00
```

Template expansion runs before edge binding. A generic Template test varies
plate/tab dimensions and proves that `Plate.Bottom.MountTab` survives
specialization. The non-Sheet-Metal dogfood fixture is
`fixtures/FirmamentV2/Canonical/valid/profile-edge-attachment-plate.firmament`;
it validates, extrudes, exports STEP, and reimports as one enclosed manifold.

## Diagnostics exercised

- `semantic-edge-fragment-overlap:<owner>:<first>:<second>`
- `semantic-edge-fragment-out-of-bounds:<fragment>:...`
- `semantic-edge-invalid-fragment:<fragment>:<kind>`
- `semantic-edge-invalid-stepped-notch:<fragment>`
- `semantic-edge-duplicate-fragment:<fragment>`
- `semantic-edge-anchor-required:<fragment>`
- `semantic-edge-owner-path-missing:<path>`
- `semantic-edge-owner-member-missing:<path>:available=...`

Tests also cover adjacency, declaration reordering, owner-length changes,
stable hashes, one-to-many descendants, and cross-kind Sheet Metal conflicts
before BRep construction.

## Sheet Metal and correspondence

Authored Sheet Metal outer-edge programs now call the generic resolver; the M8
service tab no longer owns its own cursor/span assembly loop. Sheet Metal maps
the resolved local chain into the region plane, carries fragment IDs in
`SheetMetalCorrespondence`, and exposes formed and `Flat.*` Concept Paths.
DFM continues to use semantic feature/region IDs. Full curve-level provenance
inside `SheetRegionIr.Boundary3D` is not yet represented because that record is
still a point loop; exact Profile/PlanarContour descendants do preserve it.

## CTC-03

The source-independent construction adds two semantic programs:

- `Ctc03Layout.FrontConnectorRelief`: symmetric five-curve stepped notch,
  127 mm wide, 31.75 mm deep, 25.4 mm shoulder, 6.35 mm inner chamfer.
- `Ctc03Layout.RearConnectorRelief`: symmetric seven-curve stepped notch,
  139.7 mm wide with 6.35 mm outer and inner chamfers.

The source STEP was used to recover dimensions and only for post-generation
comparison. The final `.firmament` source has no STEP path, face ID, edge ID,
or recovered polygon and still compiles from an isolated temporary copy.

| Measure | M8/Profile-M1 | Profile-M2 |
|---|---:|---:|
| formed source -> generated RMS | 19.462739 | 10.614040 mm |
| formed source -> generated p95 | 52.484591 | 19.071261 mm |
| formed source -> generated max | 56.694590 | 52.816066 mm |
| formed generated -> source RMS | 6.660487 | 6.750457 mm |
| formed generated -> source p95 | 12.735724 | 8.310053 mm |
| formed generated -> source max | 19.071261 | 19.071261 mm |
| flat width residual | 0.002447 | 0.002447 mm |
| flat height residual | 12.707820 | 0.007820 mm |
| flat contour RMS | 12.376320 | 12.038486 mm |
| flat p95 / max | 19.047460 / 19.047460 | 19.047460 / 19.047460 mm |

All seven bends and all 17 openings remain comparison passes. Formed/flat STEP
export and reimport remain manifold. The two fragment paths appear in both
formed and flat inspection. Front/rear central outline composition is now easy;
the remaining anonymous-curve pressure occurs at left/right wall ends and
mounting-flange corners, where one manufacturing corner consumes endpoints on
two adjacent owners.

## Performance, determinism, and validation

One measured CTC run: parse 26.5 ms, semantic resolve 16.1 ms, formed lower
73.3 ms, authored flat lower 58.7 ms, flatten 7.0 ms. Two independent runs
produced flat hash
`e3ec913e24f65d220839bbdbfa2d909dd8bdeeaed87ecc9ccfc6c1be7a1d297c`.

Validation performed:

- `dotnet restore Aetheris.slnx`: pass.
- `dotnet build Aetheris.slnx --no-restore`: pass, zero warnings/errors.
- Profile-M2 tests: 9/9 pass.
- Sheet Metal Profile-M2 tests: 2/2 pass.
- Firmament suite: 1148/1148 pass after final rebuild.
- Sheet Metal suite: 60/60 pass.
- CLI validation/build/STEP reimport: pass; enclosed manifold.
- Full solution run: all feature suites pass; Kernel.Core had two unrelated
  timing assertions fail under parallel load. Both passed immediately in
  isolated reruns; a later full Kernel.Core run exposed a different timing
  assertion, which also passed in isolation. This is recorded as existing
  performance-test nondeterminism, not hidden as an all-green full run.
- `git diff --check`: no errors (repository line-ending warnings only).

## Architecture answer and debt

For bounded, baseline-returning modifications, Firmament can now attach tabs,
steps, chamfers, notches, cutbacks, and a repeated stepped-notch shape to a
meaningful named Profile edge while the compiler owns local coordinates,
ordering, carrier spans, exact curves, paths, and deterministic hashes. That
removes the need to redraw the full Profile for those features, and fragment
identity survives into formed/flat Sheet Metal inspection and correspondence.

Code quality is an acceptable localized prototype. The generic resolver and IR
are clean; the Profile frontend remains the intentionally bounded regex/block
adapter documented since M1. The single largest blocker is now explicit
cross-edge corner ownership: one bounded corner program must be allowed to
consume both adjacent carrier endpoints and publish the resulting shared
endpoint without introducing a general sketch or Boolean solver.
