# FRIC-B0 — chimera mixed-subtract pressure test and boundary classification

Date: 2026-04-19 (UTC)

## Mission state

**Success (FRIC-B0.2 bounded boolean-engine generalization pass)**.

FRIC-B0 established a boundary map; FRIC-B0.1 corrected one misclassified datapoint (`zone_b_blind_cyl`). FRIC-B0.2 then generalized the bounded prismatic-continuation rule for safe box-root subtract chains:

- allow prior exterior-opening blind analytic history when prismatic footprint containment has strict radial margin,
- preserve recognized safe-subtract graph state after successful non-analytic subtracts when safe composition metadata remains valid,
- keep unsupported-builder families bounded out (non-box roots, open-slot/prismatic through-void continuation, tangent/grazing degeneracy, unsupported analytic continuation families).

## Source-of-truth inspection (code + CLI)

### CLI capability check

Commands:

- `dotnet run --project Aetheris.CLI -- --help`
- `dotnet run --project Aetheris.CLI -- build --help`
- `dotnet run --project Aetheris.CLI -- analyze --help`

Observed: CLI supports build/analyze/canon/asm workflows; no direct “boolean family debug route” command is exposed in CLI surface for bypassing Firmament feature-graph validation.

### Boolean-family boundary checks in code

Key boundaries confirmed in code before fixture authoring:

- Mixed through-void builder hard-requires box root, no open-slot history, exactly one analytic history entry, through-span only, cylinder/cone only, world-Z axis, containment-only interaction class, strict non-grazing margin.
- Prismatic subtract routing requires recognized box root and strict footprint containment; prior history must stay in bounded analytic/open-slot families.
- Rebuild route dispatches to mixed builder only when prismatic tool is present and `leftComposition.Holes.Count == 1`.
- Firmament safe-subtract feature-graph forbids non-cylinder/cone follow-on tools once safe subtract chain starts (this is the dominant blocker for mixed probes from `.firmament`).

## Chimera fixtures created

All fixtures are under `testdata/firmament/frictionlab/fric-b0/`.

### Chimera A — box-root subtract stress block

Intent: stress through vs blind, family kind spread, and mixed continuation attempts from box-root baseline.

Zones:

- `zone_a_through_cyl` (baseline through cylinder)
- `zone_b_blind_cyl` (corrected in FRIC-B0.1 to explicit exterior-face blind-hole placement; prior variant was contained cavity)
- `zone_c_cone_through`
- `zone_d_cone_blind`
- `zone_e_slot_through`
- `zone_f_tri_prism`
- `zone_g_box_prism`
- `zone_h_partial_overlap_slot` (analytic then prismatic follow-on attempt)
- `zone_i_disjoint_dual_holes_then_prism`
- `zone_j_grazing_prism` (edge-grazing follow-on attempt)

### Chimera B — cylinder-root subtract stress block

Intent: stress non-box-root/prismatic interactions and cylinder-root analytic constraints.

Zones:

- `zone_ba_through_cyl` (baseline)
- `zone_bb_blind_cyl`
- `zone_bc_prismatic_slot`
- `zone_bd_box_prism`

### Chimera C — history-composition stress block

Intent: stress sequential history limits, open-slot/prismatic follow-on behavior, non-safe-root follow-on, and mixed-count behavior.

Zones:

- `zone_ca_analytic_seed_hole`
- `zone_cb_second_analytic_hole`
- `zone_cc_open_slot_seed` + `zone_cc_follow_on_prism`
- `zone_cd_seed_sphere` + `zone_cd_follow_on_prism`
- `zone_ce_hole_1` + `zone_ce_hole_2` + `zone_ce_follow_on_prism`
- `zone_cf_mixed_prismatic_attempt`

## Commands run and what they proved

1. `dotnet run --project Aetheris.CLI -- --help`
   - Proved CLI command surface and baseline availability.
2. `dotnet run --project Aetheris.CLI -- build --help`
   - Confirmed machine-readable diagnostics path (`--json`) for pressure test matrix capture.
3. `for f in testdata/firmament/frictionlab/fric-b0/*.firmament; do dotnet run --project Aetheris.CLI -- build "$f" --json > artifacts/fric-b0/<name>.json; done`
   - Produced per-zone success/failure evidence with exact diagnostics.
4. `python3` aggregation over `artifacts/fric-b0/*.json`
   - Clustered diagnostics into blocker classes used below.
5. `dotnet run --project Aetheris.CLI -- build testdata/firmament/frictionlab/fric-b0/chimera_a_zone_b_blind_cyl.firmament --json` (pre-fix capture)
   - Confirmed old fixture behavior in INV-B0 context: rejected with `[FIRM-SCHEMA-0006]` enclosed-void diagnostic.
6. Updated `chimera_a_zone_b_blind_cyl.firmament` to use explicit `place.on_face: chimera_a_root.top_face` blind-hole placement.
   - Converted semantics from contained internal cavity to exterior-opening blind hole.
7. `dotnet run --project Aetheris.CLI -- build testdata/firmament/frictionlab/fric-b0/chimera_a_zone_b_blind_cyl.firmament --out artifacts/fric-b0.1/zone_b_blind_cyl_new.step --json`
   - Corrected case builds successfully under process `default`.
8. `dotnet run --project Aetheris.CLI -- analyze artifacts/fric-b0.1/zone_b_blind_cyl_new.step --json`
   - Confirms single-shell manifold body (no enclosed void shell), consistent with an exterior-opening blind hole.
9. `dotnet run --project Aetheris.CLI --no-build -- build testdata/firmament/frictionlab/fric-b0/chimera_c_zone_cf_mixed_prismatic_baseline_blocked.firmament --json`
   - Now passes after FRIC-B0.2 bounded continuation generalization.
10. `dotnet run --project Aetheris.CLI --no-build -- build testdata/firmament/frictionlab/fric-b0/chimera_a_zone_h_partial_overlap.firmament --json`
    - Now reaches kernel interaction classifier and rejects with tangent/edge-grazing containment diagnostic (instead of upstream tool-kind gate).
11. `dotnet run --project Aetheris.CLI --no-build -- build testdata/firmament/frictionlab/fric-b0/chimera_c_zone_cc_open_slot_history.firmament --json`
    - Remains rejected by explicit “prior prismatic/open-slot through-void history unsupported” boundary.

## Per-zone result matrix

| Chimera | Zone | Operation attempted | Result | Exact diagnostic (source) | Blocker class | Recommendation |
|---|---|---|---|---|---|---|
| A | `zone_a_through_cyl` | `subtract(box, cylinder through)` | Pass | n/a | Supported baseline | Safe to generalize now (already in family) |
| A | `zone_b_blind_cyl` | `subtract(box, cylinder blind from exterior face)` | Pass | n/a | Supported one-sided blind-hole family (after fixture correction) | Keep accepted; this row is now valid evidence for exterior-opening blind hole behavior |
| A | `zone_c_cone_through` | `subtract(box, cone through)` | Pass | n/a | Supported baseline | Safe to generalize now (already in family) |
| A | `zone_d_cone_blind` | `subtract(box, cone blind)` | Reject | `... does not match the supported subtract span family ...` (`BrepBoolean.AnalyticHole.NotFullySpanning`) | Non-through analytic continuation boundary | Generalizable later with new bounded representation |
| A | `zone_e_slot_through` | `subtract(box, slot_cut through)` | Pass | n/a | Supported prismatic through-cut family | Safe to generalize now (already in family) |
| A | `zone_f_tri_prism` | `subtract(box, triangular_prism through)` | Pass | n/a | Supported prismatic family | Safe to generalize now (already in family) |
| A | `zone_g_box_prism` | `subtract(box, box through)` | Pass | n/a | Supported prismatic family | Safe to generalize now (already in family) |
| A | `zone_h_partial_overlap_slot` | `subtract(box→analytic history, box prism)` | Reject | `... rejects tangent/edge-grazing analytic-prismatic interactions ...` (`BrepBooleanBoxMixedThroughVoidBuilder.Build`) | Kernel interaction-class rejection (now reached after gate relaxation) | Correctly remains rejected (partial-overlap/tangent class stays bounded out) |
| A | `zone_i_hole_right` | second analytic hole in chain | Reject | `blind-hole continuation exceeds the bounded family...` (`BrepBoolean.AnalyticHole.BlindContinuationOutsideBoundedFamily`) | Analytic continuation + span classification fragility | Generalizable later with stronger bounded history/span model |
| A | `zone_j_grazing_prism` | analytic then edge-grazing prism | Reject | `unsupported follow-on tool kind 'box' ...` (`firmament.feature-graph.unsupported-follow-on-kind`) | Upstream gate prevents interaction classification | Generalizable later with new bounded representation (after gate relaxation + interaction classifier exposure) |
| B | `zone_ba_through_cyl` | `subtract(cylinder, through cylinder)` | Pass | n/a | Supported cylinder-root bore family | Safe to generalize now (already in family) |
| B | `zone_bb_blind_cyl` | `subtract(cylinder, blind cylinder)` | Reject | `... only through center bores that span both planar caps are supported.` (`BrepBoolean.AnalyticHole.NotFullySpanning`) | Cylinder-root non-through rejection | **Must remain rejected** for current architecture |
| B | `zone_bc_prismatic_slot` | `subtract(cylinder, straight_slot)` | Reject | `sequential safe composition only supports subtracting supported analytic holes...` (`BrepBoolean.RebuildResult`) | Non-box root prismatic continuation boundary | Generalizable later with new bounded representation |
| B | `zone_bd_box_prism` | `subtract(cylinder, box)` | Pass | n/a | Routed as bounded orthogonal pocket candidate | Safe now within narrow admissible overlap cases |
| C | `zone_cb_second_analytic_hole` | second analytic follow-on | Reject | `blind-hole continuation exceeds the bounded family...` (`BrepBoolean.AnalyticHole.BlindContinuationOutsideBoundedFamily`) | History-span continuation fragility | Generalizable later with new bounded representation |
| C | `zone_cc_follow_on_prism` | open-slot/prismatic through-void history then prism | Reject | `... prismatic subtract continuation does not yet support prior prismatic/open-slot through-void history.` (`BrepBoolean.RebuildResult`) | Open-slot/prismatic-through-void history boundary | **Must remain rejected** for this milestone (no continuation builder yet) |
| C | `zone_cd_follow_on_prism` | sphere-history then prism | Reject | `bounded boolean family only supports recognized axis-aligned boxes ...` (`BrepBoolean.RebuildResult`) | Non-safe-root follow-on boundary | **Must remain rejected** (for current bounded family) |
| C | `zone_ce_hole_2` | multi-analytic history continuation | Reject | `blind-hole continuation exceeds the bounded family...` (`BrepBoolean.AnalyticHole.BlindContinuationOutsideBoundedFamily`) | Mixed-count/history continuation limit | Generalizable later with new bounded representation |
| C | `zone_cf_mixed_prismatic_attempt` | analytic then prismatic mixed baseline | Pass | n/a | Bounded mixed continuation (containment class) now routed and rebuilt | Improved by FRIC-B0.2 generalized continuation rule |

## Failure clusters

1. **Feature-graph follow-on-kind hard gate**
   - Signature: `firmament.feature-graph.unsupported-follow-on-kind`.
   - Effect: blocks mixed analytic+prismatic probes before kernel-level interaction classification.

2. **Analytic continuation/span constraints**
   - Signature: `BrepBoolean.AnalyticHole.NotFullySpanning`, `BlindContinuationOutsideBoundedFamily`.
   - Effect: rejects blind or ambiguously classified continuation states.

3. **Root family representational boundaries**
   - Signature: `BrepBoolean.RebuildResult` messages about recognized box roots / sequential safe composition family.
   - Effect: non-box-root and non-safe-root follow-on requests remain bounded out.

4. **Process/schema sealed-void constraint**
   - Signature: `FIRM-SCHEMA-0006` fully enclosed internal voids disallowed in process `default`.
   - Effect: catches certain blind-hole outcomes before/alongside geometric family checks.

INV-B0 follow-up (2026-04-19): the previous `zone_b_blind_cyl` fixture revision was contained (no exterior-face placement), so that historical rejection was enclosed-cavity policy behavior rather than blind-hole misclassification. FRIC-B0.1 replaces that fixture with explicit top-face placement and the zone now passes as a true exterior-opening blind hole.

## Required boundary classification synthesis

### Safe to generalize now

- **Firmament feature-graph hard gate for prismatic follow-on after safe analytic history** appears overly strict relative to downstream bounded builders. Relaxing this gate (to allow bounded candidate routing, not unconditional acceptance) would immediately increase diagnostic depth and unlock existing mixed-through-void code paths.
- **Prismatic family with recognized box roots** is already broad enough to serve as continuation candidate input when the history is bounded and classifiable.

### Generalizable later with new bounded representation

- **Non-through analytic continuation** (blind chains, cone/cylinder continuation edge cases).
- **Open-slot history + additional prismatic operations** (multi-mouth / through-slot constraints).
- **Mixed-count > 1 analytic histories with incoming prism**, where preserving/combining multiple void descriptors is needed.
- **Non-contained and analytic-prismatic tangent interaction classes** in mixed mode (once feature-graph gate permits reaching these classifiers consistently).
- **Non-box-root prismatic continuation** for cylinder roots (would need explicit bounded representation/rebuilder family).

### Must remain rejected

- **Degenerate tangent/edge-grazing interactions** should remain rejected as principled manifold-safety boundaries.
- **Non-safe-root follow-on after non-bounded families** (e.g., sphere-cavity branch) should remain rejected in the current architecture.
- **Sealed internal voids under process `default`** should remain rejected unless process policy is intentionally broadened.

## Tiny fixes made during pressure test

None. No architecture-broadening patches were applied.

## Top 1–3 high-value next moves

1. **Relax feature-graph follow-on-kind gate from “tool-kind hard reject” to “bounded candidate pass-through” for prismatic tools** so mixed-through-void builder and interaction classifiers can execute and emit precise kernel diagnostics.
2. **Add explicit diagnostic staging labels** (recognition vs admissibility vs reconstruction vs topology degeneracy) to current `BrepBoolean.RebuildResult` fallback strings to reduce ambiguity in pressure-test analytics.
3. **Introduce a minimal bounded multi-void history descriptor** (for `holes.Count > 1` mixed continuation) without attempting full general booleans.

## What not to pursue yet

- Do **not** attempt broad general CSG or arbitrary multi-family booleans.
- Do **not** remove tangent/grazing rejection boundaries.
- Do **not** expand non-box-root mixed prismatic support before a bounded representation and explicit reconstruction lane exist.
