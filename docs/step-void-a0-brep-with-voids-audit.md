# STEP-VOID-A0: BREP_WITH_VOIDS import/export and shell-role audit

Date: 2026-05-14  
Status: **Outcome A — clear support plan**  
Scope: architecture/topology audit only (no importer/exporter/topology behavior changes)

## 1) Current BRep shell model

### What exists now

- `BrepBody` already carries an optional `ShellRepresentation` metadata object (`BrepBodyShellRepresentation`) alongside core topology/geometry/bindings.
- `BrepBodyShellRepresentation` is explicit: one `OuterShellId` and zero or more `InnerShellIds`.
- `TopologyModel` itself remains generic ownership topology (`Body -> Shell -> Face -> Loop -> Coedge -> Edge -> Vertex`) with IDs for each level.
- Core topology entities (`Shell`, `Body`) do **not** encode shell role; roles are carried by `BrepBody.ShellRepresentation` when available.

### Behavior implications

- A body can technically contain multiple shells in `TopologyModel` (`Body.ShellIds` is a list).
- If no explicit shell representation is provided, `BrepBody` attempts a legacy fallback only when there is exactly one body and exactly one shell; otherwise `ShellRepresentation` remains `null`.
- This means multi-shell data is representable in topology, but role semantics are only reliable when `ShellRepresentation` is explicitly set.

### Missing/limited today

- No explicit enum role per shell object (e.g., `Outer/Void/Unknown`) in topology entities.
- No shell-orientation/closure validation API at body level for "outer vs void" semantics.
- No built-in distinction between:
  - outer + inner-void shells,
  - multiple disconnected outer solids,
  unless supplied by `ShellRepresentation`.

## 2) Current exporter behavior (`Step242Exporter`)

### Root solid emission

Exporter chooses STEP root entity from `BrepBody.ShellRepresentation`:

- If `InnerShellIds.Count == 0` => emits `MANIFOLD_SOLID_BREP` with one closed shell.
- If `InnerShellIds.Count > 0` => emits `BREP_WITH_VOIDS` with one outer closed shell plus `ORIENTED_CLOSED_SHELL` entries for each inner shell.

### Assumptions and gates

- Requires exactly one topology body (`Topology.Bodies.Length == 1`).
- If `ShellRepresentation` is missing:
  - allows only single-shell topology (auto-assumed manifold solid),
  - rejects multi-shell export with diagnostic requiring explicit shell representation.
- It does not run a planner/JudgmentEngine for entity choice yet; choice is direct from shell representation presence/content.

### Through-hole relevance

A through-hole shape can still export as `MANIFOLD_SOLID_BREP` if represented by a single connected boundary shell (no inner void shells in representation).

## 3) Current importer behavior (`Step242Importer`)

### Topology root classification

- Exact BRep import lane currently classifies rigid roots by scanning for `MANIFOLD_SOLID_BREP` entities only.
- If zero manifold roots: returns deterministic failure `Missing MANIFOLD_SOLID_BREP root entity.`
- Therefore `BREP_WITH_VOIDS`-rooted inputs are currently rejected as missing rigid root in exact BRep import.

### Shell parsing

- For accepted root, importer reads one shell reference from `MANIFOLD_SOLID_BREP` arg 1 and decodes one `CLOSED_SHELL` face list.
- It ultimately builds one shell (`builder.AddShell(faceIds)`) and one body with that one shell.
- No parsing of `BREP_WITH_VOIDS` inner shell lists.
- No parsing/preservation of shell roles from STEP.
- No import of `ORIENTED_CLOSED_SHELL` void members.

## 4) Correct semantic distinction (policy clarification)

This distinction must remain explicit:

1. **Single connected closed boundary shell with a tunnel/through-hole**  
   -> `MANIFOLD_SOLID_BREP` (no separate enclosed void shell).

2. **Outer shell + enclosed inner cavity shell(s)**  
   -> `BREP_WITH_VOIDS` (outer boundary plus inner void boundaries).

3. **Multiple disconnected outer solids**  
   -> not `BREP_WITH_VOIDS` by default; this is a multi-solid/assembly-like modeling case and needs separate handling.

Applied examples:

- Box with cylindrical through-hole (full pass-through): usually single shell => `MANIFOLD_SOLID_BREP`.
- Box with enclosed sphere/cube cavity (no opening): outer + inner void shell => `BREP_WITH_VOIDS`.
- Two disconnected boxes: multiple solids, not cavity-in-solid semantics.

## 5) Shell-role model recommendation

Options assessed:

- **Option A**: add role on `BrepShell` (not present because shell is topology record without role today).
- **Option B**: body-level sidecar map `ShellId -> Role`.
- **Option C**: importer/exporter-only transient classification.

### Recommendation: **retain and harden the existing body-level representation (Option B-like)**

`BrepBodyShellRepresentation` already exists and already encodes outer + inner roles with minimal churn. Best A1/A2 path is to formalize it as required metadata for multi-shell solids and preserve imported shell-role semantics there.

Why this is minimal/correct:

- avoids broad topology-entity churn,
- already consumed by exporter,
- preserves imported void intent explicitly,
- testable without changing all topology callsites,
- aligns with future materialization while keeping core topology generic.

## 6) JudgmentEngine-based export selection design (A2 target)

Add bounded candidate policies for STEP solid root selection:

1. `ManifoldSolidBrepExportPolicy`
2. `BrepWithVoidsExportPolicy`
3. `UnsupportedShellTopologyPolicy`

### Candidate contracts

#### `ManifoldSolidBrepExportPolicy`
- **Admissible when**: exactly one body, exactly one closed shell roleable as outer, no void shells.
- **Score**: high baseline when invariants are exact.
- **Reject reasons**: multi-shell with void roles present; ambiguous/missing role metadata; closure/orientation failures.
- **Diagnostics**: explicit counts and role summary.

#### `BrepWithVoidsExportPolicy`
- **Admissible when**: exactly one outer shell + >=1 void shells with known role metadata and shell closure checks passing.
- **Score**: high when imported/origin semantics explicitly include void shells.
- **Reject reasons**: missing outer shell, unknown roles, disconnected multi-outer pattern.
- **Diagnostics**: outer shell id, void shell ids, any orientation-normalization actions.

#### `UnsupportedShellTopologyPolicy`
- **Admissible when**: others reject.
- **Score**: lowest fallback.
- **Reject reasons encoded**: ambiguous shell roles; potential multi-solid compound; closure uncertainty.
- **Diagnostics**: explicit blocker list (what must be fixed to enable export).

## 7) Import recommendation (A1 target)

Recommended A1 behavior:

- extend rigid-root classification to include `BREP_WITH_VOIDS` (in addition to manifold root),
- parse `BREP_WITH_VOIDS( name, outer_shell, void_shell_list )`,
- resolve each `ORIENTED_CLOSED_SHELL`/`CLOSED_SHELL` reference,
- import each shell topology and preserve role in `BrepBodyShellRepresentation`,
- build a single topology body with multiple shells,
- **do not** convert void shells into boolean operations,
- emit deterministic diagnostics for unsupported/ambiguous shell references.

## 8) Through-hole export policy

CIR-RECOVERY through-hole (box-cylinder) should remain `MANIFOLD_SOLID_BREP` when topology evidence is a single connected shell and no explicit void shell role is present.

Evidence standard for through-hole != void:

- shell count = 1 in representation (or inferred single-shell legacy path),
- no inner shell IDs,
- tunnel represented as inner face bounds/connected boundary, not separate enclosed shell.

Regression guard:

- keep tests asserting through-hole exports contain `MANIFOLD_SOLID_BREP` and do **not** contain `BREP_WITH_VOIDS` unless explicit cavity shell roles exist.

## 9) Test ladder

1. **Import minimal `BREP_WITH_VOIDS`**: one outer shell + one void shell; assert body imports and shell representation preserves roles.
2. **Export single-shell box**: assert `MANIFOLD_SOLID_BREP`.
3. **Export explicit outer+void model**: assert `BREP_WITH_VOIDS` + oriented void shell refs.
4. **Through-hole regression**: box-cylinder through-hole remains `MANIFOLD_SOLID_BREP`.
5. **Ambiguous multi-shell rejection**: multi-shell body without roles emits explicit unsupported diagnostics.
6. **Round-trip role preservation**: import voided solid then export, preserving root entity family and shell role mapping.

## 10) Implementation ladder

### STEP-VOID-A1 (import support)

- extend root classifier for `BREP_WITH_VOIDS`,
- implement parser branch and shell list decode,
- materialize multi-shell topology + `BrepBodyShellRepresentation`,
- add deterministic diagnostics and tests.

### STEP-VOID-A2 (shell-role/export planner)

- add explicit shell-role validation helpers,
- add JudgmentEngine planner with three bounded policies,
- wire exporter selection through planner diagnostics (no new geometry behavior).

### STEP-VOID-A3 (export support hardening)

- keep current entity emission paths, but source decision through A2 planner,
- harden orientation/closure guards for void shells,
- add round-trip/negative corpus coverage.

## 11) Risks and guardrails

- **Risk**: conflating through-holes with enclosed voids => incorrect `BREP_WITH_VOIDS` overuse.
- **Risk**: multi-shell disconnected solids mislabeled as void semantics.
- **Risk**: importer accepts void roots but drops role data.

Guardrails:

- treat shell roles as explicit preserved metadata, not inferred from "has hole" heuristics,
- require deterministic diagnostics when role/closure evidence is insufficient,
- preserve SEM-A0 boundaries: no generated topology naming expansion as part of STEP-VOID A1/A2/A3.

## Code/test evidence inspected (source-of-truth)

- BRep shell model:
  - `Aetheris.Kernel.Core/Brep/BrepBody.cs`
  - `Aetheris.Kernel.Core/Brep/BrepBodyShellRepresentation.cs`
  - `Aetheris.Kernel.Core/Topology/TopologyModel.cs`
  - `Aetheris.Kernel.Core/Topology/TopologyEntities.cs`
- STEP export:
  - `Aetheris.Kernel.Core/Step242/Step242Exporter.cs`
- STEP import:
  - `Aetheris.Kernel.Core/Step242/Step242Importer.cs`
  - `Aetheris.Kernel.Core/Step242/Step242RigidRootClassifier.cs`
- Recovery/materialization context:
  - `docs/cir-recovery-v41-firmament-export-regression.md`
  - `docs/firmament-semantic-topology-naming.md`
- Related tests and fixtures:
  - `Aetheris.Kernel.Core.Tests/Step242/Step242ImporterTests.cs`
  - `Aetheris.Kernel.Core.Tests/Step242/Step242ExporterTests.cs`
  - `Aetheris.Kernel.Firmament.Tests/ThroughHoleRecoveryStepSmokeTests.cs`
  - `testdata/firmament/exports/boolean_box_cylinder_hole.step`
  - `testdata/firmament/exports/boolean_box_sphere_cavity_basic.step`
