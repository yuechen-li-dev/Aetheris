# EDGE-PRISMATIC-X7 — Analyze map CIR/FRep suitability audit for prismatic bodies

## 1. Executive summary

### CIR-MAP-X1 follow-up note

CIR-MAP-X1 is the primitive proof step before any prismatic mirror work. It keeps `analyze map` unchanged, admits only box/cylinder/sphere CIR mirrors in lab tests, compares their CIR tape map summaries against the existing BRep raycast primitive baseline, and records deterministic diagnostics for unsupported/prismatic mirrors. This narrows the next prismatic question: prismatic support must be separately admitted from AIR/mirror metadata or another explicit source, not inferred from arbitrary STEP imports or claimed because primitive CIR maps work.

EDGE-PRISMATIC-X6 isolated a clean analyzer split: `aetheris analyze section` can consume selected EDGE-PRISMATIC-X5 generated STEP artifacts and confirm non-empty closed line-loop section geometry, while `aetheris analyze map` is blocked by the current primitive-raycast analyzer limit.

This audit evaluates whether `analyze map` should route through CIR/FRep/tape evaluation for admitted generated prismatic bodies instead of primarily extending the older BRep raycast-only path to every generated or imported BRep family.

No implementation or production behavior changes are made in this milestone. The result is a design decision and proof-plan document only: keep the current bounded map diagnostic, do not claim prismatic BRep map support yet, and move toward representation-polymorphic map dispatch where CIR/FRep is preferred only when an admitted mirror exists.

Recommended conclusion: **hybrid dispatch**. `analyze map` should become representation-polymorphic, with CIR/tape evaluation preferred for generated AIR bodies that carry an admitted CIR/FRep mirror, existing BRep raycast retained for supported explicit-topology bodies, and deterministic unsupported diagnostics retained for STEP/imported bodies that have neither.

Authority note: `docs/air-cir-a0-authority-and-mirror-contract.md` now defines the broader AIR/CIR/BRep/STEP authority and mirror contract used by this recommendation. In that model, CIR/FRep is an optional admitted analysis mirror, AIR remains the constructive topology/intention MIR, BRep remains explicit topology/export authority, and STEP import does not imply mirror provenance.

## 2. Current analyze map architecture

### CLI command shape

The public CLI shape is:

```bash
aetheris analyze map <file.step> (--top|--bottom|--front|--back|--left|--right) --rows <N> --cols <N> --json
```

`--json` is required for successful map output. Argument validation is deterministic:

- missing input: `Analyze map requires <file.step> as the first argument.`
- missing or multiple views: `Analyze map requires exactly one orthographic view option (--top|--bottom|--front|--back|--left|--right).`
- missing grid dimensions: `Analyze map requires both --rows <N> and --cols <N>.`
- non-positive grid dimensions: `Analyze map requires positive --rows and --cols values.`
- omitted JSON after successful analysis: `Analyze map currently requires --json output. Re-run with --json.`

The command imports the STEP file, chooses one orthographic projection frame, samples a `rows × cols` grid, and emits per-sample hit/depth/thickness data plus a summary.

### Input body / STEP path handling

`StepAnalyzer.AnalyzeMap(stepPath, view, rows, cols)` calls the shared STEP import path:

1. `Path.GetFullPath(stepPath)` resolves the input path.
2. `Step242Importer.ImportBody(File.ReadAllText(fullPath))` imports exactly one BRep body.
3. Import diagnostics are wrapped as `StepAnalysisImportException` if import fails.
4. `AnalyzeImportedBodyMap(body, fullPath, view, rows, cols)` performs the map analysis.

This means generated EDGE-PRISMATIC-X5 artifacts and ordinary imported STEP files arrive at the map analyzer as `BrepBody` instances. The current map command has no AIR context, no corpus-case metadata, no CIR root, and no admitted mirror handle after STEP import.

### Current body acceptance criteria

The map analyzer requires:

- positive row/column counts;
- vertex coordinates sufficient to compute a body bounding box;
- a successful `BrepSpatialQueries.Raycast` result for every sample ray.

For each grid cell it builds a ray from the near side of the selected bounding-box projection plane, calls:

```csharp
BrepSpatialQueries.Raycast(body, ray, RayQueryOptions.Default with { IncludeBackfaces = true })
```

and treats the first forward hit as the entry and the last forward hit as the exit. Summary fields such as visible face IDs and visible surface types are therefore BRep-raycast-derived, not field-derived.

`BrepSpatialQueries.Raycast` is intentionally a v1 primitive query layer. It first calls `TryResolvePrimitive`; if the body is not recognized as one of the supported primitive layouts, raycast fails with:

```text
Spatial query v1 only supports primitive Brep bodies from BrepPrimitives.CreateBox/CreateCylinder/CreateSphere.
```

The map analyzer wraps the first raycast diagnostic in the user-visible failure:

```text
Orthographic map v1 currently supports bodies accepted by BrepSpatialQueries.Raycast (Spatial query v1 only supports primitive Brep bodies from BrepPrimitives.CreateBox/CreateCylinder/CreateSphere.).
```

In JSON mode this appears through the standard analysis failure envelope, with `success = false`, `errorKind = "analysis-failure"`, and the message above in `error`.

### Why generated/imported prismatic BReps are rejected

The EDGE-PRISMATIC-X5 successful corpus artifacts are all generated as BReps, exported to STEP, and re-imported for analysis. They are valid prismatic closed planar bodies for the section analyzer, but they are not recognized by the map analyzer's current raycast gate because their face-binding layouts do not match the three primitive cases accepted by `TryResolvePrimitive`:

- sphere: one spherical face binding;
- cylinder: one cylindrical face plus two planar caps;
- axis-aligned box: six planar face bindings with axis-aligned normals.

The prismatic section-transition cases have more general all-planar face sets, split-preserving section-boundary faces, transition faces, and non-box polygonal profiles. The top-edge chamfer case has ten planar faces rather than the six planes of a simple axis-aligned box. Scaled pentagon/hexagon/asymmetric cases have polygon side/transition faces that are explicit BRep topology but not primitive descriptors.

Therefore the X6 map failure is not caused by invalid STEP, absent vertex coordinates, missing section geometry, or prismatic emitter instability. It is principally **an analyzer architecture and BRep-raycast coverage boundary**: `analyze map` is bound to `BrepSpatialQueries.Raycast`, and that raycast implementation accepts only `BrepPrimitives.CreateBox/CreateCylinder/CreateSphere`-style primitive bodies.

More precise classification:

- **BRep raycast coverage:** primary blocker. General planar closed-shell raycast is not implemented in this path.
- **Body classification:** immediate rejection point. The body is classified as not being a supported primitive descriptor.
- **Imported/generated body path:** contributing context. STEP import strips generated AIR/prismatic intent and provides only BRep topology to map.
- **Analyzer architecture:** underlying issue. The map workload is dense sampling, but the only backend is explicit BRep raycast for primitive-recognized bodies.

## 3. Current analyze section architecture

The public CLI shape is:

```bash
aetheris analyze section <file.step> (--xy|--xz|--yz) --offset <value> --json
```

`analyze section` uses the same STEP import path as map, then calls `AnalyzeImportedBodySection`. It succeeds on selected prismatic artifacts because it does not require `BrepSpatialQueries.Raycast` or primitive descriptor recognition.

Instead, it:

1. computes the imported BRep bounding box from vertex coordinates;
2. resolves a principal section frame (`XY`, `XZ`, or `YZ`) at the requested offset;
3. iterates BRep faces;
4. for planar faces, builds planar-face/section-plane intersection segments;
5. for cylindrical faces, builds cylinder-face/section-plane intersection segments where supported;
6. stitches raw segments into loops;
7. reports loop counts, closed-loop counts, segment family counts, and section bounding boxes.

The section analyzer therefore works directly over STEP/BRep geometry and topology traversal. It is contour-oriented and face-family-specific rather than raycast-oriented. For current prismatic corpus artifacts, all relevant section intersections are line segments from planar faces, so section can confirm non-empty closed line-loop geometry even while map cannot raycast the same body.

This is the key contrast:

- `analyze section` asks: “Which face/plane intersections form section contours?”
- `analyze map` asks: “For every pixel ray, what are the ordered ray/body hits?”

The former has enough planar BRep support for these artifacts. The latter delegates to a primitive-only raycast acceptance layer.

## 4. CIR/FRep architecture relevant to map

CIR is Aetheris's constructive implicit/FRep evaluation representation. Current core CIR node kinds are primitive/CSG-oriented:

- primitives: `Box`, `Cylinder`, `Sphere`, `Torus`, `Cone`;
- composition: `Union`, `Subtract`, `Intersect`;
- placement: `Transform`.

Every `CirNode` exposes:

```csharp
double Evaluate(Point3D point)
```

where negative values classify as inside, positive values classify as outside, and near-zero values classify as boundary under a tolerance. `CirAnalyzer.ClassifyPoint` wraps that convention for point classification.

`CirTape` is the linear runtime direction for hot CIR evaluation paths. It lowers primitive and CSG node evaluation into instruction/payload arrays and supports:

- point evaluation via `Evaluate(Point3D)`;
- interval evaluation via `EvaluateInterval(CirBounds)`;
- region classification via `ClassifyRegion(CirBounds, tolerance)` returning inside/outside/mixed.

The current CIR runtime design already calls out `Analyze map` as a workload that can reuse per-tile interval culling, while dense volume and section sampling can also benefit from tape/interval evaluation.

Map sampling is naturally compatible with FRep evaluation because a map pixel can be evaluated without knowing BRep face adjacency:

1. choose an orthographic pixel ray or sample column;
2. evaluate the implicit field at points along that ray or over ray intervals;
3. identify inside/outside transitions or occupied depth intervals;
4. accumulate depth/thickness/occupancy output.

This avoids requiring a full raycast adapter for every BRep face type. For a CIR mirror, the analysis backend asks a field question rather than a topology-intersection question.

Important limitation: CIR point evaluation is not automatically an exact ray-intersection solver. A CIR-backed map still needs a sampling/root-finding/interval policy for entry/exit depth and boundary tolerance. The advantage is that the primitive/CSG field runtime is the natural backend for dense repeated evaluations, especially once tape and conservative intervals are used.

## 5. Representation authority model

The corrected Aetheris V2 direction is:

```text
Firmament / semantic intent
  -> AIR constructive topology MIR
    -> BRep explicit topology / STEP
    -> CIR/FRep mirror for analysis where admitted
```

Authority should be explicit:

- **AIR is authoritative for construction intent.** It knows the operation family, section correspondence, profile stack, and semantic route that a STEP-only import may not preserve.
- **BRep is authoritative for explicit topology/export.** It owns faces, edges, loops, trims, materialized shell structure, and STEP export/import behavior.
- **CIR is authoritative only for admitted field/evaluation mirrors.** It can answer field/inside-outside/sampling questions when the mirror is declared valid for the body family and tolerance policy.

`analyze map` should not pretend BRep and CIR are identical. A CIR path is valid only when a body has an admitted mirror with a documented drift policy, source authority, and diagnostics. Conversely, a BRep-only STEP import should not silently reconstruct a CIR mirror unless an explicit recognizer/mirror-admission milestone proves that recovery.

## 6. Prismatic CIR mirror feasibility

Current CIR node kinds are primitive/CSG-oriented. There is no current `ConvexPolyhedron`, half-space intersection, general prism, section-stack transition, or prismatic-transition CIR node. That means no current exact CIR mirror exists for the EDGE-PRISMATIC-X5 corpus cases unless they happen to reduce to an existing primitive/CSG expression.

### Corpus case assessment

| Corpus case | Shape character | CIR mirror feasibility today | Clean future mirror |
|---|---|---:|---|
| `rectangle-inset` | two Z sections; bottom rectangle `10 × 8`, top inset rectangle `8 × 6`, identity correspondence | Not directly available. It is a tapered rectangular frustum-like all-planar body, not a current `Box` unless sections are equal. | Half-space intersection / convex polyhedron, or a dedicated section-stack implicit evaluator. |
| `top-edge-chamfer` | rectangular body with a controlled top `+X` horizontal chamfer expressed as three sections and split-preserving planar faces | Not directly available. It is all-planar and convex for the controlled case, but not an axis-aligned six-face box and not a current CIR primitive. | Half-space intersection / convex polyhedron is likely sufficient for this controlled convex case; section-stack evaluator could preserve route intent. |
| `pentagon-scaled` | regular pentagon scaled between two Z sections | Not directly available. Existing CIR has no polygonal prism/frustum node. | Convex half-space intersection or section-stack implicit evaluator. |
| `hexagon-scaled` | regular hexagon scaled between two Z sections | Not directly available. `BrepPrimitives` has a hexagonal prism primitive elsewhere, but current CIR node kinds do not include polygon prisms or tapered polygonal transitions. | Convex half-space intersection or section-stack implicit evaluator. |
| `pentagon-asymmetric` | asymmetric pentagon translated/reshaped between two Z sections with identity correspondence | Not directly available. Requires general polygonal ruled side handling. | Dedicated section-stack implicit evaluator is cleaner than trying to infer a special primitive; half-spaces may work if the resulting body remains convex. |

### Mirror strategies

1. **Half-space intersection / convex polyhedron CIR node.**  
   Preferred for convex all-planar prismatic cases if the body can be represented as an intersection of oriented planes. This would map naturally to current implicit composition (`max` over plane fields for intersection), but Aetheris does not currently have a first-class half-space or convex polyhedron CIR node. It would also need robust plane orientation, bounds, and interval support.

2. **Triangulated/mesh-like FRep.**  
   Likely not preferred. A mesh-derived signed field would risk approximation drift, boundary ambiguity, and loss of the analytic planar contract. It also duplicates topology-derived behavior in a less inspectable form.

3. **Section-stack implicit evaluator.**  
   A future dedicated CIR primitive could evaluate interpolated line-only section profiles along Z, using the same AIR/prismatic correspondence contract as the emitter. This is attractive for route-authoritative generated AIR bodies because it mirrors the construction intent rather than reverse-engineering exported topology.

4. **Fallback: no CIR mirror yet.**  
   This is the current honest state. The corpus artifacts are good BRep/STEP/section evidence, but they do not currently carry a CIR/FRep mirror suitable for `analyze map`.

## 7. Options for analyze map backend

### Option A: Extend BRep raycast

Pros:

- Uses exported/materialized topology directly.
- Avoids AIR/CIR mirror drift.
- Can report explicit face IDs, surface types, hit points, and face normals naturally.
- Works for imported STEP bodies if the explicit topology is supported.

Cons:

- Requires robust raycast support for all emitted/imported BRep faces and trims.
- Can become another topology-specific burden parallel to section, containment, volume, and exporter logic.
- Dense map workloads magnify every raycast robustness/tolerance issue.
- Teaching primitive-raycast v1 about prismatic cases alone may solve one corpus while leaving the architecture unchanged.

### Option B: CIR/FRep map backend

Pros:

- Natural for dense sampling/map workloads.
- Aligns with the existing CIR/tape direction for hot point loops and interval culling.
- Avoids broad BRep raycast expansion for generated bodies whose construction intent already admits a field mirror.
- Can support generated AIR families through explicit mirror contracts instead of reverse-engineering STEP.

Cons:

- Needs an AIR-to-CIR mirror for each supported body family.
- Current CIR lacks half-space/polyhedron/section-stack nodes needed by the prismatic corpus.
- Loses explicit topology/face identity unless provenance is added separately.
- Mirror drift risk: map output may diverge from exported BRep if the mirror and materializer disagree.
- Entry/exit depth still needs a precise sampling/root-finding/interval policy; point classification alone is insufficient.

### Option C: Hybrid dispatch

Preferred direction:

- If a body has an admitted CIR/FRep mirror, use the CIR/tape map evaluator.
- Otherwise, if the BRep body is accepted by `BrepSpatialQueries.Raycast`, use the existing BRep raycast path.
- Otherwise, report unsupported with deterministic diagnostics that distinguish “no admitted CIR mirror” from “BRep raycast unsupported.”
- Keep `analyze section` BRep/contour-oriented unless and until a separate CIR-backed section sampling design is admitted.

This option respects the authority model: generated AIR bodies can carry an evaluation mirror, primitive/imported BReps can keep using explicit raycast where supported, and unsupported STEP imports remain honest instead of being guessed into CIR.

### Option D: AIR-native map evaluator

AIR atoms could theoretically produce map/section data directly without lowering to CIR. For prismatic section transitions, an AIR-native evaluator could march through section profiles and correspondence intervals.

However, AIR-native map evaluation risks duplicating the CIR runtime. AIR should provide intent and mirror admission; CIR/tape should be the reusable dense evaluation substrate. AIR-native direct evaluation may be useful as an oracle or initial lab scaffold, but it should not become a second production analyzer runtime unless CIR is shown to be the wrong abstraction for a specific family.

## 8. Recommended architecture

`analyze map` should become **representation-polymorphic** rather than BRep-raycast-only.

Recommended dispatch policy:

1. Build a map-analysis request containing the desired view, grid, tolerance, and any available representation handles.
2. Evaluate candidates with explicit admissibility, score, and rejection reasons. This is a suitable JudgmentEngine use because multiple bounded strategies compete.
3. Prefer CIR/tape when:
   - the source is generated AIR or another trusted origin;
   - an admitted CIR mirror exists;
   - mirror version/tolerance/provenance match the requested body;
   - the requested map output does not require explicit face identity beyond what CIR provenance can supply.
4. Use BRep raycast when:
   - there is no admitted CIR mirror;
   - the body is explicit-topology-only or imported STEP;
   - `BrepSpatialQueries.Raycast` accepts the body;
   - face ID/surface hit reporting is important and supported.
5. Reject deterministically when neither backend is admissible.

Policy details:

- For generated AIR bodies with a valid CIR mirror, prefer CIR/tape evaluation for map sampling.
- For BRep-only bodies with supported raycast, retain the existing BRep raycast path.
- For STEP/imported bodies without a CIR mirror or supported raycast, continue reporting unsupported.
- `analyze section` can remain BRep/contour-oriented because it currently needs explicit contours and already succeeds for selected prismatic artifacts.
- Do not make STEP import reconstruct prismatic CIR mirrors implicitly until an explicit recognizer/mirror contract exists.
- Do not change production/export behavior merely to satisfy analyzer convenience.

## 9. Smallest proof milestones

### EDGE-PRISMATIC-X7.1 / CIR-MAP-X1 — CIR-backed map for box/primitives

Use existing CIR `Box`, `Cylinder`, and `Sphere` nodes where applicable. Build a lab-only CIR map evaluator and compare output against the existing primitive BRep raycast map for the same primitive STEP fixtures.

Proof target:

- same grid/view inputs;
- bounded parity of hit count, depth, and thickness within a documented tolerance;
- explicit diagnostics showing backend selection;
- no production CLI behavior change unless separately scoped.

### CIR-PRISMATIC-X1 — CIR mirror for rectangle->inset prismatic transition

Prove the first prismatic mirror on the simplest corpus case, `rectangle-inset`.

Two acceptable outcomes:

- add a lab-only/design-only convex prismatic section-transition evaluator if feasible; or
- document that a first-class `ConvexPolyhedron` / half-space CIR node is required before exact map support is honest.

The milestone should not broaden STEP, Boolean, exporter, topology, or production prismatic behavior.

### EDGE-PRISMATIC-X8 — map analyzer hybrid dispatch prototype

After a CIR map backend and at least one prismatic mirror exist, prototype hybrid dispatch:

- admitted CIR mirror -> CIR evaluator;
- BRep primitive accepted by raycast -> existing raycast evaluator;
- neither -> current deterministic unsupported diagnostic, expanded with backend rejection reasons.

For the prismatic corpus, the first target should be generated bodies before imported STEP-only recovery. STEP-only prismatic map support should remain unsupported until mirror recovery is designed.

### AIR-CIR-A0 — AIR/CIR mirror authority contract

Define mirror availability, drift policy, provenance, diagnostics, and authority for AIR atoms and prismatic section-transition routes.

Minimum contract questions:

- Which AIR operations can emit an admitted CIR mirror?
- How is mirror drift against BRep materialization detected?
- Which analyzer outputs are allowed from CIR-only data?
- How are face identity, operation identity, and topology-dependent fields represented or rejected?
- What diagnostics are emitted when no mirror exists?

## 10. Risks and guardrails

Risks:

- **CIR/BRep mirror drift:** a map may describe the mirror rather than the exported body.
- **False confidence from approximate maps:** dense sampling can look convincing while missing narrow features or boundary/tolerance differences.
- **Boundary/tolerance mismatch:** CIR field zero-crossings and BRep trimmed-face intersections may classify boundary samples differently.
- **Topology lost in CIR path:** face IDs, trims, loop identity, shell identity, and explicit adjacency are not inherently available from a scalar field.
- **Representation-dependent output:** the same model could report different visible face metadata or depths depending on backend.
- **Performance illusions:** a recursive `CirNode.Evaluate` prototype may be slower or less robust than expected if tape/interval runtime is not used.
- **Imported STEP ambiguity:** reconstructing CIR from BRep without an admitted recognizer can silently manufacture intent that was not preserved.

Guardrails:

- Keep production/export behavior unchanged for analyzer convenience.
- Keep `analyze map` unsupported for prismatic BReps until a backend is actually admitted. AIR-CIR-X1 adds explicit status language for this boundary: prismatic section transitions remain `mirror-rejected-unsupported-atom` / `mirror-unavailable`, and profile-authored chamfers remain `mirror-unavailable` unless a later real mirror is admitted.
- Require backend diagnostics and rejection reasons.
- Treat CIR maps as field-analysis output, not explicit topology confirmation.
- Keep `analyze section` BRep/contour-oriented unless a separate design proves CIR-backed section semantics.
- Do not run gated artifact corpus tests by default.

## 11. Recommended conclusion

`analyze map` should **not** stay permanently BRep-raycast-first, and it should **not** switch to CIR-first unconditionally.

The recommended architecture is **hybrid dispatch**:

- CIR/tape first for generated AIR bodies with admitted CIR/FRep mirrors;
- BRep raycast for explicit-topology bodies already accepted by `BrepSpatialQueries.Raycast`;
- deterministic unsupported diagnostics for STEP/imported bodies that lack both an admitted CIR mirror and supported BRep raycast.

For prismatic section-transition bodies, CIR-backed map analysis is architecturally attractive, but the corpus cannot be mirrored exactly with current CIR node kinds. The next meaningful blocker is therefore not broad BRep raycast implementation; it is proving a small CIR map backend on existing primitives and defining a first prismatic CIR mirror, likely via half-space/convex-polyhedron or section-stack implicit evaluation.

## 12. Non-goals

This milestone explicitly does not include:

- implementation;
- map behavior change;
- CLI behavior change;
- raycast expansion;
- CIR node addition unless separately scoped;
- CIR-to-BRep extraction;
- STEP exporter/importer changes;
- Boolean core changes;
- BRep topology changes;
- prismatic emitter behavior changes;
- AirEdgeSweep changes;
- 3D Boolean work;
- production route changes;
- claims that `analyze map` supports prismatic BReps;
- default execution of gated artifact tests;
- test weakening.

## 13. CIR-MAP-X2 prismatic status note

CIR-MAP-X2 adds a lab/test-only mirror-aware primitive map dispatcher, but it deliberately does not add prismatic mirror support. Prismatic section-transition and profile-authored chamfer sources still resolve to mirror-unavailable or unsupported admission for map occupancy, emit no-prismatic-mirror-used diagnostics, and do not select the CIR map backend. Prismatic map support remains blocked until a real admitted prismatic mirror exists and can be validated against an appropriate baseline.

## CIR-PRISMATIC-X1 follow-up note

CIR-PRISMATIC-X1 adds a lab-only prismatic mirror feasibility prototype for the required `rectangle-inset` and `top-edge-chamfer` prismatic corpus cases. It compares a convex half-space/polyhedron evaluator against a section-stack implicit evaluator, admits both as exact for bounded point containment and map-like occupancy in the lab, and recommends the half-space/convex-polyhedron path for the next first-class implementation step.

This does **not** change the X7 production audit conclusion: production `analyze map` remains unsupported for prismatic STEP/BRep artifacts until a later milestone introduces an admitted prismatic mirror into production dispatch. X1 also makes no STEP, BRep topology, Boolean, AIR emitter, CLI, or CIR-to-BRep extraction changes.

## CIR-PRISMATIC-X2 follow-up

CIR-PRISMATIC-X2 implements the first reusable convex prismatic CIR mirror (`CirConvexPolyhedronMirror`/`CirPrismaticMirrorBuilder`) for admitted convex all-planar section stacks. The mirror supports test-visible point containment and top-view occupancy/thickness summaries for rectangle-inset and top-edge-chamfer, but X7 conclusions remain intact: production `analyze map` is not integrated with prismatic CIR mirrors yet, and topology/face identity parity remains out of scope.

## EDGE-PRISMATIC-X8 follow-up

EDGE-PRISMATIC-X8 prototypes the hybrid dispatch recommended by this audit in Core test scope. The prototype uses generated prismatic source data plus an admitted `CirConvexPolyhedronMirror` to select a CIR convex-polyhedron map backend for `rectangle-inset` and `top-edge-chamfer`, while preserving BRep raycast as the primitive baseline path and returning deterministic unsupported diagnostics for imported STEP-only/no-source cases. Production `analyze map` behavior and default CLI behavior remain unchanged.

## EDGE-PRISMATIC-X9 route boundary note

EDGE-PRISMATIC-X9 adds `aetheris experimental prismatic-map` as a generated-source-only inspection route for the X8/CIR-PRISMATIC-X2 prismatic mirror proof. Normal `aetheris analyze map <file.step> ... --json` remains unchanged and continues to use the existing STEP analyzer path. X9 does not accept STEP input on the experimental route, does not infer imported STEP mirrors, and does not make topology or face-identity claims from CIR maps.
