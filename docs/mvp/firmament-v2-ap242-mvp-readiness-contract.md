# Firmament V2 AP242 MVP readiness contract

## 1. Purpose

This document is the finish-line contract for Firmament V2 MVP readiness. It defines what “done” means for the AP242 MVP and gives future fixtures a gradeable stage contract.

A Firmament V2 fixture is not MVP-ready merely because it parses, lowers to AIR, traces, materializes internally, or has admissibility proven. Those results are useful intermediate evidence, but they do not prove that the V2 build path can produce a real exchange artifact.

MVP-ready requires real AP242 emission through the real `Step242Exporter` from a real `BrepBody`, plus independent verification of the emitted AP242 file. Anything short of that is an intermediate stage, not completion.

This document is not an implementation plan or behavior change. It does not add the build/export pipeline, alter parser/lowering/kernel/materializer/exporter behavior, or define new product semantics.

## Phase closeout

The current phase closeout audit is recorded in `docs/mvp/firmament-v2-ap242-mvp-phase-closeout.md`.

## 2. Stage taxonomy

Every Firmament V2 fixture should report one precise stage from this taxonomy, or an explicit `blocked` state with a blocker reason.

### parsed

Firmament V2 source parses without syntax errors.

### semantic-lowered

Source lowers into the semantic model/AIR while preserving authored intent.

### air-materialized

AIR or the semantic feature plan materializes into an internal body or executable materialization plan.

### brep-built

A real `BrepBody` exists.

### step-emitted

The real `Step242Exporter` was invoked on a real `BrepBody`. The output is a real AP242/STEP file, not a hardcoded trace string, template string, or synthetic golden text. The emitted file contains real topology entities such as `ADVANCED_FACE` and `VERTEX_POINT`, with each relevant entity count greater than zero.

### step-roundtrip

The emitted AP242 file reimports into a `BrepBody`.

### step-verified

The emitted AP242 file is independently checked:

* topology counts match expected Aetheris canonical topology where exact counts are defined;
* volume or other measurable properties match hand-computed expected values within tolerance;
* the reimported body has matching or expected topology counts;
* diagnostics are deterministic.

Only `step-verified` counts as MVP-ready.

## 3. Definition of MVP-ready

A V2 fixture is MVP-ready only when it reaches `step-verified`.

```text
parsed != done
semantic-lowered != done
air-materialized != done
brep-built != done
step-emitted != done
step-roundtrip != done
step-verified == MVP-ready
```

`step-emitted` proves the export wire exists. `step-roundtrip` proves importer/exporter interoperability. `step-verified` proves the emitted result is geometrically meaningful and independently checked against expected topology, measurements, and deterministic diagnostics.

## 4. AP242 verification contract

Every MVP fixture must satisfy this verification contract:

* the command under test is `aetheris build` or the repository's true build/export command, not only `trace`;
* a real V2 source fixture is used;
* a real `BrepBody` is produced;
* the real `Step242Exporter` is invoked;
* the emitted file exists on disk;
* the emitted file contains `ADVANCED_FACE` and `VERTEX_POINT`, each with count greater than zero;
* reimport succeeds when the importer supports the feature;
* expected topology counts are checked where canonical;
* expected volume is checked within tolerance where a formula exists;
* diagnostics are deterministic;
* failure is reported with a precise stage label.

Use existing repository tolerance conventions where they exist. If no stronger convention applies, use a recommended default such as relative tolerance `1e-6` or an absolute tolerance appropriate to the fixture's units. This document recommends tolerance policy only; it does not implement it.

Exact topology counts are required for simple canonical primitives such as boxes. For cylinders, cones, and spheres, counts should reflect the Aetheris canonical BRep representation, not universal CAD assumptions.

## 5. Tier 0 — Pipeline wiring sanity

This tier blocks everything else.

| Fixture ID | Required stage | Asserts |
| --- | ---: | --- |
| `pipeline-v2-box-reaches-build-command` | `step-emitted` minimum | `aetheris build` accepts a V2 source file without syntax rejection and attempts real build/export path. |
| `pipeline-v2-box-emits-real-step` | `step-emitted` | Output AP242 has `ADVANCED_FACE` count >= 6 and `VERTEX_POINT` count > 0. |
| `pipeline-v2-box-step-reimports-correctly` | `step-roundtrip` | Reimported emitted AP242 produces `BrepBody` with 6 faces, 8 vertices, 12 edges for canonical box. |
| `pipeline-v2-box-volume-matches-expected` | `step-verified` | `analyze volume` or equivalent on emitted/reimported body matches `size.x * size.y * size.z` within tolerance. |

If Tier 0 fails, no other MVP fixture can be considered complete.

## 6. Tier 1 — Primitives reaching real geometry

| Fixture ID | Required stage | Asserts |
| --- | ---: | --- |
| `primitive-v2-box-step-verified` | `step-verified` | Box emits AP242, reimports, topology count 6/8/12, volume matches expected. |
| `primitive-v2-cylinder-step-verified` | `step-verified` | Cylinder emits AP242, reimports, canonical face count expected by Aetheris, volume matches `πr²h`. |
| `primitive-v2-cone-step-verified` | `step-verified` or `blocked` | Cone emits AP242 and verifies if supported; otherwise explicitly marked blocked with missing capability. |
| `primitive-v2-sphere-step-verified` | `step-verified` or `blocked` | Sphere emits AP242 and verifies if supported; expected topology must match Aetheris canonical representation. |

Do not claim unsupported primitives are MVP-ready. They must be `blocked` or out-of-scope.

## 7. Tier 2 — Single semantic features reaching AP242

| Fixture ID | Required stage | Asserts |
| --- | ---: | --- |
| `feature-v2-shaft-hole-through-step-verified` | `step-verified` | Base body minus through shaft hole; cylindrical wall present; entry/exit topology expected; volume = base volume − `πr² * throughDepth`. |
| `feature-v2-shaft-hole-blind-step-verified` | `step-verified` | Blind/depth shaft hole; cylindrical wall + bottom face expected; volume = base volume − `πr² * depth`. |
| `feature-v2-counterbore-step-verified` | `step-verified` | Counterbore stack emits wider entry cylinder + shaft; face/topology counts and volume checked. |
| `feature-v2-countersink-step-verified` | `step-verified` | Countersink stack emits conical entry + shaft; face/topology counts and volume checked. |
| `feature-v2-side-hole-step-verified` | `step-verified` | Existing +X/-X side-hole fixture must route through real exporter, not hardcoded trace template. |
| `feature-v2-side-hole-arbitrary-axis-step-verified` | `step-verified` | Side hole on a face other than the frozen +X/-X configuration, proving axis/face generalization. |

The existing semantic hole work makes shaft, counterbore, and countersink fixtures good early candidates, but they still are not MVP-ready until AP242 emission and verification pass.

## 8. Tier 3 — Composition primitives

| Fixture ID | Required stage | Asserts |
| --- | ---: | --- |
| `derivation-v2-with-size-override-step-verified` | `step-verified` | A `with`-derived solid emits AP242 with expected dimensions/volume, not just correct AST. |
| `derivation-v2-with-chained-twice-step-verified` | `step-verified` | Two sequential `with` derivations emit correct AP242 and do not accumulate stale state. |
| `semanticref-v2-expose-face-alias-resolves-in-step` | `step-verified` | `expose { face(+Z) => top }` or equivalent alias resolves when used by later feature; verified by emitted topology. |
| `semanticref-v2-expose-edge-loop-alias-resolves-in-step` | `step-verified` or `blocked` | Outer-loop/edge-loop alias resolves in emitted topology if supported; otherwise explicitly blocked. |

## 9. Tier 4 — Multi-feature composition

| Fixture ID | Required stage | Asserts |
| --- | ---: | --- |
| `composite-v2-two-independent-holes-step-verified` | `step-verified` | Two non-interacting holes on same part emit AP242 and verify; proves feature composition works in trivial case. |
| `composite-v2-hole-plus-derived-variant-step-verified` | `step-verified` | `with`-derived solid with hole/feature emits correct AP242; proves derivation and features compose. |
| `composite-v2-adjacent-non-overlapping-holes-step-verified` | `step-verified` | Two nearby but non-overlapping holes emit correct AP242; realistic near-miss case. |
| `composite-v2-overlapping-holes-rejected-with-clear-diagnostic` | deterministic rejection | Overlapping holes should reject under current doctrine unless/until resolved composition is implemented; diagnostic must be precise. |

At least `composite-v2-two-independent-holes-step-verified` must pass for MVP pitch readiness.

## 10. Tier 5 — Validation / DFM enforcement

| Fixture ID | Required stage | Asserts |
| --- | ---: | --- |
| `template-v2-cnc-min-tool-radius-enforced` | deterministic rejection | `template<CNC>` / concept constraint blocks a build that violates minimum tool radius, not merely parses metadata. |
| `template-v2-concept-unit-mismatch-rejected-at-build` | deterministic rejection | Unit mismatch rejection fires when the template/concept is actually used in build context, not only in standalone parse. |

Tier 5 is required for MVP readiness if the demo claims DFM/concept enforcement. At minimum, one DFM enforcement fixture should block a bad build before MVP pitch.

## 11. Tier 6 — Minimal PMI, optional

| Fixture ID | Required stage | Asserts |
| --- | ---: | --- |
| `pmi-v2-hole-diameter-callout-emits-in-step` | `step-verified` or `optional` | Basic hole-diameter PMI annotation emits into AP242 and round-trips if V2 PMI is in demo scope. |
| `pmi-v2-datum-plane-emits-in-step` | `step-verified` or `optional` | Basic datum plane emits into AP242 and round-trips if V2 PMI is in demo scope. |

Tier 6 is optional for MVP unless the specific pitch/demo requires annotated AP242 output. Do not quietly inflate MVP scope with PMI.

Implementation status: STEP-V2-X7 adds semantic-only AP242 evidence for the two Tier 6 fixtures via `docs/implementation/step-v2-x7-semantic-pmi-ap242.md`; graphical PMI remains out of scope.

## 12. Fixture metadata requirements

Every V2 fixture should carry metadata with the following fields or equivalent meaning:

```text
fixtureId
tier
expectedStage
currentStage
mvpRequired: true/false
featureArea
buildCommand
expectedOutputPath
expectedTopology
expectedVolume
tolerance
roundtripRequired
blockedReason, if blocked
```

Stage honesty rules:

* A fixture with hardcoded trace output cannot be `step-emitted`.
* A fixture that does not invoke `Step242Exporter` cannot be `step-emitted`.
* A fixture without reimport cannot be `step-roundtrip`.
* A fixture without topology/volume checks cannot be `step-verified`.
* A design-only semantic candidate must not be marked MVP-ready.

## 13. Recommended execution order

```text
1. Implement Tier 0 pipeline gate.
2. Verify Tier 1 box/cylinder primitives.
3. Verify Tier 2 semantic holes.
4. Verify Tier 3 derivation/semantic references.
5. Verify Tier 4 trivial multi-feature composition.
6. Add Tier 5 DFM enforcement if demo claims concept/DFM support.
7. Add Tier 6 PMI only if required by demo.
```

Do not harden Tier 2 features before Tier 0 proves the pipeline can emit and verify a V2 box.

## 14. Non-goals

This milestone explicitly does not include:

* implementation;
* exporter changes;
* parser/lowering changes;
* test harness changes;
* new fixture creation unless a docs convention requires examples;
* PMI implementation;
* DFM implementation;
* geometry behavior changes.

## 15. MVP pitchable bar

MVP is pitchable when:

* all Tier 0 fixtures pass;
* Tier 1 box and at least cylinder pass;
* Tier 2 shaft/counterbore/countersink semantic holes pass AP242 verification;
* existing side-hole golden path is upgraded to real AP242 verified, or explicitly removed from demo claims;
* Tier 3 derivation/alias fixtures pass if derivation/alias features are demoed;
* at least `composite-v2-two-independent-holes-step-verified` passes;
* at least one Tier 5 DFM enforcement fixture passes if DFM/concepts are in pitch;
* every MVP-claimed fixture is `step-verified`;
* no MVP claim relies on hardcoded STEP templates, trace-only output, or non-exported AIR.

If it does not emit valid AP242 through the real exporter, it is not MVP-ready.

## Implementation notes

* STEP-V2-X2 semantic hole AP242 fixture hardening is documented in `docs/implementation/step-v2-x2-semantic-holes-ap242.md`.
* STEP-V2-X6 minimal Tier 5 DFM/concept build enforcement is documented in `docs/implementation/step-v2-x6-dfm-concept-enforcement.md`.
