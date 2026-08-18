# CTC-01 LLM pressure X4

Milestone: `CTC-01-LLM-PRESSURE-X4`

## Outcome

X4 preserves the X3 scaffold, seven authored Profiles, and three-slab composition, then adds the four reference-backed Ø35 mounting holes as semantic `Hole<Shaft>` declarations. The declarations lower statically into exact circular removal regions inside the one authoritative `PrismaticSectionStackBrepPlan`; they do not create Boolean tool bodies or bypass BRepPlan.

The updated source is `fixtures/LegacyV1/Reconstruction/nist_ctc_01/ctc01_prismatic_blockout_x4.firmament`.

| Feature | Center XY (mm) | Diameter | Material span | Evidence |
| --- | ---: | ---: | ---: | --- |
| LeftTopMount | (-325, 175) | 35 mm | Z=-100..0 | PMI Ø35 plus exact reference cylinders |
| RightTopMount | (325, 175) | 35 mm | Z=-100..0 | PMI Ø35 plus exact reference cylinders |
| LeftBottomMount | (-325, -175) | 35 mm | Z=-100..0 | PMI Ø35 plus exact reference cylinders |
| RightBottomMount | (325, -175) | 35 mm | Z=-100..0 | PMI Ø35 plus exact reference cylinders |

`ThroughAll` is declared over the composition, but correspondence is derived from the retained host wall. The composition reaches Z=50 at the central hex while material at each mounting-hole center ends at Z=0. Entry loops are therefore correctly published at Z=0 rather than the nominal composition maximum.

## Evidence used

### Tooling actually used

- Firmament source inspection: X3/X4 source, Profile resolver, composition normalization, static expansion, and compiler signatures.
- `inspect-profile --json`: exact line/arc counts and stable Profile provenance.
- `inspect-compose --json`: operation intervals, arrangement fragments, loop roles, areas, transitions, semantic shaft-hole table, and analytic volume.
- `inspect-selections --json`: Profile descendants plus typed Hole entry-loop and wall-face selections.
- STEP `analyze`: exact topology counts, analytic surface families, bounds, and per-face cylinder/cone parameters for all 117 reference faces.
- STEP `sections`: Below/At/Above evidence at Z=-100, -60, 0, and 50, cross-checked with the earlier reference section packet.
- STEP export, reimport, `canon`, SHA-256 linkage, and independent M8 verification.
- Cadmata in-app browser: reference STEP, X3 compiler overlays, X4 semantic Hole entity, Profile/semantic layers, and source-to-descendant highlighting.
- Repository restore/build/test and frontend test/build/lint validation.

### Existing tooling not useful for this decision

- The legacy checkpoint Boolean chain was not used: it does not preserve the X3 semantic composition and its bounded subtract failure is already documented.
- `match` was not used to infer the holes. Its present Concept matching cases do not group a reference cylinder family into four semantic holes, while direct analytic faces plus PMI were stronger evidence.
- Generic Firmament `validate` adds little to this Profile-composition artifact because its relevant invariants are arrangement, topology correspondence, STEP round-trip, and M8 enclosure.
- M8 was deliberately withheld from the exploratory loop; it is final evidence, not a feature-recognition instrument.
- Raw At-level reference section fragments were not treated as material loops when normalization rejected conflicting coincident ownership.

## Reference and X3 comparison

The reference STEP contains one body and one shell with 117 faces, 318 edges, and 206 vertices. Its analytic surface inventory is 56 planes, 57 cylinders, and 4 cones; its embedded analytic volume is 14,644,822.6361138 mm³. X3 has the correct global bounds and a strong prismatic scaffold, but is intentionally over-material: 27,503,809.028687116 mm³ before the mounting holes.

The Ø35 family is supported independently by PMI diameter evidence; reference cylinders at radius 17.5 mm; four unique axes at `(±325, ±175)` parallel to Z; exact reference spans from Z=-100 to Z=0; and visible agreement between analytic reference display and the trusted R50 lobe centers. No screenshot measurement was used to author a coordinate.

## Ranked remaining feature candidates

Scores are 1 (low) to 5 (high). Cost is reversed: 5 means inexpensive. Pressure value estimates how much the candidate teaches about LLM-native authoring.

| Rank | Candidate | Confidence | Semantic importance | Manufacturing relevance | Capability fit | Cost | Pressure value | Decision |
| ---: | --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| 1 | Four Ø35 through mounting holes | 5 | 5 | 5 | 4 | 4 | 5 | Selected and completed |
| 2 | Four Ø25 local holes at `(±160, ±45)` | 5 | 5 | 5 | 3 | 3 | 4 | Defer until the true Z=-50 relief floor is modeled |
| 3 | Left capsule slot, R20 ends, 80 mm center spacing | 4 | 4 | 5 | 4 | 3 | 5 | Strong next subtractive Profile candidate |
| 4 | Right rounded rectangular opening, R10 corners | 4 | 4 | 5 | 4 | 3 | 5 | Closely related to the capsule and relief stack |
| 5 | Paired side Ø20 holes with conical entries | 5 | 5 | 5 | 1 | 1 | 5 | Needs face-normal composite Hole construction on a composed host |
| 6 | Z=-50 relief/pocket family | 3 | 5 | 5 | 3 | 2 | 5 | Semantically central; grouping remains ambiguous in current sections |
| 7 | Central R5 finish transition at Z=0..5 | 4 | 4 | 4 | 2 | 2 | 4 | Defer until post-finish replacement correspondence is complete |

The R25 circular family around `(±225, ±150)` is real analytic geometry, but whether each circle is best grouped as a hole, boss boundary, or relief artifact remains less certain.

## Authoring route and smallest capability

The first attempt used the existing semantic `Hole<Shaft>` route. That route only admitted primitive/Box hosts; it could not consume the composed X3 body. Anonymous circular Profiles would have produced geometry but lost Hole identity, entry/exit meaning, consumer role, and pressure-test value.

The smallest production addition is bounded syntax inside `Compose`:

```firmament
Hole<Shaft> LeftTopMount {
    Center: [-325mm, 175mm]
    Diameter: 35mm
    End: ThroughAll
    Role: ObservedMountingHole
}
```

Only `Shaft` plus `ThroughAll` is admitted. The compiler validates name, center, diameter, role, and collisions; creates an erased exact four-arc circle Profile; contributes a `Remove` operation across the composition interval; and preserves the Hole stable ID through arrangement, slab topology, entry/exit edges and loops, and cylindrical wall faces. The route remains `(Base ∪ Adds) − Removes` in 2D sections followed by one exact section-stack BRep emission.

Five adjacent defects surfaced under X4 and were fixed locally:

- Static expansion used an X3 comment as a parse anchor; it now anchors structurally on `Profile PrimaryWeb`.
- Multiple inner loops reused transition source IDs; inner-loop IDs now include deterministic indices.
- A nominal `ThroughAll` maximum outside local host material suppressed the entry loop; entry/exit now come from extrema of retained Hole wall boundaries.
- Curved inner walls were bound with the cylinder's outward `SameSense`, making M8 add two-thirds of each void volume even though topology was enclosed. Inner circular side faces now bind `SameSense=false`; STEP reimport preserves the inward material normal.
- Earcut-style point filtering retained a removed ring anchor while bridging several curved holes and could loop forever. Its mutable end anchor is now updated when a node is removed, with X4 as the regression case.

## Composition and selection evidence

| Slab (mm) | Outer loops | Inner loops | Area (mm²) |
| --- | ---: | ---: | ---: |
| -100..-60 | 1 | 4 | 251,859.51226730144 |
| -60..0 | 1 | 4 | 276,859.51226730144 |
| 0..50 | 1 | 0 | 8,660.254037844385 |

The analytic volume is 27,118,963.928622365 mm³. The X3-to-X4 delta is 384,845.100064751 mm³, equal to `4 × π × 17.5² × 100` within floating-point tolerance.

`LeftTopMountEntry` resolves exactly one connected, closed `HoleEntryLoop`. `LeftTopMountWall` resolves eight `HoleWallFace` descendants: four quarter-cylinder faces in each of two host slabs. This partition is compiler structure, not eight authored holes.

The X4 STEP section packet reports five closed loops immediately above Z=-100 and on both sides of Z=-60, five below Z=0 and one above it, and one below Z=50. At Z=-60, normalization leaves transition ownership diagnostic rather than manufacturing a false loop.

## Cadmata observations

Cadmata answered two concrete questions. On X3, selecting `profile:LeftTopEar.Outer.OuterArc` linked one authored arc to arrangement and final descendants, establishing that the intended hole center lies inside the trusted R50 lobe. On X4, selecting `hole:Ctc01PrismaticBlockoutX4.LeftTopMount` reports 18 material descendants: eight boundary edges, eight wall faces, and entry/exit loops. It highlights all split faces without a geometric search.

The initial X4 screenshot exposed a genuine analytic-display defect. DisplayIR published only axial hints, derived from unrelated global planes. Each quarter-cylinder face was rendered as a complete cylinder, so eight coincident previews z-fought and appeared clipped. DisplayIR now publishes face-local `(minU,maxU,minV,maxV)` bounds derived from the actual boundary, and the cylinder/cone preview mapper respects them. The corrected view shows four clean holes and a stable semantic highlight.

Evidence images:

- `artifacts/reconstruction/ctc01-llm-pressure-x4/cadmata-reference.png`
- `artifacts/reconstruction/ctc01-llm-pressure-x4/cadmata-x3-left-top-outer-arc.png`
- `artifacts/reconstruction/ctc01-llm-pressure-x4/cadmata-x4-left-top-mount-hole-fixed.png`

## LLM friction log

| Task | Tool | Input representation | Output representation | Reliable inference | Remaining ambiguity/friction | Workaround | Category | Smallest improvement | Implemented |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Retain X3 intent | source + `inspect-profile` | Named guides/Profiles | Stable IDs, spans, curve table | Exact trusted structure | None material | Preserve X3 in X4 | Representation | Stable source identity | Already worked |
| Understand sections | `inspect-compose` | Compose operations | Compact slabs, loops, areas | Interval structure and volume | Lowered Profiles can clutter | Compact `shaftHoles` table | Representation | Report semantic records | Yes |
| Find next feature | STEP `analyze --face` | 117 exact faces | Per-face parameters | Radius, axis, center, Z span | Manual split-face/PMI correlation | Batch and group analytically | Perception/context | Analytic feature-family table | Not yet |
| Cross-check levels | `sections` | STEP + levels | Deep Below/At/Above graphs | Closed loops away from transitions | Verbose; coincident At ownership | Use Below/Above and prior packet | Representation/performance | Compact section difference | Not yet |
| Inspect provenance | Cadmata | Compiler artifact | Source/material overlay | Exact descendants | Reference lacks semantic source | Cross-check face table | Perception | Reference/source bundle | Not yet |
| Author on Compose | Hole + Compose | Hole intent/host | Initially no route | Primitive Hole semantics fit | Host capability gap | Bounded static lowering | Authoring/compiler | Compose Shaft/ThroughAll | Yes |
| Emit inner loops | arrangement/emitter | Four circles | Duplicate transition IDs | Geometry was valid | Collision blocked result | Deterministic loop index | Compiler | Unique inner provenance | Yes |
| Resolve entry | selections | ThroughAll | Initially no entry | Wall faces proved existence | Global extent differed locally | Retained boundary extrema | Provenance | Host-relative entry/exit | Yes |
| View walls | analytic facade | Eight trimmed faces | Initially eight full cylinders | Family/radius exact | UV trim absent/global | Add face-local UV bounds | Visualization | Publish/honor trim domain | Yes |
| Iterate CLI | `dotnet run`/binary | Repeated commands | CLI JSON | Correctness | Build contention/latency | Built executable, sequential calls | Performance | Document fast loop | Workaround |
| Materialize four holes | M8 tessellation | Exact multi-inner-loop BRep | Initially hung | Failure localized to planar cap filtering | Removed ring anchor remained the loop sentinel | Trace CPU-bound verifier and compare X3 | Verification/performance | Mutable earcut end anchor + regression | Yes |
| Verify void orientation | `verify`/M8 | STEP + analytic volume | Initially `+511,914 mm³`, outside bound | Error was exactly consistent with outward void cylinders | Topological orientation alone did not expose surface sense | Compare X3/X4 boundary-integral contributions | Verification/compiler | Bind curved inner walls `SameSense=false` | Yes |
| Verify final | `verify`/M8 | STEP + expected volume | Hash-linked report | Independent mass/topology | Too expensive for exploration | Run once at final stage | Verification/performance | Staged ladder | Existing doctrine |

## Artifact evidence

- Firmament: `fixtures/LegacyV1/Reconstruction/nist_ctc_01/ctc01_prismatic_blockout_x4.firmament`.
- Route: semantic Compose Hole → exact arc removal Profiles → `ProfileArrangement2D` → `PrismaticSectionStackConstruction` → authoritative BRepPlan → BRep → STEP AP242.
- STEP SHA-256: `9F868B75A36C68D377F269F2E7AB6922A23312A49C14EF5C54EF78BDF1B9AE39`.
- Canonical re-export SHA-256: `10D1E6DE4EC51A6455B6CCCCED1AAB71634840B1202A7D1B54F0E1E8B782ABA0`.
- Reimport topology: 1 body, 1 shell, 95 faces, 232 edges, 140 vertices.
- Analytic surfaces: 47 planes and 48 cylinders; no cones, spheres, tori, splines, or mesh-only construction.
- Bounds: `[-400,-225,-100]..[400,225,50]` mm.
- Structural analysis: enclosed manifold.
- Analytic volume: 27,118,963.928622365 mm³.
- M8: `NumericalWithBound`; 27,118,117.403542258 mm³, surface area 881,817.6884389693 mm², centroid approximately `(0, -0.00000043, -47.6961545)` mm.
- M8 comparison: -846.5250801406801 mm³ (-0.00312%) from the analytic volume, inside the reported 352,727.07537558774 mm³ bound; enclosed and orientation-consistent.
- External verification: pending; no configured external CAD Assistant was invoked.

## Known omissions and recommendation

X4 still omits the Z=-50 relief system, Ø25 local holes, capsule and rounded-rectangle openings, side Ø20 holes and conical entries, R25 family interpretation, local pads/bosses, and central R5/other edge finishes.

The next milestone should reconstruct the Z=-50 relief together with one unambiguous opening, starting with the left R20 capsule if section ownership can be summarized cleanly. First add a bounded analytic feature-family/section-difference report grouping reference faces by shared axis, radius, center, and support interval. If the side-hole family is chosen instead, the explicit construction blocker is a semantic face-normal shaft/countersink on a composed host—not generic Boolean subtraction.

## Validation status

- `dotnet restore Aetheris.slnx` and the final solution build pass; the build has 11 existing JavaScript dependency-audit warnings and no errors.
- Focused final tests pass: 36 analytic-display/planar-triangulation tests, 2 X4 semantic-hole tests, 3 inspect-compose CLI tests, all 40 server tests, and all 55 Cadmata tests.
- Cadmata's production build and targeted lint for the changed analytic mapper/API/test files pass.
- The solution-wide test invocation remains red in unrelated existing suites: 21 Core NIST audit/snapshot tests, 4 Firmament static-logic/legacy M8-tolerance tests, and 5 CLI help/corpus tests. The two legacy geometry failures reproduce with the X4 surface-sense and triangulator changes locally reverted; the CLI help assertion expects an obsolete command list. No X4-focused test fails.
- Full frontend lint remains red on six existing errors in `App.tsx`, `StepImportDropzone.tsx`, `button.tsx`, `AetherisViewport.tsx`, `CadmataOverlay.tsx`, and `tailwind.config.ts`; the changed trim files are clean.
- `git diff --check` passes.

## Direct research answers

**Did the toolchain let GPT reason about and author the next feature without human-style visual drafting?** Yes. Exact reference surfaces and PMI selected it; Firmament authored it; arrangement, provenance, STEP, and M8 validated it. Cadmata answered alignment and descendant questions, not measurements. Manual reference-face grouping remains costly.

**Which representation most improved GPT mechanical reasoning?** A compact analytic feature tuple `(center, axis, radius, support interval)` combined with stable source-to-material correspondence. It converted a visual circle into a manufacturing-relevant Ø35 ThroughAll Hole with verifiable entry, exit, and wall topology.

**Which missing capability is the largest remaining LLM bottleneck?** For reasoning, a compact semantic difference packet that groups reference analytic faces and section regions into feature candidates. For construction, face-normal composite Hole support on composed hosts, needed for the exact side holes and conical entries.
