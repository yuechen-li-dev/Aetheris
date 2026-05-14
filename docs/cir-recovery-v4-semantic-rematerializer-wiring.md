# CIR-RECOVERY-V4: controlled rematerializer/fall-forward wiring for ThroughHoleRecoveryPolicy

## Integration point selected

Chosen: **Option A (`NativeGeometryRematerializer`)**.

Why this is least disruptive:
- `FirmamentStepExporter` already routes `CirOnly` export attempts through `NativeGeometryRematerializer.TryRematerialize(...)`.
- This keeps STEP exporter behavior unchanged.
- Existing `CirBrepMaterializer` registry remains intact as a bounded fallback path.
- Semantic policy/executor wiring is gated at the rematerialization boundary where root CIR and replay log are both available.

## Semantic recovery gates

Semantic recovery is attempted only when:
1. rematerializer is invoked for `CirOnly`,
2. CIR root is lowered successfully,
3. `FrepMaterializerPlanner` selects `ThroughHoleRecoveryPolicy`,
4. selected evaluation contains a valid `ThroughHoleRecoveryPlan`,
5. `ThroughHoleRecoveryExecutor` succeeds with non-null `BrepBody`.

If any gate fails, rematerializer falls back to existing `CirBrepMaterializer` behavior.

## Fallback behavior

- No admissible semantic policy: fallback to existing replay-guided strategy registry (`CirBrepMaterializer`).
- Policy selected but no usable plan: fallback to registry.
- Executor fails or null body: fallback to registry.
- If both semantic route and existing registry fail, failure remains explicit and diagnostics include semantic attempt summary.

## State transition behavior

On semantic success:
- `ExecutionMode`: `CirOnly -> BRepActive`
- `MaterializationAuthority`: `PendingRematerialization -> BRepAuthoritative`
- transition event message identifies semantic recovery policy.

On semantic non-success:
- transition semantics are unchanged and governed by pre-existing fallback route.

## Export behavior

No STEP exporter logic changes were made.

`FirmamentStepExporter` still:
- selects export body,
- asks rematerializer for `CirOnly` states,
- exports through existing `Step242Exporter.ExportBody(...)`.

This milestone only enables additional `CirOnly` recoverability through the rematerializer branch.

## Diagnostics

Semantic helper emits explicit diagnostics for:
- attempt start,
- planner run,
- selected policy,
- no admissible policy,
- plan extraction,
- executor success/failure,
- BRep recovered.

Rematerializer failure now appends semantic attempt summary when it had been attempted before falling back/ending in failure.

## Non-goals preserved

- No broad materializer replacement.
- No generic boolean recovery.
- No generic through-hole recovery.
- No STEP exporter behavior change.
- No topology naming expansion (SEM-A0 preserved).

## Recommended CIR-RECOVERY-V5

Add a bounded multi-policy semantic recovery catalog (still planner-driven) with explicit precedence/diagnostics, plus richer propagation of semantic diagnostics into export result metadata without changing public export APIs.
