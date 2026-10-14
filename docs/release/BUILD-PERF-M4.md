# BUILD-PERF-M4 — Kernel hotspots (first pass)

Ladder: [build-perf-ladder.md](../development/milestones/general/build-perf-ladder.md) ·
previous: [BUILD-PERF-M2](BUILD-PERF-M2.md).

**State: meaningful progression.** Every change is bit-exact: STEP and display-mesh hashes are
identical for every showcase project. The guitar `CarvedMaple` edit, the slow edit left by M2, fell
5.7 s → 1.14 s natively. The guitar warm build is 5.3 s (median of 3, runs 4.6–5.7 s) against the
≤ 5 s working target. The rest of the edit time is the bounded-pcurve qualification sampling. Making
it cheaper is a policy decision, not an optimization (see the end).

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

## Results (native, median of 3, 2-core container)

| Project | Scenario | M2 | M4 |
|---|---|---|---|
| guitar | `CarvedMaple` comment edit | 5.70 s (M1) | **1.14 s** |
| guitar | warm build | 7.64 s | **5.27 s** |
| guitar | cold build | 16.4 s | **10.9 s** |
| guitar | knob value edit | ~0.40 s | 0.34 s |
| atlas | cold / warm | 7.3 / 2.7 s | **4.6 / 2.0 s** |
| bolt | root comment edit | 3.06 s | **1.57 s** |
| bolt | cold | 6.9 s | 5.6 s |

New ratchet baseline written (`Aetheris.Web.Perf/build-perf-budgets.json`). Full lane 4581/4581.
AOT browser numbers to be re-measured on the author's machine.

## What remains, with evidence

- **Bounded pcurve qualification** (`pcurve.bounded-build`, 856 of the 1 144 ms CarvedMaple edit;
  3.0 s of the guitar cold build). 80% of it is `MaximumResidual`: every recovered spline pcurve is
  lifted and compared at **8193** samples, plus up to 4097 inverse samples while refining. One guitar
  cold build performs about 10.5 million B-spline surface evaluations. The arithmetic is now as lean
  as it can be without changing results. Fewer lift samples, or an adaptive (error-bounded) residual,
  would change the qualification evidence each binding records, so it is a policy decision.
- **Bolt**: two `BrepMassProperties.Evaluate` enclosure checks (thread composition and maker-mark
  imprint, ~2.3 s together, tessellation-based) are now the bulk of every bolt rebuild.
- **Datum-seat validation** on cold builds (guitar 1.2 s): tessellates every placed body once.
