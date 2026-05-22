# Aetheris assembly model (ASM-A5 canonical)

This document is the authoritative assembly contract for Aetheris v0.

## 1) Core philosophy

- STEP/AP242 is a **foreign interchange format**.
- `.firmasm` is Aetheris's **authoritative internal assembly representation**.
- Aetheris does not adopt or normalize away STEP assembly ambiguity as native semantics.
- Assembly behavior is defined by Aetheris contracts (`.firmasm`, executor, bounded exporter), not by importer guesswork.

## 2) STEP classification rule (ASM-A2.75)

Aetheris classifies STEP input by rigid root count (`MANIFOLD_SOLID_BREP` roots):

- exactly 1 rigid root => **part-like STEP**
- more than 1 rigid root => **assembly-like STEP**

Consequences:

- Multi-root STEP is never treated as a native multi-body part in Aetheris.
- Multi-root STEP is always assembly-like input and must go through the assembly extraction/import path.
- Single-part exact BRep import path accepts one rigid root only.

## 3) Assembly pipeline (full flow)

```text
STEP assembly
  ↓
classification (part-like / assembly-like)
  ↓
assembly extraction
  ↓
.firmasm + canonical STEP parts
  ↓
execution (ASM-A3)
  ↓
world-space multi-body composition
  ↓
bounded roundtrip export (ASM-A4)
```

Stage semantics:

1. **Classification**
   - Detects part-like vs assembly-like STEP by rigid root count.
   - Guarantees deterministic routing decision.
   - Does not attempt to reinterpret multi-root STEP as a single native part.

2. **Assembly extraction**
   - Produces flat `.firmasm` instances plus deduplicated part STEP files.
   - Uses composed rigid transforms for each emitted instance.
   - Does not create mates, motion, or solver constraints.

3. **`.firmasm` + canonical STEP parts**
   - `.firmasm` captures authoritative assembly semantics.
   - STEP files are foreign part payloads validated at load time through bounded import.

4. **Execution (ASM-A3)**
   - Executes `.firmasm` instances as rigid placements of real imported geometry.
   - Emits one composed multi-body BRep containing all instance bodies.

5. **Bounded roundtrip export (ASM-A4)**
   - Exports one STEP file per executed instance.
   - Writes `roundtrip.package.json` as outbound package metadata.
   - Does not emit a true AP242 assembly file in v0.

## 4) `.firmasm` contract (ASM-A0)

`.firmasm` is JSON by design.

Why JSON:

- machine IR first
- deterministic serialization behavior
- strict schema-like validation with explicit unknown-field rejection

Top-level required sections:

- `manifest` (object)
  - `version` (string, required)
- `assembly` (object)
  - `name` (string, required)
  - `units` (string, required)
- `parts` (object, non-empty)
  - each part value requires:
    - `kind`: `firmament` or `step`
    - `source`: relative/absolute path string
- `instances` (array, non-empty)
  - each instance requires:
    - `id`: unique string
    - `part`: existing key in `parts`
    - `transform` object with:
      - `translate`: `[x, y, z]` numeric array of length 3 (required)
      - `rotate_deg_xyz`: `[rx, ry, rz]` numeric array of length 3 (optional)

Validation rules (non-exhaustive but contract-critical):

- `.firmasm` file extension is required for manifest files.
- Top-level manifest must be a JSON object.
- Unknown top-level fields are rejected.
- Unknown fields in part, instance, and transform objects are rejected.
- `parts` and `instances` must both be present and non-empty.
- Instance ids must be unique.
- Every instance must reference an existing part.
- STEP part files must exist and pass bounded importer acceptance.
- Part kind outside `firmament|step` is rejected.

Current execution scope note:

- Contract allows `firmament` and `step` part kinds.
- ASM-A3 executor currently executes only loaded STEP parts; `firmament` parts are loadable but not executable yet.

## 5) Execution model (ASM-A3)

Execution model is **flat and rigid**:

- No hierarchy execution tree in v0.
- Instances are executed from a flat list.
- Rigid transforms only.
- Geometry is real imported BRep geometry, not proxy meshes.
- Output is one composed multi-body BRep with deterministic ID remapping.

Transform order is fixed:

1. rotate around X
2. rotate around Y
3. rotate around Z
4. translate

No mates/constraints/motion solving occurs during execution.

## 6) Roundtrip export model (ASM-A4)

Outbound export shape is intentionally bounded:

- one STEP file per executed instance
- one package manifest: `roundtrip.package.json`

`roundtrip.package.json` carries:

- `schema`: `asm-a4-step-instance-package/v1`
- `nativeAuthority`: `.firmasm`
- `exportIntent`: `outbound-step-interop`
- source assembly metadata and per-instance STEP file records

Why true STEP assembly export is not used yet:

- v0 freezes a truthful interop boundary without pretending full AP242 assembly authoring support.
- Per-instance package avoids lossy/ambiguous mapping while preserving executed geometry output.

Authority rule after export:

- `.firmasm` remains authoritative even after roundtrip export.
- Exported STEP artifacts are outbound interop deliverables, not native authority.

## 7) CLI surface (source of truth)

These commands are part of the current assembly-adjacent workflow:

1. `aetheris analyze <file.step> [--face|--edge|--vertex] [--json]`
   - Imports a part-like STEP body and reports topology/geometry summary.
   - For assembly-like STEP, JSON mode returns structured failure (`errorKind: assembly-like-step`) with route hint.
   - Use for part-like inspection and classifier-visible failure routing.

2. `aetheris analyze map <file.step> ... --json`
   - Computes orthographic depth/thickness maps for imported STEP bodies.
   - Use when view-based sampling of a part-like body is needed.

3. `aetheris analyze section <file.step> ... --json`
   - Computes principal-plane section loops for imported STEP bodies.
   - Use when deterministic cross-section diagnostics are needed.

4. `aetheris canon <file.step> --out <canonical.step> [--json]`
   - Imports a part-like STEP body and re-exports canonical AP242.
   - Use for bounded canonicalization of supported single-root input.

5. `aetheris asm exec <file.firmasm> [--json]`
   - Loads `.firmasm`, executes rigid instance placements, and composes multi-body result.
   - JSON output includes assembly counts and analyzer summary on composed body.

6. `aetheris asm export <file.firmasm> --out <directory> [--json]`
   - Executes `.firmasm` then writes per-instance STEP files + `roundtrip.package.json`.
   - Use for bounded outbound STEP interop packages.

## 8) Invariants (non-negotiable)

- `.firmasm` is authoritative for Aetheris assemblies.
- STEP assembly is not native authority in Aetheris.
- Multi-root STEP is assembly-like input, never native multi-body part.
- Execution in v0 is flat + rigid only.
- No silent healing beyond bounded seams (strict parse/import/validation failures are surfaced).
- Roundtrip export does not transfer authority away from `.firmasm`.

## 9) Non-goals (v0)

- mates/constraints solving
- kinematic motion
- hierarchy-preserving assembly execution
- native multi-body part semantics from multi-root STEP
- full AP242 assembly export
- feature reconstruction from STEP assemblies

## 10) Known limitations

- ASM-A3 executor currently executes STEP parts only; `firmament` parts are not yet executable in assembly runtime.
- Roundtrip exporter emits per-instance STEP package, not true AP242 assembly structure.
- Exported instance STEP files are expected single-body instance payloads.
- Part-like importer rejects assembly-like multi-root exact-BRep input and requires extraction/import routing.
- Some STEP importer subsets remain bounded by existing STEP/AP242 support matrix and emit explicit unsupported diagnostics.

## 11) Future directions (bounded)

- true AP242 assembly export
- hierarchy-aware execution mode
- mates/constraints layer on top of rigid instances
- richer native part reconstruction pipelines

