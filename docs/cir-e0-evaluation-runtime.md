# CIR-E0 evaluation runtime design

Status: **Design milestone only** (no production code changes).

> CIR-E1.1 framing note (May 5, 2026): the linear `CirTape` introduced in E1 is now the intended runtime/MIR direction for serious CIR evaluation paths. `CirNode` remains intentionally in place as a semantic prototype/builder surface, a compatibility lowering source, and an oracle for parity tests while lowering/execution paths are still converging.

## 1) Current CIR evaluation limitations

Current CIR is intentionally a semantic tree with `CirNode.Evaluate(point)` dispatch per node kind and recursive composition (`min/max` for boolean ops). This is correct for representation but has runtime limits for dense sampling workloads. Specifically:

- Point evaluation is recursive and recomputes shared work for every sample; no linearized execution form exists today.
- `CirVolumeEstimator.EstimateVolume` currently performs a dense triple loop and calls `node.Evaluate(p)` per cell center, so all tree recursion overhead appears in hot loops.
- `CirAnalyzer` only provides point classification plus passthrough volume estimate; there is no region-level classification/early-out.
- Current boolean composition follows implicit field algebra (`Union=min`, `Subtract=max(left,-right)`, `Intersect=max`), which is semantically useful but not guaranteed to preserve exact signed-distance behavior after composition.
- Transform evaluation currently inverts transform per call path (`CirTransformNode.Evaluate`), so repeated region queries have no transform cache plan.

Implication: CIR has a semantic kernel, but no dedicated evaluation runtime yet for map/section/volume-heavy analysis paths.

## 2) Proposed tape IR (linear evaluation tape)

### 2.1 Goal

Introduce a **point-evaluation-first** linear tape lowered from CIR tree, with stable instruction indices and optional provenance metadata.

### 2.2 Instruction shape

A minimal instruction record should include:

- `OpCode` (primitive eval, boolean combine, transform, constant load if needed).
- `DestSlot` (result register index).
- `InputA`, `InputB` (source slot indices, `-1` when unused).
- `PayloadIndex` (index into side tables for primitive/transform parameters).
- `NodeId` / provenance handle (optional but recommended for tracing).

Conceptual form:

```text
0: EvalBox payload=box0 -> s0
1: EvalCylinder payload=cyl0 -> s1
2: Neg s1 -> s2
3: Max s0,s2 -> s3     // subtract
return s3
```

### 2.3 Side tables

Keep instruction payload small by storing structured parameters in side arrays:

- `BoxParams[]`, `CylinderParams[]`, `SphereParams[]`
- `TransformParams[]` (forward + inverse cached)
- optional `ConstDouble[]`

### 2.4 Boolean + transform op mapping

- `Union(left,right)` -> `Min`
- `Subtract(left,right)` -> `Neg` + `Max`
- `Intersect(left,right)` -> `Max`
- `Transform(child,T)` -> `ApplyInverseTransform(point,T)` then evaluate child path

For point-only tape, transforms can be represented either as:
- explicit point-frame instructions (`PushFrame/PopFrame` style), or
- lower-time baking into primitive-local evaluators.

E0 recommendation: prefer explicit transform instructions for inspectability.

### 2.5 Output and provenance

- Tape has one designated `OutputSlot`.
- Every instruction should optionally retain source-node provenance (`CirNodeKind`, stable lowering index, and optional user-facing trace label) to support diagnostics.

## 3) Interval / region evaluation model

## 3.1 Interval value type

Define region evaluation as interval over implicit field values:

- `FieldInterval { MinValue, MaxValue, CertaintyFlags }`

Interpretation over region `R`:
- `MaxValue < 0` => region fully inside.
- `MinValue > 0` => region fully outside.
- otherwise straddling zero => mixed/uncertain boundary crossing.

Because CIR composition is implicit-field algebra (not strict SDF post-CSG), this is a **conservative classification signal**, not exact distance semantics.

## 3.2 Primitive intervals over AABB

For each primitive, compute conservative bounds over region AABB:

- Box/sphere/cylinder: use distance-to-shape bound formulas using nearest/farthest-point envelopes from region to primitive frame.
- Transformed primitives: evaluate interval in local space by transforming region conservatively (AABB in local frame) or using bound propagation with transform norms.

E0 policy: correctness-first conservative intervals (may widen), never optimistic narrowing.

## 3.3 Boolean interval combination

Given `A=[aMin,aMax]`, `B=[bMin,bMax]`:

- Union (`min`): `[min(aMin,bMin), min(aMax,bMax)]`
- Intersect (`max`): `[max(aMin,bMin), max(aMax,bMax)]`
- Subtract (`max(a,-b)`): negate B as `[-bMax,-bMin]`, then max-combine.

These are monotone interval lifts of current scalar ops.

## 3.4 Safe pruning rules

Examples:

- Union node: if left interval `leftMax < 0` (definitely inside), union result definitely inside; right child can be skipped for region classification.
- Intersect node: if either child `childMin > 0` (definitely outside), result definitely outside; other child skip allowed.
- Subtract node (`max(left,-right)`):
  - if `leftMin > 0`, result definitely outside regardless of right.
  - if `rightMax < 0` (region fully inside subtracted tool => `-rightMin > 0` tendency), classification may become outside; combine rule still governs.

Pruning must be gated by conservative interval facts only.

## 4) JudgmentEngine / utility-scored planning model

`JudgmentEngine<TContext>` already matches CIR planning structure: candidate list, admissibility predicates, numeric score, deterministic tie-break, and rejection reasons.

### 4.1 Candidate action families

For region/point workloads, candidate actions can be expressed as:

- `FullTapeInterpreter`
- `UseCachedInterval`
- `ClassifyByIntervalOnly`
- `PruneChildSubtree`
- `SubdivideRegion`
- `UseCompiledDelegate`
- `FallbackRecursiveInterpreter`
- `UnsupportedUnknown`

### 4.2 Context wrapper needed

Current engine is sufficient as chooser, but CIR needs a small wrapper context (e.g., `CirEvalPlanContext`) containing:

- workload kind (point/map/section/volume)
- region size/depth budget
- interval confidence/width metrics
- cache hit metadata
- compilation availability/warmth
- trace sink options
- determinism policy and tolerance context

### 4.3 Admissibility, score, rejection

Each candidate defines:

- admissibility: hard safety/availability checks (e.g., compiled delegate exists; interval bound valid; depth < max).
- score: utility estimate (expected cost, expected prune gain, cache locality, required fidelity).
- rejection: explicit reason string, mirroring existing Boolean routing style.

Deterministic behavior is inherited from `JudgmentEngine`: score, then tie-break priority, then name, then declaration order.

## 5) HFSM / Dominatus analogy: where valid vs not

- **CIR semantic tree**: declarative geometry semantics (not FSM).
- **Tape**: linear program/dataflow (not FSM).
- **Execution planner**: bounded decision scheduling (FSM-like only in control policy transitions, e.g., classify/subdivide/fallback states).
- **Runtime state**: work queue of regions, caches, and diagnostics.

So analogy is useful at planner/orchestration level (deterministic inspectable policy transitions), not as a claim that CIR geometry itself is an FSM.

## 6) .NET execution tiers (recommended path)

A staged model:

1. **Tier A**: current recursive interpreter (`CirNode.Evaluate`) remains baseline oracle.
2. **Tier B**: tape interpreter (flat arrays/spans, no recursion for core primitives/booleans) for hot point loops and better tracing.
3. **Tier C**: compiled delegate (`Func<Point3D,double>` or struct evaluator) emitted from tape via expression trees or generated methods; rely on .NET JIT.
4. **Tier D**: region-specialized reduced tape (after interval pruning), interpreted first, optionally compiled if repeatedly reused.
5. **Tier E**: custom/native JIT explicitly deferred (anti-goal for near-term milestones).

Recommendation: prove correctness and diagnostics at A/B before moving to C/D.

## 7) C# iterator assessment

- **Helpful**: planning pipelines, trace event streams, and offline explainability (`IEnumerable<TraceEvent>`).
- **Risky in hot path**: iterator state machines and allocations can hurt tight voxel/map loops.
- **Recommendation**:
  - use iterators for non-hot planning/trace formatting layers,
  - use arrays/spans/struct-based loops in final tape execution and interval kernels.

## 8) Trace / diagnostic model

Design trace schema with event records tied to provenance:

- `PlanCandidateEvaluated` (name, admissible, score, rejection reason)
- `PlanSelected`
- `IntervalComputed` (node/instruction, region id, min/max, method)
- `PruneApplied` (node, reason, skipped range)
- `RegionClassified` (full/empty/mixed, supporting interval facts)
- `SubdivisionChosen` (axis/split, budget/depth rationale)
- `ExecutionTierChosen` (recursive/tape/compiled and why)

Trace goals:
- explain *why* pruning occurred,
- explain region class decision basis,
- explain planner selection and fallback,
- align with existing Aetheris style of explicit rejection reasons.

## 9) Analyzer workload integration

How runtime supports workloads:

- **Point containment**: tape interpreter first benefit (many probe points).
- **Approximate volume**: biggest gain from region classification/pruning + subdivision policy.
- **Analyze map**: grid scan can reuse per-tile interval culling.
- **Analyze section**: 2D slice sampling benefits from tape + region short-circuit.
- **Analyze compare**: repeated evaluations across two kernels benefit from deterministic traces.
- **Future local/sub-box volume**: directly aligns with region interval planner.

First implementation target recommendation: **approximate volume path**, because it is currently dense and repeatedly evaluates point samples over a bounded region.

## 10) Risks and anti-goals

Key risks:

- **Dual-kernel drift**: recursive and tape evaluators diverge semantically.
- **Transitional transform recursion**: E1 `EvalTransform` currently stores inverse transform payload + child node and calls recursive child evaluation; keeping this too long can hide hot-path recursion costs and delay true flat-tape execution guarantees.
- **Bad interval math**: optimistic bounds cause incorrect pruning.
- **Runtime monster**: over-abstraction before B-tier tape value is proven.
- **Performance illusions**: synthetic wins without end-to-end analyzer improvement.
- **Debugging complexity**: planner + caching + tiers obscure errors unless traces are first-class.
- **Semantic mismatch**: CIR implicit field semantics vs BRep materialized behavior differ in edge conventions.
- **Tolerance ambiguity**: inconsistent boundary thresholds across point/region classifiers.

Anti-goals for near term:
- custom native JIT,
- GPU evaluator,
- replacing BRep materialization/production Firmament runtime,
- full speculative optimizer before tape interpreter exists.

## 11) Recommended implementation ladder

- **CIR-E1**: Lower CIR tree to linear tape + point tape interpreter (parity with recursive oracle, trace scaffolding).
- **CIR-E2**: Interval evaluation over AABB for primitives/booleans/transforms with conservative proofs.
- **CIR-E3**: Utility-scored region planner using `JudgmentEngine` wrapper context + rejection diagnostics.
- **CIR-E4**: Integrate planner/tape into CIR analyzer volume/map/section paths behind explicit experimental switch.
- **CIR-E5**: Add compiled delegate tier from tape (reuse .NET JIT), include deterministic eligibility diagnostics.
- **CIR-E6**: Region-specialized reduced plans (pruned tape snapshots), optional compile-on-reuse heuristics.

## 12) Recommended immediate next milestone

Immediate next step: **CIR-E1 (tape lowering + tape point interpreter)**.

Definition of done for CIR-E1:

- deterministic lowering from existing `CirNode` tree to linear tape with provenance,
- tape point evaluation parity checked against recursive evaluator on existing CIR fixtures,
- no behavioral change required yet in external CLI paths,
- first-pass trace output showing instruction execution and selected evaluation tier.

## 13) CIR-E1.1 tape-first runtime framing (transition policy)

### 13.1 What changed in framing

- M0/M1 used tree-first framing to prove semantics quickly (`CirNode` primitives/booleans/transforms).
- After E1, runtime framing is tape-first: `CirTape` is the intended MIR/runtime substrate for evaluation-centric workloads.
- Tree-to-tape lowering remains a first-class adapter during transition and for backward compatibility.

### 13.2 `CirNode` role during transition

`CirNode` is retained deliberately for three roles:

1. convenience semantic builder/prototype surface,
2. compatibility source for deterministic lowering to tape,
3. recursive oracle used by parity/differential tests.

This is not a deprecation notice for `CirNode`; it is an execution-path reframing.

### 13.3 Transform handling after CIR-E2

CIR-E2 removes the transitional recursive transform escape hatch from tape evaluation.

Chosen strategy: **Option B (baked local inverse transform per primitive payload)**.

- lowering carries an accumulated inverse transform while traversing the tree,
- `CirTransformNode` composes that accumulator and lowers only its child (no emitted transform opcode),
- primitive tape payloads (`EvalBox`/`EvalCylinder`/`EvalSphere`) now store the accumulated inverse transform,
- tape execution applies that payload transform before primitive SDF evaluation.

Resulting runtime shape:

- tape remains straight-line SSA for current node kinds,
- transform evaluation is fully self-contained in tape data/instructions,
- tape point evaluation no longer calls `CirNode.Evaluate` for supported nodes.

This preserves deterministic lowering and prepares interval evaluation and later bytecode/register compaction work by keeping execution ownership inside tape runtime data.

### 13.4 Lowering target policy going forward

- New evaluation/runtime lowerers should target tape directly where feasible.
- Tree-lowering adapters remain valid for migration and parity, but are no longer the conceptual end-state runtime.
