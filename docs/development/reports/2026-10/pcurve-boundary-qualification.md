# Pcurve boundary qualification performance — 2026-10-26

Baseline: Aetheris `aaaa2cce`. Scope: pcurve recovery/qualification in the existing
kernel owner. No inverse fitting, recovered coordinates, engineering tolerances,
STEP emission, parser routes or display authority were changed.

## Finding and repair

A fresh import of the axis coil contained 4,096 spline-boundary coedges. Each
received an 8,193-point lift check. A per-coedge probe measured about 89 ms in
recovery and 4,336 ms in those checks. Most recovered pcurves were already simple
lines on polynomial Bezier patch boundaries. More aggressive fitting would not
address that measured cost.

`BrepPcurveLiftBounds` now bounds the actual recovered line against the actual 3D
edge over its full trim. For a clamped polynomial tensor-product Bezier patch,
paired boundary/edge control differences share the same Bernstein basis, so
their maximum norm bounds the entire curve difference. Derivative control hulls
bound the additional error from the recovered line's displacement in U and V.
Endpoint errors bound that whole affine displacement. The allowance also covers
the existing evaluators' end snapping and floating-point arithmetic; calculations
that cannot produce a finite bound fall back.

The fast family is deliberately bounded: degrees 1–8, polynomial supports and
edges, single clamped spans, matched varying parameter domains, finite
parameter spans above 1e-9, and a line pcurve with the actual edge trim domain. Both
boundary directions, reversal and partial trims are handled. No source filename,
trusted-import flag or kernel-origin claim is accepted as evidence.

Qualification uses this bound only when it fits the existing resolved tolerance.
Otherwise the previous 1,025/8,193-point checks remain. General rational and
multi-span recovery is unchanged. Retained source pcurves keep their existing
checks and provenance. The implementation has no global mutable cache/state.

BRep evidence reports `BezierBoundaryControlHull`, zero lift samples and the
conservative upper bound. Origin remains RecoveredSpline. Zero samples is honest
proof metadata rather than a fictitious claim that the dense grid ran. The
surface inspection guide and API XML explain the distinction. A NURBS surface
can itself be exact; approximation introduced by exchange/conversion remains
subject to the existing recovery budget.

## Timing protocol and results

Windows, .NET 10.0.401, same host, frozen baseline/candidate CLI directories.
Each invocation starts a new process; this is process-cold with a warm OS file
cache, not a reboot/storage-cold test. Three paired trials per source alternate
baseline/candidate order. No other agent-owned qualification workload ran during
these trials. Baseline and candidate emit the same engineering artifacts.

| Command/input | Baseline median s | Candidate median s | Speedup |
|---|---:|---:|---:|
| validate axis-coil | 7.522 | 3.370 | 2.23× |
| validate helix | 7.428 | 3.309 | 2.25× |
| validate coil-on-stem | 5.983 | 3.041 | 1.97× |
| build guitar | 9.257 | 9.354 | 0.99× |

The guitar control has no demonstrated improvement; its roughly 1% difference
lies within the overlapping sample ranges. This is not a claim that every
source exceeds the earlier strict 2× target: coil-on-stem measures 1.97×.

An initial standalone importer profile fell from 6.619 s to 2.448 s total.
Pcurve recovery/qualification fell to 0.311 s; orientation still took 1.679 s.
This isolates the next dominant import cost without changing it in this pass.
Those profiles and the paired timings are distinct experiments, not combined
into an invented composite timing.

## Qualification

- Sixteen focused Core tests cover all four boundaries and reversed curve
  direction, partial/nondefault domains, UV fitting error, degrees 1–8, large
  coordinates, off-domain/rational/multi-span exclusions, interior-trim fallback,
  geometry preservation and an off-surface curve whose endpoints/midpoint agree.
- Two real compiler tests build axis-coil and helix, import their generated STEP,
  require bounded qualification, then independently check every recovered trim
  and UV-loop closure with a separate 4,099-point grid. These generated imports
  are the test subject; external corpus tests retain Step242Corpus ownership.
- Full regression, canonical STEP parity and dense-bound comparison completion
  are recorded below. No generated logs/STEP/JSON outputs are tracked.

Ignored evidence: `artifacts/local/pcurve-perf/`. `baseline-profile.json` separates
recovery from dense checks per coedge; `paired-timings.json` retains every sample;
`baseline-cli` and `candidate-cli` freeze the compared executables. Reproduce with
normal CLI validate/build on `fixtures/Canonical/WireForm/{axis-coil,helix}.firmament`,
`Assembly/coil-on-stem.firmament`, and `AssemblyInterfaces/GuitarX0/guitar-x0.firmament`.

This replaces redundant brute-force qualification for a provable family. It does
not reduce a physical tolerance to chase a timing number, and it does not claim
that sampled fallback is a universal continuous-error certificate.

## Completion and verdict

**Success for the bounded optimization.** The motivating builds are materially
faster, the tolerance contract is preserved, and the actual engineering outputs
remain unchanged. The earlier strict 2× target is still narrowly missed by the
coil-on-stem median (1.97×); no universal 2× promise is made.

- Release solution build: zero errors (`build.log`).
- Core fast lane: 1,127 passed (`fast.log`).
- Full solution: **6,117 passed, zero failed, two existing Drawing skips** across
  20 nonempty test projects (`full.log`); empty FrictionLab remains empty.
- Real compiler corpus: **380 sources**, 322 validate, 297 STEP builds succeed,
  **zero exit-status or SHA-256 differences**, no timeouts (`corpus/`). This is
  compared with the prior fully qualified corpus, not a newly recorded baseline.
- The unchanged full NIST/OCCT interchange lane passed without golden updates.
- `dense-bound-comparison.json` reapplies the old 8,193-point check independently
  to all **4,096** bounded coil coedges. **Zero bound underestimates**. Worst
  observed dense deviation is below 9.1e-9 mm; the largest reported bound is below
  9.5e-9 mm. These are internal numerical errors, not manufacturing callouts.
- The browser development SDK was rebuilt (`../pcurve-sdk-build.log`), so the
  shared kernel change is available to linked Helios checkouts. No browser
  timing or new live UI qualification is claimed in this kernel pass.
- `benchmark-info.json` records the host/runtime and frozen CLI/Core assembly
  hashes. Source changes are confined to this repository; output stays ignored.
- Code-quality review: no input trust bypass, tolerance inflation, altered UVs,
  source rewriting, global mutable state or additional native dependency.
  `git diff --check` passes. Temporary test/profiler/build processes have exited.

General rational/multi-span trim recovery retains dense fallback. Its fitting
and periodic refinement checks were inspected, but this pass does not substitute
an unproven low-resolution grid for that family. The next measured importer cost
is orientation/display tessellation, isolated from pcurve qualification.
