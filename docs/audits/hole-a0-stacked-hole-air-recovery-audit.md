# HOLE-A0 — stacked semantic hole AIR experiment recovery audit

## 1. Purpose

This audit checks whether a prior stacked-hole / semantic-hole AIR experiment or implementation exists in the repository and whether any of it can be refactored into production. It is a discovery milestone only: it does not change parser, lowering, kernel, DisplayIR, STEP, frontend, or product behavior.

## 2. Search summary

Commands run:

```bash
git grep -n -i "stacked.*hole\|hole.*stack\|holefeature\|counterbore\|countersink\|clearance hole\|blind hole\|through hole\|drill tip\|tapped\|threaded" .
git grep -n -i "AIR.*Hole\|Hole.*AIR\|CylinderCut\|cut cylinder\|cylindrical" -- Aetheris.Kernel.Core Aetheris.Kernel.Firmament Aetheris.*.Tests docs Aetheris.Firmament.FrictionLab
git grep -n "record .*Hole\|class .*Hole\|enum .*Hole\|HoleRecovery\|ProfileStackExtrude\|HoleProfile" -- Aetheris.Kernel.Core Aetheris.Kernel.Firmament Aetheris.Firmament.FrictionLab Aetheris.*.Tests docs
git grep -n -i "hole" -- Aetheris.Kernel.Firmament/FirmamentV2 Aetheris.Kernel.Firmament/Parsing Aetheris.Kernel.Firmament/Lowering Aetheris.Kernel.Firmament/Air
find . -maxdepth 2 -type d | sed 's#^./##' | sort | head -80
find docs -maxdepth 3 -type f | sed 's#^#/#' | head -80
```

Key search terms included stacked hole, hole stack, `HoleFeature`, counterbore, countersink, clearance hole, blind hole, through hole, drill tip, tapped/threaded, `AIR.*Hole`, `Hole.*AIR`, `CylinderCut`, cylindrical, `HoleRecovery`, `ProfileStackExtrude`, and `HoleProfile`.

Directories inspected included `Aetheris.Kernel.Firmament/Materializer`, `Aetheris.Kernel.Firmament/FirmamentV2`, `Aetheris.Kernel.Firmament/Parsing`, `Aetheris.Kernel.Firmament/Air`, `Aetheris.Kernel.Core/Air`, `Aetheris.Firmament.FrictionLab/CIRLab`, `Aetheris.FrictionLab.Tests/CIRLab`, `Aetheris.Kernel.Firmament.Tests`, `Aetheris.Kernel.Core.Tests`, and `docs/`.

Build/test constraints: this audit intentionally touched documentation only. Per milestone validation, `git diff --check` is sufficient. No .NET build was required because no code changed.

## 3. Found artifacts

| Artifact | Classification | Notes |
| --- | --- | --- |
| `Aetheris.Kernel.Firmament/Materializer/HoleRecoveryPlan.cs` (`HoleRecoveryPlan`, `HoleProfileSegment`, hole enums, placement validator) | refactor candidate | Production code already has a semantic recovery plan with hole kind, depth kind, entry/exit feature, axis, host kind, tool/host translation, profile stack, surface patch expectations, trim expectations, capability, diagnostics, and segment placement validation. It is recovery/materializer-oriented rather than a Firmament-facing or AIR-owned semantic `HoleFeature`. |
| `Aetheris.Kernel.Firmament/Materializer/HoleRecoveryPolicy.cs` | refactor candidate | Production policy uses `JudgmentEngine` over through, blind, counterbore, chamfered-entry, countersink, and stepped variants. It is recognition/recovery from CIR subtract shapes, not an authored AIR hole node. |
| `Aetheris.Kernel.Firmament/Materializer/HoleRecoveryExecutor.cs` | refactor candidate | Executes recognized hole plans by normalizing to profile-stack / safe-boolean composition paths. It is useful as a lowering reference but should not become the semantic source model. |
| `Aetheris.Kernel.Firmament/Materializer/ProfileStackExtrudeExecutor.cs` (`ProfileStackExtrudeSpec`, `ProfileStackLayer`) | production-ready candidate for bounded executor utility; refactor candidate for semantic holes | Production executor accepts z-bounded layers with inner circle radii and roles, then builds a safe boolean composition without a generic 3D subtract route. It preserves role strings, but it is geometry/profile-stack oriented and not a semantic hole object. |
| `Aetheris.Firmament.FrictionLab/CIRLab/AirProfileStackExtrudeLab.cs` | dead experiment / test-only artifact | This is the closest prior AIR experiment: an `AirProfileStackExtrude` lab maps profile-stack specs into AIR-like layers and explicitly recommends `HoleRecoveryPlan -> ProfileStackExtrudeSpec -> AIR` while preserving diagnostics/provenance. It is in FrictionLab, not production AIR. |
| `Aetheris.Firmament.FrictionLab/CIRLab/ProfileStackExtrudeLab.cs` | test-only artifact | Demonstrates through, blind, stepped, and counterbore profile-stack cases with semantic role metadata. Useful fixture ideas, not production source. |
| `Aetheris.Firmament.FrictionLab/CIRLab/AirProfileStackIntervalLab.cs` | test-only artifact | Exercises interval/profile-stack scenarios and semantic `HoleRecoveryPlan` examples for blind and counterbore cases. Useful as corpus/evidence. |
| `Aetheris.Firmament.FrictionLab/CIRLab/HoleFamilyPolicyShapeLab.cs` | documentation/test-only artifact | Scores possible hole-family policy shapes and names through, blind, counterbore, countersink, stepped, chamfered-entry, threaded, and unknown feature kinds. It is architecture evidence, not a product path. |
| `Aetheris.Kernel.Firmament/FirmamentV2/FirmamentV2Ast.cs`, parser, and side-hole route policy | refactor candidate for placement concepts; not stacked-hole implementation | Firmament V2 has a side-hole intent for cylindrical side holes with attach/through faces, radius, center, and route evidence. It does not model stack components, end-condition families, counterbore/countersink, drill tips, or threads. |
| `Aetheris.Kernel.Core/Air/AirRegions.cs` side-hole placeholder/materialization records | unrelated cylinder/boolean path with semantic wrapper fragments | AIR region side-hole code preserves side-hole placeholders and cylindrical cut-wall evidence for controlled side-hole materialization. It is side-hole-region-specific and not a general semantic hole-stack AIR node. |
| `Aetheris.Kernel.Firmament.Tests/*Hole*`, `Aetheris.FrictionLab.Tests/CIRLab/*Hole*`, and docs such as `docs/cir-recovery-v12-hole-family-coverage-matrix.md`, `docs/cir-recovery-v13-stepped-hole-variant-executor-step.md`, `docs/cir-recovery-v14-stepped-hole-corpus-hardening.md`, `docs/cir-recovery-v19-hole-family-capability-manifest.md` | test-only / documentation-only | These provide reusable corpus, expected behavior, and capability notes. They should be mined for promotion tests after a production semantic hole scaffold exists. |
| PMI and STEP semantic hole references (`PmiCylindricalFeatureReference`, `Step242SemanticPmiHole`) | documentation-only / adjacent metadata | These preserve downstream hole-like PMI metadata but do not define authored semantic hole geometry or stacked AIR ownership. |

## 4. Existing semantic model, if any

There is no production `HoleFeature`, `HoleStack`, or equivalent AIR node found. The closest existing semantic model is `HoleRecoveryPlan`, which lives under Firmament materialization/recovery and describes recovered intent from CIR/boolean-style inputs.

Current/old representation can express:

- Hole family: through, blind, counterbore, countersink, chamfered entry, stepped, and unsupported.
- Depth/end-condition class: through, blind, through with entry relief, blind with entry relief, and unsupported.
- Entry/exit feature classes: plain, counterbore, countersink, chamfer, stepped, closed bottom, unsupported.
- A profile stack made of cylindrical, conical, chamfer, thread-deferred, or unsupported segments with radius start/end and depth/z interval metadata.
- Rectangular-box host and Z-axis only in the core recovery plan enums.
- Host/tool translations, but not face/datum identity as a stable authored anchor.
- Expected surface patches and trim curve roles for diagnostics.

Question answers:

- `HoleFeature` / `HoleStack`: no production source-facing or AIR-owned type by that name was found. `HoleRecoveryPlan` + `ProfileStackExtrudeSpec` are the closest equivalents.
- Counterbore/countersink: yes, represented in recovery variants and profile stack tests; counterbore is cylindrical entry relief, countersink/chamfered entry use conical/chamfer stack segments.
- Through/depth/blind: yes, represented by `HoleDepthKind`, `HoleTierAnchorSide`, through flags, and z-span validation.
- Placement: recovery uses host/tool translations and top/bottom/through anchors; Firmament V2 side-hole intent uses attach/through faces plus face-local center coordinates. There is no unified semantic hole datum/face placement model.
- Hole intent vs raw cylinder subtract: the recovery path distinguishes recovered semantic hole intent from raw subtracts, but it is downstream recognition. Firmament source and AIR do not yet preserve a first-class authored hole intent through lowering.
- Lowering target: existing production recovery lowers to BRep/safe-boolean composition via materializer/executor utilities. The AIR-like stacked-hole path is FrictionLab-only.
- Stack ownership: `HoleRecoveryPlan.ProfileStack` preserves stack ownership until execution. `ProfileStackExtrudeSpec` preserves layer roles. The final BRep path necessarily emits primitive analytic faces and role metadata, not a first-class retained semantic hole feature.

## 5. Production refactor viability

Verdict: **partial reuse**.

What can be kept:

- The vocabulary in `HoleRecoveryPlan`: hole kind, depth kind, entry/exit feature kind, segment kind, surface patch roles, trim roles, and diagnostics.
- The `JudgmentEngine`-based variant selection pattern in `HoleRecoveryPolicy`, especially for distinguishing bounded hole-family variants and rejection reasons.
- The `ProfileStackExtrudeSpec` / `ProfileStackLayer` executor concepts as a lower-level geometry lane for simple shaft holes and later stack components.
- The FrictionLab and kernel tests as corpus seeds for promoted semantic-hole tests.

What must be renamed or moved:

- A new production model should be named around authored intent (`HoleFeature`, `SemanticHoleFeature`, or `AirHoleFeature`), not `HoleRecoveryPlan`.
- The current recovery-specific types should remain in materializer/recovery or be split so shared enums/components live in a neutral feature/AIR namespace.
- FrictionLab `AirProfileStackExtrude` should not be moved wholesale; only its mapping insight and fixture cases should be promoted.

What is too experimental to keep directly:

- FrictionLab AIR classes and lab architecture-score experiments.
- Controlled side-hole placeholder materialization as a general semantic-hole implementation.
- Any CIR-only boolean recognition assumptions that require rectangular-box host and Z-axis only.

Tests that can be promoted later:

- Through/blind/counterbore/countersink/chamfered/stepped plan-recognition assertions as semantic model fixture tests.
- Profile-stack executor determinism and role metadata assertions.
- Negative tests for unsupported/non-coaxial/missing placement invariants.

Missing invariants:

- Stable entry-face/datum identity.
- Face-local coordinate frame and explicit axis/direction independent of rectangular boxes.
- End-condition semantics as authored intent (`throughAll`, `depth`, `upToFace`, `upToNext`) rather than inferred z intervals only.
- Stack component ownership and ordering rules before BRep/profile flattening.
- Diameter/standard class separation from raw radius.
- Pattern/group identity.
- Thread/tap metadata as metadata, not geometry.
- Clear boundary between semantic source/AIR and executable BRep profile-stack lanes.

Risks if hooked into Firmament V2 now:

- The current recovery plan can overfit to CIR subtract shape recognition and rectangular-box/Z-axis assumptions.
- Directly exposing `ProfileStackExtrudeSpec` as source/AIR would prematurely flatten authored intent into z layers and lose face/datum/end-condition semantics.
- Side-hole route support is useful placement evidence, but it is not general enough for stacked top-face holes or arbitrary datums.
- Existing executor success could be mistaken for full semantic-hole support even though counterbore/countersink/thread/wizard semantics are not source-owned.

## 6. Recommended production shape

Recommended direction:

```text
Semantic Hole Feature
  name
  entry face or datum
  center / placement
  axis / direction
  shaft diameter or standard
  end condition
    throughAll
    depth
    upToFace
    upToNext
  optional stack components
    counterbore
    countersink
    shaft
    drill tip
    mouth chamfer
    thread/tap metadata
  group / pattern identity
```

Firmament-facing source should own author intent: feature name, target body, entry face/datum selector, face-local center, axis/direction, shaft diameter or standard, end condition, and optional metadata-oriented stack components. It should not require authors to describe z-layer booleans.

AIR should own normalized semantic intent and preserve it before geometry lowering. A production AIR shape should include a semantic hole node/feature with stable identity, placement frame, axis, end condition, stack component list, diagnostics, and provenance. AIR may also include a derived/profile-stack lowering plan, but that plan should be subordinate to the semantic feature and should not replace it.

Lower BRep/kernel geometry should own executable analytic geometry: profile-stack intervals, safe-boolean composition, cylindrical/conical/chamfered wall emission, trim loops, and STEP/BRep output. This layer may consume semantic-hole AIR but should not be the source of the semantic contract.

## 7. Suggested next milestone

Suggested next milestone:

```text
HOLE-X1 — add production semantic HoleFeature AIR scaffold and simple shaft-hole lowering
```

Smallest safe scope:

- Add a production semantic `HoleFeature` / AIR scaffold without parser syntax changes unless existing syntax is only documented.
- Support only a simple semantic shaft hole.
- Support face-local placement and a single axis/direction.
- Support `throughAll` and/or fixed `depth`.
- Preserve semantic hole intent before any lowering.
- Reuse `HoleRecoveryPlan` vocabulary where clean, but avoid moving recovery-only assumptions into the authored model.
- Do not lower counterbore/countersink in production unless an existing executor path can be wrapped without changing behavior.
- Add tests proving semantic hole intent survives source-to-AIR or scaffold-to-lowering boundaries before profile/BRep flattening.

If the team instead chooses to refactor the existing recovery code first, a narrower alternate is:

```text
HOLE-X1 — refactor existing stacked-hole recovery vocabulary into production semantic HoleFeature scaffold
```

## 8. Non-goals

- No parser changes unless the audit discovers existing parser support and only documents it.
- No production lowering changes.
- No BRep behavior changes.
- No STEP behavior changes.
- No DisplayIR/frontend changes.
- No attempt to implement a full hole wizard.
- No manufacturing standard library.
- No thread geometry.
