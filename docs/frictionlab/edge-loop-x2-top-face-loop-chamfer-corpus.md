# EDGE-LOOP-X2 — Top-face loop chamfer artifact corpus

## Purpose and scope

EDGE-LOOP-X2 promotes the EDGE-LOOP-X1 top-face outer-loop chamfer proof into a deterministic artifact corpus. The corpus writes STEP artifacts for successful Class B loop chamfer cases and records rejected/deferred rows as JSON-only evidence.

This milestone is artifact/corpus work only. It does not production-route loop chamfers, replace production chamfer/fillet behavior, alter STEP import/export, alter Boolean behavior, or change BRep topology behavior outside generated lab artifacts.

## References

- EDGE-LOOP-A0: `docs/edge-loop-a0-face-boundary-edge-finish-audit.md`
- EDGE-LOOP-X1: `docs/frictionlab/edge-loop-x1-top-face-loop-chamfer-prismatic-lab.md`
- EDGE-A3 selection taxonomy: `docs/edge-a3-edge-finish-selection-taxonomy.md`
- Prismatic split policy: `docs/edge-prismatic-x4-coplanar-split-merge-policy-audit.md`
- Sweep-first doctrine: `docs/aetheris-v2-sweep-first-architecture.md`

## Corpus command

The chosen route is an explicit experimental CLI hook:

```bash
aetheris experimental loop-chamfer-corpus --out-dir <dir> [--json]
```

The command is lab-only and does not alter normal production CLI commands or defaults. `--json` echoes the written summary to stdout; the summary is always written to `<dir>/edge-loop-x2-corpus.json`.

## Artifact filenames

Successful cases write:

- `edge-loop-x2-canonical-top-face-loop-chamfer.step`
- `edge-loop-x2-larger-top-face-loop-chamfer.step`
- `edge-loop-x2-non-square-top-face-loop-chamfer.step`

The JSON summary is:

- `edge-loop-x2-corpus.json`

Rejected/deferred rows write no STEP artifact.

## Case list

### Successful STEP cases

| Case | Width | Depth | Height | Chamfer distance | Expected bounds |
| --- | ---: | ---: | ---: | ---: | --- |
| `canonical-top-face-loop-chamfer` | 10 | 8 | 6 | 1 | `[-5,-4,0]..[5,4,6]` |
| `larger-top-face-loop-chamfer` | 10 | 8 | 6 | 2 | `[-5,-4,0]..[5,4,6]` |
| `non-square-top-face-loop-chamfer` | 12 | 5 | 7 | 1 | `[-6,-2.5,0]..[6,2.5,7]` |

### JSON-only rejected/deferred cases

- `invalid-zero-chamfer-distance`
- `invalid-negative-chamfer-distance`
- `too-large-chamfer-distance`
- `invalid-width`
- `invalid-depth`
- `invalid-height`
- `non-finite-dimensions`
- `non-uniform-rule-rejected`
- `arbitrary-graph-rejected`
- `open-chain-deferred`
- `non-closed-loop-rejected`
- `non-outer-loop-deferred`
- `non-planar-owning-face-deferred`
- `inset-self-intersection-risk`

## JSON schema

The summary includes:

- `milestone`: `EDGE-LOOP-X2`
- `corpusRoute`: experimental CLI command string
- `constructionRoute`: `prismatic-section-transition`
- `splitPolicy`: `preserve-section-splits`
- `cases[]` with:
  - `caseName`
  - `status`: `succeeded`, `rejected`, `deferred`, or `failed`
  - `artifactPath` / `artifactFileName` when written
  - `selectionClass`: `Class B / face-boundary loop`
  - `loopSelectionSummary`
  - `ruleSummary`
  - `constructionRoute`
  - `splitPolicy`
  - `topologySummary`
  - `stepMarkerSummary`
  - `diagnostics`
  - `errors`
  - `guarantees`
- corpus-level `diagnostics`, `errors`, and `guarantees`

## Class B selection summary

Successful rows encode one Class B face-boundary-loop selection:

- owning face: top cap
- loop kind: outer
- closed: true
- edge count: 4
- ordered: true
- rule: uniform symmetric chamfer

This remains a history-known rectangular-prism lab route. Imported/no-history loop inference is not claimed.

## Split-preserving topology contract

Each successful top-face loop chamfer is emitted as a three-section prismatic stack:

1. full lower rectangle,
2. full rectangle at pre-chamfer height,
3. inset top rectangle.

Expected topology for all successful rows:

| Metric | Expected |
| --- | ---: |
| section count | 3 |
| vertices | 12 |
| edges | 20 |
| faces | 10 |
| planar faces | 10 |
| cylindrical faces | 0 |
| cap faces | 2 |
| lower prism side faces | 4 |
| transition faces | 4 |
| chamfer transition faces | 4 |
| loops | 10 |
| coedges | 40 |

The split boundary between the lower prism side faces and chamfer transition faces is preserved. No coplanar merge is performed.

## STEP marker expectations

Successful STEP artifacts must contain:

- `ISO-10303-21`
- `MANIFOLD_SOLID_BREP`
- `ADVANCED_FACE`
- `PLANE`

Successful STEP artifacts must not contain:

- `CYLINDRICAL_SURFACE`
- `BREP_WITH_VOIDS`

These marker checks are smoke evidence only; corpus topology authority remains the generated BRep/topology summary from the lab path.

## Rejected/deferred behavior

Rejected/deferred rows are JSON-only. They preserve deterministic diagnostics such as:

- `edge-loop-x2-case-rejected:<case>:<reason>`
- `edge-loop-x2-case-deferred:<case>:<reason>`

They do not write STEP artifacts, do not invoke fallback constructive routes, and do not broaden support for arbitrary graphs, inner loops, open chains, non-planar loops, non-uniform rules, variable distances, or fillets.

## Route-exclusion guarantees

The corpus guarantees record:

- no production route replacement,
- no AirEdgeSweep,
- no BrepBoundedChamfer,
- no topology graft/body mutation,
- no 3D Boolean,
- no coplanar merge,
- not four independent single-edge chamfers.

## Loop operation, not four independent single-edge chamfers

The successful path creates one loop selection and lowers it to one prismatic section-transition stack. The four chamfer transition faces are emitted as part of that whole-loop construction. Diagnostics include `edge-loop-x2-not-four-independent-single-edge-chamfers:<case>` to make that distinction machine-checkable.

## Non-goals

EDGE-LOOP-X2 does not provide:

- production loop chamfer routing,
- inner-loop support,
- open-chain support,
- arbitrary edge graph support,
- fillet support,
- unequal-distance or mixed chamfer rules,
- triangle migration retry,
- NURBS/freeform expansion,
- STEP exporter/importer changes,
- Boolean core changes,
- production BRep topology changes.

## Tests run

During implementation, the focused gates were run:

```bash
dotnet run --project Aetheris.CLI --framework net10.0 -- --help
dotnet run --project Aetheris.CLI --framework net10.0 -- experimental loop-chamfer-corpus --out-dir "$tmp/edge-loop-x2" --json
dotnet test Aetheris.CLI.Tests/Aetheris.CLI.Tests.csproj --filter "LoopChamfer|FaceLoopChamfer|Prismatic|Analyze|Map|CliBaseline|Step|AirChamfer|Experimental|Corpus"
dotnet test Aetheris.FrictionLab.Tests/Aetheris.FrictionLab.Tests.csproj -c Release -f net10.0 --filter "FaceLoopChamfer|LoopChamfer|PrismaticSectionTransition|PrismaticTopEdgeChamfer|ProfileChamfer|ProfileStackChamfer|AirChamfer|EdgeSweep|Fillet|Chamfer|CIRLab"
dotnet test Aetheris.Kernel.Core.Tests/Aetheris.Kernel.Core.Tests.csproj --filter "Chamfer|Fillet|Corner|Prismatic|CIR|Cir|BrepPrimitives|BrepExtrude|Step242|Primitive|Boolean|SafeComposition"
dotnet test Aetheris.Kernel.Firmament.Tests/Aetheris.Kernel.Firmament.Tests.csproj --filter "Chamfer|Fillet|Corner|ProfileStack|LineArcProfileExtrude|FirmamentPrimitive|FirmamentStepExporter|SemanticRecovery|FrepMaterializer|Rematerialize"
```

## Recommended next milestone

Recommended follow-up options:

1. **EDGE-LOOP-X3** — gated stability/analyzer confirmation for the X2 corpus.
2. **EDGE-LOOP-X4** — no-history/imported loop rejection diagnostics.
3. **EDGE-FILLET-A0** — Class B fillet policy audit without implementing fillet production routing.

## AIR-A0 evidence note

AIR-A0 classifies this corpus as evidence for a future `AirTopFaceLoopChamfer` Feature/Constructive AIR lane. The intended future lowering is `ChamferLoop` over a Class B top-face outer loop into a prismatic section transition, with BRep emission authority remaining in the prismatic route and any CIR convex-polyhedron mirror admitted separately. See `docs/air-a0-aetheris-v2-compiler-ir-constitution.md`.

## AIR-X1 wrapper note

AIR-X1 adds an internal `TopFaceLoopChamfer` AIR wrapper around the existing `PrismaticTopFaceLoopChamferPrototype` lane. The wrapper preserves the EDGE-LOOP-X2 Class B face-boundary-loop metadata, uniform chamfer rule, split-preserving topology summary, STEP smoke summary, and route-exclusion guarantees; it does not replace production routing or change chamfer geometry.

## AIR-X2 route-selection note

AIR-X2 can select the top-face loop chamfer wrapper through deterministic switch/match policy for `FaceBoundaryLoop + UniformChamfer + history-known top-face loop`. This is route-decision evidence only; it does not change the corpus writer or production geometry routes.


## AIR-X4 BRepPlan mapping note

AIR-X4 maps the corpus chamfer transition faces into BRepPlan `ChamferFace` semantic roles while preserving the existing prismatic/top-face loop chamfer emitter summaries and corpus expectations.
