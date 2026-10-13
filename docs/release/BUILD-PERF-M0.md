# BUILD-PERF-M0 — Baseline and ratchet

Ladder: [build-perf-ladder.md](../development/milestones/general/build-perf-ladder.md).

**State: meaningful progression.** Native baseline, browser dev-runtime baseline and the ratchet
are in place and reproducible. Two exit items are open: the AOT browser number (needs a machine
with more than ~8 GB RAM to link) and naming the part-compile interior for the bolt.

## What changed

- **Instrumentation.** `BuildPerfTrace` phases at the boundaries the profiler identified:
  assembly parse, route bindings, per-definition materialization, definition-cache key / hit
  re-import / store re-import, M0 compile, execute, residual, interference and datum-seat
  validation; SectionChain authoring compile, materialize, build-body and self-intersection
  validation; bounded pcurve build; Firmament V2 parse; `Step242Importer.ImportBody`; web
  assembly compile, STEP export and display-mesh export; web scene compile and display
  projection. Phases are no-ops unless a request sets `performance`.
- **`BuildPerfTrace` GC counts are now per build** (deltas since `Start`), not process totals.
- **`Aetheris.Web.Perf showcase`** (native lane). Each showcase project runs in a fresh process
  through `Aetheris.Web.Runtime.Program.Invoke`: cold, warm (new session), no-change rebuild,
  leaf value edit (guitar knob radius), leaf comment edit, root comment edit. Records invoke and
  rebuild time, response bytes, allocated bytes, reused definitions, per-phase exclusive time
  and GC counts, plus STEP DATA and display-mesh hashes. `--runs N` reports medians and spread.
- **`Aetheris.Web.Perf/build-perf-budgets.json`** and `--check`. Always gated:
  STEP DATA hash and mesh hash (exact), response bytes (+1%), allocated bytes (+10%), reused
  definitions (no fewer). Wall time is gated only with `--check-time` (default ×1.5) because it
  is meaningful only on the recording machine. Verified: a fresh run passes; a run under heavy
  CPU contention (time +25–45%) still passes because allocations stay within 10%; a tampered
  budget fails with exit code 1.
- **`HeliosCAD/scripts/measure-build-perf.mjs`** (browser lane). The same projects through the
  public SDK in a Worker, on the interpreted dev runtime (`dev`) and/or the AOT runtime (`aot`),
  appending to `artifacts/local/build-perf/browser.jsonl`; `--dump-step` writes the STEP.

```powershell
dotnet run --project Aetheris.Web.Perf -c Release -- showcase --runs 3 --check Aetheris.Web.Perf/build-perf-budgets.json
node ../HeliosCAD/scripts/measure-build-perf.mjs --variants dev,aot --projects bracket,bolt
```

## Native baseline

.NET 10.0.12 Release, 2-core Linux container, commit `2b3abbb`, median of 3 runs.

| Project | Cold | Warm | No change | Leaf edit | Root comment | Response | Warm alloc |
|---|---|---|---|---|---|---|---|
| bracket | 683 ms | 34 ms | 31 ms | — | 31 ms | 51 KB | 3 MB |
| bolt | 5.86 s | 3.86 s | 3.26 s | 3.31 s | 3.09 s | 13.3 MB | 1.76 GB |
| atlas | 10.05 s | 3.98 s | 677 ms | 1.23 s (2/24 reused) | 456 ms | 3.3 MB | 3.36 GB |
| guitar | 25.11 s | 14.98 s | 2.89 s | 5.77 s (4/56 reused) | 2.75 s | 17.2 MB | 9.48 GB |
| house | 7.01 s | 2.46 s | 827 ms | 1.76 s | 679 ms | 5.1 MB | 718 MB |

Spread across runs is under 10% for bolt, atlas and guitar. House varies 30–55% and bracket's
sub-50 ms numbers are noise-dominated; neither affects the ratchet, which gates allocations.

### Where the time goes (warm build, exclusive)

| Project | Largest phases |
|---|---|
| guitar 15.0 s | SectionChain self-intersection validate 4.56 s (×4) · bounded pcurve build 2.96 s (×4) · Firmament parse 2.21 s (**×208**) · datum-seat validation 1.35 s · STEP import 0.87 s (×118) · tessellation 0.71 s · display-mesh export 0.56 s |
| atlas 4.0 s | bounded pcurve build 1.54 s · SectionChain validate 0.62 s · Firmament parse 0.49 s (×88) · STEP import 0.34 s (×48) |
| house 2.5 s | STEP import 0.89 s (×98) · Firmament parse 0.62 s (×196) · scene compile 0.35 s |
| bolt 3.9 s | part compile interior 1.99 s (**not yet named**) · tessellation 1.64 s |

### The no-change rebuild floor

- **guitar 2.9 s:** datum-seat validation 1.24 s runs on every build even when nothing changed;
  display-mesh export 0.50 s; all 56 cached bodies re-tessellated (0.31 s) and re-imported from
  STEP text (0.27 s); assembly STEP export 0.22 s.
- **bolt 3.3 s = a full build.** The part path has no definition cache: every rebuild recompiles.
- **Invalidation:** the knob value edit reuses 4 of 56 definitions. A comment appended to
  `CarvedMaple.firmament` reuses 55 of 56 but costs 7.6 s, because the one definition it owns is
  the SectionChain body.

## Browser baseline (interpreted dev runtime)

Headless Chromium in the same container, plus the author's Windows desktop for the guitar.

| Project | Cold | vs native | No change | vs native | GC gen0/1/2 (cold) |
|---|---|---|---|---|---|
| bracket | 1.26 s | 1.8× | 0.39 s | 13× | 1/0/0 |
| bolt | 313 s | 53× | 313 s | 96× | 658/22/22 |
| atlas | 333 s | 33× | 30.7 s | 45× | 807/16/16 |
| house | 81 s | 12× | 33.6 s | 41× | 199/4/4 |
| guitar (Windows desktop, `npm run dev`) | **870 s** | ~35× (different machines) | — | — | — |

`npm run dev` always serves the interpreted runtime; production serves AOT. The penalty tracks
allocation and GC more than raw interpretation (bolt, 1.8 GB per build, is the worst ratio),
which makes allocation a first-class target for M1–M4, not only wall time.

## Determinism

- **Within a runtime:** STEP DATA and display-mesh hashes are identical across all native runs,
  and the browser STEP is stable across browser runs.
- **Across runtimes it is not byte-identical.** Bolt: 4 of 50,110 STEP lines differ, all in the
  last printed digit (relative 3.9e-14). Atlas: the first difference is also a last digit, but
  definition 0 is written with 6 fewer STEP entities under WASM, which shifts all later entity
  ids; assembly structure, all 70 definitions, occurrences and the world bounding box agree
  (`aetheris analyze assembly`). This points to a different B-spline representation produced
  from floating-point drift. Equivalence within tolerance is **not yet established**.
- Consequence for the ladder: the byte-exact guard is per runtime (native hashes for the native
  ratchet). Cross-runtime equivalence needs a tolerance comparator — added to M1 as a gate.

## Open items

1. **AOT browser numbers.** Linking the AOT runtime OOMs at `wasm-opt -O2` (5.4 GB RSS) in an
   8 GB container even with `DisableParallelEmccCompile` and `DisableParallelAot`. Measure on a
   larger machine with `npm run sdk:install:production` and the browser lane's `--variants aot`.
2. **Name the part-compile interior** (bolt: 1.99 s of `web.compile-and-export` exclusive).
3. **Cross-runtime tolerance comparator** (see Determinism).
