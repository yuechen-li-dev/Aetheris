# BUILD-PERF-M2 — Invalidation granularity

Ladder: [build-perf-ladder.md](../development/milestones/general/build-perf-ladder.md) ·
previous: [BUILD-PERF-M3](BUILD-PERF-M3.md) (M3 ran first).

**State: success.** The exit criterion is met: the guitar knob edit reuses 54/56 definitions and
costs only the definitions it owns (native 3.86 s at M1 → ~0.4 s). Every incremental rebuild equals
a fresh build, and STEP and display-mesh hashes are unchanged for every showcase project. The first
production (AOT) browser measurements show root and include edits at 70–220 ms for ATLAS, house and
the guitar; the one slow edit left is a SectionChain body, which is kernel work (M4).

## Changes

1. **Definitions compile from their declaration closure** (`AssemblyDeclarationScope`). The
   declaration catalog (every included document) is split into top-level declarations, and a
   template definition compiles from the transitive closure of what its template references. The
   split is conservative: a declaration starts only at a column-0 identifier after `}` or `;`, so
   multi-line expressions are never cut. If the closure does not compile on its own, the definition
   compiles against the whole catalog exactly as before. The definition cache stores an entry under
   the closure key (closure compiled) or under a distinct fallback key (closure failed for exactly
   this catalog). Both cases are deterministic, so a reused entry is always what a fresh build would
   produce. **Agreed semantic change:** an error in an unrelated declaration no longer fails every
   part, and semantic spans of a scoped part are relative to its closure module (they were already
   offsets into a synthetic module, not into the user's file).
2. **Published ports bind from the same closure**, with the same fallback and key discipline.
3. **Derived values follow content, not source text.** The definition cache hands out a content key
   (identity, canonical STEP, retained CIR) as the input key of display meshes and datum-seat
   queries. A cached definition's body *is* the import of its canonical STEP, so everything derived
   from the body is a function of that key. (M1 rejected STEP keys for bodies that are not reimports;
   this applies only to reimported definition bodies.) A rebuilt definition whose STEP is unchanged
   reuses a clone of the cached body instead of importing again.
4. **Scenes use the session caches.** Assemblies use their session's display cache; boxes, parts and
   assembly-derived definitions are reused by reference, so the web runtime retains them (house
   bridged rebuild 2.5 MB → 213 KB). `DisplayProjection` passes through definitions that already
   carry a shader.
5. **Per-catalog facts** (`DeclarationTextFacts`): the InlineStep check, Gear-blanked text and catalog
   hash are computed once per catalog instead of once per definition.
6. **Parse floor.** The placement solver rebuilt a semantic-value dictionary for every candidate
   constraint; it is now built once per solve (guitar project parse 150 → 24 ms native).
   `FirmamentV2Parser` keeps a bounded memo of recent parses. The web runtime raises
   `Regex.CacheSize` from 15 to 1024 (the compiler has ~860 static Regex call sites).
7. **Kernel, bit-exact** (found while profiling the test lane):
   - `BrepPcurveRecovery.GridSeed` evaluates its ~9300 seed samples once per surface rather than per
     target, scanning them in the same order with the same comparison.
   - `BSplineSurfaceWithKnots.Evaluate` runs on flat arrays with the same arithmetic in the same
     order.
   - `Step242Importer.ImportBody` replays the last 8 successful imports as clones. It does this only
     when no policy, diagnostics capture or rational preservation is in effect.

   Result: coil-on-stem assembly 39 → 11 s, EditableV8 engine 79 → 21 s native.
8. **Tooling.**
   - `verify-incremental <project> <root> [--documents N]`: per document, appends a comment, inserts
     a line at the top (shifting every offset) and changes the first value, then compares the
     session rebuild with a fresh build, and the revert with the original.
   - `verify-bridge` also runs an include-comment edit.
   - `profile <file> [--project]` prints the top phases of one build.
   - The parser reports `m0.*` sub-phases.
   - The browser lane (HeliosCAD `scripts/measure-build-perf.mjs`) also runs an include-comment edit.

## Results

### Native (kernel, 2-core container)

| Project | Scenario | M1 | M2 | Reused |
|---|---|---|---|---|
| guitar | knob value edit (include) | 3.57 s | **0.40–0.64 s** | 4/56 → **54/56** |
| guitar | include comment (`hardware-layout`) | 2.4 s | **0.22–0.65 s** | 4/56 → 55/56 |
| guitar | root comment edit | 214 ms | ~190–250 ms | 56/56 |
| atlas | include edit (`gripper`, value) | 409 ms | **85–190 ms** | 24/24 or 23/24 |
| house | root comment edit | 394 ms | **~100 ms** | — |
| coil-on-stem | assembly build | 39 s | **11 s** | — |
| EditableV8 | assembly build | 79 s | **21 s** | — |

Timings are single runs and noisy (±30%). A "warm" build in the showcase harness now also benefits
from the import memo (guitar warm 11.4 → 7.6 s, 7.5 → 1.9 GB allocated), and so does a reopened
session in the same browser worker.

### Browser, production AOT runtime (first AOT numbers; Chrome 155 on the author's machine)

| Project | Init | Cold | No-change | Root comment | Include comment |
|---|---|---|---|---|---|
| atlas | 618 ms | 10.0 s | 80 ms | **70 ms** | **75 ms** (`base`, 24/24) |
| house | 461 ms | 3.0 s | 100 ms | **96 ms** | **101 ms** (`components`) |
| guitar | 459 ms | 29.4 s | 220 ms | **219 ms** | 10.7 s (`CarvedMaple`, 55/56) |

The guitar include edit touches a SectionChain body: 10.3 of its 10.7 s are
`sectionchain.self-intersection-validate` (4.0 s), `sectionchain.materialize` (3.5 s) and
`pcurve.bounded-build` (2.8 s). That is kernel time on the one definition that changed → **M4**.
The cold builds are the same three phases plus datum seats.

### Browser, interpreted development runtime (headless Chromium, container)

| Project | Scenario | M3 | M2 |
|---|---|---|---|
| atlas | root comment | 1.53 s | **1.06 s** |
| atlas | include comment | — (≈2.8 s before part 3) | **0.91 s** |
| house | root comment | 13.1 s | **1.47 s** |
| house | include comment | — (≈3.7 s before part 3) | **1.68 s** |
| atlas / house | cold | 327 s / 65 s | 219 s / 33 s |

What remains in the interpreted floor is regex-heavy assembly parsing (`m0.assembly-definitions`,
`m0.tree`, `m0.patterns`, ~0.5 s). AOT removes it (70 ms total), so this matters only for the dev
runtime.

### Test lane

Same lane (`dotnet test Aetheris.slnx -c Release --no-build -m:1 -- RunConfiguration.MaxCpuCount=1`),
per project: Firmament 5m14 → 3m06, Kernel.Core 4m00 → 2m39, EditableV8 81 → 20 s, Server 53 → 27 s.
The kernel fixes did this; no test was weakened. The Core test that rewrote a tracked STEP fixture
on every run now asserts the fixture is current (`AETHERIS_REFRESH_FIXTURES=1` rewrites it). The
checked-in copy was stale, so CLI baselines used to see different bytes depending on test order.

## Verification

- `verify-incremental`: every document of ATLAS (11), guitar (spreads of 4–6 documents including
  `knob`, `hardware-layout`, `pickups-assembly`, `body-profile`, `selector`), house (7). Each was
  checked with a comment, a line inserted at the top and a value change, plus the revert. Every
  incremental rebuild equals a fresh build.
- `verify-bridge` (ATLAS, guitar, house, including the include-comment edit): bridged equals legacy.
- Showcase hash budgets: pass (STEP DATA and mesh hashes identical to M0/M1).
- New `AssemblyDeclarationScopeTests`: scoped build equals whole-catalog build for every canonical
  assembly fixture (the guitar is covered by the hash budgets), closure unit cases, include-edit
  reuse. New `IncrementalCompilationTests` for content-keyed meshes and scene reuse.
- Full lane: 4581/4581 at `e2c3895` (and at `98ad677`); the working tree stays clean after the lane.

## What remains

- **M4 (kernel):** SectionChain self-intersection validation and materialization, bounded pcurves,
  datum-seat validation on cold builds, bolt engraving. In AOT these now *are* the edit cost when
  the edit touches a swept body, and most of every cold build.
- **Parts:** a single-part document (the bolt) is still one definition; a value edit rebuilds it.
- **Interpreted runtime:** generated regexes in the assembly parser would bring the dev floor down;
  production is unaffected.
