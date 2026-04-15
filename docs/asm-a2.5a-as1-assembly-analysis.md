# ASM-A2.5a — Full assembly analysis for `as1.step` (analysis-only)

## Outcome

**Success** — bounded structural analysis completed for `testdata/step242/OCCT/as1.step` without reconstruction.

## 1) CLI/tool surface inspected first (source-of-truth)

The actual CLI help surface was inspected before analysis:

- `dotnet run --project Aetheris.CLI -- --help`
- `dotnet run --project Aetheris.CLI -- analyze --help`
- `dotnet run --project Aetheris.CLI -- canon --help`
- `dotnet run --project Aetheris.CLI -- build --help`

What this confirmed:

- Only `build`, `analyze`, and `canon` are exposed top-level.
- `analyze` is single-shape oriented (summary + face/edge/vertex/map/section modes), not an explicit assembly-graph analyzer.
- Running `analyze` directly on `as1.step` fails with `Multiple MANIFOLD_SOLID_BREP roots are unsupported in M23 import subset.`

Implication: assembly analysis for this milestone must use STEP-structure traversal diagnostics (entity graph / AP242 relationships), not full importer reconstruction.

## 2) Assembly structure summary

Target: `testdata/step242/OCCT/as1.step`.

### Topology of assembly usage graph (`NEXT_ASSEMBLY_USAGE_OCCURRENCE`)

- Total assembly usage edges (instances): **27**
- Product-definition nodes participating in usage graph: **15**
- Root assembly nodes: **1** (`#5`, product name `as1`)
- Leaf instances reachable from root: **18**
- Maximum hierarchy depth (root depth = 0): **3**

### Repeated subassemblies

Repeated subassembly families are present:

- `nut-bolt-assembly*` appears as 6 sibling/variant assembly nodes, each with 2 children (`bolt`, `nut`).
- `l-bracket-assembly*` appears as 2 assembly nodes, each with 4 children (3 nut-bolt assemblies + 1 `l-bracket`).
- `rod-assembly` appears once with 3 children (2 `nut` + 1 `rod`).

### Leaf instance distribution (from root traversal)

- `nut`: **8** instances
- `bolt`: **6** instances
- `l-bracket`: **2** instances
- `rod`: **1** instance
- `plate`: **1** instance

Total = 18 leaf instances.

## 3) Dedup picture (bounded)

### Likely unique part groups (geometry-bearing leaves)

Using product-definition leaves and associated manifold solids, the likely deduplicated unique part set is:

1. `nut` (PD `#70`) — 8 instances, 1 manifold solid, shell references 8 face refs
2. `bolt` (PD `#579`) — 6 instances, 1 manifold solid, shell references 7 face refs
3. `l-bracket` (PD `#937`) — 2 instances, 1 manifold solid, shell references 16 face refs
4. `rod` (PD `#364`) — 1 instance, 1 manifold solid, shell references 4 face refs
5. `plate` (PD `#1652`) — 1 instance, 1 manifold solid, shell references 18 face refs

So for flattened extraction planning: **18 instances over 5 likely unique parts**.

### Ambiguity / dedup risk notes

- This dataset appears **clean for identity-level dedup** (reused `PRODUCT_DEFINITION` leaves with repeated NAUO usage).
- There are many non-geometry entities (`CARTESIAN_POINT`, styling/presentation entities), so naive whole-file hashing would be noisy; dedup should key off product-definition + representation lineage (or canonicalized geometry signature), not raw textual neighborhoods.
- No immediate evidence of near-duplicate-but-not-identical solid variants among the five leaf groups in this file.

## 4) Transform-chain picture

### Placement / transform linkage evidence

- `CONTEXT_DEPENDENT_SHAPE_REPRESENTATION`: **27**
- `ITEM_DEFINED_TRANSFORMATION`: **27**
- `NEXT_ASSEMBLY_USAGE_OCCURRENCE`: **27**

Mapping shows a one-to-one placement transform per NAUO usage edge.

### Composition depth and shape

- Leaf transform-chain lengths mirror hierarchy depth:
  - 1-hop: 1 leaf path
  - 2-hop: 5 leaf paths
  - 3-hop: 12 leaf paths
- Maximum transform composition depth to leaf: **3**.

### Rigid/composable quality

- All sampled/applied `AXIS2_PLACEMENT_3D` bases used by usage transforms were orthonormal (no non-unit axis or non-orthogonal axis pair detected in this file pass).
- Distinct translation vectors used across 27 instance transforms: **11**.

Risk for flattening to world transforms: **low** for this file, provided composition follows NAUO/CDSR order deterministically.

## 5) Reconstruction readiness decision

### Readiness

**Yes** — `as1.step` appears ready for a **bounded flattened `.firmasm` extraction attempt** after this milestone.

### Recommended extraction strategy (next milestone)

1. Traverse usage graph from root `#5` via NAUO edges.
2. For each leaf instance path, compose chained NAUO transforms (from CDSR/IDT placements) into one world transform.
3. Dedup leaf part definitions into a 5-part candidate library (nut/bolt/l-bracket/rod/plate) keyed by PD+shape-representation lineage (with geometry-signature guard).
4. Emit flattened instance table referencing dedup part IDs + composed world transforms.
5. Keep this bounded to structure/placements only (no mates/motion/parametric reconstruction in this phase).

### Known risk areas to watch next

- Transform composition order mistakes across nested assemblies.
- Dedup key fragility if future files introduce representation-level variation for same named part.
- Importer path currently fails on multi-root manifold solids in `analyze`; extraction should bypass this limitation by using assembly graph traversal path first.

## 6) Intentional non-generalization for ASM-A2.5a

Intentionally **not** done in this milestone:

- No `.firmasm` full assembly artifact generation.
- No part geometry reconstruction/modeling.
- No mates/constraints/motion extraction.
- No redesign of importer architecture; only bounded structural diagnostics for this target file.
