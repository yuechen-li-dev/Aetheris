# SURF-X3 — Section Chain / Loft consolidation

## Executive verdict

**Meaningful progression.** Aetheris now materializes a useful standalone general 3D body from an ordered semantic chain of changing, fully framed profiles. The real BRep/STEP path preserves shared internal section topology, exact/non-rational representation, deterministic correspondence, local edit identity, and successful STEP reimport. The complete SURF-X3 milestone is not accepted because Firmament text authoring, BodyState `AddSectionChain`/`RemoveSectionChain`, authorized-region/preservation enforcement for those sculpt operations, and a general chain self-intersection proof remain unimplemented.

## Audit and consolidation result

The existing ruled implementation is `RuledSurfaceIr` plus `RuledSurfaceLowering`. It already owns normalized boundary parameter correspondence, line/arc/circle/non-rational-B-spline evaluation, circle-to-cylinder/cone recognition, developability evidence, and the non-rational fallback. SectionChain uses that implementation for every adjacent semantic span; no second ruled formula was introduced.

The ruled path was strengthened in place:

- coplanar line pairs now emit exact `PlaneSurface`;
- compatible polynomial B-spline pairs emit an exact degree `(p,1)` non-rational ruled surface;
- line-to-B-spline uses the B-spline basis' Greville control positions to represent the line exactly in the compatible basis;
- prior saddle and ruled-panel behavior remains on the same lowering path.

Circular Sweep remains intentionally specialized. Its Concept Path parser, planar tangent frame, bend-radius/clearance validation, and analytic segment realization produce exact cylinders for lines and tori for arcs. A generic section-face realization would turn the Paperclip into weaker geometry. Sweep and SectionChain share the ordered-correspondence design law, but not a runtime god-object or a forced BRep emitter.

## SectionChain semantics

`SectionChain` contains ordered `Section` values, adjacent correspondence, `Ruled` transition policy, and explicit start/end termination. Every section contains `SectionId`, `SectionFrame`, and `SectionProfile`. Every profile contains stable semantic spans and an explicit seam span. X3 admits one CCW closed outer loop made from line, positive circular arc, or non-rational polynomial B-spline spans.

Correspondence is strong semantic identity or explicit one-to-one mapping. The qualified mapping preserves seam-relative span order; nearest-geometry inference and topology branching are rejected. Internal profile edges are allocated once and reused by both adjacent transition faces.

`SectionChainEditor.ReplaceSection` records the replaced section, the two rebuilt adjacent transition IDs, distant preserved transitions, and preserved terminations. The flagship edit of `Rise` rebuilds only `PalmFront->Rise` and `Rise->Peak`; five distant transitions remain preserved.

## Surface realization and flagship

The flagship `surf-x3-section-chain-ergonomic-body` has eight stations:

```text
Nose -> Front -> PalmFront -> Rise -> Peak -> PalmRear -> Rear -> Tail
```

Width, height, side roundness, origin, clocking, and plane tilt vary across the chain. `Front -> PalmFront` proves exact line-to-cubic-B-spline semantic span transitions. A separate three-section witness applies 0°, 10°, and 20° frame clocking. A 180° collapse witness is rejected with `section-chain-transition-foldover`.

Fresh CLI artifact evidence from `aetheris section-chain build flagship`:

| Evidence | Result |
|---|---:|
| Sections | 8 |
| Transitions | 7 |
| Bodies / shells | 1 / 1 |
| Faces / edges / vertices | 30 / 60 / 32 |
| Plane surfaces | 4 |
| Non-rational B-spline surfaces | 26 |
| Rational product surfaces | 0 |
| Structural assessment after STEP reimport | enclosed-manifold |
| STEP reimport | succeeded |
| SHA-256 | `5e1a382f4869466269903262c45cacddd5efefe8d7dde07e65bffce7c15a4ec6` |

The generated artifact is local by policy at `artifacts/local/surf-x3/surf-x3-section-chain-ergonomic-body.step`, with sibling structured evidence. Reproduce it with:

```powershell
dotnet run --project Aetheris.CLI -c Release -- section-chain build flagship --out artifacts/local/surf-x3/surf-x3-section-chain-ergonomic-body.step --json
dotnet run --project Aetheris.CLI -c Release -- analyze artifacts/local/surf-x3/surf-x3-section-chain-ergonomic-body.step --json
```

Manual inspection should check the overall silhouette, section flow, unexpected twist, seams, flat spots, foldovers, nose/tail caps, and whether the body reads as an intentionally lofted fairing.

## Validation evidence

Targeted tests cover the eight-section closed body, deterministic export and reimport, exact mixed line/B-spline realization, analytic planar recognition, 20° twist, local section edit identity, shared-edge incidence, profile correspondence failure, profile orientation failure, extreme-twist foldover rejection, BRep binding, export preflight, and CLI artifact evidence. Paperclip's five kernel Sweep tests and CLI semantic-route regression pass unchanged.

The Release solution build completed with zero warnings and zero errors. The full serial .NET run passed 3,170 tests across 17 test assemblies; the FrictionLab assembly currently reports no discoverable tests and did not fail the run. The web client passed 82 tests, TypeScript/Vite production build, and ESLint. The VS Code extension passed TSPack typecheck, 13 tests, bundle build, and VSIX packaging. A freshly packed and locally installed `Aetheris.CLI` tool built and reimported the analytic two-profile witness. The repository layout guard and `git diff --check` pass.

## Remaining acceptance blockers

- A Firmament parser/binder/lowering declaration for arbitrary authored SectionChain values.
- Canonical and invalid Firmament fixture corpus plus fresh-agent A–F qualification.
- `AddSectionChain` and `RemoveSectionChain` against `BodyState`, including attachment/penetration, authorized envelope, preservation, failure atomicity, downstream selector behavior, and actual bounded composition.
- General chain-to-chain self-intersection validation beyond per-profile and per-transition foldover checks.
- Face-local pcurves for the newly emitted SectionChain faces.
- Add/remove flagship artifacts and downstream current-state feature witnesses.

These are real system boundaries, not documentation omissions. The standalone ruled-loft substrate removes the first blocker without pretending the sculpt milestone is complete.
