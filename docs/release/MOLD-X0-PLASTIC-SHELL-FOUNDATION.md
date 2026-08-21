# MOLD-X0 — Plastic Shell Foundation

## Executive verdict

**Meaningful progression**

Aetheris can now define and realize a bounded molded plastic shell from `PlasticShell` manufacturing intent instead of a post-hoc Shell/Draft sequence. The real path is:

```text
PlasticShell source
→ retained PlasticShellIr
→ wall/tooling/parting constraint checks
→ exact drafted thin-wall realization
→ thickness, draft, and pullability evidence
→ gate/ejector validation and AutoRib judgment
→ GeometricDelta
→ rational-free STEP AP242 plus evidence sidecar
```

This is not yet Accepted because standoff and selected-rib added material is not grafted into the single product boundary, and the admitted exterior is a bounded analytic frustum rather than the requested accepted SURF freeform housing. Those are concrete geometry-lowering blockers; representing them as disconnected solids would violate the one-solid acceptance condition.

## Domain architecture

`PlasticShellIr` retains shell identity, exterior authority, material identity, `PlasticWallPolicy`, normalized tooling direction, explicit `PlasticPartingPlane`, minimum draft, gates, standoffs, ejectors, preservation targets, and `PlasticAutoRibRequest`. Parser syntax does not expand into generic Shell and Draft operations.

The module reuses SURF `BodyStateId`, `GeometricDelta`, preservation identifiers, and authorized influence envelopes. AutoRib uses the shared `JudgmentEngine`; eligibility is a hard gate and utility cannot override it.

## Thickness algorithm

The X0 exterior family is a coaxial frustum with radius

```text
r_o(z) = R_b + kz,  k = (R_t - R_b) / H.
```

The inner conical support is the exact parallel offset at normal distance `T`:

```text
r_i(z) = R_b + kz - T sqrt(1 + k²),  z ∈ [T, H].
```

The bottom inner plane is `z = T`. Admission requires positive inner radii and height clearance. Independent thickness evidence consumes the paired-support witnesses and measures exact cone-to-cone normal distance and plane-to-plane distance. No medial-axis or general freeform thickness claim is made. X0 has no local correction path: a collapsed exact offset fails with `plastic-shell-wall-offset-collapse` and never deforms the protected exterior.

Flagship wall evidence:

- Requested nominal: 2.2 mm
- Minimum measured: 2.2 mm at `(55, 0, 0)`
- Maximum measured: 2.2 mm at `(55, 0, 0)`
- Mean: 2.2 mm
- Violations: none
- Strength: exact analytic on the admitted topology

## Draft and pullability

Draft is the signed cone semi-angle relative to +Z:

```text
θ = atan((R_t - R_b) / H).
```

The flagship realizes `5.710593137499643°` against a `3°` requirement on both outer and parallel-offset inner conical walls. The outer wall is cavity-side, the inner wall and inner bottom are core-side, and the annular rim is the parting boundary.

Pullability uses exact monotone-generatrix classification for the admitted coaxial family. Negative taper is an undercut; insufficient positive taper is a draft conflict. This is bounded directional accessibility, not general ray certification. The flagship has no undercut regions.

## Gate and geometric flow proxy

`MainGate` is an edge gate at `(0, 57, 20)` associated with `TopAnnularRim`. The proxy uses bounded geometric distance statistics only:

- Maximum proxy distance: 111.8 mm
- Representative mean proxy distance: 69.316 mm

It does not compute pressure, fill time, temperature, weld lines, air traps, shrinkage, or warpage.

## Standoffs and ejectors

The flagship retains four PCB standoffs (`PcbA`–`PcbD`) with position, height, outer diameter, core-hole diameter, and support intent. It retains four 4 mm ejector contacts (`E1`–`E4`) on the core floor. Analytic radial containment and circle-clearance checks show all ejectors are core-accessible, collision-free against standoff envelopes, and outside the protected exterior.

Ejectors remain tooling contacts and are not subtracted as product holes. Standoffs remain semantic product features in this progression; their cylindrical added material is not present in the STEP boundary.

## AutoRib judgment

The request generates two deterministic networks from the four support nodes. The fan root is the standoff nearest the declared gate, so moving the gate changes the proposed graph. Flow compatibility is computed from mean gate-to-edge-midpoint distance normalized by shell diameter; support is graph connectivity plus edge redundancy; the sink proxy penalizes authored thickness ratio and node convergence. Both flagship candidates pass thickness-ratio, height/draft, measured minimum-spacing, parting-family, and named-keepout gates.

| Candidate | Eligible | Support proxy | Flow compatibility | Sink proxy | Rib length (mm) | Complexity | Utility |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| perimeter-network | yes | 1.000 | 0.477011 | 0.6825 | 200.000000 | 0.500 | 0.6839084810544059 |
| gate-oriented-fan | yes | 0.925 | 0.557773 | 0.6200 | 170.710678 | 0.375 | 0.6975702688965096 |

The Judgment Engine selects `gate-oriented-fan`. Candidate metrics and the deterministic tie-break basis survive in the JSON evidence. The stiffness and sink values are geometric proxies, not FEA or verified sink analysis. Rib solid realization is the next isolated blocker.

## BodyState, locality, and interfaces

The delta reads the protected exterior and parting plane, preserves `EnclosureHalf.ExteriorDesignSurface` and `MountingInterface`, and authorizes only generated interior/manufacturing regions inside `[-57,57] × [-57,57] × [0,20]` mm. Exact exterior parameters are unchanged. The assembly-interface name survives semantic evidence, but no face-bound interface binding was present in the new fixture to round-trip.

## Flagship and representation inventory

- Model: `MoldX0ElectronicsEnclosureHalf`
- Material: `ABS` identity only; no unsourced constitutive or process properties were added
- Tooling direction: +Z
- Parting plane: origin `(0,0,20)`, normal +Z
- Body topology: 1 body, 1 closed shell, 5 faces, 4 edges, 4 vertices
- STEP reinspection: `enclosed-manifold`
- Planes: 3
- Cylinders: 0
- Cones: 2
- Spheres: 0
- Tori: 0
- Non-rational B-splines: 0
- Rational product surfaces: 0

## Diagnostics and fixtures

Focused invalid fixtures exercise wall collapse, draft conflict, negative-taper undercut, invalid parting, invalid gate, invalid ejector, and no eligible rib network. Canonical fixtures exercise the minimal shell and flagship enclosure. Public docs state the precise bounded scope and explicitly reject Moldflow and mold-tool claims.

## Manual artifact

- STEP: `artifacts/local/mold-x0/mold-x0-plastic-shell-enclosure.step`
- Evidence: `artifacts/local/mold-x0/mold-x0-plastic-shell-enclosure.plastic-shell.json`
- SHA-256: `e9f583677771c4be940e11a64607e35488a5341b48a5fb107addd2589e29b430`

Please inspect the STEP manually for the exterior taper, open rim, inner wall, bottom thickness, parting location, unexpected undercuts, and topology artifacts. Standoffs, ribs, gate, and ejectors are intentionally semantic annotations in this progression and should not be expected as visible solids.

## Remaining blocker

The next convergent step is a topology-authoritative feature graft that integrates drafted cylindrical standoffs and drafted rib networks with the inner bottom and wall while preserving one closed manifold, exact analytic surfaces, locality correspondence, and zero rational product surfaces. After that, the same offset/verification interfaces can be generalized to accepted non-rational SURF patches with adaptive draft and accessibility evidence.

## Qualification record

- Release solution build: passed, 0 warnings and 0 errors.
- Full serial .NET suite: 3,151 tests passed; the existing `Aetheris.FrictionLab.Tests` assembly reports no discoverable tests.
- PlasticShell domain suite: 11 tests passed, including parser/IR, exact wall, STEP reimport, rational prohibition, seven focused invalid fixtures, deterministic judgment, and gate-dependent fan generation.
- Canonical fixture qualification: 99 fixtures passed, including both PlasticShell fixtures.
- Real CLI `build`, `validate`, `inspect`, `analyze`, and `verify`: passed. Reimport is one enclosed, orientation-consistent body/shell; external CAD display remains a requested manual step.
- Client: 16 test files / 82 tests passed; production build and lint passed.
- VS Code extension via TSPack: sync/check/typecheck, 13 tests, build, and VSIX packaging passed. TSPack repeated the repository's acknowledged multi-version and blocked lifecycle-script notices.
- Repository layout guard and `git diff --check`: passed.
- Independent fresh-agent A–G review: correct PlasticShell/Standoff/Gate/AutoRib authoring and bounded pullability/export/judgment evidence were discoverable. Reviewers independently confirmed the same remaining blockers: no freeform exterior realization and no standoff/rib material in the B-rep.
- NativeAOT Forge was not affected by this managed built-in domain addition.
