# CIR-SWEEP-X0 profile-stack extrude BRep emitter feasibility lab

## Purpose and scope
FrictionLab-only feasibility test for sweep-first profile-stack emission on bounded coaxial hole families using existing BRep composition builders.

## Code inspected
- Hole recovery architecture: `HoleRecoveryPolicy`, `HoleRecoveryPlan`, `HoleProfileSegment`, `HoleRecoveryExecutor`.
- BRep and topology: `BrepBody`, `TopologyModel`, `BrepPrimitives`, `BrepBooleanBoxCylinderHoleBuilder`, `Step242Exporter`.
- FrictionLab prior work: stepped architecture lab and hole family policy labs.

## Prototype profile-stack model
`ProfileStackExtrudeSpec` + ordered `ProfileStackLayer` (z-range + optional inner radius).

## Emitter topology strategy
Translate layer stack into `SafeBooleanComposition` hole chain and call `BrepBooleanBoxCylinderHoleBuilder.BuildComposition` directly (no `BrepBoolean.Subtract` calls in this lab path).

## Scenario results
- stepped-hole: attempted via 3 cylindrical layers with shoulders metadata.
- through-hole: single through layer.
- blind-hole: lower solid interval + upper blind layer.
- counterbore: through core + top larger relief.

## STEP smoke
Every successful scenario checks STEP markers: `ISO-10303-21`, `MANIFOLD_SOLID_BREP`, `ADVANCED_FACE`, `CYLINDRICAL_SURFACE`, and no `BREP_WITH_VOIDS`.

## Boolean-path comparison
Lab report surfaces per-scenario status for profile-stack path and is intended to be compared with existing semantic/boolean executor baselines in tests.

## Semantic role clarity
Lab captures deterministic roles including outer walls, top/bottom, inner wall radii, shoulder transitions, and blind bottom cap.

## What sweep-first solves
Coaxial axis-aligned through/blind/counterbore/stepped families with layered radius changes.

## What sweep-first does not solve
Cross-axis/oblique interactions, multi-feature interference, and non-sweep constructs; these remain boolean-fallback territory.

## Minimal Sketch2D / Region2D requirements
For this nucleus: rectangle outer boundary + centered circle per z-layer + ordered interval validation.

## Relationship to HoleRecoveryPlan
`HoleRecoveryPlan.ProfileStack` semantics map naturally onto layer intervals and tier radii.

## Architecture recommendation
Prefer profile-stack extrude executor for admissible coaxial layered holes; retain boolean fallback boundary for non-sweepable topology.

## Risks and guardrails
Avoid claiming full boolean replacement; keep this as FrictionLab experiment without production routing changes.

## Confidence
Medium for coaxial bounded families; low for generalized multi-axis/non-coaxial features pending dedicated topology builder evolution.
