# DISPLAY-PHASE-CLOSEOUT-A1 — Frontend/display workstream boundary and Firmament V2 lowering guardrails

## 1. Purpose

This document closes the current DisplayIR/frontend stabilization phase and records the workstream boundary that should hold while the main Aetheris compiler path returns to Firmament V2 / AIR lowering.

The boundary separates three workstreams:

- **Frontend/display/viewer workstream:** owns client rendering, status presentation, viewer QA, and renderer-backend details for already-emitted view data.
- **Compiler/lowering workstream:** owns Firmament V2 source parsing, semantic lowering, AIR construction, route policy, and compiler-to-kernel intent.
- **Kernel/import/export workstream:** owns BRep topology, AP242 / STEP import and export semantics, tessellation algorithms, and backend materialization authority.

Frontend/display work must not casually change Firmament V2 source semantics. Frontend/display work must not change AIR lowering. Frontend/display work must not change AP242 import/export semantics, BRep topology, CIR authority, or tessellator algorithms. DisplayIR is a view contract, not compiler/lowering authority.

## 2. Current display architecture summary

Current intended display architecture:

```text
BRep:
  geometry/topology authority

DisplayIR:
  view authority

AetherisViewport / Three.js:
  replaceable renderer backend

BoundedMesh:
  explicit bounded mesh lowering lane

WirePatch:
  display degradation lane

DiagnosticPatch:
  explicit per-face failure lane

AnalyticPatch:
  semantic analytic display lane, currently rendered approximately in frontend preview
```

`BRep` remains the source of geometry/topology truth for imported or materialized bodies. `DisplayIR` describes what the viewer may render and how display degradation is surfaced; it is not a modeling or compiler IR. `AetherisViewport` and Three.js consume typed renderables and may be replaced without changing backend authority. `BoundedMesh`, `WirePatch`, `DiagnosticPatch`, and `AnalyticPatch` are display lanes with explicit meanings and failure/degradation behavior.

## 3. Firmament V2 / AIR lowering boundary

Firmament V2 source parsing/lowering is not part of frontend/display work.

AIR is compiler IR / topology-generating middle layer. BRep and STEP are backend/export materialization. DisplayIR does not feed back into Firmament or AIR. DisplayIR does not authorize topology. DisplayIR is not a modeling operation layer.

Protected rules:

- Firmament V2 syntax and source semantics are owned by the compiler/lowering workstream.
- AIR route policy, AIR region behavior, and topology-generating lowering decisions are owned by the compiler/lowering workstream.
- BRep topology and STEP/AP242 import/export semantics are owned by the kernel/import/export workstream.
- CIR remains an analysis/authority layer according to the AIR/CIR authority contract; frontend display state must not redefine that authority.
- DisplayIR may present, approximate, degrade, or diagnose a view, but it must not become input to feature lowering, topology generation, or export semantics.

## 4. Allowed frontend/display work without compiler approval

The following work is normally frontend/display scoped and does not require compiler approval when it only consumes already-emitted DisplayIR/API data:

- `AetherisViewport` rendering changes.
- Three.js material, shading, lighting, camera, or framing changes.
- `DisplayScene` / `DisplayRenderable` frontend mapping.
- `WirePatch` visual rendering.
- `DiagnosticPatch` markers and status presentation.
- DisplayIR inspector/status UI.
- Viewer performance improvements.
- Client tests.
- Playwright visual QA.
- Frontend lane-selection policy that only selects between already-emitted DisplayIR lanes.

These changes must remain view-only. They may alter how a user sees an already-authorized display packet, but they must not change what geometry/topology exists, how source lowers, or what STEP/BRep/CIR means.

## 5. Frontend/display work requiring explicit compiler/kernel approval

The following require explicit compiler and/or kernel approval before being mixed into frontend/display work:

- Changing DisplayIR server schema in a way that changes meaning.
- Changing server display preparation semantics.
- Changing BRep topology.
- Changing AP242 import/export.
- Changing STEP import/export semantics.
- Changing tessellator algorithms.
- Changing analytic patch admission policy.
- Changing Firmament V2 syntax.
- Changing Firmament V2 parsing or lowering.
- Changing AIR lowering.
- Changing AIR Region route policy.
- Changing CIR authority.
- Using DisplayIR as modeling/lowering input.
- Adding new CAD feature behavior.

If a display issue appears to require one of these changes, split the work into a compiler/kernel milestone or stop with evidence rather than slipping semantic changes through a viewer PR.

## 6. Regression guardrails

Frontend/display PRs should answer this checklist explicitly:

```text
Does this change STEP import/export semantics?
Does this change BRep topology?
Does this change Firmament V2 parsing/lowering?
Does this change AIR Region route policy?
Does this change CIR authority?
Does this change tessellator algorithms?
Does this change DisplayIR server authority?
Does this only affect frontend rendering/status/QA?
```

A healthy frontend-only PR should normally answer “no” to every protected compiler/kernel question and “yes” to the final frontend-only question. Any “yes” on a protected item means the PR is no longer frontend-only and needs the appropriate workstream review.

## 7. Required tests by workstream

Frontend-only display PRs should usually run:

```bash
cd aetheris.client
npm run build
npm test -- --run App.test.tsx displayRenderables.test.ts AetherisViewport.test.tsx displaySceneBuilder.test.ts displaySceneBounds.test.ts
```

If DTOs, display API shape, or server display preparation are touched, add targeted server/core smoke tests:

```bash
dotnet test Aetheris.Kernel.Core.Tests/Aetheris.Kernel.Core.Tests.csproj -f net10.0 --no-build --filter "DisplayIR|BoundedMesh|WirePatch|DisplayPrepare|ViewMaterialization|FTC07|Ftc07|Tessellation|FTC06|Ftc06" --logger "console;verbosity=minimal"

dotnet test Aetheris.Server.Tests/Aetheris.Server.Tests.csproj -f net10.0 --no-build --filter "DisplayIR|BoundedMesh|WirePatch|DisplayPrepare|FTC07|Ftc07|FTC06|Ftc06|KernelApi|ViewMaterialization|Tessellate|Tessellation|StepIo" --logger "console;verbosity=minimal"
```

Compiler/lowering PRs should run the Firmament V2 / AIR suites that exercise parsing, lowering, route policy, and semantic topology. They should not run full frontend visual QA by default unless the change also touches the DisplayIR/viewer contract.

For compiler-path guard checks around current Firmament V2 / AIR work, a useful targeted slice is:

```bash
dotnet test Aetheris.Kernel.Firmament.Tests/Aetheris.Kernel.Firmament.Tests.csproj -f net10.0 --no-build --filter "FirmamentV2|FirmamentV2SideHole|FeatureAir|ConstructiveAir" --logger "console;verbosity=minimal"
```

## 8. Recommended next compiler milestone

Recommended next compiler milestone:

```text
AIR-FIRMAMENT-X13 — reusable PrismaticThroughCut semantic lowering policy
```

Direction:

- `sideHole` remains a user-facing feature family.
- `PrismaticThroughCut` becomes the reusable internal semantic lowering concept.
- Route policy from X12 can be reused.
- Future features should lower through stable AIR/BRep semantics, not through DisplayIR.

This milestone returns the main compiler path to reusable semantic lowering rather than continuing viewer/display stabilization in the same lane.

## 9. Recommended separate frontend workstream

Frontend/display work can proceed independently under a separate workstream, for example:

- `DISPLAY-QUALITY-X2 — FTC-06/FTC-07 visual polish`.
- `DISPLAY-ARCH-X7 — DiagnosticPatch proxy/marker rendering`.
- `DISPLAY-ARCH-X8 — wire rendering polish`.
- `DISPLAY-QUALITY-X3 — material/lighting/shading pass`.
- `DISPLAY-QA-X2` follow-ups as needed.

These milestones should remain bounded to frontend rendering, status, visual QA, and view-only lane selection unless they are explicitly promoted to compiler/kernel work.

## 10. Non-goals

This closeout does not make or authorize:

- Product behavior changes.
- STEP import/export changes.
- AP242 importer/exporter behavior changes.
- BRep topology changes.
- Firmament V2 parsing or lowering changes.
- AIR changes.
- AIR Region route policy changes.
- CIR changes.
- Firmasm changes.
- Tessellator algorithm changes.
- CAD feature behavior changes.
- Backend/display fixes.
- DisplayIR schema migration unless needed for docs/tests only.
