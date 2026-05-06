# CIR-F0: BRep-first / CIR fall-forward execution design

## 1) Current pipeline summary (source-of-truth snapshot)

Current production `.firmament` flow is still **BRep-first and eager**:

1. parse + validate + lower (`FirmamentCompiler`),
2. eager primitive/boolean BRep execution (`FirmamentPrimitiveExecutor`),
3. select the latest executed geometric body (`FirmamentStepExporter.SelectExportBody`),
4. derive PMI payload,
5. export AP242 (`Step242Exporter.ExportBody`).

This is observable in:

- `FirmamentCompiler` calling primitive lowering/execution before returning compilation artifact.
- `FirmamentStepExporter.Export` requiring successful primitive execution and exporting selected BRep.
- `FirmamentPrimitiveExecutor` returning hard failure when a primitive/boolean cannot execute.

CIR currently exists as an analysis-oriented path:

- `FirmamentCirLowerer` lowers a bounded subset and fails when unsupported CIR lowering appears.
- `CirNativeAnalysisService` analyzes `FirmamentPrimitiveLoweringPlan` by CIR lowering + tape eval and emits explicit "CIR lowering unsupported" notes.

## 2) Fall-forward concept

CIR-F0 proposes a **hybrid runtime contract**:

- Keep eager exact BRep as first-class authority where supported.
- On a bounded set of "unsupported materialization" outcomes (not invalid intent), transition execution state to CIR intent continuation.
- Continue model/analyzer continuity in CIR.
- Only materialize/export when a compatible exact CIR→BRep materializer exists.

This is **not** replacing BRep and **not** using CIR as an invalid-model sink.

## 3) Fall-forward eligibility rules

### A. Eligible: unsupported materialization for otherwise valid intent

Eligible triggers are runtime failures with `NotImplemented`/"unsupported family" character where intent graph is valid, e.g.:

- unsupported bounded Boolean continuation family,
- unsupported bounded chamfer/fillet rebuild family,
- CIR-known intent where exact BRep rebuild path absent,
- export-time missing CIR materializer for a valid CIR graph.

Diagnostic class proposed: `Execution.MaterializationUnsupported` (error for export/build, non-fatal for continued CIR execution phase).

### B. Not eligible: invalid model (hard fail)

Never fall-forward for structural/semantic invalidity, including:

- malformed/invalid Firmament fields,
- selector ambiguity/unresolvable selector,
- illegal placement semantics,
- DFM/schema invalidation,
- non-manifold or topology-invalid construction intent,
- tolerance degeneracy that indicates invalid geometry rather than missing implementation.

These remain hard errors in compile/execution with current diagnostic style.

### C. Not trigger by itself: analyzer uncertainty

Analyzer uncertainty (e.g., approximate volume bounds, unsupported exact classifier in analyzer) is **not an execution transition trigger**. It is analysis metadata only.

## 4) Native geometry state model

Introduce internal (non-public) `NativeGeometryState` per executed feature frontier:

- `ExecutionMode`: `BRepActive | CirOnly | Failed`
- `MaterializedBody`: optional `BrepBody`
- `ConstructiveIntentRoot`: optional `CirNode` (or tape handle)
- `MaterializationAuthority`: `BRepAuthoritative | CirIntentOnly | PendingRematerialization`
- `Provenance`: feature/op mapping, placement transform history, selector anchor provenance
- `Diagnostics`: accumulated state diagnostics with transition events

Design choices:

- Carry both BRep + CIR references once CIR mirror exists (authoritative flag prevents ambiguity).
- BRep remains authoritative while `BRepActive` and successful.
- CIR-only is explicit (`ExecutionMode=CirOnly`) and must appear in user-visible diagnostics.
- Feature IDs remain operation-stream IDs; selectors/PMI references stay keyed by authored feature ids, not generated topology names.

## 5) CIR mirroring / replay strategy recommendation

Recommend **Option C (operation history + lazy CIR build)** for F1 baseline, then selective parallelization in F2:

- Persist normalized replayable operation log from lowering plan and resolved placement/selector anchors.
- Build CIR lazily on first eligible BRep materialization-unsupported event by replaying history deterministically.
- In F2, enable always-build CIR mirror for small bounded subset as a differential oracle.

Why:

- Avoid immediate full dual-runtime cost/risk.
- Avoid lossiness of reconstructing history from only final BRep bodies.
- Preserve deterministic replay for diagnostics and future external op contracts.

## 6) Operational state transitions

Primary transitions:

- `BRepActive -> BRepActive`: op executes in eager BRep.
- `BRepActive -> CirOnly`: op fails with eligible unsupported-materialization diagnostic; replay/build CIR and continue intent execution.
- `BRepActive -> Failed`: invalid-model failure class.
- `CirOnly -> CirOnly`: continue intent ops in CIR.
- `CirOnly -> BRepActive` (optional later stage only): when full rematerialization checkpoint succeeds via CIR→BRep materializer with equivalence guard.
- any state -> `Failed`: invalidity/contradiction introduced.

Policy:

- No silent re-entry to BRep; must be explicit rematerialization event.
- Materialization attempts default at export/checkpoint boundaries, not after every CIR op (cost/control).
- Build/export result must distinguish:
  - valid materialized BRep,
  - valid CIR-only unmaterialized,
  - invalid/failure.

## 7) STEP export/materialization behavior

### A. `BRepActive`

Use existing `FirmamentStepExporter` path unchanged.

### B. `CirOnly` + supported materializer

Run exact CIR→BRep materializer for supported pattern, then normal AP242 export.
Emit note that source execution was CIR-only until materialization checkpoint.

### C. `CirOnly` + unsupported materializer

Fail export clearly with deterministic diagnostic:

- model is valid/analyzable in CIR,
- exact BRep/AP242 materialization is unavailable for encountered CIR pattern,
- include blocking feature/op id + pattern family.

No tessellation fallback in v1.

## 8) Existing BRep builder reuse plan

Current BRep builders remain core assets:

- eager production path for supported families,
- future CIR materializer handlers for matching patterns,
- differential oracle against CIR evaluations.

Examples:

- `box` primitive: direct BRep path now; future CIR `Box` materializer parity.
- box minus cylinder/counterbore-like subtract families: reuse bounded boolean recognizers/rebuilders as materializer kernels.
- prismatic slot/pocket families: reuse safe composition + bounded reconstruction logic.
- chamfer/fillet bounded handlers: stay eager now, later invoked as CIR materializer passes where patterns match.

## 9) Forge / StandardLibrary implications

Contract direction:

- StandardLibrary ops should ultimately define both:
  - eager BRep constructor (today),
  - CIR intent form (future).
- Forge-backed shapes can emit CIR subtrees for intent fidelity where BRep family is incomplete.

Interop rules:

- external op with only CIR form: executable in intent/analyzer path; export gated on materializer availability.
- external op with only BRep form: remains BRep-only executable; CIR differential support optional until mirrored form added.

This implies future package API supports dual-form capability descriptors (`hasBrepMaterializer`, `hasCirEmitter`).

## 10) Selectors / semantic topology / PMI implications (SEM-A0 guardrails)

- Preserve authored feature provenance as primary identity through state transitions.
- CIR-only state supports selectors that resolve at authored semantic level (feature/declared selector contracts), not generated edge/vertex names.
- Topology-derived selectors requiring concrete BRep entities remain bounded/unsupported until semantic-topology provenance mapping exists.
- PMI may attach to semantic feature references pre-materialization; topology-bound PMI export remains gated by materialized topology availability.

Unsupported until SEM lineage work:

- stable generated edge/vertex naming across CIR-only continuation,
- full topology-level selector resolution without materialized semantic map.

## 11) Analyzer implications

- `CirOnly` states are analyzable via `CirNativeAnalysisService` directly.
- Analyzer outputs should report backend/state explicitly (`BRepActive` vs `CirOnly`).
- When both BRep + CIR are present, compare/differential tools should run both and surface divergence/uncertainty bands.
- Uncertainty reporting remains explicit (approximate estimator, unsupported exact metrics) and does not alter execution validity class.

## 12) Risks and guardrails

Key risks:

- dual-truth drift between BRep and CIR semantics,
- misclassification of invalid as "unsupported" (unsafe fall-forward),
- selector/provenance loss across transition,
- user confusion about exportability vs analyzability.

Guardrails:

- strict diagnostic taxonomy split (`InvalidIntent` vs `MaterializationUnsupported`),
- deterministic transition event diagnostics with feature/op ids,
- explicit execution state in build/export/analyze result payloads,
- staged rollout with differential tests before enabling fall-forward for production classes.

## 13) Implementation ladder

### CIR-F1 — Native geometry state + replay log skeleton

- add internal `NativeGeometryState` model and replayable op history,
- no behavior change in public CLI,
- diagnostics schema extension only.

### CIR-F2 — bounded CIR mirror + differential harness

- always-build CIR mirror for minimal subset (box/cylinder/sphere + add/subtract/intersect subset),
- run differential checks vs existing BRep where both supported,
- still no fall-forward.

### CIR-F3 — first execution fall-forward

- enable transition on one proven eligible unsupported BRep family,
- continue CIR-only for downstream ops,
- build/analyze succeed in CIR-only mode,
- STEP export fails clearly when materializer missing.

### CIR-F4 — first CIR→BRep materializer checkpoint

- implement one exact materializer pattern reusing existing bounded BRep builders,
- allow `CirOnly -> BRepActive` rematerialization at export/checkpoint.

### CIR-F5 — Forge/StandardLibrary dual-form contracts

- external op descriptors for CIR emit + BRep materializer capability,
- conformance tests for mixed capability packages.

## 14) Recommended immediate next milestone

Proceed with **CIR-F1** as immediate next step:

1. introduce internal state machine + operation history model,
2. define diagnostic taxonomy and transition-event records,
3. thread state object through compiler/executor/exporter internally with no CLI surface change,
4. add tests proving no behavior regressions for current BRep-first golden path.

This isolates core architectural risk (state/diagnostics/provenance) before enabling any runtime fall-forward behavior.
