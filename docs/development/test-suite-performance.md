# Test-suite performance profile — 2026-09-24

Measured on the Cowork Linux VM (`nproc` = 2), Release, `dotnet test` with a TRX logger.
Wall-clock numbers are therefore parallelism-bound on this box and will differ on the
dev machine. **CPU time is the machine-independent number and is what the analysis below
uses.**

| suite | tests | wall | sum of test durations |
|---|---|---|---|
| Aetheris.Kernel.Core.Tests | 1092 | 58-61s | 117s |
| Aetheris.Kernel.Firmament.Tests | 1624 | 37-43s | 76s |
| Aetheris.Server.Tests | 59 | 9s | - |

## 1. Kernel.Core is a STEP-import benchmark wearing a unit-test costume

By namespace: **Step242 84.1% (98.2s), Brep 15.4% (18.0s), everything else 0.5%.**
Roughly 200 tests account for all 117s; the other ~890 cost under 20ms combined.

Top classes:

| CPU | tests | class |
|---|---|---|
| 17.8s | 25 | Step242LoopRoleNormalizationRegressionTests |
| 11.4s | 18 | NistDisplayCorpusRegressionTests |
| 9.4s | 21 | Step242NistAuditHarnessTests |
| 8.8s | 9 | Step242PeriodicBoundaryOrientationTests |
| 8.7s | 21 | Step242RationalSurfaceReductionTests |
| 8.6s | 18 | Step242ConicalSurfaceRegressionTests |
| 8.5s | 11 | Step242BSplineSurfaceWithKnotsTests |

The cause is structural, not algorithmic: there are **203 `Step242Importer.ImportBody`
call sites** and **52 `File.ReadAllText` sites** across the test project, against **48
distinct corpus files**. Every test re-reads and re-imports from scratch.

Measured cost of importing *every* corpus file exactly once (117 files, 40MB):

```
read = 550ms    parse = 957ms    import (incl. parse) = 10.3s
```

So the suite spends ~98s doing work whose irreducible content is ~10s. **About 90% of
Kernel.Core's CPU is redundant re-import.** Parse caching alone is worth only ~9% - the
cost is in topology construction and geometry binding, not tokenizing.

### Recommendation

A memoized corpus fixture: `Step242Corpus.Text(relativePath)` and
`Step242Corpus.Body(relativePath)`, both `ConcurrentDictionary<string, Lazy<...>>` so they
are safe under xunit's parallel collections. `BrepBody` and everything it exposes is
get-only, so sharing one instance across tests is safe for read-only consumers.

Two categories cannot share a body and should still share the *text*:

- tests using `Step242Importer.CaptureLoopRoleXDiagnostics(...)` scopes, which collect
  during import and therefore need a real import (this is most of the 17.8s in
  `Step242LoopRoleNormalizationRegressionTests`);
- tests using `Step242Importer.PreserveRationalSurfaces()`, which changes import behaviour.

For the capture-based group the deeper win is a single import that collects *all*
diagnostic families at once, shared by the tests that each want one family. That is a
larger refactor and should be costed separately.

## 2. A one-face torus costs a quarter of a second to import

Not a test problem - this is shipping importer behaviour, and it shows up in the corpus
timings as tiny files with absurd costs:

```
m10g2-torus.step                1KB    499ms
torus_basic.step                1KB    262ms
toroid.step                     3KB    243ms
nist_ctc_01_asme1_ap242-e1.stp  387KB  551ms
```

`m10g2-torus.step` contains one `TOROIDAL_SURFACE`, one `ADVANCED_FACE`, two `CIRCLE`s and
two `EDGE_CURVE`s. Instrumenting `ProjectLoopToTorus` shows its loops arrive with
**513 samples each**.

That comes from `ComputeAdaptiveCircleSegmentCount`, whose `maxSegmentAngle` is
`pi / 64` - 2.8 degrees, so 128 segments per full circle regardless of radius or of how
coarse a *classification* polygon actually needs to be. The method's own comment says the
polygon is for classification only and that the constant was raised from a two-chord
quarter arc because that "falsely rejected close polynomial inner trims" - a fix that
overshot. The 513-point loops then feed helpers that are pairwise in the sample count
(`CountUniquePoints`, `SimplifyClosedPolygon`, per-point containment against the loop),
so the cost is quadratic in a constant nobody intended to be large.

I did **not** change it. Lowering it risks re-introducing exactly the false rejection it
was raised to fix, so it needs its own pass with the NIST corpus as the oracle. Candidate
approaches: make the count adaptive on sagitta relative to part size rather than on a
fixed angle, cap total samples per loop, or make the pairwise helpers use a spatial hash.

Ruled out by measurement: `TryRecoverToroidalProjectionFromCyclicUnwrap` - which *is*
O(n^2) by construction, re-projecting the whole loop once per rotation with no early exit -
is never called for these files (`unwrapCalls = 0`). It remains a latent hazard for any
file that does hit the degenerate-projection path.

## 3. Firmament has no structural hotspot

76s over 1624 tests with a flat distribution: the largest class is 12.3% and the slowest
single test is 5.9s. This is real geometry work - sweeps, coil meshing, gear lowering -
not repeated I/O. Caching buys little here. The realistic lever is the same corpus-sharing
idea applied to the `.firmament` fixtures, which is a much smaller prize.

## 4. Parallelism is already saturated *on this box*

117s CPU / 58s wall = 1.9x on 2 cores. There is no headroom here, but there may be plenty
on the dev machine: xunit parallelizes across collections, and a suite whose time is
concentrated in a handful of large classes parallelizes poorly no matter how many cores
are available. Splitting the big Step242 classes into more collections is worth trying on
real hardware before anything else, because it is free.

`[CollectionDefinition(..., DisableParallelization = true)]` on `StorageGateMeshing` in
the Firmament tests serializes that collection; worth confirming it still needs to.

## 5. The fast lane already exists and is under-used

`[Trait("Category", "SlowCorpus")]` is applied 29 times in Kernel.Core and 7 times in
Firmament.

```
dotnet test Aetheris.Kernel.Core.Tests --filter "Category!=SlowCorpus"
  -> 981 tests, 28s   (vs 1092 tests, 58s)
```

111 tests cost 33s of the 61. Extending the trait to the rest of the corpus-driven classes
would bring the inner-loop suite under ~15s without touching coverage, provided the full
lane still runs before commit. This is the cheapest immediate win and needs no code change
beyond attributes.

## Results

Applied: the memoized `Step242Corpus` fixture, conversion of the read-only import call sites to
it, eleven more `SlowCorpus` traits, and the two-lane workflow written into CONTRIBUTING.md and
AGENTS.md.

| | before | after |
|---|---|---|
| Kernel.Core, full lane (wall) | 61s | 31s |
| Kernel.Core, full lane (CPU) | 117s | 63s |
| Kernel.Core, fast lane (wall) | 28s | 10s |
| Kernel.Core, fast lane (tests) | 981 | 959 |
| Firmament, full lane (wall) | 37-43s | 33s |

Step242 fell from 98.2s to 51.4s of CPU. All suites green: Kernel.Core 1092/1092,
Firmament 1624/1624, Server 59/59.

Two things to know when reading per-class numbers after this change. Cost now migrates to
whichever test touches a corpus file first, so individual classes move up or down between runs
without anything having changed - only the total is meaningful. And `RunConfiguration.MaxCpuCount=1`
in the canonical command controls parallelism *across* test assemblies, not xunit's parallelism
within one, so a single-project run is the same speed with or without it.

The largest remaining item is `Step242LoopRoleNormalizationRegressionTests` at 15s. It cannot
share a body: each of its tests installs a `CaptureLoopRole*Diagnostics` scope that collects while
the import runs, so each needs a real import, and several of them import the same file to collect
different diagnostic families. Unifying capture so one import serves them all is the next step and
is a real refactor rather than a mechanical one.

## Suggested order## Suggested order

1. ~~Extend `SlowCorpus` and document the two-lane workflow.~~ Done.
2. ~~Memoized corpus fixture (text + body).~~ Done - Step242 CPU 98.2s to 51.4s.
3. Split the large Step242 classes into separate collections; measure on real hardware.
4. The torus classification-sampling constant, as its own investigation.
5. Unify diagnostic capture so one import serves several capture-based tests. Now the largest
   single remaining item at 15s.
