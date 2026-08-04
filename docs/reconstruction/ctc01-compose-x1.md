# CTC-01 Profile + Compose blockout X2

## Verdict

CTC-BLOCKOUT-X2 succeeds as a bounded prismatic pressure test. The current scaffold-backed Profile + Compose route naturally expresses the dominant plate/web, four observed R50 end transitions, the Z=-60 central plate level, and the central hexagonal raised region. It lowers through three normalized material slabs, four horizontal transitions, one authoritative `PrismaticSectionStackBrepPlan`, and no 3D operation-solid Boolean.

This is not a whole-part reconstruction. Holes, side holes, conical entries, the left capsule slot, the right rounded opening, uncertain Z=-50 relief grouping, Z=5 finish transition, fillets, chamfers, and finish chains are deliberately absent. The result is an honest prismatic blockout classified `Approximate` against the reference.

> Compose material regions first. Emit topology once.

Profiles are a bounded planar blockout and escape-hatch mechanism. Higher-level semantic CNC features remain preferred where they express the actual intent.

## Artifact identity and verification

| Item | Evidence |
|---|---|
| Reference | `testdata/step242/nist/CTC/nist_ctc_01_asme1_ap242-e1.stp` |
| Reference SHA-256 | `85a5752da05f53c456ca3a9e038c90358e1d5a3141d1f0d6e5f0970f2356e821` |
| Source | `testdata/firmament/reconstructions/nist_ctc_01/ctc01_prismatic_blockout_x2.firmament` |
| STEP | `artifacts/reconstruction/ctc01/ctc01-prismatic-blockout-x2.step` |
| STEP SHA-256 | `0add5993e0db1fc76482959f33eeecca5034bd580586ece5188655b1e28040f0` |
| Placement | explicit `ReferenceWorld`: anchor `[0,0,0]`, plane `XY`, axis `+Z`, reference direction `+X` |
| Plan | `compose:Ctc01PrismaticBlockoutX2:slabs=3:transitions=4`, authoritative |
| Topology | 1 body, 1 shell, 63 faces, 152 edges, 92 vertices; enclosed-manifold |
| Surfaces | 47 planes, 16 cylinders, 0 cones, 0 B-splines |
| CompilerVerified | yes; parser, Profile validation, normalization, emitter, and STEP export succeeded |
| BRepVerified | yes; in-memory M8 is enclosed and orientation-consistent |
| SerializationVerified | yes; STEP reimport is one enclosed body with matching bounds/topology |
| ExternalInspectionPending | yes; no external display was requested; this does not reduce in-process M8 evidence |

The declared blockout M8 volume tolerance is `5 mm³`. Analytic volume is `27,503,809.028687116 mm³`; in-memory M8 is `27,503,806.564529333 mm³`; STEP-reimported M8 is `27,503,806.564529568 mm³`. Both deltas are `2.465 mm³` or less and are also within M8's reported conservative `340.22728550337837 mm³` numerical bound. The ordinary analyzer reports surface area `850,568.2137584459 mm²`.

`inspect-profile --json` reports all seven referenced Profiles valid, with 38 total source segments: 30 lines and 8 exact circular arcs. `inspect-compose --json` reports the explicit placement, operation provenance, slab arrangement counts below, transition region sets, M8 status, and authoritative plan.

## Arrangement material-policy completion

The material predicate is order-independent:

```text
Material = (Base union Adds) - Removes
```

| Fixture | Final section | Analytic volume | M8 after STEP | Topology result | STEP SHA-256 |
|---|---:|---:|---:|---|---|
| Add overlapped by Remove | 418 | 4090 | 4089.9999999999995 | 1 Outer, 1 Inner | `b8d35030046b355473ad7cbb08c485416f06bc07af0e6a62a5495f31e69a3c79` |
| Overlapping Removes | 304 | 5520 | 5519.999999999996 | one 96 mm² void, 1 explicit Inner | `62a1d2602e64c1dc547dc449b160f563729cdf8b11fc9e05b3fedb1f90c74afb` |
| Shared-boundary Adds | 600 | 5000 | 5000 | shared edge removed, no duplicate face | `ce5a4224897717ee4dee20e5d97268b9bffac311412e5dda314ff4c35890ad5c` |
| Crossing removal notch | 352 | 3760 | 3760.000000000001 | one connected notched Outer, no Inner | `3f1cf1fb5e9ba125e27ee103f7aeb186b814b24a8bb3715216381b4c4651abe0` |

All four ordinary analyses report one enclosed-manifold body. In-memory BRep and STEP-reimported M8 values agree with analytic values within `0.01 mm³`. Reversing Add and Remove enumeration in the first fixture preserves the two slab areas, loop roles, and `4090 mm³` volume.

The invalid corpus rejects before BRep/STEP emission:

| Fixture | Required behavior observed |
|---|---|
| point-only tangent connection | `point-only-tangent-or-zero-width-ligament` and unresolved incidence |
| zero-width ligament | tangent/zero-width vertex rejected without perturbation |
| contradictory coincident Add/Remove | `contradictory-coincident-add-remove-boundary` |
| ambiguous tangent crossing | bounded tangent crossing rejected |
| dangling arrangement fragment | Profile endpoint mismatch rejects before arrangement |
| disconnected final material | two outer material loops rejected |
| unresolved angular ordering | non-manifold vertex reports `unresolved-angular-order` |

Each invalid real `build` returned exit code 1 and emitted no STEP. No coordinate perturbation, tolerance healing, or fallback solid was used.

## Final Concept scaffold and source excerpts

One `Ctc01BlockoutScaffold` owns all 2D evidence:

- `PrimaryWebGuide`: observed `800 x 250 mm` broad web.
- `MidLevelGuide`: strong-inference `500 x 300 mm` central plate at Z=-60..0.
- Four named six-segment ear Profiles. Each uses four named line guides and two exact R50 `Circle2` guides at observed support centers `(±300 or ±350, ±175)`.
- `CentralHex`: six named points/lines, section bounds `[-50,-57.7350538379252]..[50,57.735]` from the Z=6 reference section.

The Compose root explicitly anchors those scaffold coordinates in the reference world frame: `[0,0,0]`, `XY`, `+Z`, with `+X` as the in-plane reference direction. The parser validates and `inspect-compose` reports this declaration, so the signed Z intervals and XY landmarks are not positional conventions hidden in prose.

The Compose operations are:

```text
Base PrimaryPlate             PrimaryWeb      -100..0   Observed
Add  Left/Right Top/Bottom    four ear paths  -100..0   StrongInference
Add  MidLevelPlate            MidLevelWeb      -60..0   StrongInference
Add  CentralRaisedRegion      CentralHex         0..50  StrongInference
```

No giant raw-coordinate Profile was used: the broad levels are `Rect2`-derived; each observed lobe owns a small named path; symmetric coordinates are explicit scaffold landmarks rather than duplicated inside Profiles. No source translation was needed because Compose accepts signed levels.

## Construction journal

| Stage | Scaffold/Profile/operation change | Arrangement and slab evidence | Analytic contribution | Topology/evidence change | Source pressure |
|---|---|---|---:|---|---|
| Primary web | `PrimaryWebGuide` -> `PrimaryWeb` -> Base -100..0 | 200,000 mm² before lobe union | included below | one connected slab | existing Rect2/Profile route natural |
| Four lobe ears | 24 named line/arc segments, four Adds | lower slab: 28 sources, 24 intersections, 32 atoms, 24 retained; area `255,707.96326794894` | `10,228,318.530717958` for -100..-60 | 8 exact arc boundary segments / cylindrical faces | current points/lines/circles sufficient |
| Mid level | `MidLevelGuide` -> Add -60..0 | upper plate slab: 32 sources, 28 intersections, 44 atoms, 24 retained; area `280,707.96326794894` | `16,842,477.796076935` | Z=-60 needs two disconnected cap patches | demanded bounded transition region-set support |
| Central raise | six-line `CentralHex` -> Add 0..50 | area `8,660.254037844385` | `433,012.70189221925` | Z=0 cap is one concave analytic Outer with one Inner | demanded bounded alternate bridge for M8 tessellation |
| Export/reimport | one authoritative root plan | 3 slabs, 4 transitions | total `27,503,809.028687116` | 63/152/92, 47 planes, 16 cylinders | no source workaround |

## Pressure-led implementation

Two implementation changes were demanded by the real CTC source:

1. **Transition region sets.** The Z=-60 central shoulder adds material in two disconnected horizontal patches even though both adjacent slabs and the final body are connected. `PrismaticSectionTransition` now owns bounded exact region lists; cap faces are emitted once from those lists. Slab policy and the one-body requirement are unchanged.
2. **Single-hole concave planar bridge search.** The Z=0 top face has the concave lobe outline plus the central hex inner loop. The existing nearest visible triangulation bridge was valid but not ear-clippable. The tessellator now retries remaining visible outer vertices in deterministic score order, rebuilding the rings for each attempt. This is bounded to one inner loop and changes verification/display tessellation only, not authoritative geometry.

The arrangement guards, explicit bounded placement declaration, and `inspect-compose` M8 status/error fields were completed for evidence. Nonzero anchors and alternate orientations reject because CTC did not require a general transform. No translated frames, literal point sets, mirroring, tracing convenience, sketch solving, offsets, splines, or generic Boolean fallback were added.

## Pretzels avoided and deliberate omissions

- Avoided primitive Boolean soup, direct BRep construction, STEP editing, duplicated operation solids, and arbitrary closure approximations.
- Avoided inventing the reference's Z=-50 relief topology and Z=5 finish transition. Both remain `ReferenceIntentUncertain` at blockout fidelity.
- Did not materialize Ø35/Ø25 hole groups, side holes/cones, capsule/right openings, semantic slots, fillets, chamfers, or finish chains per milestone scope.
- Did not introduce mirror syntax merely to shorten four bounded ear Profiles; current named points/lines were adequate.

The remaining source awkwardness is moderate coordinate repetition across the four symmetric ear scaffolds. It did not block or distort the result, so no mirror/translated-frame feature was justified.

## Reference comparison

| Evidence | Reference | Blockout | Classification |
|---|---:|---:|---|
| Bounds | `[-400,-225,-100]..[400,225,50]` | exact same | EquivalentWithinTolerance |
| Connected bodies/shells | 1 / 1 | 1 / 1 | EquivalentWithinTolerance |
| Principal Z levels | -100,-60,-50,0,5,50 | -100,-60,0,50 | Approximate; -50 and 5 deliberately omitted |
| Section bounds | full XY bounds and central hex observed | full bounds; hex bounds match Z=6 evidence | SemanticallyEquivalentDifferentTopology for modeled landmarks |
| Slab areas | exact reference grouping unavailable | 255,707.9633; 280,707.9633; 8,660.2540 mm² | Unsupported for exact equivalence |
| Volume | 14,644,822.6361138 | 27,503,809.028687116 mm³ analytic | Approximate; 1.87805682x due omitted voids/details |
| Surface area | 807,080.802199914 | 850,568.213758446 mm² M8 | Approximate; 1.05388235x |
| Plane/cylinder/cone | 56 / 57 / 4 | 47 / 16 / 0 | Approximate; holes/cones/finishes omitted |
| R50 lobe supports | observed at end regions | 8 exact quarter-arc uses on observed centers | SemanticallyEquivalentDifferentTopology |
| Central raised region | hexagonal, Z=0..50 with Z=5/R5 finish evidence | sharp six-line prism, Z=0..50 | Approximate |
| Holes, slots, reliefs, finishes | present | absent | NotReconstructed |

No whole-part equivalence is claimed.

## Remaining blocker and next milestone

The current scaffold-backed workflow answered the milestone question positively for the main prismatic construction. The next blocker is no longer basic authoring ergonomics or exact line/arc arrangement. It is **reference section analysis and semantic grouping**: the analyzer still cannot authoritatively group the Z=-50 relief regions, holes/openings, and Z=5 transition into material sections with correspondence evidence.

Recommended next milestone: **CTC-SECTIONS-X1 — exact reference section grouping and correspondence**. Extract connected, bounded line/arc section loops at the principal Z intervals; group split cylinder supports into physical features; compare candidate/reference loop bounds, areas, and landmarks. Use that evidence to decide whether the next material increment is a Profile relief, a semantic hole/slot, or a finish transition. Do not add mirror/constraint/selection features until that evidence produces a concrete blocked source operation.
