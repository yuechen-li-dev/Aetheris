# CIR-A0: Analyzer Integration Design for CIR-Backed Native Firmament Analysis

## Outcome

**Outcome A — clear integration design.**

This document defines a staged, explicit, non-disruptive analyzer integration plan for CIR-backed analysis of native Firmament models while preserving the current BRep/STEP analyzer path as-is.

---

## 1) Current analyzer architecture summary (as implemented today)

### CLI routing and command surface

Current `aetheris analyze` commands are STEP-only at the CLI boundary:

- `analyze <file.step>` routes to `StepAnalyzer.Analyze`.
- `analyze volume <file.step>` routes to `StepAnalyzer.AnalyzeVolume`.
- `analyze compare <reference.step> <candidate.step>` routes to two `StepAnalyzer` runs and compares BRep-derived summaries.
- `analyze map <file.step> ...` routes to STEP map analysis.
- `analyze section <file.step> ...` routes to STEP section analysis.

Usage/help text and argument validation are all STEP-centric and do not expose Firmament-native/CIR analysis yet.

### Existing analysis backend

`StepAnalyzer` is the only production analyzer backend currently used by CLI. It:

- imports STEP to `BrepBody`,
- computes topology/geometry summaries,
- supports exact volume for selected cases plus approximate voxel mode,
- supports map/section queries against imported BRep.

### Production Firmament path

The production native path remains:

`Firmament compile -> primitive execution -> body selection -> STEP export` via `FirmamentStepExporter`.

This path is authoritative for materialized geometry/export and must remain unchanged in CIR-A0.

---

## 2) CIR analysis capabilities today

### Available CIR capabilities

From the current kernel/tests/docs state:

- Firmament-to-CIR lowering exists but is subset-bounded (`FirmamentCirLowerer`), with explicit failures for unsupported primitives/booleans/placement semantics.
- Point classification exists (`CirAnalyzer.ClassifyPoint` over signed-distance evaluate).
- Dense grid volume estimate exists (`CirVolumeEstimator`).
- Adaptive/tape interval-assisted volume estimation exists (`CirAdaptiveVolumeEstimator` + region planning).
- CIR-vs-BRep differential matrix/reporting exists in tests/artifacts.
- Calibration harness exists (currently test-internal) and reports policy/work/error metrics.

### Current limitations

- Lowering subset is intentionally incomplete.
- CIR analysis API shape is primitive-level (`CirNode`, `CirTape`) and not yet integrated into CLI contracts.
- No user-facing analyzer output schema yet communicates CIR uncertainty and lowering coverage.

---

## 3) Recommended CIR analyzer input scope

### Decision

**Recommended scope (staged):**

1. **Primary external/native input: `.firmament` source** (eventual CLI-level CIR entry).
2. **Primary internal service input: compiled/lowered Firmament artifact** (A1 service API).
3. **Secondary internal/test input: CIR tape or CIR node** (for deterministic tests/calibration).
4. **Not in scope:** STEP input to CIR backend.

### Rationale

- CIR is intended to analyze **construction intent**, so `.firmament` is the correct user-facing source.
- Internal analyzers should consume already-compiled/lowered artifacts to avoid duplicate parse/compile work and to preserve structured diagnostics.
- Tape-level entry remains useful for calibration/tests but should not be the initial public API.
- Reverse-lowering STEP into CIR is out of scope and would blur intent-vs-materialized boundaries.

---

## 4) Backend selection policy

### A0 recommendation

**Initial policy: explicit opt-in only, hidden/experimental, no automatic switching.**

### Staged policy

- **A1 (internal):** no public CLI backend flag.
- **A2 (hidden CLI):** require explicit `--backend cir --experimental` for Firmament inputs only.
- **A3/A4:** add explicit differential/native modes; keep backend visible in output.
- **A5:** revisit defaulting only after coverage + drift + uncertainty UX are proven.

### Guardrail rules

- No silent fallback from CIR to BRep on unsupported lowering.
- Backend must be echoed in every result payload and text output.
- If file extension and backend mismatch (e.g., `.step --backend cir`), fail with clear guidance.

---

## 5) Command/API support priorities

### Priority order

1. **`analyze volume` (first CIR target)**
   - Highest value and already supported by dense/adaptive estimators.
   - Natural place to establish uncertainty semantics.

2. **`analyze compare` (next, high value)**
   - Add native differential mode: CIR intent vs materialized BRep/STEP from the same `.firmament` source.
   - Strongest control against semantic drift.

3. **`analyze map` / `analyze section` (later)**
   - Good fit for CIR sampling, but should come after core schema/uncertainty conventions are stable.

4. **plain `analyze` summary (optional later)**
   - Could expose lowering support summary (supported/unsupported ops, node counts, bounds), but lower priority than volume + compare.

### Proposed staged CLI shapes (future, not implemented in A0)

- `aetheris analyze volume part.firmament --backend cir --experimental --mode adaptive --json`
- `aetheris analyze compare --native part.firmament --backend cir-vs-brep --experimental --json`

Keep existing STEP commands unchanged.

---

## 6) Output semantics and JSON shape (backend + uncertainty)

Every CIR result must disclose backend and confidence semantics explicitly.

### Required top-level fields

- `backend`: `"cir" | "brep" | "cir-vs-brep"`
- `resultKind`: `"exact" | "approximate" | "classifier-derived"`
- `inputKind`: `"firmament" | "step" | "internal-tape"`
- `success`: bool
- `notes`: string[]
- `diagnostics`: structured diagnostics (especially lowering failures)

### CIR volume-specific fields

- `estimator`: `"dense" | "adaptive" | "interval-assisted"`
- `resolution` (dense grid)
- `adaptiveOptions` (maxDepth, directSampleGrid, minimumRegionExtent, treatRejectUnknownAsSample)
- `boundingBox`
- `estimatedVolume`
- `sampledPointCount`
- `totalRegionsVisited`
- `regionsClassifiedInside`
- `regionsClassifiedOutside`
- `regionsSubdivided`
- `regionsSampledDirectly`
- `unknownOrRejectedRegions`
- `unknownPolicy`
- `maxDepthReached`
- `traceEventCount` (and optional trace head)

### Lowering coverage fields

- `lowering.supported`: bool
- `lowering.supportedOpCount`
- `lowering.unsupportedOpCount`
- `lowering.diagnostics[]` with op index/feature/message

### Text output requirements

Human output should always include:

- `Backend: cir`
- `Result semantics: approximate` (or classifier-derived)
- estimator and options
- sample/region counts
- unknown/reject counters + policy
- lowering coverage summary
- caution line: “CIR result is intent-domain and may differ from materialized BRep/STEP.”

---

## 7) Unsupported-lowering behavior

### Required behavior

On CIR lowering unsupported cases:

- Return failure with explicit lowering diagnostics (feature/op-specific).
- Do **not** silently run BRep backend.
- Include actionable hint: “BRep backend may still analyze exported STEP/materialized geometry.”

### Optional explicit fallback mode (later)

If fallback is introduced later, it must be opt-in and visible, e.g.:

- `--fallback brep` with output field `fallbackUsed: true` and a warning.

Default remains no fallback.

---

## 8) CIR/BRep coexistence and differential mode design

### Coexistence model

Support three explicit modes (eventually):

1. **CIR-only** (native intent analysis)
2. **BRep-only** (existing STEP/materialized analysis)
3. **Differential CIR-vs-BRep** (native `.firmament` model compared against its materialized output)

### Differential mode (A3 target)

For one `.firmament` source:

1. compile/lower to CIR,
2. run CIR probes/volume/bounds,
3. run production materialization path (existing execution/export path) and BRep analysis,
4. emit deltas + classifications (bounds drift, probe disagreements, volume delta),
5. mark whether mismatch is expected/known bucket vs regression.

This should reuse patterns already exercised in `FirmamentCirDifferentialAnalysisTests`.

---

## 9) Calibration and uncertainty reporting policy

### Policy decision

- Keep calibration harness as test/internal source of policy truth for now.
- Promote **selected calibration-derived defaults** into CIR analyzer options only when policy files/threshold governance exist.
- Expose runtime counters in analyzer output (not hidden), because they are essential for interpreting approximate results.

### Regression strategy

- Continue calibration test suite as required CI coverage.
- Add analyzer-level golden JSON contract tests before public CLI exposure.
- Require differential drift thresholds to be versioned and reviewed (no silent tightening/loosening).

---

## 10) Risks and guardrails

### Key risks

- Dual-truth confusion between intent-domain CIR and materialized-domain BRep.
- CIR/BRep semantic drift.
- Unsupported lowering masked as success.
- Approximate values interpreted as exact.
- CLI option proliferation and cognitive overload.
- Output schema churn before stabilization.
- Unverified performance claims.
- Contamination of production build/export path.

### Guardrails

- Explicit backend selection (initially hidden+opt-in).
- Mandatory backend/resultKind fields in every response.
- No silent fallback.
- Differential mode as first-class drift detector.
- Keep production build/export path unchanged.
- Mark CIR as experimental until coverage and drift confidence are demonstrated.

---

## 11) Recommended implementation ladder

### CIR-A1 — Internal CIR analysis service

- Add internal service abstraction for CIR analysis over compiled/lowered Firmament or CIR tape.
- Return structured result contract with backend/uncertainty/lowering diagnostics.
- No public CLI wiring.

### CIR-A2 — Hidden experimental CLI volume backend

- Add experimental flag path for Firmament volume analysis via CIR.
- Require explicit backend and experimental gate.
- Emit full uncertainty/coverage metadata in text+JSON.

### CIR-A3 — Native differential analysis mode

- Add compare mode for `.firmament` to evaluate CIR vs materialized BRep in one command.
- Integrate thresholded drift reporting and explicit mismatch classes.

### CIR-A4 — CIR map/section backend

- Extend CIR sampling to map/section workflows.
- Keep backend explicit.

### CIR-A5 — Production policy decision

- Decide default backend policy for native analysis only after evidence from A2–A4.

---

## 12) Immediate next milestone (CIR-A1)

**Recommended CIR-A1 scope:**

- Introduce an internal `NativeAnalysisService` (name flexible) that accepts:
  - compilation artifact or primitive lowering plan, and
  - optional pre-lowered CIR node/tape for tests.
- Implement structured `CirAnalysisResult` contract with:
  - backend/result semantics metadata,
  - lowering diagnostics,
  - estimator/counter metrics,
  - uncertainty fields.
- Add unit tests that validate:
  - unsupported lowering behavior,
  - deterministic JSON contract shape,
  - no fallback behavior by default.

No public CLI behavior changes in CIR-A1.
