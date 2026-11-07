# Mold: injection-molding DFM by declaration

## Goal

Get a plastic part from first design to tool-ready while the changes are still cheap. The tooling intent a
toolmaker would otherwise reconstruct after release (pull direction, parting, side actions, gates, ejectors,
cosmetic faces, material) is declared next to the part, and the mechanical checks that follow from it run on every
build. Moldflow stays a targeted follow-up on the regions the cheap checks flag, starting from a model whose gates,
parting and material are already defined.

## What exists

### PlasticShell (X0a)

`Model { PlasticShell X { ... } }` is a first-class domain with the right vocabulary and the right evidence shape, on
one geometry:

| Area | State |
| --- | --- |
| Exterior | open coaxial frustum only (`Exterior: Frustum`, radii, height) |
| Intent | `Material`, `ToolingDirection` (+Z only), `MinimumDraftAngle`, `Preserve` (cosmetic), `WallPolicy`, one `PartingPlane` (must sit at the rim), `Gate` (Point/Edge, must touch the rim), `Standoff`, `EjectorPin`, `AutoRib` |
| Thickness | exact, from the frustum planner's two analytic witnesses |
| Draft | exact cone semi-angle; constant-section ribs and standoffs warn at 0° |
| Pullability | a fixed table: `OuterConicalWall` cavity, `InnerConicalWall` core, `TopAnnularRim` parting |
| Gates | rim contact test; straight-line flow-distance proxy |
| Ejectors | floor containment and planar clearance from standoffs and selected ribs |
| AutoRib | two candidate networks (perimeter, gate fan) scored by the Judgment Engine; materialized as exact constant-wall ribs |
| Evidence | `PlasticEvidenceStrength` (ExactAnalytic / CertifiedBounded / SampledConservative / GeometricProxy), per-check records, AP242 notes on the faces |

Every check is a closed form over the frustum's parameters, and regions are string names, not faces. None of the
analyses carries over to another exterior. What does carry over: the diagnostic vocabulary, the evidence-strength
ladder, the rule that ejectors and gates are never silently moved, and the AutoRib judgment pattern.

### Around it

- **Hollow** (`Compose<Hollow>`, `Extrude<Hollow>`, `Revolve<Hollow>`): general constant-wall bodies with exact wall
  witnesses. This replaces PlasticShell's frustum as the exterior authority.
- **Draft**: no `Draft` on the modern path. Extrude and Compose walls are vertical, so every one of them fails a draft
  check today. `Rib<Rectangular>` has its own side draft (conical sides when drafted). This is the biggest geometric
  gap.
- **Face references**: PMI already resolves `face(-Z)`, `face(op.Top)`, `face(pocket.Floor)` and holes by name, and
  rejects what does not resolve. Mold reuses that resolver.
- **DFM policy families**: `Concept` (contract) + `Record` (data) + `Static` defaults with `with` variants + a
  `Template<Policy: ...>` exist for CNC, FDM, additive and sheet metal. No molding policy.
- **Judgment Engine**: eligibility-then-utility selection with retained rejections; already used by AutoRib.

## The split: policy is a Concept, tooling is a Mold block

The scalar rules (minimum draft, wall range, rib ratio, flow ratio, minimum inside radius, texture draft per depth)
are compile-time requirements. They belong in a `Concept InjectionMoldingPolicy` with Record/Static data, checked by
`Require` before geometry, exactly like the sheet-metal and CNC families. A material is a Static:
`Static ABS: MoldingPolicySpec`, `Static PC_GF30 = ABS with { ... }`.

The tooling intent cannot be a Concept. It names faces, edges and points of a built part, and every check against it
needs the B-rep. It is a `Mold` block that refers to the part. That gives:

- one part, several tools (single cavity, family mold, different gate strategy) without touching part geometry;
- the part file stays a product definition; the Mold file is the tooling contract;
- checks run after materialization, on faces, with evidence.

```firmament
Include "lid.firmament";

Static Lid_ABS = ABS with { MinimumDraft: 1deg }

Mold LidTool {
  part: EnclosureLid
  policy: Lid_ABS
  stage: Concept                    // Concept | Prototype | Tooling: how hard failures are
  pull: +Z                          // the direction the cavity (A) half opens

  parting: Plane { at: face(body.Bottom) }
  cosmetic: [face(+Z), face(stock.Side)]
  texture: { faces: [face(+Z)]; depth: 0.025mm }

  Action LatchCore { pull: -X; faces: [face(latch.Wall)] }

  Gate Main { kind: Edge; at: Point3(0mm, 30mm, 0mm) }
  Ejectors { zone: face(body.Floor); diameter: 4mm }   // or explicit Pin E1 { at: ... }
}
```

`Model<PlasticShell>` keeps working until the Mold route reproduces X0a (as `Revolve<Hollow>` + `Mold`), then retires
under the convergence rule. AutoRib survives as a Mold-aware interior feature, not as part of the shell domain.

## Checks

Each check reports per face (or edge, or point) with an evidence strength. Severity scales with `stage`: at `Concept`
everything is a warning except contradictions in the declaration itself; at `Tooling` every failure is an error and
the build is the release gate.

| Check | Input | Method | Strength |
| --- | --- | --- | --- |
| Pull side | pull, parting, actions | every face assigned to exactly one of A, B or an action; by normal against pull and side of the parting | exact on analytic faces; a face whose normals straddle ⟂pull must be split by the parting line |
| Draft | pull, policy, texture | per-face angle to its own pull; plane exact, cylinder/cone from axis and semi-angle, torus/sphere/B-spline over the trimmed normal range | exact / bounded |
| Undercut | pull, actions | face visible from its side along its pull; generator certificates where they exist (a section stack with sections monotone along the pull has none), otherwise ray casting on the tessellation | certified / sampled |
| Parting | parting, classification | parting line is a closed loop of edges or face splits where A meets B; the parting surface leaves no A face below it or B face above it | exact for planar parting |
| Action | action pull | its faces release along its pull, its pull is not blocked by the part, and it does not cross the main parting | certified / sampled |
| Wall | Hollow witness, features | constant wall exact from the Hollow construction; feature junctions as ratios to nominal (the existing thick-section proxy); free-form thickness by inscribed-ball sampling | exact / proxy |
| Sharp inside corners | B-rep edges | concave edges with no blend, against policy minimum inside radius (0.5t typical) | exact |
| Ribs and bosses | feature intent | rib thickness/wall, height/wall, draft, base radius; boss OD/hole, boss wall/nominal, coring; from the feature declarations | exact |
| Ejection | ejectors, classification | each pin on a B face, pin face near-normal to pull, outside cosmetic faces, clear of walls and features by policy distance; total pin area against projected area | exact |
| Gate | gates | on an accessible face for its kind (edge gates on the parting line), not cosmetic | exact |
| Flow length | gates, wall | geodesic distance over the part from the nearest gate to every point, divided by local wall: the L/t ratio against the material limit | sampled |
| Knit lines | gates, holes | where flow fronts from different gates meet, and behind holes the front splits around | sampled |

Not checks, but carried in the report: shrinkage scale for the tool, projected area (clamp tonnage), part volume and
shot weight from the material density.

The geodesic flow proxy is the one new algorithm worth care. It runs on the display tessellation's face-adjacency
graph, so it is cheap and honest about being sampled. It replaces nothing in Moldflow; it catches parts that will not
fill.

## What has to be built that is not a check

1. **Draft on generators.** `draft: 1deg` on `Extrude`, Compose `Base`/`Boss`/`Pocket`, and through `<Hollow>`. Walls
   become ruled cones and drafted planes about a declared neutral plane (the parting by default). The Hollow table
   already lists uniform draft as an exact offset (distance t / cos α), so a drafted hollow stays exact. Without this,
   every Compose part fails its draft check and the feature is decorative.
2. **Interior features on hollow bodies.** Ribs and screw bosses grown from the cavity (Hollow phase 5). Manual ribs
   exist on Box stock only; standoffs exist only in PlasticShell.
3. **Parting split.** A face that straddles the parting (a cylinder with its axis ⟂ pull) has to be split along its
   silhouette for the B-rep to carry A/B assignment. Planar parting against planes and cylinders first.

## Outputs

- Diagnostics in the normal build, per stage.
- A JSON sidecar with every check's per-face evidence, like PlasticShell's.
- Viewport classification: A, B, action, parting line, undercut, draft below policy, cosmetic. For a human this view
  is most of the value.
- AP242 notes on the faces (pull side, gate, ejector, action), as PlasticShell already writes.
- A Moldflow handoff: gates, material, part STEP. Nothing simulated.

## Phases

| Phase | Delivers | Useful alone because |
| --- | --- | --- |
| M0 | `InjectionMoldingPolicy` family with ABS/PC/PP Statics; `Mold` block; pull, parting plane, cosmetic; pull-side classification and draft report on any part | answers "will this pull" for every existing Compose/Hollow/Revolve part, read-only |
| M1 | `draft` on Extrude/Compose/Hollow | makes M0's draft check passable |
| M2 | undercut detection, `Action`, parting split for straddling faces | side actions declared instead of discovered at the toolmaker |
| M3 | wall/junction, sharp inside corners, rib and boss rules | the classic DFM review |
| M4 | gates, ejectors, flow length, knit lines | tool layout checked |
| M5 | interior ribs and bosses on Hollow; AutoRib on Mold; PlasticShell X0a reproduced and retired | one plastics route |
| M6 | suggestions: ejector layout and gate position by Judgment Engine, parting from silhouette | assist, never silent placement |

M0 needs no new geometry and works on parts that already exist, which is why it goes first.

## Decisions for the author

1. **Mold as a separate block referring to the part** (proposed), not `Model<PlasticShell>` fields. Same Mold can
   also sit inside the Model file for a one-off.
2. **`pull` means the direction the A (cavity) half opens.** B (core) faces release along −pull.
3. **Stage-scaled severity** (`Concept`/`Prototype`/`Tooling`) instead of a strict flag. Mirrors EVT/DVT/PVT: early
   designs get the full report without failing builds; the tooling stage is the release gate.
4. **Draft as a generator parameter** (proposed), exact and consistent with Hollow, rather than a `Modify Draft` that
   tilts finished faces. A Modify Draft can come later for imported parts.
5. **Parting forms in M0**: `Plane` only. Stepped and contoured parting surfaces and parting from silhouette later.

## Implementation status

M0 is in (`Aetheris.PlasticShell/Mold*.cs`, CLI `build` and `validate`, `docs/public/firmament/mold.md`).

- The Mold block is a top-level declaration beside the part's Model. The part builds through the ordinary compiler
  with the block blanked out, and is read back from its STEP, so the review sees exactly what ships.
- Materials are built-in defaults (ABS, PC, PC_ABS, PP, HDPE, PA66, PA66_GF30, POM, PMMA); a typed `Static` named by
  `policy:` overrides them field by field, and `minimumDraft:` overrides both. A full `InjectionMoldingPolicy` Concept
  waits for the M3 rules that would use its other fields.
- Faces are named geometrically, so the review also works on parts whose construction names are not available:
  `face(±X|Y|Z)` and `side(±pull)`. Construction names (`face(body.Floor)`) need the PMI resolver; later.
- Side and draft are exact on planes and on cylinders and cones about the pull; elsewhere they are the extremes over
  the face's mesh. Undercuts are traced from up to 64 mesh samples per face against the part's mesh, bucketed on the
  plane normal to the pull. Rays start three chord tolerances off the surface, because neighbouring faces may chord a
  shared curved edge differently: with the default 0.05 mm chords, a 12 mm tray corner first reported a 12–88% false
  undercut from exactly that overlap.
- A face parallel to the pull is assigned to the half it can leave towards, and to the half on its side of the parting
  plane when both are open. This puts a cup's inner wall with the core and its outer wall with the cavity.
- Draft findings are aggregated into one diagnostic per required angle; a first review of a Compose part otherwise
  prints one line per vertical wall.

Evidence: a 2° PP cup releases with exact 2.0002° cones and its projected area equals the rim disk to 0.1%; the hollow
tray releases with every wall at 0° (24 warnings at `Concept`, errors at `Tooling`) and its projected area equals the
rounded-rectangle outline; a bottle pulled along its axis reports the inside of its shoulder, body and floor as
undercuts and a parting line from 39.4 to 58 mm, off its rim plane; a grooved pulley reports exactly the groove's two
faces and floor.
