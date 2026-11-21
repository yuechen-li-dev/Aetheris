# Test lane speed

## Where the time goes

This was measured on the 2-core session container, with per-test timings from TRX logs and the Firmament testhost
sampled for 3 minutes.

- **The run is CPU-bound.** Tests total about 31 CPU-minutes. With 2 cores, the lane takes about 10.5 minutes of
  wall time, plus 2.3 minutes of build.
- **The slow tail dominates.** 62 tests over 5 s account for 45% of all test time. The other ~6,800 tests are short.
- **The Firmament test assembly is the critical path.** It holds 37% of the total. It starts after the CLI tests and
  then runs alone for about 6 minutes.
- **Common costs inside Firmament tests.** Time is spread across STEP import (the orientation resolver's mesh
  included), display tessellation, drawing generation and assembly compilation. No single hotspot dominates.
- **Sculpting was the biggest single class.** `SculptingCliTests` took 260 s in one serially run class. Each CLI call
  recompiles the sculpt chain, and every Catmull-Clark refine, limit and evaluation re-hashed every edge id with
  SHA-256. A `ConcurrentDictionary.Count` call on each cache miss, which takes every lock, added a lock storm.

## Changed

- **Sculpt edge ids are remembered.** The ids are pure functions of their vertex pairs. A cage's edges are cached by
  its face list, which `with` copies share. Hashes are computed only for the 10 digest bytes an id uses. The output
  is byte-identical: the compact-mouse `inspect` JSON differs only in its timing field. That `inspect` went from
  8.0 s to 4.7 s.
- **The orientation sign mesh scales with the part.** Its chord is a fortieth of the smallest extent, at most
  0.5 mm. When that chord is no coarser than the display mesh's, the coarse pass is skipped, so small test parts no
  longer mesh twice. NIST fingerprints are unchanged.

## Measured

| | Before | After |
|---|---|---|
| Test CPU, all assemblies | 1874 s | 1725 s |
| CLI tests | 476 s | 350 s |
| `SculptingCliTests` | 260 s | 153 s |
| Lane wall time (tests only) | 635 s | 538 s |

The full lane passes.

## Not changed (options)

- **A fast inner-loop lane.** Putting the 62 tests over 5 s in a `Slow` category would let iterations run
  `--filter Category!=Slow` and keep the full lane for the commit gate. That roughly halves test time. It changes the
  AGENTS.md workflow, so it is the author's call.
- **Splitting `SculptingCliTests`.** It is still one 150 s serial class. Splitting it lets xUnit run its parts on
  separate cores.
- **The pcurve recovery seed grid.** `BuildSeedSamples` (one grid per B-spline surface) and the dense lift proofs are
  the largest remaining import costs. Any change there alters qualification evidence.
