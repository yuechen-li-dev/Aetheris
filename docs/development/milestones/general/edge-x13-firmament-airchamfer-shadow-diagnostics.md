# EDGE-X13 — Firmament AirChamfer shadow diagnostics probe

## Purpose and scope

EDGE-X13 adds a non-authoritative, test-only Firmament-facing diagnostics probe for the supported controlled AirChamfer row. The probe answers whether a Firmament execution can surface an internal AirChamfer shadow sidecar for the tightly bounded convex plane-plane single-edge case without changing the actual production result.

This is a diagnostics milestone only. It does not promote AirChamfer to production authority and does not change normal Firmament chamfer or fillet behavior.

## References

- EDGE-A1: `docs/development/milestones/general/edge-a1-chamfer-fillet-support-compatibility-matrix.md` defines CH-03 as the strongest AirChamfer row: convex plane-plane single-edge, controlled body, shadow/artifact/corpus supported, not production-authoritative.
- EDGE-V3: `docs/development/milestones/general/edge-v3-air-chamfer-shadow-route.md` defines `AirChamferShadowRoute`, the internal/test-only non-authoritative route used by this probe.
- EDGE-X10: `docs/development/milestones/frictionlab/edge-x10-airchamfer-cube-step-artifact.md` introduced the first CLI-visible controlled AirChamfer STEP artifact.
- EDGE-X11: `docs/development/milestones/frictionlab/edge-x11-airchamfer-step-artifact-corpus.md` introduced the tiny deterministic AirChamfer artifact corpus.
- EDGE-X12: `docs/development/milestones/frictionlab/edge-x12-airchamfer-corpus-stability.md` gated repeated-run corpus stability.

## Firmament seam chosen

The chosen seam is a **Firmament test-only sidecar fixture** under `Aetheris.Kernel.Firmament.Tests`.

The fixture compiles and executes a normal Firmament box plus bounded chamfer operation through the existing production path. After that production result exists, the test-only probe invokes `AirChamferShadowRoute` against the controlled pre-chamfer source body and records a separate report.

This is Firmament-facing because the motivating body and legacy chamfer route are produced by the real Firmament compiler/executor. It is not a public API, not a CLI command, and not a production runtime option.

## True Firmament-facing vs. FrictionLab-only status

EDGE-X13 is **Firmament-facing test-only**, not FrictionLab-only. The shadow candidate still uses the FrictionLab `AirChamferShadowRoute->AirChamferRealBodyPrototype` candidate path, but the probe is attached around a real Firmament fixture and asserts the real Firmament production result before and after sidecar evaluation.

## Shadow diagnostics model

The report captures deterministic machine-checkable fields:

- seam: `Firmament test-only sidecar`
- production authority: `BrepBoundedChamfer`
- candidate path: `AirChamferShadowRoute->AirChamferRealBodyPrototype`
- shadow route status
- candidate body topology summary
- STEP smoke summary when candidate body is available
- feature-recognition parity summary
- recommendation
- diagnostics

Required EDGE-X13 diagnostics include:

- `edge-x13-firmament-shadow-probe-started`
- `edge-x13-firmament-production-route-executed`
- `edge-x13-air-chamfer-shadow-route-invoked`
- `edge-x13-shadow-candidate-produced`
- `edge-x13-shadow-feature-recognition-captured`
- `edge-x13-shadow-step-smoke-succeeded`
- `edge-x13-legacy-authority-preserved`
- `edge-x13-production-output-unchanged`
- `edge-x13-no-production-route-replacement`
- `edge-x13-no-3d-boolean-used`
- `edge-x13-shadow-deferred:<reason>` for disabled or unsupported deferred rows
- `edge-x13-shadow-rejected:<reason>` for invalid unsupported rows

Allowed EDGE-X13 recommendations are:

- `air-chamfer-firmament-shadow-ready-for-controlled-opt-in`
- `air-chamfer-firmament-shadow-needs-diagnostic-seam`
- `air-chamfer-firmament-shadow-needs-recognition-hardening`
- `air-chamfer-firmament-shadow-rejected-invalid`
- `air-chamfer-firmament-shadow-deferred-unsupported`
- `air-chamfer-firmament-shadow-keep-legacy-authority`

## Supported controlled case

The only accepted case is:

- controlled Firmament fixture body: 10 × 8 × 6 box;
- one explicitly selected convex planar vertical edge equivalent to `x_max_y_max`;
- adjacent planar faces resolved to deterministic normals `(1,0,0)` and `(0,1,0)`;
- positive finite chamfer distance `1`;
- deterministic convex classification;
- no edge chain;
- no corner chain;
- no legacy-dependent topology flag;
- planar face family only;
- optional cheap STEP smoke enabled for the accepted fixture.

Unsupported/deferred cases are covered for opt-in disabled, non-planar adjacent face marker, edge chain, corner chain, and legacy-dependent topology. These cases do not produce a shadow candidate.

## Production output unchanged guarantee

The probe exports the Firmament legacy chamfer result to STEP before and after shadow sidecar execution and asserts byte-for-byte equality. The shadow candidate body is stored only in the sidecar report; it is not assigned to the Firmament compilation artifact and cannot replace the executed boolean result.

## Legacy authority and no-route-replacement guarantee

Legacy `BrepBoundedChamfer` remains the production authority. The real Firmament operation is still a bounded chamfer executed by the existing Firmament production path, and the probe report declares `BrepBoundedChamfer` as production authority. The AirChamfer sidecar is invoked only after the production result exists.

## No-3D-Boolean guarantee

The AirChamfer candidate path remains `AirChamferShadowRoute->AirChamferRealBodyPrototype`, the same no-3D-Boolean shadow/prototype route used by EDGE-V3/X10/X11/X12. The probe records `edge-x13-no-3d-boolean-used` and does not invoke Boolean fallback in the AirChamfer sidecar.

## Test cases and results

Focused EDGE-X13 tests are in `Aetheris.Kernel.Firmament.Tests/AirChamferFirmamentShadowProbeTests.cs`:

1. supported controlled Firmament fixture produces a shadow candidate;
2. production STEP output before/after the sidecar is unchanged;
3. legacy authority and no-route-replacement diagnostics are present;
4. no-3D-Boolean diagnostic is present;
5. feature-recognition summary is captured;
6. STEP smoke summary is captured;
7. opt-in disabled does not invoke the shadow route or produce a candidate;
8. non-planar, edge-chain, corner-chain, and legacy-dependent cases produce rejected/deferred reports without candidates.

Observed focused result during implementation:

```bash
dotnet test Aetheris.Kernel.Firmament.Tests/Aetheris.Kernel.Firmament.Tests.csproj --filter "AirChamferShadow"
```

Historical result: passed when the repo still multi-targeted. Current validation should use the sole active `net10.0` target framework.

## Non-goals

EDGE-X13 does not add or change:

- production chamfer replacement;
- arbitrary user/model edge selection;
- fillets or fillet geometry;
- edge chains, corner chains, or corner patches;
- STEP exporter/importer behavior;
- Boolean core behavior;
- public API surface;
- production route replacement;
- triangle migration retry;
- sketch solver behavior;
- clipping engine behavior;
- NURBS/freeform support.

## Recommended next milestone

The recommended next milestone is **EDGE-V4 controlled opt-in AirChamfer route** only if EDGE-X13 diagnostics and the EDGE-X12 corpus stability lane remain stable. If future work finds the Firmament seam too awkward for broader diagnostics, use **EDGE-X13.1 diagnostic seam hardening** before any route authorization. If priorities shift to fillets, use **EDGE-A2 / EDGE-FILLET-A0** instead.

## EDGE-V4 follow-up

EDGE-V4 builds on this shadow diagnostic probe by adding a gated internal/test-only Firmament execution seam. The new seam remains off by default, keeps legacy `BrepBoundedChamfer` authoritative for normal execution, and allows the AirChamfer candidate to become the result only for the exact controlled CH-03-style box edge when explicit experimental options and candidate evidence are supplied. Unsupported or failed cases fallback to legacy with deterministic EDGE-V4 diagnostics.
