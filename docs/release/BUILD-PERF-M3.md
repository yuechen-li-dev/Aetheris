# BUILD-PERF-M3 — Binary bridge

Ladder: [build-perf-ladder.md](../development/milestones/general/build-perf-ladder.md) ·
previous: [BUILD-PERF-M1](BUILD-PERF-M1.md). M3 ran before M2 because the M1 browser lane showed
the response, not the kernel, dominating the edit floor (ATLAS: 4.1 of 5.7 s).

**State: success.** In the browser, the response overhead of an edit fell from seconds to tens of
milliseconds, and every bridged model is proven identical to the legacy JSON model.

## Changes

1. **The duplicate mesh is gone.** Every build response carried `mesh`, `tree` and `entityIds`
   twice: inside `model` (the snapshot the SDK applies) and again at the top level, which nothing
   reads. Responses now carry them once (legacy and bridged).
2. **Binary arrays** (request `bridge: 1`, sent by the SDK when the runtime exports `TakeBinary`).
   `double[]` and `int[]` of at least 64 elements are written to one little-endian, 8-byte-aligned
   buffer and replaced in the JSON by `{"$binary":"f64"|"i32","byteOffset","length"}`. The runtime
   (`WebResponseBridge`) applies this to every response shape (parts, assemblies, scenes).
   `TakeBinary()` returns the buffer; the SDK revives typed-array views of it (`reviveBinary`),
   reinterprets `i32` indices as the `Uint32Array` the contract promises (no copy), and transfers
   the single buffer from the Worker. Values are bit-identical to the JSON path.
3. **Retained definitions.** A rebuild writes a mesh definition the session's previous response
   already delivered as `{"id","identity","retained":true}`. "Already delivered" is reference
   identity: M1's display cache returns the same prepared definition object exactly when its
   input key is unchanged. `ModelSession.apply` substitutes the definition it holds and fails
   with `retained-definition-missing` rather than leave a hole. The stored snapshot keeps full
   definitions, so the `snapshot` operation is unaffected. telos already reuses GPU geometry for
   the same arrays, so retained definitions are not re-uploaded either.
4. **Verification tooling.** `Aetheris.Web.Perf verify-bridge <project> <root>` builds the project
   in a legacy session and a bridged session through cold build, no-change rebuild and root-comment
   edit, reconstructs each bridged model as the SDK does and requires it to equal the legacy model.
   SDK tests (`tests/bridge.test.mjs`): exact revival into one buffer, index reinterpretation,
   deduplicated transferables, retained substitution and the missing-definition error.
   The browser lane records the runtime's bridge timings.

Compatibility: an older SDK does not send `bridge` and receives legacy JSON (minus the duplicate
top-level fields, which the SDK never read). The native perf harness uses legacy mode, so the
M0/M1 hash budgets remain valid.

## Results

### Response size (native, `verify-bridge`; all steps identical to legacy)

| Project | Before M3 | Legacy now | Bridged cold | Bridged edit (root comment) |
|---|---|---|---|---|
| guitar | 17.2 MB | 8.6 MB | 942 KB JSON + 4.8 MB binary | **165 KB**, 56/56 definitions retained |
| atlas | 3.3 MB | 1.7 MB | 323 KB + 910 KB | **118 KB**, 24/24 retained |
| house (Scene) | 5.0 MB | 2.5 MB | 663 KB + 1.6 MB | 647 KB + 1.6 MB (Scenes do not reuse definitions yet) |
| bolt (part) | 13.3 MB | 6.6 MB | 1.6 MB + 2.8 MB | 1.6 MB + 2.8 MB (one definition, rebuilt) |

### Browser (interpreted dev runtime, headless Chromium, same container)

| Project | Scenario | M1 | M3 | of which outside the kernel |
|---|---|---|---|---|
| atlas | root comment edit | 5.66 s | **1.53 s** | 4.1 s → **0.07 s** |
| atlas | no-change rebuild | 5.78 s | **1.65 s** | |
| house | root comment edit | 19.4 s | **13.1 s** | 7.0 s → 0.8 s |
| atlas / house | cold | 328 s / 74 s | 327 s / 65 s | response parse 1–2 ms, binary revive 3–6 ms |

Browser STEP for ATLAS is unchanged since M0 (`f6c57e57…`).

### What remains

- **Edit floor is now kernel time:** ATLAS 1.46 s, house 12.3 s (scene compile) on the dev
  runtime → M2.
- **Edge polylines stay JSON** (thousands of `[x,y,z]` triples; bolt 1.4 MB). A flattened binary
  edge layout needs a contract change; deferred until it shows up in a profile.
- **Parts and Scenes are not retained:** a single part is one definition; Scenes do not use the
  assembly display cache → M2.
- **AOT browser numbers** still need a machine with > 8 GB RAM to link.
