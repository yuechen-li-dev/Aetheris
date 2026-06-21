# Firmament V2 AP242 MVP phase closeout audit

## 1. Purpose

This is the phase closeout audit for the current Firmament V2 AP242 MVP push. It grades the current repository against the MVP readiness contract in `docs/mvp/firmament-v2-ap242-mvp-readiness-contract.md` and distinguishes real AP242 `step-verified` fixtures from parsed, semantic-lowered, trace-only, or design-only evidence.

This document is an audit only. It does not implement new behavior, change product behavior, or modify parser, lowering, kernel, fixture behavior, or exporter semantics.

## 2. Verification method

The audit was performed by inspecting:

- the MVP readiness contract: `docs/mvp/firmament-v2-ap242-mvp-readiness-contract.md`;
- Firmament V2 fixture directories under `fixtures/FirmamentV2`;
- implementation notes for STEP-V2-A1, STEP-V2-X1, STEP-V2-X2, STEP-V2-X3, STEP-V2-X4, STEP-V2-X5, STEP-V2-X6, STEP-V2-X7, and SIDEHOLE-REAL-X1 under `docs/implementation`;
- current fixture metadata comments, including `fixture-id`, `expected-stage`, `current-stage`, `tier`, `mvp-required`, topology/volume fields, and diagnostic fields;
- relevant pipeline tests in `Aetheris.CLI.Tests`, especially the Firmament V2 AP242 build/export/reimport tests.

Validation commands are recorded in [12. Validation commands](#12-validation-commands).

This audit treats `step-verified` as requiring all applicable real-path evidence:

- real build command;
- real `BrepBody`;
- real `Step242Exporter`;
- emitted AP242 file with topology markers;
- reimport where required;
- topology/evidence and volume verification;
- deterministic diagnostics where invalid.

## 3. Stage taxonomy reminder

The MVP readiness contract uses this stage taxonomy:

```text
parsed
semantic-lowered
air-materialized
brep-built
step-emitted
step-roundtrip
step-verified
```

Only `step-verified` counts as MVP-ready.

Invalid fixtures may instead prove `deterministic rejection`: a real build path rejects invalid source with a stable, precise diagnostic rather than emitting successful geometry.

## 4. Tier-by-tier MVP scoreboard

| Tier | Scope | Required for current MVP? | Status | Evidence | Notes |
| --- | --- | --- | --- | --- | --- |
| Tier 0 | Pipeline wiring sanity | Yes | Complete | `pipeline-v2-box-step-verified` exists and is tested by STEP-V2-A1 build/export/reimport/volume coverage. | Proves the V2 Box vertical slice reaches AP242 through the real build path. |
| Tier 1 | Primitives reaching real geometry | Yes for Box, Cylinder, Cone/frustum, Sphere, Torus | Complete for current MVP | Box plus `primitive-v2-cylinder-step-verified`, `primitive-v2-cone-step-verified`, `primitive-v2-sphere-step-verified`, and `primitive-v2-torus-step-verified` exist with `expected-stage: step-verified`. | Triangular/hexagonal prism and StandardLibrary primitives were not required Tier 1 MVP scope and remain deferred/out-of-scope rather than failed. |
| Tier 2 | Single semantic features | Yes for shaft through, shaft blind/depth, counterbore, countersink, locked side-hole | Complete for bounded scope | Four semantic hole fixtures plus `feature-v2-side-hole-step-verified` exist and are marked/currently verified. | Arbitrary-axis side-hole generalization remains deferred; SIDEHOLE-REAL-X1 is the locked +X -> -X path. |
| Tier 3 | Composition primitives | Yes for with size override, chained with twice, face alias consumed by semantic hole | Complete for required scope | `derivation-v2-with-size-override-step-verified`, `derivation-v2-with-chained-twice-step-verified`, and `semanticref-v2-expose-face-alias-resolves-in-step` exist. | Edge-loop alias remains deferred/blocked until there is a real AP242-producing consuming operation. |
| Tier 4 | Multi-feature composition | Yes for simple admitted cases and deterministic overlap rejection | Complete for bounded scope | Two independent holes, adjacent non-overlapping holes, overlapping-hole deterministic rejection, and derived variant plus hole fixtures exist. | This does not claim general feature overlap resolution. |
| Tier 5 | Validation / DFM enforcement | Yes if DFM/concept enforcement is claimed | Complete for narrow MVP enforcement | `template-v2-cnc-min-tool-radius-enforced` and `template-v2-concept-unit-mismatch-rejected-at-build` exist as deterministic rejections. | Enforcement is intentionally narrow; no full Forge runtime, standards library, or broad DFM claim is implied. |
| Tier 6 | Minimal PMI | Optional unless demo/pitch claims PMI support | Complete for semantic-only optional scope | `pmi-v2-hole-diameter-callout-emits-in-step` and `pmi-v2-datum-plane-emits-in-step` exist. | Tier 6 remains optional for MVP unless the demo/pitch claims PMI support; evidence is semantic AP242 PMI only, not graphical PMI. |

## 5. Step-verified fixtures

Current phase step-verified fixture count: **18**.

| Fixture ID | Path | Tier | Feature area | Expected volume / topology evidence | Verification evidence | Implementation milestone |
| --- | --- | ---: | --- | --- | --- | --- |
| `pipeline-v2-box-step-verified` | `fixtures/FirmamentV2/Primitive/valid/pipeline-v2-box-step-verified.valid.firmfixture` | 0 | pipeline/box | Volume `480`; faces=6, vertices=8, edges=12 | STEP file contains topology markers, reimports as canonical box, and volume matches. | STEP-V2-A1 |
| `primitive-v2-cylinder-step-verified` | `fixtures/FirmamentV2/Primitive/valid/primitive-v2-cylinder-step-verified.valid.firmfixture` | 1 | analytic primitives | Volume `π * 2^2 * 10`; canonical faces=3 | STEP topology markers, reimport, face count, and exact volume. | STEP-V2-X1 |
| `primitive-v2-cone-step-verified` | `fixtures/FirmamentV2/Primitive/valid/primitive-v2-cone-step-verified.valid.firmfixture` | 1 | analytic primitives | Frustum volume `(πh/3)(r1² + r1r2 + r2²)` with `r1=3`, `r2=1`, `h=10`; canonical faces=3 | STEP topology markers, reimport, face count, and exact volume. | STEP-V2-X1 |
| `primitive-v2-sphere-step-verified` | `fixtures/FirmamentV2/Primitive/valid/primitive-v2-sphere-step-verified.valid.firmfixture` | 1 | analytic primitives | Volume `(4/3)π5³`; canonical faces=1 | STEP topology markers, reimport, face count, and exact volume. | STEP-V2-X1 |
| `primitive-v2-torus-step-verified` | `fixtures/FirmamentV2/Primitive/valid/primitive-v2-torus-step-verified.valid.firmfixture` | 1 | analytic primitives | Volume `2π² * 8 * 2²`; canonical faces=1 | STEP topology markers, reimport, face count, vertex evidence, and exact volume. | STEP-V2-X1 |
| `feature-v2-shaft-hole-through-step-verified` | `fixtures/FirmamentV2/Hole/valid/feature-v2-shaft-hole-through-step-verified.valid.firmfixture` | 2 | semantic hole | Volume `480 - π * 1² * 6`; cylindrical-wall evidence | Real build/export/reimport, topology markers, cylinder surface count, and analytic volume. | STEP-V2-X2 |
| `feature-v2-shaft-hole-blind-step-verified` | `fixtures/FirmamentV2/Hole/valid/feature-v2-shaft-hole-blind-step-verified.valid.firmfixture` | 2 | semantic hole | Volume `480 - π * 1² * 3`; cylindrical wall and blind bottom evidence | Real build/export/reimport, topology markers, cylinder surface count, and analytic volume. | STEP-V2-X2 |
| `feature-v2-counterbore-step-verified` | `fixtures/FirmamentV2/Hole/valid/feature-v2-counterbore-step-verified.valid.firmfixture` | 2 | semantic hole | Volume `480 - (π * 1² * 6 + π * (2² - 1²) * 1)`; shaft and counterbore cylinders | Real build/export/reimport, topology markers, cylinder surface count, and analytic volume. | STEP-V2-X2 |
| `feature-v2-countersink-step-verified` | `fixtures/FirmamentV2/Hole/valid/feature-v2-countersink-step-verified.valid.firmfixture` | 2 | semantic hole | Volume subtracts shaft plus conical entry; shaft cylinder and countersink cone | Real build/export/reimport, topology markers, cylinder/cone surface evidence, and analytic volume. | STEP-V2-X2 |
| `feature-v2-side-hole-step-verified` | `fixtures/FirmamentV2/Region/valid/feature-v2-side-hole-step-verified.valid.firmfixture` | 2 | locked side-hole | Volume `10*8*6 - π*1²*10`; +X entry to -X exit; closed integrated shell | Real AP242 through `Step242Exporter`, topology smoke, reimport, and volume analysis for the locked golden path. | SIDEHOLE-REAL-X1 |
| `derivation-v2-with-size-override-step-verified` | `fixtures/FirmamentV2/RecordDerivation/valid/derivation-v2-with-size-override-step-verified.valid.firmfixture` | 3 | derivation | Volume `576`; faces=6, vertices=8, edges=12 | Real build/export/reimport and volume proves selected derived dimensions. | STEP-V2-X3 |
| `derivation-v2-with-chained-twice-step-verified` | `fixtures/FirmamentV2/RecordDerivation/valid/derivation-v2-with-chained-twice-step-verified.valid.firmfixture` | 3 | derivation | Volume `672`; faces=6, vertices=8, edges=12 | Real build/export/reimport and volume guards against stale chained derivation state. | STEP-V2-X3 |
| `semanticref-v2-expose-face-alias-resolves-in-step` | `fixtures/FirmamentV2/SemanticRefs/valid/semanticref-v2-expose-face-alias-resolves-in-step.valid.firmfixture` | 3 | semantic reference | Volume `461.15044407846124`; cylindrical-wall evidence; alias `top` resolves to `face(+Z)` | Later semantic hole consumes exposed face alias before AP242 export; topology and volume verify the result. | STEP-V2-X3 |
| `composite-v2-two-independent-holes-step-verified` | `fixtures/FirmamentV2/Composite/valid/composite-v2-two-independent-holes-step-verified.valid.firmfixture` | 4 | multi-feature composition | Volume `442.3008881569225`; cylindrical-wall-face-count=2 | Real build/export/reimport, two independent shaft holes, topology markers, and analytic volume. | STEP-V2-X4 |
| `composite-v2-adjacent-non-overlapping-holes-step-verified` | `fixtures/FirmamentV2/Composite/valid/composite-v2-adjacent-non-overlapping-holes-step-verified.valid.firmfixture` | 4 | multi-feature composition | Volume `442.3008881569225`; two radius-1 holes separated by 2.5 | Real build/export/reimport verifies admitted adjacent non-overlap without conflict resolution. | STEP-V2-X4 |
| `composite-v2-hole-plus-derived-variant-step-verified` | `fixtures/FirmamentV2/Composite/valid/composite-v2-hole-plus-derived-variant-step-verified.valid.firmfixture` | 4 | derivation + semantic hole | Volume `12 * 8 * 6 - π * 1² * 6`; cylindrical-wall-face-count=1 | Real build/export/reimport proves selected derived Box dimensions compose with a semantic shaft hole. | STEP-V2-X5 |
| `pmi-v2-hole-diameter-callout-emits-in-step` | `fixtures/FirmamentV2/PMI/valid/pmi-v2-hole-diameter-callout-emits-in-step.valid.firmfixture` | 6 | semantic PMI | Volume `480 - π * 1² * 6`; semantic diameter metadata; no graphical PMI required | Real AP242 includes semantic hole diameter PMI metadata and geometry remains verified. | STEP-V2-X7 |
| `pmi-v2-datum-plane-emits-in-step` | `fixtures/FirmamentV2/PMI/valid/pmi-v2-datum-plane-emits-in-step.valid.firmfixture` | 6 | semantic PMI | Volume `480`; semantic datum metadata; topology markers | Real AP242 includes semantic datum plane metadata and no graphical PMI requirement. | STEP-V2-X7 |

## 6. Deterministic rejection fixtures

Current deterministic rejection fixture count: **3**.

| Fixture ID | Path | Tier | Diagnostic | What it proves | Implementation milestone |
| --- | --- | ---: | --- | --- | --- |
| `composite-v2-overlapping-holes-rejected-with-clear-diagnostic` | `fixtures/FirmamentV2/Composite/invalid/composite-v2-overlapping-holes-rejected-with-clear-diagnostic.invalid.firmfixture` | 4 | `firmament-v2-semantic-hole-overlap` | Same-face same-axis circular shaft hole overlap rejects deterministically instead of pretending general overlap resolution exists. | STEP-V2-X4 |
| `template-v2-cnc-min-tool-radius-enforced` | `fixtures/FirmamentV2/Templates/invalid/template-v2-cnc-min-tool-radius-enforced.invalid.firmfixture` | 5 | `firmament-v2-dfm-minimum-tool-radius-violation` | Build-time CNC minimum tool radius enforcement rejects a semantic shaft hole below the declared radius. | STEP-V2-X6 |
| `template-v2-concept-unit-mismatch-rejected-at-build` | `fixtures/FirmamentV2/Templates/invalid/template-v2-concept-unit-mismatch-rejected-at-build.invalid.firmfixture` | 5 | `firmament-v2-dfm-concept-unit-mismatch` | Template/concept metadata can parse, but build enforcement rejects minimum tool radius declared with angular units. | STEP-V2-X6 |

## 7. Optional/deferred fixtures and features

Deferred does not mean failed. Deferred means not part of this MVP phase claim.

- arbitrary-axis side-hole beyond the locked +X -> -X path;
- edge-loop alias consumed by an AP242-producing operation;
- patterns / hole groups;
- chamfer / fillet / draft V2 source hooks as MVP AP242 features;
- triangular/hexagonal prism V2 syntax as MVP AP242 Tier 1 scope;
- StandardLibrary primitives such as `RoundedCornerBox`, `SlotCut`, and `LibraryPart` as MVP AP242 fixtures;
- full Forge runtime / dynamic NuGet loading;
- standards / fit libraries;
- thread/tap geometry;
- drill-tip geometry;
- `upToFace` / `upToNext`;
- general feature overlap resolution;
- general raw 3D Boolean authoring;
- graphical PMI;
- DisplayIR PMI;
- DFM beyond the two Tier 5 checks above.

## 8. Current honest MVP claim

Firmament V2 now has a real AP242 MVP vertical slice for bounded CNC/prismatic-style authoring:

- primitive solids: Box, Cylinder, Cone/frustum, Sphere, and Torus;
- semantic shaft, counterbore, and countersink holes;
- source derivation and face aliases consumed by later semantic features;
- simple multi-hole composition with deterministic overlap rejection;
- derived variant plus semantic hole composition;
- narrow DFM/concept build-time rejection for minimum tool radius and concept unit mismatch;
- semantic-only AP242 PMI for hole diameter and datum plane;
- the locked legacy +X -> -X side-hole golden path rerouted through the real exporter.

Every claimed fixture either emits real AP242 through `Step242Exporter` and reaches `step-verified`, or rejects deterministically with a precise diagnostic.

## 9. Claims that remain forbidden

Do not claim:

- V2 supports arbitrary side holes.
- V2 supports arbitrary feature Booleans.
- V2 supports patterns/hole groups.
- V2 supports graphical PMI.
- V2 supports DisplayIR PMI.
- V2 supports full GD&T.
- V2 supports full CNC DFM.
- V2 supports thread/tap geometry.
- V2 supports drill-tip geometry.
- V2 supports `upToFace` / `upToNext` end conditions.
- V2 supports fillet/chamfer/draft source hooks as MVP AP242 features unless separately verified.
- V2 can import/reconstruct arbitrary STEP as Firmament source.
- Forge supports NuGet extension loading.
- StandardLibrary primitives are Firmament V2 semantic concept packs.

## 10. Remaining known risks

- AP242 verification is strong for these fixtures, not proof of all possible dimensions, placements, or feature combinations.
- Exact topology counts are Aetheris canonical evidence, not universal CAD topology assumptions.
- The side-hole real path is locked/narrow and not generalized beyond the existing +X -> -X golden path.
- DFM enforcement is intentionally narrow and limited to the two Tier 5 checks.
- PMI is semantic-only; graphical PMI, annotation layout, leaders, drawing views, and DisplayIR PMI remain out of scope.
- Multi-feature composition supports admitted non-overlap cases and deterministic overlap rejection, not general conflict resolution.
- V2 still uses a compatibility bridge to the existing lowered primitive executor; future V1 sunset needs planned migration.
- Some current StandardLibrary/Forge geometry helpers remain old-style and are not yet semantic concept packs.
- The current fixture suite proves representative MVP readiness, not general CAD completeness or arbitrary STEP reconstruction.

## 11. Recommended pause criteria

This phase is reasonable to pause if validation remains green because:

- all current required MVP tiers have passing evidence;
- optional/deferred items are explicitly documented;
- no fixture marked `step-verified` was found to be fake, trace-only, or design-only;
- build/test validation passed in this audit run.

The next phase should be chosen only after pause, and should pick one focused direction rather than expanding all scopes at once:

- patterns/hole groups;
- V2 edge finish source hooks;
- StandardLibrary/Forge semantic migration;
- side-hole generalization;
- additional primitive/library wiring;
- import/decompilation MVP.

This closeout does not scope or implement those next-phase items.

## 12. Validation commands

Commands run for this audit:

```bash
dotnet restore Aetheris.slnx
dotnet build Aetheris.slnx -f net10.0 --no-restore /m:1
dotnet test Aetheris.slnx -f net10.0 --no-build --filter "STEP-V2|pipeline-v2|primitive-v2|feature-v2|derivation-v2|semanticref-v2|composite-v2|template-v2|pmi-v2|SIDEHOLE|FirmamentV2|AP242|Build"
dotnet run --project Aetheris.CLI -- --help
git diff --check
git status --short
```

Results from the closeout run:

- `dotnet restore Aetheris.slnx`: passed.
- `dotnet build Aetheris.slnx -f net10.0 --no-restore /m:1`: passed with existing nullable/analyzer warnings and no errors.
- `dotnet test Aetheris.slnx -f net10.0 --no-build --filter "STEP-V2|pipeline-v2|primitive-v2|feature-v2|derivation-v2|semanticref-v2|composite-v2|template-v2|pmi-v2|SIDEHOLE|FirmamentV2|AP242|Build"`: passed.
- `dotnet run --project Aetheris.CLI -- --help`: passed.
- `git diff --check`: passed.
- `git status --short`: showed only this docs-only closeout document and the readiness-contract forward link after removing a generated client build directory created by validation.

## 13. Non-goals

- No implementation changes.
- No fixture behavior changes.
- No parser/lowering/exporter changes.
- No test weakening.
- No new features.
- No product behavior changes.
