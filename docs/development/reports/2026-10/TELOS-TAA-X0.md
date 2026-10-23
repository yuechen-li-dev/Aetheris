# TELOS-TAA-X0

Verdict: **Meaningful progression**, 2026-10-05. The missing utility frontend/lowering prerequisite is removed, and the shared experimental temporal path renders real WebGPU mesh, CIR and mixed scenes. **The visual acceptance question remains unresolved.** Utility reduces error against a supersampled static reference compared with fixed TAA, but both temporal modes are worse than spatial-only in that witness. Spatial AA remains the product default. No claim of useful silhouette convergence or superior ghosting is made.

## Executive answer and exact next blocker

Three Telos can own conservative temporal resources and execute a deterministic, source-authored utility resolve without a per-pixel CPU policy. This run does not yet prove that it provides better CAD AA than the existing spatial path.

The next bounded blocker is **subpixel silhouette coverage/correspondence in the shared resolve**. `temporal-resolve.wgsl` requires both current and previous pixels to contain geometry and uses nearest previous-depth evidence. Background/foreground coverage changes fail correspondence; those pixels use the current jittered sample rather than integrating valid edge coverage. Strong depth rejection protects against ghosts but leaves sample-phase variation. The measured static image errors below make this a concrete quality failure rather than a subjective tuning concern. Do not increase history weights to conceal it.

Smallest next repair: distinguish bounded edge-coverage evidence from interior surface correspondence in Telos, improve the confidence test at fractional reprojection positions, and rerun this exact supersampled reference and motion witness. Keep selection rays and post-resolve topology stable. A transform-history redesign, CIR rebuild, product-specific resolve, or general policy engine is not justified by this result.

## Utility syntax audit

| Owner before this work | Syntax/status found | Actual lowering/ownership |
| --- | --- | --- |
| Copeland TS | No implemented `decide` or `when utility` expression in the ordinary parser or graphics binder. | Graphics used ordinary locals, comparisons, branches and helper calls in VD-MIR. No shader-compatible utility runtime existed. |
| Older Aurelian SDSL-V in Copeland | Lexer `KeywordUtility`, `SdslvWhenUtilityExpression`, candidate/options AST records. | Declaration scaffolding is not implementation: `HlslEmitter` explicitly lists this expression as unsupported; no completed parser/binder utility lane was found. |
| Copeland-hosted Dominatus | `Ai.Decide` / `Ai.Option`, explicit option scoring, controller commitment/hysteresis and reports. | CLR agent/controller policy. It is implemented, but is not Copeland TS syntax or shader lowering and must not become a per-pixel CPU decision path. |
| Oct | Standalone `when utility { case value when Bool score Int ... else value }`; enum-targeted form; separate controller-bound `when policy`. | AST, parser, typecheck, interpreter and compiled ordinary-block lowering are present. Guards once in source order, score only when eligible, greatest score, earliest-source tie, only selected result evaluated. Required fallback, compatible result types. Standalone form has no commitment or policy fields. |
| Aetheris JudgmentEngine | Named admissible candidates, normalized utility helpers, finite scores, rejection reporting and deterministic priority/source ties. | Existing .NET bounded judgment authority; not shader syntax. Its finite/admissible/deterministic model is reused conceptually, without duplicating its runtime on the GPU or adding CPU decisions. |

Audit source pointers: Copeland `src/Copeland/Copeland.TS/Syntax`, `Gpu/GpuGraphicsBinder.cs`; `src/Aurelian/Aurelian.Shaders/Language/{Tokens,Ast,Emission/Hlsl}`; Oct `Language/reference/runtime/21-octomata.md`, `internal/ast/program.go`, `internal/typecheck/typecheck.go`, `internal/interpret/interpret.go`, and compiled utility corpus/lowering; Aetheris `Aetheris.Kernel.Core/Judgment/JudgmentEngine.cs`. Oct was inspected without changing its source.

### Chosen canonical syntax for this milestone

Reuse Oct's one-shot form in the **Copeland graphics GPU profile**:

```typescript
return when utility {
    case RejectHistory() when true score Score(risk)
    case StableHistory(stable) when depth > 0.8 && motion > 0.7 && edge < 0.2 score Score(stable)
    case ClampedHistory(clamped) when depth > 0.8 && motion > 0.5 && edge < 0.2 score Score(clamped)
    case EdgeConservative(depth * motion * color) when depth > 0.8 && motion > 0.8 && color > 0.8 && edge >= 0.2 score Score(depth * motion * color)
    else RejectHistory()
};
```

The GPU subset already admits `u32`, rather than Oct's signed `Int`, so **every score is explicitly `u32`**. Author confidence as `f32`; `Score` maps it to finite integer buckets 0,20,40,60,80,100. This introduces no floating/NaN score law. Cases number 1–16, guards are `bool`, all results/fallback share one type, ties keep the earliest source case. Reject is first, so uncertain ties favor rejection. Result functions give alternatives visible names; this subset does not yet admit GPU judgment enums.

Added: shared syntax AST/parser, bounded graphics typechecking and normalization to a closed helper with ordinary `local`, `if`, `assign`, `return`, numeric comparisons and call expressions. Guards execute once, ineligible scores do not execute, and only the selected value executes. Nested choices are supported. No special utility instruction reaches VD-MIR, WGSL or HLSL. Host compilation explicitly refuses the form with `COPE-UTILITY-PROFILE-0001`; compute utility, enum-targeted choices, host utility, hysteresis and commitment remain unqualified. This is a candidate canonical **one-shot shader form**, not a declaration of complete host-language parity.

The direct WGSL backend now exposes compiler-owned source-function/emitted-name metadata for static linkage. The offline generator uses that map to generate `telosPolicy`, avoiding guessed backend names or runtime reflection. `fixtures/three-telos/temporal-policy.v.ts` owns all actual policy scoring, admissibility, weights and winner selection. Its generated runtime WGSL is a necessary package asset with a reproduction command, not a handwritten substitute for utility. Sampling/reprojection/clamping are ordinary Telos WGSL plumbing.

## Shared architecture and temporal inputs

`TelosTemporal` belongs to Three Telos, alongside `TelosHost`, `TelosFrame` and `TelosCamera`. Cadmata and the actual sibling Helios wrapper accept `aaMode`/`aaDebug` and call the same `host.setAA`; there is no second temporal implementation. Both default to `SpatialOnly`. `None` disables temporal resolve/jitter, while intrinsic line coverage AA remains active in all modes. `TAA` is the fixed baseline; `TAAUtility` is the experiment.

Minimum inputs: current surface color and authoritative `depth32float`; previous resolved surface color and previous authoritative depth; current inverse jittered VP, previous jittered VP and its inverse. Reconstructed world points use WebGPU [0,1] depth. CIR writes depth at its analytic hit, so the same reconstruction works for CIR and mesh pixels. Additional normal/identity attachments were not introduced: they cost bandwidth and require changing the admitted field output ABI. Appearance, visibility, selection, geometry and occurrence updates invalidate the whole history through `setScene`, preserving correctness without identity buffers.

Eight Halton(2,3) samples are bounded to half a physical pixel. Rendering consumes separate jittered matrices and CIR rays; `camera.project`, `unproject`, `worldRay`, PMI projection and engineering picking retain the logical unjittered camera. Both orthographic and perspective math are checked. A static dirty viewport requests eight accumulation frames and then stops scheduling. Interactive camera input requests further frames; direct qualification stepping disables RAF so there are no unrecorded samples.

Camera-only reprojection is supported. **No retained previous occurrence transforms existed to reuse.** Moving occurrences therefore reset all history through `setScene`; they do not claim object-motion reprojection. Callers must respect the existing immutable scene/geometry contract and submit transform/appearance changes through `setScene`.

### Policies and parameters

| Policy | Weight | Reason |
| --- | --- | --- |
| RejectHistory | 0 | Always admissible; score is maximum depth/motion risk or edge-plus-color risk. Conservative ties favor this arm. |
| StableHistory | 0.50 | High depth/motion/color confidence, away from edges. |
| ClampedHistory | 0.25 | Valid correspondence with neighborhood/color disagreement. |
| EdgeConservative | 0.12 | Valid, slow, color-consistent edge evidence. |

Stable score is depth × motion × color × (1−edge). Clamp score is depth × motion × (1−color) × (1−edge). Reject score is max(1−depth,1−motion,edge×(1−color)). All accepted histories are clipped to a bounded 3×3 current RGB min/max range, including the stable policy.

Depth confidence drops with previous-world separation measured in current pixel footprint, to zero at two footprints; admission >0.8 means less than 0.4 footprint discrepancy. This is intentionally strict and contributes to the isolated coverage issue. Motion allows <1 pixel of sample jitter and drops to zero by 8 pixels/frame. Luminance confidence drops to zero at a 0.15 normalized-color difference. Edge risk comes from a 3×3 depth discontinuity measured against four pixel footprints. These are documented experimental thresholds, not universal CAD tuning. No normal-agreement claim is made.

The baseline uses the same jitter, reprojection, depth admission and neighborhood clamp with fixed 0.50 history. Thus the comparison changes policy, not resources or sampling. Policy debug colors are red reject, green stable, yellow clamp and blue edge. Confidence, motion and depth disagreement views are also available. Debug rendering reads frozen last color history; debug pixels never enter history. Seed a color view first to inspect accumulated decisions.

### Pass order and lifecycle

Background → opaque mesh → CIR field → temporal resolve → capture surface history/depth → reference grid → topology → authoring/selection overlays → DOM PMI/presentation. Topology and authoring geometry use logical camera projection. DOM PMI never enters a GPU history texture. The line shader now computes local derivative coverage on expanded screen-space quads; this was missing in the original hard-edged line shader. It does not temporally accumulate lines or replace BRep topology authority.

Resize/resource generation, DPR, camera mode/projection parameters, AA mode, and `setScene` invalidate history. Appearance/selection updates deliberately reset rather than leave old colors. GPU loss keeps the existing host-stop diagnostic behavior. Missing temporal assets or pipeline validation failure retains spatial viewport startup. Temporal textures are created only on the first enabled render, retained across frames, recreated on physical-size changes and released on disposal. Returning to spatial mode retains the allocated temporal textures for reuse until resize/disposal; this memory behavior is explicit.

Logical topology depth testing against jittered surface depth can still disagree within half a pixel near silhouettes; strict topology occlusion/coverage parity in temporal mode is not accepted.

## Memory

Color source/history use the adapter's ordinary `bgra8unorm`/`rgba8unorm`, not HDR. Previous depth is `depth32float`. Additional temporal texture storage is **12 bytes per physical pixel**: source color, history color, previous depth. Existing current depth/canvas storage is excluded. One 224-byte uniform and optional timestamp query/readback storage are negligible next to the textures.

| CSS viewport / DPR | Physical dimensions | Additional temporal textures |
| --- | --- | --- |
| 1080p / 1 | 1920×1080 | 23.73 MiB |
| 1440p / 1 | 2560×1440 | 42.19 MiB |
| 1080p / 2 | 3840×2160 | 94.92 MiB |
| 1440p / 2 | 5120×2880 | 168.75 MiB |

These are allocation arithmetic, not browser memory telemetry. Large-DPR performance/OOM behavior is unqualified.

## Controlled browser evidence and comparison

Real Edge WebGPU, 512×512 physical pixels, deterministic 24-frame sequences for each mode: static mixed mesh/CIR, orbit, fast camera, disocclusion behind a foreground edge, and moving occurrence. Includes a bright direct-WGSL CIR cylinder, a dark mesh foreground and thin topology strokes. All 360 recorded frames complete without GPU validation/page errors; canonical picking rays remain unchanged by rendering. Policy debug includes 77,071 stable and 410 conservative-edge pixels; rejected pixels include the background. This static witness did not exercise a winning clamped policy; its branch exists and typechecks, but branch occupancy is not claimed.

The nontrivial CTC03 mechanical STEP witness renders all 129 faces in each mode, with bends, holes and slots. Repository CLI inspection independently reports one body, one shell, 129 faces, 306 edges and 198 vertices. The CAD capture consumes the existing Cadmata display packet; this is a shared-host mesh witness, not an interactive TAA qualification of both products or a real-part CIR admission claim.

Static quality reference: render the same scene at DPR4 (2048²), box-average 16 physical samples per 512² output pixel, compare RGB mean absolute error in 8-bit channel units after 16 controlled frames:

| Mode | Error to supersampled reference |
| --- | --- |
| Spatial/no temporal | 0.105024 |
| Fixed TAA | 0.170217 |
| Utility TAA | 0.145127 |

Utility is about 14.7% better than baseline for this one static capture, but about 38.2% worse than spatial-only. This is **not** successful AA qualification. Motion image difference against the unjittered render is a diagnostic, not an anti-alias ground truth; static frame-to-frame variation remains nonzero in temporal modes. No progressive-blur acceptance claim is made.

Ghost energy samples background pixels at least two pixels outside the unjittered visible geometry. Both temporal paths have essentially zero energy there, so this witness does not distinguish their ghosting quality. Pixel-boundary coverage and subpixel trails are outside that metric. Inspect the motion recording before judging visual quality; there is no validated superior-ghosting claim.

Raw local evidence: `artifacts/local/telos-taa/browser/metrics.json`, `policy-counts.json`, mode/scenario PNGs, `utility-policy.png`, CTC03 captures and `comparison.webm`. The recording labels each mode/scenario and includes controlled motion. Raw captures are ignored generated output and are not promoted into source control. `fallback.json` qualifies startup/rendering after deliberately blocking both temporal shader assets; the host remains running in `SpatialOnly`.

## Measured performance

Local Edge run only, same 512² mixed CIR/mesh witness. Warm-frame arithmetic means over frames 8–23; browser CPU timer granularity makes sub-0.1ms CPU values noisy. GPU resolve duration comes from optional WebGPU timestamp queries. Queue-completion time includes drawing, resolve/copies and qualification readback-copy submission; it is not a production presented-frame percentile or display latency. History copies are outside the resolve GPU timestamp interval. No large-viewport scaling or universal performance claim follows.

| Mode | View | Resolve GPU ms | Resolve encode CPU ms | Reprojection/setup CPU ms | Queue completion ms |
| --- | --- | --- | --- | --- | --- |
| None | static | — | 0.000 | 0.000 | 3.825 |
| None | orbit | — | 0.000 | 0.000 | 3.556 |
| None | fast | — | 0.000 | 0.000 | 3.875 |
| None | disocclusion | — | 0.000 | 0.000 | 5.037 |
| None | moving-occurrence | — | 0.000 | 0.000 | 5.463 |
| TAA | static | 0.283 | 0.031 | 0.000 | 6.250 |
| TAA | orbit | 0.426 | 0.019 | 0.013 | 5.694 |
| TAA | fast | 0.446 | 0.000 | 0.013 | 5.613 |
| TAA | disocclusion | 0.315 | 0.025 | 0.012 | 6.287 |
| TAA | moving-occurrence | 0.344 | 0.013 | 0.006 | 6.219 |
| TAAUtility | static | 0.369 | 0.031 | 0.000 | 6.506 |
| TAAUtility | orbit | 0.348 | 0.025 | 0.000 | 5.987 |
| TAAUtility | fast | 0.565 | 0.013 | 0.006 | 5.787 |
| TAAUtility | disocclusion | 0.295 | 0.000 | 0.000 | 6.325 |
| TAAUtility | moving-occurrence | 0.373 | 0.031 | 0.000 | 6.375 |

## Validation and qualification limits

- Aetheris Release solution build: 0 errors, 10 existing WebAssembly warnings. Fast lane: 1,005 passed. Full serial lane: **4,312 passed**, 20 projects, no failures/skips. No legacy Zig lane was changed or needed.
- Copeland TS full suite: **1,394 passed**, including utility selection, expression scores, zero/equal ties, fallback, incompatible scores/guards/results, nested choices, bounded case count, source-map/symbol linkage and explicit host refusal.
- Aurelian shader suite: **152 passed**. New utility test emits ordinary HLSL and validates vertex/fragment SPIR-V through native tools. The first full run found missing Naga; installed the repository-documented pinned Naga 27.0.0 under ignored local output and reran successfully. The Telos direct WGSL runtime/compiler path does not require Naga.
- Telos strict TypeScript/build/unit checks: 9 passed, including render-only jitter bounds and exact logical ray stability for both camera modes. Browser runtime compilation validates the generated policy and plumbing together.
- Cadmata production frontend build and **85 tests pass**. Helios development build and **29 tests pass**. Helios production build refuses the installed non-AOT SDK with its existing explicit message: `Production requires the AOT SDK package. Run npm run sdk:install:production.` No SDK republish/AOT packaging expansion was attempted for this graphics milestone.
- The ordinary spatial/mixed-depth browser witness also passes with optional temporal assets blocked. Product-level TAA camera/PMI/selection motion remains unqualified; optional controls are integrated and built, not declared production accepted.

## Friction log

| Friction | Workaround / owner / permanent repair / status |
| --- | --- |
| Utility language gap | Removed in Copeland graphics frontend; ordinary VD-MIR normalization. Host/compute/enum-targeted utility explicitly deferred. |
| WGSL helper naming | Removed by compiler-owned FunctionNames metadata and generated static shim. No text guessing or runtime reflection. |
| Missing occurrence motion data | Safe workaround: whole-history reset on `setScene`. Owner Telos scene lifecycle. Retained previous transforms/identity correspondence remain deferred; do not infer camera-only motion for moved objects. |
| Silhouette/reprojection confidence | Existing strict rejection prevents stale trails but does not integrate fractional coverage. Owner shared Telos resolve/local primitive coverage. Next bounded repair; concrete supersampled failure above. |
| CIR/mesh mismatch | Shared analytic-hit/mesh depth reprojection works in one frame; smooth fractional CIR silhouette accumulation is not accepted. Avoid changing CIR authority. |
| Topology smear/occlusion | Lines stay after resolve and get local spatial coverage. No temporal smear; half-pixel surface-depth/line alignment remains a qualification limit. |
| Appearance changes | Whole-history reset; no prior colors retained through scene updates. Per-occurrence selective invalidation deferred. |
| History format | Standard UNORM + depth32float qualified at 512². DPR2 cost is significant; large viewport allocation/OOM recovery unqualified. |
| Browser readback | Canvas swap texture can expire after an awaited frame boundary. Qualification submits readback immediately after `render`; await its completion afterwards. Persistent screenshot staging is not introduced. |
| Browser feature/assets | Optional temporal loading/pipeline fallback; missing-assets spatial rendering qualified. Timestamp availability never gates startup. GPU loss follows existing stop diagnostic. |
| Toolchain | Legacy shader suite needed pinned Naga; local installation removed that environment blocker. Direct WGSL remains managed. |
| Helios production package | Development integration passes. AOT SDK package is absent in current installation; production packaging remains a separate explicit gap. |

## Code-quality review

One shared owner contains history, jitter, reprojection, resolve and optional timing. Policy lives only in source VTS; there is no duplicate CPU scoring function or handwritten WGSL policy. Generated shader names come from the compiler's ABI metadata. Uniform storage and VP/inverse matrices are retained; render projection accepts a retained output matrix. Textures and bind groups are created only on size generation changes. Existing per-draw host/field allocations remain outside this milestone; no claim of an allocation-free renderer is made. Repeated primary scene updates intentionally trade accumulation for correctness. Both products merely pass shared AA settings. No background services, schedules, parallel rule engine, model inference, DOM filtering or new CIR engine were added.

## Reproduction

From the Aetheris root, with the explicitly supplied Copeland compiler checkout and a Playwright module:

```powershell
./scripts/prepare-telos-temporal-policy.ps1 -CopelandRoot <CopelandRoot>
./scripts/prepare-three-telos-cir.ps1 -CopelandRoot <CopelandRoot>
npm --prefix Aetheris.Web.Runtime/telos test
npm --prefix Aetheris.Web.Runtime/telos run build:witness
node scripts/qualify-telos-taa.mts <absolute-playwright-index.mjs>
node scripts/qualify-telos-taa.mts <absolute-playwright-index.mjs> --fallback-only
```

The mechanical-model phase reuses `artifacts/local/three-telos/x1/ctc03-display.json` from `scripts/qualify-three-telos-x1.mts`; regenerate that canonical display packet first on a clean machine. Its source is the tracked CTC03 STEP under `docs/development/milestones/modules/sheetmetal/artifacts/ctc03-manufacturing-release/`. All raw logs, runtime probes and captures default to ignored `artifacts/local/`.

## Acceptance status

Language audit, bounded utility lowering, shared history ownership, render-only jitter, camera reprojection, baseline/utility policies, clamp, rejection, debug modes, CIR/mesh/mixed rendering, conservative moving-object rejection and developer product controls exist and have real-path evidence. Relevant compiler/kernel/frontend regressions pass as scoped above.

**Withhold Accepted**: useful silhouette AA against the spatial reference fails; superior ghosting is not established; temporal topology occlusion/product PMI interaction and object-motion reprojection remain unqualified; Helios production AOT packaging is absent. These boundaries are explicit, and the default spatial viewport remains preserved. The next action is the isolated fractional-coverage/confidence repair, not more history-weight tuning.
