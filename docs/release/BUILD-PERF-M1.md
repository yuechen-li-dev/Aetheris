# BUILD-PERF-M1 — Rebuild floor

Ladder: [build-perf-ladder.md](../development/milestones/general/build-perf-ladder.md) ·
previous: [BUILD-PERF-M0](BUILD-PERF-M0.md).

**State: meaningful progression.** The assembly rebuild floor fell 5–12× with output unchanged:
STEP and display-mesh hashes are identical to M0 for every showcase project, and rebuilds now
provably reproduce the cold build. ATLAS meets the < 100 ms exit; the guitar stops at 214 ms on
its project parse, which moves to M2. The bolt (a single part) is unchanged by design and moves to
M4 with its hotspots named.

## Metric correction

M0 measured a rebuild with no source change. An IDE rarely rebuilds unchanged source, and a
whole-result memo would make that number vanish without helping any edit. The floor every edit
pays is a **root comment edit**: the source changed, so the build runs, but no geometry changed.
M1 reports both, and states kernel time (before the response is serialized) separately from
invoke time, because re-sending the unchanged mesh as JSON costs 107–141 ms natively on its own;
that belongs to M3.

## Changes

1. **Definition cache stores bodies, not STEP text.** `BrepBody.Clone()` copies the topology,
   geometry and binding stores (add-only dictionaries of immutable records; insertion order is
   preserved, so downstream output is identical). The cache keeps a private clone and every hit
   returns a fresh clone. No STEP parse or pcurve recovery on a hit.
2. **Derived values keyed by definition input key.** Each cached definition carries the input key
   the cache already used to decide reuse (`MaterializedAssemblyDefinition.InputKey`,
   `AssemblyM1CompilationResult.DefinitionInputKeys`). A generational `DerivedValueCache` keeps
   what one build used and drops the rest at the next build:
   - **datum-seat queries** per world body (input key + exact transform bits), so seat contact
     checks stop tessellating every instance body on every build;
   - **prepared display-mesh definitions** (`FirmamentCompilationSession.ExportDisplayMesh`, used
     by the web runtime);
   - **published-port bindings** (`AssemblyPublishedPorts.BindAuthored`): a template
     specialization's ports depend on identity, declarations and source name; a SectionChain
     resource's on identity, path and loaded text. Diagnostics are recorded and replayed; only
     bindings the definition cache can copy are kept, and reuse returns copies.
   Keys are input keys, not STEP hashes: two bodies can export identical STEP and still differ in
   state the tessellator reads.
3. **Assembly STEP is written on request.** `AssemblyIrAp242Exporter.PrepareExport` runs every
   structural check of `Export` during the build (a failure still fails the build); the AP242
   writer runs on `exportStep` and produces the same text. **Contract note:** a failure inside the
   writer itself now surfaces at export (`step-export-failed`) instead of failing the build.
4. **Harness:** `rebuildStepMatchesCold` / `rebuildMeshMatchesCold` (a no-change rebuild must
   reproduce the cold build; gated by budgets), revision-independent mesh hash, and
   `Aetheris.Web.Perf compare-step` (cross-runtime equivalence, below).
5. **Tests:** `BrepBodyCloneTests` (identical STEP and order; clone independence);
   `IncrementalCompilationTests` gains session display-mesh equality with a fresh export, and a
   check that mutating a body handed out by the cache cannot leak into the next build.

## Results

Native, median of 3 runs, same 2-core container as M0. Kernel = before response serialization.

| Project | Scenario | M0 kernel | M1 kernel | M1 invoke | M0 alloc | M1 alloc | Reused |
|---|---|---|---|---|---|---|---|
| guitar | root comment edit | 2.61 s | **214 ms** | 353 ms | 2.75 GB | 145 MB | 56/56 |
| guitar | no-change rebuild | 2.75 s | **108 ms** | 292 ms | 2.75 GB | 145 MB | 56/56 |
| guitar | knob value edit | 5.64 s | 3.57 s | 3.71 s | 4.38 GB | 2.19 GB | 4/56 |
| guitar | leaf comment edit (`CarvedMaple`) | 7.42 s | 5.70 s | 5.83 s | 5.20 GB | 3.99 GB | 55/56 |
| guitar | warm build | 14.82 s | 12.87 s | 13.03 s | 9.48 GB | 8.89 GB | — |
| atlas | root comment edit | 432 ms | **74 ms** | 100 ms | 374 MB | 36 MB | 24/24 |
| atlas | no-change rebuild | 632 ms | **48 ms** | 90 ms | 375 MB | 36 MB | 24/24 |
| house | root comment edit | 635 ms | 394 ms | 442 ms | 304 MB | 177 MB | — |
| bolt | root comment edit | 3.00 s | 3.06 s | 3.15 s | 1.75 GB | 1.75 GB | — |

All five projects: STEP DATA and display-mesh hashes equal to M0; stable across runs; no-change
rebuild reproduces the cold build. Root-comment timings are noisy (spread 26–42%) because they
are short; allocations are the gate.

### What remains in the floor

- **guitar 214 ms:** `assembly.parse-project` ~200 ms. Port binding is now reused; the rest is
  `InstantiateAssemblyDefinition`, named-profile resolution and spelling normalization over the
  concatenated project text, all re-run on every edit → **M2** (per-definition parse and
  invalidation).
- **house 394 ms:** `web.scene-compile` 330 ms; the Scene path does not use the assembly session
  caches yet → M2.
- **response 107–141 ms native:** the unchanged mesh re-sent as JSON → **M3**.

### Bolt (M0 open item 2, named)

74% of the bolt's part compile is the maker-mark engraving: `PlanarCapEngraving.Engrave` runs
`BrepMassProperties.Evaluate` (41%: tessellates the whole threaded body to verify the engrave)
and glyph profile normalization (34%). A single part is one definition, so a cache cannot help
an edit. Moved to **M4**.

## Browser (interpreted dev runtime)

Headless Chromium, same container as M0, M1 runtime.

| Project | Scenario | M0 | M1 | M1 kernel | M1 response (rest) |
|---|---|---|---|---|---|
| atlas | no-change rebuild | 30.7 s | **5.8 s** | — | — |
| atlas | root comment edit | — | **5.7 s** | 1.55 s | **4.1 s (73%)** |
| house | no-change rebuild | 33.6 s | 19.4 s | — | — |
| house | root comment edit | — | 19.4 s | 12.3 s (scene compile 10.8 s) | 7.0 s |
| atlas / house | cold | 333 s / 81 s | 328 s / 74 s | | |

In the browser the unchanged mesh's JSON round trip, not the kernel, is now the larger part of the
edit floor (natively it was 107–141 ms). **M3 moves ahead of M2.**

## Cross-runtime determinism (M0 open item 3)

`compare-step` imports two STEP files (product structure when present), matches definitions by
name and occurrences by path, requires identical topology counts, and compares volume, area,
centroid and bounds within a relative tolerance (default 1e-6) using `BrepMassProperties`.

| Project | Native vs WASM (dev runtime) | Worst relative difference |
|---|---|---|
| bolt | equivalent | 1.9e-19 |
| atlas (70 definitions, 112 occurrences) | equivalent | 0 |

Atlas' 6-entity STEP difference is therefore entity sharing in the writer (point deduplication
tripped by last-digit floating-point drift), not geometry. A sanity run (atlas vs bolt) reports
184 differences.

## Open items

1. AOT browser numbers (M0 item 1) — still need a machine with > 8 GB RAM to link.
2. Guitar project parse (~200 ms) and Scene compile → M2.
3. Response serialization of unchanged meshes → M3, now next: it is 73% of ATLAS' browser edit floor.
