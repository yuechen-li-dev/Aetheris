# V2-A3 — Legacy topology contracts and parallel V2 emitter lanes

## 1. Executive summary
Aetheris V2 emitters are the preferred direction for new declared-topology construction because they make profile admissibility, curve-family constraints, and emission diagnostics explicit at the AIR-to-BRep seam.

However, existing legacy emitters can carry downstream topology contracts that are not captured by simple geometry validity or STEP-valid interchange checks alone. In particular, recognizers that depend on adjacency, corner incidence, or seam ordering may implicitly rely on legacy topology conventions.

Migration must therefore be gated by all observable and semantic consumers, not only by surface-family parity. Parallel emitter lanes are acceptable and in some cases required when a V2 lane is constructive-correct for new work but legacy topology remains load-bearing for existing bounded-feature behavior.

Doctrine:
- **Geometry parity is not feature parity.**
- **STEP parity is not recognizer parity.**
- **Load-bearing topology stays legacy until the replacement contract is proven.**

## 2. Why this doctrine exists
V2-V5 attempted production migration of triangle prism emission to `LineArcProfileExtrudeEmitter` based on prior representability and summary parity evidence.

That migration was correctly reverted after V2-X8.1 demonstrated bounded chamfer recognition divergence in triangle cases, and V2-X8.2 isolated the first deterministic mismatch as adjacency-structural at loop/coedge seam ordering/orientation.

In short:
- triangle migration was attempted,
- topology/STEP summary evidence was insufficient,
- downstream bounded chamfer recognition depended on legacy adjacency/corner ordering,
- migration was reverted to preserve production behavior,
- adjacency-structural delta was captured as the first concrete divergence.

This doctrine formalizes that result as a general migration guardrail rather than a one-off exception.

## 3. Definitions
- **legacy topology contract**: the de facto and/or explicit topology semantics (ordering, orientation, adjacency, corner incidence, seam convention) emitted by a legacy route and depended upon by downstream behavior.
- **V2 emitter lane**: an AIR/V2 constructive path that emits topology from resolved profile/sweep intent under bounded admissibility.
- **parallel emitter lane**: coexistence of legacy and V2 lanes where each has an explicit authority boundary and neither silently replaces the other.
- **topology summary parity**: parity of high-level body/face/edge/vertex/loop/coedge counts and broad semantic families.
- **STEP parity**: parity of expected STEP/AP242 markers and surface-family evidence under current exporter behavior.
- **feature-recognition parity**: parity of downstream recognizer outputs (candidate sets, admissibility, and first divergence diagnostics) for features that consume emitted topology.
- **adjacency/corner contract**: the local incidence semantics around corners/edges/seams used by bounded recognizers.
- **load-bearing topology**: topology conventions that materially affect downstream behavior correctness.
- **migration gate**: required parity layer that must pass before replacing a production route.
- **fallback/legacy authority**: policy that keeps legacy route authoritative when required parity is not proven.

## 4. Migration parity ladder
Production migration requires the following ladder, in order:

1. **Construction validity**
   - body produced for valid bounded inputs,
   - invalids rejected deterministically,
   - no forbidden operations introduced for the scoped route.

2. **Topology summary parity**
   - vertex/edge/face counts,
   - face family counts,
   - loop/coedge counts,
   - extents/bounds parity.

3. **STEP/interchange parity**
   - expected STEP markers preserved,
   - no `BREP_WITH_VOIDS` regressions where not expected,
   - no exporter/importer changes unless explicitly scoped.

4. **Diagnostic/fallback parity**
   - route diagnostics remain stable where externally observed,
   - fallback behavior remains preserved where relevant.

5. **Feature-recognition parity**
   - downstream recognizers yield equivalent admissible candidates,
   - adjacency/corner/edge incidence semantics match required contracts,
   - chamfer/fillet/hole/surface-feature recognizers remain green for affected scopes.

A migration is **not production-ready** if it fails any required downstream parity layer for existing consumers.

## 5. Triangle prism case study
- **Legacy route**: `BrepPrimitives.CreateTriangularPrism(...)`.
- **Candidate route**: `LineArcProfileExtrudeEmitter` with one outer line loop.
- **V2-X8 evidence**: triangle is representable by line-profile extrusion; body/summary/STEP signals were promising.
- **V2-V5 attempt**: production migration attempted then reverted.
- **V2-X8.1 finding**: feature-recognition mismatch in bounded triangle chamfer/corner scenarios.
- **V2-X8.2 finding**: first deterministic delta is adjacency-structural at loop/coedge seam, with chamfer admissibility divergence (`legacy=1/1`, `candidate=0/1`).

Final decision:
- triangle remains legacy for production routing,
- line-arc triangle remains valid for new and/or parallel profile-first uses,
- do not retry triangle production migration until adjacency/chamfer contract is resolved or parity is explicitly proven.

## 6. Parallel lane policy
Parallel lanes are allowed when:
- V2 emitter behavior is constructive-correct for new declared-topology use,
- legacy topology remains load-bearing for downstream behavior in existing production paths.

Examples:
- legacy triangle prism remains authoritative for chamfer-sensitive production route,
- line-arc profile triangle can be used in lab/new profile-first contexts without legacy chamfer contract obligations,
- future slot/capsule profile emission may proceed in parallel before replacing legacy faceted slot routes.

Policy requirements:
- keep lane names and diagnostics explicit,
- do not silently substitute V2 emitter for legacy route,
- document authority boundary,
- preserve fallback behavior,
- add migration-gate tests before replacement.

## 7. Feature-recognition parity requirements
For any shape that feeds bounded downstream features, require parity checks for:
- chamfer/corner recognition,
- fillet/edge-sweep recognition,
- surface-feature descriptor recognition,
- hole/profile-stack recovery,
- importer/exporter semantic reconstruction where applicable.

For each recognizer family, require:
- candidate counts,
- admissible counts,
- first-divergence diagnostics,
- edge/face/vertex incidence summaries where needed to isolate local contract deltas.

## 8. Relationship to future AirEdgeSweep
Triangle/chamfer divergence indicates current chamfer/fillet logic still depends on legacy topology contracts.

Future `AirEdgeSweep` (or equivalent edge-surfacing contract) is the likely architecture for reframing these features as constructive edge-surface operations:
- chamfer as straight-profile edge sweep / ruled transition,
- fillet as circular-profile edge sweep,
- concave cases may be additive/easier in bounded contexts,
- convex cases require replacement/trim semantics.

Until `AirEdgeSweep` exists as a proven contract:
- keep load-bearing chamfer-sensitive topology on legacy authority,
- use V2 emitters in parallel where safe.

Non-action for this milestone: do **not** implement `AirEdgeSweep` here.

Follow-up architecture audit reference: `docs/edge-a0-air-edge-sweep-fillet-chamfer-audit.md` (EDGE-A0 / V2-A4).

## 9. Implications for prismatic migration roadmap
Roadmap adjustments:
- triangle production migration: blocked by feature-recognition parity.
- hex migration: must not proceed on summary parity alone; run feature-recognition parity when hex feeds bounded features.
- slot/capsule migration: requires explicit topology-contract decision (legacy faceted vs analytic cylindrical-side expectations).
- rectangle/circle through-hole: already integrated because scoped gates passed.
- new profile-first operations may use V2 emitters without replacing legacy routes.

## 10. Updated migration gate checklist
Every migration PR must include:
- supported shape boundary,
- old route identified,
- new route identified,
- invalid behavior parity,
- topology summary parity,
- STEP/interchange parity,
- diagnostics parity,
- downstream feature-recognition parity,
- CLI/example fixture parity (if applicable),
- fallback/legacy authority policy,
- docs updated with decision and boundary.

## 11. Non-goals
This milestone does **not** include:
- production route changes,
- chamfer/fillet redesign,
- `AirEdgeSweep` implementation,
- STEP exporter/importer changes,
- Boolean core changes,
- hex/slot migration,
- sketch solver/clipping engine/NURBS/freeform expansion.

## 12. Recommended next milestones
- **V2-A4 / EDGE-A0**: `AirEdgeSweep` fillet/chamfer architecture audit.
- **V2-X9**: slot/capsule production evaluation as parallel V2 lane (not automatic replacement of legacy faceted slot route).
- **V2-X8.3**: triangle emitter ordering/adjacency hardening only if triangle migration remains a priority.
- **V2-V6**: hex migration only after downstream feature-recognition parity scope is defined and passed.
- **V2-A3.1**: apply migration checklist rigor across existing V2 migration docs/tests where needed.
