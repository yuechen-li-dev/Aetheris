# BUILD-PERF — Firmament build performance ladder (M0–M5)

## Problem

Helios is an IDE for geometry. Its loop is edit → build → see. Today that loop is dominated by
the kernel pipeline, not by WebAssembly:

| Project (showcase) | Native cold (JIT) | Native warm | Response JSON | Browser, dev runtime (Windows desktop) |
|---|---|---|---|---|
| bracket | 0.7 s | 29 ms | 49 KB | 0.76 s |
| bolt | 5.6 s | 3.7 s | 13.5 MB | — |
| ATLAS | 10.0 s | 4.2 s | 3.4 MB | — |
| house | 7.3 s | 2.2 s | 5.1 MB | — |
| guitar | 25.8 s | 13.9 s | 17.5 MB | 870 s |

Guitar rebuilds in one native `WebModelSession`:

| Rebuild | Time | Definitions reused |
|---|---|---|
| no source change | 3.1 s | 56 / 56 |
| one knob radius edited (`knob.firmament`) | 5.7 s | 4 / 56 |
| comment appended to the root | 2.7 s | — |

Measured 2026-10-13 through `Aetheris.Web.Runtime.Program.Invoke` (the worker's entry point),
.NET 10 Release, commit `2b3abbb`. Native numbers are from a 2-core Linux container; the browser
number is from the author's Windows desktop running `npm run dev` (non-AOT dev runtime).

### Where guitar build time goes (sampled, native, share of the run)

- `SectionChainMaterializer.Materialize` 46%: `SectionChainSelfIntersectionValidator.Validate` 23%,
  `BoundedPcurveBuilder.Populate` 16%.
- `Step242Importer.ImportBody` 18%. `AssemblyDefinitionCache` stores each definition as STEP text;
  every cache hit re-parses STEP and re-runs pcurve recovery. This is the 3 s no-change floor.
- `FirmamentV2Parser.Parse` 13% (parsed repeatedly).
- `FirmamentBuildAndExport.ExportSource` 10%: STEP is produced on every build.
- Display tessellation 12% (torus faces 9%); `BSplineSurfaceWithKnots.Evaluate` 17% inclusive.

### Root causes outside the kernel algorithms

- Cache key (`AssemblyDefinitionCache.Key`) hashes the full `declarations` text including offsets,
  so an edit anywhere invalidates nearly every definition.
- The C#→JS boundary returns display meshes as JSON number arrays (17.5 MB for the guitar), which
  the interpreter-mode runtime serializes and the worker parses on every build.

## Rules for every milestone

- **Determinism guard.** STEP DATA and display-mesh output for every showcase project stays
  byte-identical to the M0 baseline *within a runtime*, or each difference is listed and justified
  in that milestone's report. Native and WASM output are not byte-identical today (floating-point
  drift; see BUILD-PERF-M0), so cross-runtime agreement is checked by tolerance, not bytes.
- **Allocation is a target, not only time.** The browser penalty tracks allocation and GC
  (bolt: 1.8 GB per build, 96× slower than native on the dev runtime). Exits state allocation too.
- **Real path only.** Gains are measured through `Program.Invoke` and in the browser, never in a
  side harness that skips work the editor does.
- **Ratchet.** `Aetheris.Web.Perf showcase --check Aetheris.Web.Perf/build-perf-budgets.json` gates
  hashes, response bytes, allocations and reuse; `--check-time` adds wall time on the recording
  machine. Re-record with `--write-baseline` when a milestone lands.
- Each milestone ends in Success, Meaningful progression or Honest stop (AGENTS.md convergence rule).

## Ladder

### M0 — Baseline and ratchet

- Extend `Aetheris.Web.Perf` from helix fixtures to the showcase projects (multi-file
  `projectDocuments`): cold, warm, no-change rebuild, single-leaf edit, comment edit; per-phase
  `BuildPerfTrace` output aggregated by phase name; response size.
- Instrument the uninstrumented assembly path (`AssemblyM1Pipeline.CompileParsed`,
  `AssemblyDefinitionCache.Materialize`, SectionChain materialize/validate, STEP export/import).
- Record browser timings for the dev runtime and the production AOT runtime on real hardware.
- Commit budgets (`artifacts/local` for raw output; a small checked-in budget file) and a check
  that fails on regression.

Exit: baselines reproducible within ±10% across three runs; every phase above 2% of guitar time
is named.

**Result** ([BUILD-PERF-M0](../../reports/2026-10/BUILD-PERF-M0.md)): meaningful progression. Native and
dev-runtime browser baselines, instrumentation and the ratchet are in place. Open: AOT browser
numbers (AOT link needs > 8 GB RAM), naming the bolt's part-compile interior.

### M1 — Rebuild floor

- Cache payload becomes the immutable materialized body (or Aetheris native binary form), cloned
  on hit. No STEP parse on a cache hit.
- STEP is produced only on `exportStep`, not on every build.
- Per-document parse cache keyed by content hash (guitar parses Firmament 208 times per build).
- Part path gets the same definition cache as assemblies (bolt no-change rebuild = full build).
- Validation that cannot change when nothing changed is not re-run (datum seats: 1.24 s of the
  guitar's 2.9 s no-change rebuild); cached bodies keep their display tessellation.
- Cross-runtime tolerance comparator for STEP output (native vs WASM).

Exit: guitar and bolt no-change rebuild < 100 ms native (from 2.9 s and 3.3 s); guitar no-change
allocation < 50 MB (from 2.9 GB). STEP bytes unchanged per runtime.

**Result** ([BUILD-PERF-M1](../../reports/2026-10/BUILD-PERF-M1.md)): meaningful progression. The metric
is now the *root comment edit* (source changed, geometry unchanged), stated as kernel time before
response serialization. Guitar 2.61 s → 214 ms (2.75 GB → 145 MB), ATLAS 432 → 74 ms; hashes
unchanged; rebuilds reproduce the cold build; native and WASM STEP proven equivalent. Bolt
unchanged and moved to M4 (a single part is one definition). Guitar's remaining ~200 ms project
parse and the Scene path move to M2.

**Order after M1: M3 before M2.** In the browser, ATLAS' root-comment edit is 5.7 s of which only
1.55 s is kernel; 4.1 s is the unchanged mesh's JSON round trip.


### M2 — Invalidation granularity

- Definition cache key = that definition's own dependency closure, not the whole declarations text.
- Cached semantic evidence stores definition-relative spans, rebased on reuse.
- Parse per definition instead of re-parsing the concatenated project on every edit (guitar
  `assembly.parse-project` ~200 ms after M1).
- Scene compilation uses the assembly session caches (house `web.scene-compile` 330 ms).

Exit: the knob edit reuses ≥ 52 / 56 definitions and costs only the definitions it owns.
Diagnostics, source maps and STEP match a clean build exactly.

**Result** ([BUILD-PERF-M2](../../reports/2026-10/BUILD-PERF-M2.md)): success. Definitions (and their
published ports) compile from their declaration closure with a whole-catalog fallback; derived
meshes and seat queries follow definition content; Scenes use the session caches. Knob edit 54/56
reused, 3.57 s → ~0.4 s native; every incremental rebuild proven equal to a fresh build
(`verify-incremental`); hashes unchanged. Agreed semantic change: an unrelated broken declaration
no longer fails every part. First production AOT browser numbers: root and include edits 70–220 ms
for ATLAS, house and guitar; an edit to a SectionChain body (guitar `CarvedMaple`, 10.7 s) is
kernel time → M4. Exact kernel fixes found via the test lane (pcurve seeds, B-spline evaluation,
import replay) cut spline-tube imports ~3–4× and the lane's two largest projects by ~40%.

### M3 — Binary bridge

- Display meshes cross the JS boundary as typed arrays (`JSExport` memory views → transferable
  `ArrayBuffer`s to the page). JSON carries only tree, diagnostics and projections.

Exit: guitar JSON response < 1 MB; mesh content hash identical; browser time between "build done
in C#" and "mesh on GPU" measured and reported.

**Result** ([BUILD-PERF-M3](../../reports/2026-10/BUILD-PERF-M3.md)): success. Duplicate top-level mesh
removed (responses halved), binary arrays (`bridge: 1`, `TakeBinary`) and retained definitions.
Guitar cold response 942 KB JSON + 4.8 MB binary; a root-comment edit 165 KB with 56/56 definitions
retained; every bridged model proven identical to the legacy model (`verify-bridge`). Browser dev
runtime: ATLAS root-comment edit 5.66 → 1.53 s, with 0.07 s outside the kernel. Next: M2.

### M4 — Kernel hotspots

In order of measured share, re-profiled after M1–M3:

1. SectionChain self-intersection validation: spatial index, and re-validate only changed chains.
2. Pcurve construction and residual sampling (`BoundedPcurveBuilder`, `BrepPcurveRecovery.MaximumResidual`).
3. `BSplineSurfaceWithKnots.Evaluate` allocation and basis reuse.
4. Torus and trimmed-surface tessellation.
5. Bolt maker-mark engraving: `PlanarCapEngraving.Engrave` verifies with a full
   `BrepMassProperties.Evaluate` of the threaded body (41% of the part compile) and glyph profile
   normalization (34%).

Exit: guitar warm native build target set from M0 data (working target ≤ 5 s from 13.9 s); full
`Aetheris.slnx` test lane green; STEP unchanged or justified.

**First pass** ([BUILD-PERF-M4](../../reports/2026-10/BUILD-PERF-M4.md)): meaningful progression, all
bit-exact. SectionChain self-intersection validation via an X sweep (same verdict, pair and
counts), MaximumOvershoot bounding box hoisted, lazy preview SVG, B-spline V-span reuse, text
profile memo. Guitar `CarvedMaple` edit 5.7 → 1.14 s, warm 7.6 → 5.3 s (target ≤ 5 s: at the line),
bolt edit 3.1 → 1.6 s. Remaining share is the 8193-sample bounded-pcurve qualification (policy
decision) and the bolt's two mass-property enclosure checks.

**Result** ([BUILD-PERF-M4](../../reports/2026-10/BUILD-PERF-M4.md)): success. Second pass: SectionChain
trims constructed with their patches (`ConstructedBoundary`: each edge is an isoparametric boundary
of its own patch) instead of recovered and qualified at 8193 samples; the old density is now a test
oracle. Guitar warm 3.9 s (target ≤ 5 s), `CarvedMaple` edit 0.64 s; STEP for guitar/ATLAS changes
by its trims only (compare-step: 3.2e-12 relative), rebaselined deliberately. Next authoring-time
candidate: face orientation on reimport of our own STEP.

### M5 — First paint

- Showcase projects ship a precomputed binary display mesh keyed by source hash; the editor shows
  it immediately and replaces it with the authoritative build.
- Per-definition cache persisted in IndexedDB by content hash, so a reload does not start cold.

Exit: a showcase project shows geometry in < 1 s on open; the authoritative build replaces it with
no visual difference.

## Out of scope

Kernel algorithm rewrites beyond the measured hotspots; changes to Firmament language semantics;
Helios UI work (tracked separately).
