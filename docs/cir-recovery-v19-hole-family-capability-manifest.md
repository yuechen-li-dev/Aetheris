# CIR-RECOVERY-V19: Hole-Family Semantic Recovery Capability Manifest

## 1) Purpose

This document is the **current capability manifest** for semantic FRep/CIR → BRep recovery in the hole-family lane.

Architectural framing:

- **FRep/CIR → BRep is semantic intent recovery / decompilation, not ordinary lowering.**
- The goal is to recover high-level hole intent (family, entry side, profile stack, placement) into explicit executable plans where supported.

## 2) Architecture summary

Current routing:

```text
FrepMaterializerPolicyCatalog
  → HoleRecoveryPolicy
      → variants
  → CirOnlyFallbackPolicy

HoleRecoveryPolicy
  → HoleRecoveryPlan
  → HoleRecoveryExecutor
  → BrepBody
  → existing Step242Exporter
```

Notes:

- Policy and variant selection are mediated through **JudgmentEngine** candidate admissibility/score selection in `HoleRecoveryPolicy`.
- `CirOnlyFallbackPolicy` is an intent-preserving fallback and is not exact BRep execution.
- `FrepSemanticRecoveryRematerializer` only executes when `HoleRecoveryPolicy` is selected and provides `HoleRecoveryPlan`.

## 3) Supported capability matrix

| Variant | CIR shape | Entry side(s) | Profile stack | BRep execution | STEP smoke | Expected STEP root | Placement semantics | Key tests | Status |
|---|---|---|---|---|---|---|---|---|---|
| Through-hole | `Subtract(Box, Cylinder)` (translation wrappers tolerated) | Through (`AnchorSide=Through`) | 1 cylindrical through segment | Yes (`HoleRecoveryExecutor` prefers ProfileStackExtrude executor for cylindrical through profile-stack) | Yes | `MANIFOLD_SOLID_BREP` | Required and asserted | `ThroughHoleRecoveryPolicyTests`, `ThroughHoleRecoveryExecutorTests`, `ThroughHoleRecoveryStepSmokeTests` | Supported / executable |
| Counterbore | `Subtract(Subtract(Box, small cyl through), large cyl shallow)` | Top or Bottom relief + through core | 2 cylindrical segments (relief + through core) | Yes (profile-stack deferred; bounded legacy route executes) | Yes | `MANIFOLD_SOLID_BREP` | Required and asserted | `CounterboreVariantTests`, `CounterboreRecoveryExecutorTests`, `CounterboreRecoveryStepSmokeTests` | Supported / executable |
| Blind-hole | `Subtract(Box, Cylinder)` with non-through span and entry-face contact | Top or Bottom | 1 cylindrical blind segment | Yes (profile-stack deferred; bounded legacy route executes) | Covered through exporter/rematerializer + matrix lanes | `MANIFOLD_SOLID_BREP` | Required and asserted | `BlindHoleVariantAndExecutorTests`, `BlindHoleVariantHardeningTests`, `FirmamentStepExporterTests` | Supported / executable |
| Countersink | `Subtract(Subtract(Box, Cylinder), Cone)` with countersink-sized cone | Top or Bottom | 1 conical entry + 1 cylindrical core | Yes | Yes | `MANIFOLD_SOLID_BREP` | Required and asserted | `CountersinkVariantAndExecutorTests`, `FirmamentStepExporterTests` | Supported / executable |
| Chamfered-entry | `Subtract(Subtract(Box, Cylinder), Cone)` with chamfer-sized cone | Top or Bottom | 1 conical entry + 1 cylindrical core | Yes | Yes | `MANIFOLD_SOLID_BREP` | Required and asserted | `ChamferedEntryHoleVariantAndExecutorTests`, `FirmamentStepExporterTests` | Supported / executable |
| Stepped-hole | Nested subtract with small through + medium/large entry-relief cylinders (bounded 3-tier family) | Top or Bottom (must match across relief tiers) | 3 cylindrical segments (large relief, medium relief, small through) | Yes (bounded route) | Yes | `MANIFOLD_SOLID_BREP` | Required and asserted | `SteppedHoleVariantAndExecutorTests`, `SteppedHolePlacementSemanticsTests`, `SteppedHoleCorpusHardeningTests`, `FirmamentStepExporterTests` | Supported / executable (bounded) |
| CirOnlyFallback | Any not admitted by exact semantic policies | N/A | N/A | No (plan absent) | N/A | N/A | N/A | `FrepMaterializerPolicyCatalogTests`, `FrepSemanticRecoveryRematerializerTests` | Supported as non-executable intent preservation |

## 4) Placement contract

Shared placement fields (on each `HoleProfileSegment`):

- `AnchorSide`
- `DepthFromAnchor`
- `ZMin`
- `ZMax`
- `IsThrough`
- `PlacementDiagnostics`

Current rules:

- Through segments explicitly declare `IsThrough=true` and `AnchorSide=Through`.
- Non-through segments must have `AnchorSide=Top` or `AnchorSide=Bottom` with positive `DepthFromAnchor`.
- Unknown anchor (`AnchorSide=Unknown`) is not executable.
- Executors consume explicit `ZMin`/`ZMax` for tool construction.
- No hidden entry-side inference at execution time; placement metadata is authoritative.

## 5) Per-variant details

### Through-hole

- Accepted CIR shape: box host minus cylindrical tool, including translation-only wrappers.
- Axis/entry assumptions: Z-axis through profile.
- Profile stack: one cylindrical through segment.
- Executor route: `ProfileStackExtrudePlanAdapter` -> `ProfileStackExtrudeExecutor` (cylindrical profile-stack preferred).
- Export expectations: solid manifold output in STEP smoke lanes.
- Rejection boundaries: recognizer rejection for non box/cylinder/subtract or invalid clearance/placement.

### Counterbore

- Accepted CIR shape: nested subtract where first lane yields through core and second lane is shallow larger coaxial cylinder.
- Entry support: top-entry and bottom-entry relief supported.
- Profile stack: shallow larger cylindrical relief + through smaller cylindrical core.
- Executor route: profile-stack adapter explicitly defers in V2; bounded counterbore legacy placement-driven subtract route executes.
- Export expectations: manifold solid; no void-root expectation.
- Rejection boundaries: non-coaxial tools, invalid radius ordering, relief not entering from host face, full-depth relief, invalid host/axis/profile form.

### Blind-hole

- Accepted CIR shape: box minus cylinder that does not span full host depth and touches exactly one entry face.
- Entry support: top-entry and bottom-entry supported.
- Profile stack: one cylindrical blind segment with explicit anchor side.
- Executor route: profile-stack adapter explicitly defers in V2; bounded blind-hole legacy placement-driven subtract route executes.
- Export expectations: manifold solid in STEP outputs where exported.
- Rejection boundaries: through-depth spans, missing entry-face contact, invalid clearance, invalid shape nesting.

### Countersink

- Accepted CIR shape: nested subtract with cylindrical core plus conical entry relief.
- Profile stack: conical segment + cylindrical core.
- Entry support: top-entry and bottom-entry supported.
- Executor route: countersink-specific conical/cylindrical placement-driven subtract sequence.
- Export expectations: manifold solid in smoke coverage.
- Rejection boundaries: missing/non-coaxial cone, invalid cone radius ordering, transition mismatch to cylinder radius, full-depth cone, clearance failures.
- Anti-steal from chamfer: chamfer-sized cone envelopes are explicitly rejected by countersink variant.

### Chamfered-entry

- Accepted CIR shape: same nested subtract family as countersink.
- Distinction: cone must satisfy chamfer bounds (`depth/hole-radius` and `radius-delta` thresholds).
- Entry support: top-entry and bottom-entry supported.
- Profile stack: conical chamfer entry + cylindrical core.
- Executor route: countersink-like conical/cylindrical placement-driven execution.
- Export expectations: manifold solid in smoke coverage.
- Rejection boundaries: non-coaxial/missing cone, transition mismatch, full-depth cone, cone too large (promoted out of chamfer family), clearance failures.

### Stepped-hole

- Supported case: bounded three-level stepped family (small through core + medium/large entry relief tiers).
- Canonicality: top-entry is supported; bottom-entry is also supported when anchor/coaxial/depth ordering rules are satisfied.
- Placement requirement: explicit segment `AnchorSide` + `ZMin/ZMax` for all tiers; unknown/mismatched anchors are rejected.
- Executor route: profile-stack-extrude using explicit placement metadata and safe composition builder (no repeated 3D subtract route in migrated cylindrical lanes).
- Validator dependency: A-series stepped placement/validator hardening lanes back this behavior.
- Known limits: only bounded profile count/ordering family is admitted by current variant and executor gates.

## 6) Anti-steal / ambiguity boundaries

Current ambiguity protections are implemented by **admissibility gates first**, then scoring:

- Through vs blind: blind rejects through-span cases; through lane requires through recognizer path.
- Counterbore vs stepped: stepped requires bounded 3-tier structure + strict radius/depth/anchor ordering; counterbore remains 2-tier relief+through.
- Countersink vs chamfer: chamfer-size thresholds prevent countersink from stealing chamfer-sized entry relief; chamfer rejects larger countersink-like cones.
- Independent overlap vs coaxial stepped: coaxial and shared entry-side/anchor requirements gate stepped admissibility.
- Family selection expectation: through-hole / blind-hole / counterbore / countersink / chamfer / stepped are separated by admissibility criteria, not score alone.

## 7) STEP root policy

Current policy in tested hole-family lanes:

- Executable open hole-family variants export as `MANIFOLD_SOLID_BREP` roots.
- They should not emit `BREP_WITH_VOIDS` for ordinary hole subtraction outputs.
- `BREP_WITH_VOIDS` remains reserved for explicit outer+inner void-shell representations (STEP-VOID family guidance).

## 8) Known deferred cases

Intentionally deferred / non-goal lanes at this milestone:

- Arbitrary N-level stepped holes beyond bounded admitted case.
- Additional stepped permutations beyond current anchor/depth/radius gating families.
- Threaded hole semantics (`ThreadDeferred` remains non-executed intent shape).
- Generic profile-stack executor across arbitrary segment compositions.
- Arbitrary rotated/non-translation transform support beyond current translation-oriented assumptions.
- Generic Boolean semantic recovery beyond bounded hole-family recognizers.
- Freeform surface-feature semantic families.
- Slot/keyway families (outside current hole-family scope).
- Source-surface extraction breadth outside currently asserted placement/recovery lanes.

## 9) How to add a new hole-family variant

For future Codex/LLM authors:

1. Add local variant under `HoleRecoveryPolicy`.
2. Add explicit admissibility gates (shape, placement, anti-steal boundaries).
3. Emit `HoleRecoveryPlan` with complete placement metadata.
4. Add anti-steal tests vs neighboring semantic families.
5. Add executor route **or** explicitly mark plan-only/deferred status.
6. Add STEP smoke coverage if executable.
7. Add/extend coverage matrix tests.
8. Update this V19 manifest.

## 10) Test references

Key test classes and what they prove:

- `HoleRecoveryPolicyCoverageMatrixTests`: family-level coverage matrix and lane separation.
- `ThroughHoleRecoveryPolicyTests` / `ThroughHoleRecoveryExecutorTests` / `ThroughHoleRecoveryStepSmokeTests`: through-hole admission, execution, STEP smoke.
- `CounterboreVariantTests` / `CounterboreRecoveryExecutorTests` / `CounterboreRecoveryStepSmokeTests`: counterbore admissibility, execution path, STEP smoke.
- `BlindHoleVariantAndExecutorTests` / `BlindHoleVariantHardeningTests`: blind-hole recognition, execution, boundary hardening.
- `CountersinkVariantAndExecutorTests`: countersink gating, anti-steal, execution, STEP lane checks.
- `ChamferedEntryHoleVariantAndExecutorTests`: chamfered-entry gating, top/bottom entry handling, execution.
- `SteppedHoleVariantAndExecutorTests` / `SteppedHolePlacementSemanticsTests` / `SteppedHoleCorpusHardeningTests`: bounded stepped-hole admission + execution + placement/corpus hardening.
- `HoleRecoveryPlacementSemanticsTests` / `HoleRecoveryExecutorPlacementDrivenTests` / `FirmamentPlacementAnchorSemanticsTests` / `FirmamentPlacementValidationTests` / `D3PlacementSemanticsTruthExtractionTests`: placement contract and placement-driven executor behavior.
- `FrepSemanticRecoveryRematerializerTests`: planner/policy selection and rematerializer execution integration.
- `FirmamentStepExporterTests`: STEP root/entity behavior and manifold expectations for supported lanes.
- `FrepMaterializerPolicyCatalogTests`: catalog ordering and fallback presence.

## 11) Recommended next lab / expansion

Candidate next labs (user-prioritized):

- Threaded/deferred hole variant lab.
- Slot/keyway semantic family lab.
- Generic profile-stack executor lab.
- Further stepped-hole expansion/hardening beyond bounded family.
- Surface-feature/groove semantic bridge lab.


## AIR-V1 note

Through-hole and stepped-hole profile-stack routes now materialize a bounded AIR scaffold (`AirProfileStackExtrude`) before executor emission. Blind/counterbore/conical variants are explicitly deferred in AIR-V1 and remain on their legacy executor routes.


## AIR-V2A.1 update (2026-05-23)
Capability manifest correction: blind-hole and counterbore remain AIR-deferred and execute via legacy bounded routes. Through/stepped remain on AIR/profile-stack; conical variants remain AIR-deferred.
