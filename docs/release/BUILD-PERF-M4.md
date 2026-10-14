# BUILD-PERF-M4 — Kernel hotspots

Ladder: [build-perf-ladder.md](../development/milestones/general/build-perf-ladder.md) ·
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

## What remains, with evidence

- **Reimport orientation check.** Reimporting our own canonical STEP still resolves face
  orientation by tessellating the body (580 of 667 ms for CarvedMaple). Like the trims, the exporter
  already knows the orientation. The next authoring-time candidate.
- **Other recovered trims**: analytic surfaces (cylinders, cones, tori via `BuildPeriodicAnalytic`)
  and approximated ruled patches still go through recovery. They were not hot in the showcase.
- **Bolt**: two `BrepMassProperties.Evaluate` enclosure checks (thread composition and maker-mark
  imprint, ~2.3 s together, tessellation-based) are now the bulk of every bolt rebuild.
- **Datum-seat validation** on cold builds (guitar 1.2 s): tessellates every placed body once.
