# BUILD-PERF-M4 — Kernel hotspots

Ladder: [build-perf-ladder.md](../../milestones/general/build-perf-ladder.md) ·
previous: [BUILD-PERF-M2](BUILD-PERF-M2.md).

**State: success.** The exit target is met: guitar warm build 3.9 s (≤ 5 s; 13.9 s at M0). The
first pass (changes 1–4) is bit-exact. The second pass (change 5) changes how SectionChain trims are
*produced*: they are constructed with their patches instead of recovered and densely re-checked. That
changes STEP and display-mesh hashes for the two projects with SectionChain bodies (guitar, ATLAS),
justified below. The guitar `CarvedMaple` edit fell 5.7 s → 0.64 s natively.

## Changes

1. **SectionChain self-intersection validation.** The validator tested every triangle pair of its
   24×6 proxy (O(n²)). Candidate pairs (non-neighbours whose tolerance-expanded bounds intersect) now
   come from a sweep over X. They are visited in the same (left, right) order, so the verdict, the
   reported pair and the candidate/qualified counts in the evidence are identical. CarvedMaple:
   2.66 s → ~0.08 s.
2. **SectionChain materialization.**
   - `SmoothSectionChainBuilder.MaximumOvershoot` re-scanned the chain's bounding box six times for
     each of its 81 samples per patch; the box is now computed once.
   - The wireframe preview SVG (read only by the CLI) is rendered on first read, from a body snapshot
     taken at materialization.
   - Guitar `sectionchain.materialize`: 5.0 s → 0.55 s.
3. **B-spline surface evaluation.** A rectangular net finds its V knot span once per evaluation
   instead of once per row. The last surface's flat arrays are kept per thread, skipping the
   table lookup in long runs (99.8% hit rate on the guitar). Arithmetic and its order are unchanged.
4. **Planar text profiles.** Outline conversion and glyph normalization for the maker mark are a pure
   function of (content, height, alignment, feature id); the last 16 results are reused. Bolt edit
   3.0 s → 1.5 s.

5. **Trims by construction** (second pass). Every SectionChain transition face is bounded by four
   isoparametric curves of its own patch: source profile (v = start), target profile (v = end), and
   the two longitudinal boundaries (u = start, u = end). In the smooth lane those are literally the
   patch's control rows; in the ruled lane they are its boundary curves and straight rulings. Each edge
   is bound with a parameter affine to the patch's, so its trim is the matching straight side of the
   parameter rectangle. `SectionChains.BuildBody` now binds these trims at construction, with the new
   origin `PcurveBindingOrigin.ConstructedBoundary`, a 33-sample guard (≤ 1e-7 mm, else the trim is
   left to recovery) and the existing 129-sample retained-trim check in `Populate`. Before, every
   trim was recovered by inverse projection (grid seed + Newton at 129–4097 points) and qualified at
   8193 lifted samples, about 10.5 million spline evaluations per guitar cold build. Measured on the
   guitar and ATLAS bodies: every transition trim is constructed (CarvedMaple 288, Neck 216,
   MahoganyBack 72, IvoryBinding 72, fore/upper housing 128 each), with lift deviations of 0 to
   2.3e-13 mm. Approximated (non-exact) ruled patches and analytic surfaces keep the recovery path. The
   exporter writes the constructed trims as `PCURVE`s, and reimport keeps them as source trims.

   **Why the hashes changed, and why that is correct.** STEP now carries exact isoparametric lines
   instead of recovered polylines (fewer `LINE`/`VECTOR`/`DIRECTION` entities, same `PCURVE` count).
   The display tessellator trims with them. `compare-step` between the previous and new exports:
   guitar worst relative difference 3.2e-12, ATLAS 4.9e-16 (tolerance 1e-6): equivalent products.
   The old qualification density is now a test oracle (`ConstructedSectionChainTrimTests`: 8193
   lifted samples per constructed trim, ≤ 1e-7 mm, smooth and ruled lanes; constructed trims round-trip
   through STEP as source trims).

## Results (native, median of 3, 2-core container)

| Project | Scenario | M2 | M4 |
|---|---|---|---|
| guitar | `CarvedMaple` comment edit | 5.70 s (M1) | 1.14 s (pass 1) → **0.64 s** |
| guitar | warm build | 7.64 s | 5.27 s (pass 1) → **3.88 s** |
| guitar | cold build | 16.4 s | 10.9 s (pass 1) → **9.8 s** |
| guitar | knob value edit | ~0.40 s | 0.35 s |
| atlas | cold / warm | 7.3 / 2.7 s | **4.5 / 2.2 s** |
| atlas | `fore-housing` edit (verify-incremental) | ~1.3 s | **0.13–0.23 s** |
| bolt | root comment edit | 3.06 s | **1.57 s** |
| bolt | cold | 6.9 s | 5.6 s |

New ratchet baseline written after each pass (`Aetheris.Web.Perf/build-perf-budgets.json`; the
second one deliberately takes the new guitar/ATLAS hashes). Full lane 4585/4585. `verify-incremental`
(guitar, all ATLAS documents) and `verify-bridge` pass. AOT browser numbers to be re-measured on the
author's machine.

## Third pass: the two checks named at the end of pass 2

6. **Reimport orientation: trim containment index (bit-exact).** Reimport resolves face orientation
   with a signed volume from a display tessellation, and that tessellation spent most of its time
   classifying candidate triangles against the trim loop. Each point-in-polygon test scanned every
   segment of a densely sampled loop. A per-loop index now buckets segments by V range, widened by
   the on-segment tolerance margin. A point visits only the segments that can flip its crossing
   parity or report it on-segment, using the original expressions, so answers are identical
   (`TrimContainmentIndexTests` compares both on vertices, midpoints, near-edge offsets inside and
   outside the tolerance, and 4,000 random points, including a zero-length segment). This helps every
   trimmed-face tessellation, display included. Reimporting CarvedMaple's tessellation halves.
   The orientation step itself is kept. The importer's rule, that source `same_sense` is evidence and
   never authority, still holds even for our own files.
7. **Bolt enclosure checks (verdict-only evaluation).** `HexBoltThreadComposition` called
   `BrepMassProperties.Evaluate` twice, which integrates the whole threaded body at two resolutions,
   and used only `IsEnclosed` and `IsOrientationConsistent`. Both are topological, apart from a
   "volume is not degenerate" guard. The new `BrepMassProperties.EvaluateEnclosure` computes exactly
   the verdict: topology, the exact recognizers, and one coarse boundary integral for the
   non-degeneracy guard. It can disagree with `Evaluate` only for a body whose coarse and refined
   volumes straddle (linear tolerance)³; such a body is degenerate either way. Bolt root edit
   1.57 → 1.05 s; output hashes unchanged.

Native after pass 3 (median of 3): guitar warm 3.79 s, `CarvedMaple` edit 0.51 s, bolt root edit
1.05 s, bolt cold 4.85 s. Full lane 4589/4589; showcase hashes unchanged by pass 3; baseline rewritten.

## What remains, with evidence

- **Reimport orientation, authority question.** Skipping the orientation solve for our own
  canonical STEP would mean trusting the source's face senses. The importer deliberately does not
  (doctrine: source `same_sense` is cross-check evidence only). Pass 3 made the solve cheaper instead.
  Removing it is a doctrine decision, not an optimization.
- **Other recovered trims**: analytic surfaces (cylinders, cones, tori via `BuildPeriodicAnalytic`)
  and approximated ruled patches still go through recovery. They were not hot in the showcase.
- **Bolt**: the rest of a rebuild is the thread/head composition and display tessellation; a single
  part is still one definition, so any edit rebuilds it.
- **Datum-seat validation** on cold builds (guitar 1.2 s): tessellates every placed body once.
