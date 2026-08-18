# Aetheris V2.0 Phase 3 AIR chamfer MVP scope contract

Milestone: **PHASE3-L0**; bounded implementation completed by **AIR-CHAMFER-PRODUCTION-M1**

Implementation note (2026-08-03): the PascalCase `Box` plus `Modify`/`EdgeFinish` source form described here is now implemented for the exact `Face: +Z`, `Target: Boundary`, uniform equal-distance domain. The final path uses Feature AIR, ordered-profile Construction AIR, an authoritative shared topology realization plan, real changed BRep geometry, AP242 export, and immediate reimport verification. See `docs/development/implementation/air-chamfer-production-m1.md`. Broader chamfer claims remain out of scope.

This document opens Aetheris V2.0 Phase 3 with a documentation-only scope contract for productionizing the AIR chamfer MVP.

It does not add parser behavior, AST nodes, AIR nodes, BRep materialization changes, STEP/AP242 export changes, tests, or fixture rewrites.

Phase 1 closed the manufacturing-intent/AP242 path:

```text
existing STEP/AP242 model
  -> Firmament semantic overlay
  -> typed/toleranced manufacturing values
  -> Forge concept applications
  -> record-shaped PMI
  -> validation report
  -> AP242 datum/diameter export with evidence
```

Phase 2 established the interop doctrine:

```text
Firmament remains data.
C# owns logic.
Forge is the interop boundary.
```

Phase 3 reopens a narrow modeling/materialization path:

```text
Firmament source
  -> AIR node
  -> bounded prismatic BRep materializer
  -> STEP/AP242 artifact
```

The Phase 3 goal is not general CAD modeling. The goal is to productionize one bounded, demonstrable AIR chamfer path.

## 1. Executive summary

Aetheris V2.0 Phase 3 productionizes the AIR chamfer MVP.

This is a narrow modeling/materialization phase. It proves Aetheris can construct real BRep topology, not only annotate existing STEP. The MVP is bounded to prismatic chamfer operations, and face-boundary chamfer comes first.

Phase 3 is not general CAD modeling.
Phase 3 is not arbitrary BRep surgery.
Phase 3 is not a full fillet/chamfer framework.
Phase 3 is a bounded AIR materialization proof for prismatic chamfer operations.

The first production target is:

```text
Box/prismatic body + top face boundary chamfer -> BRepBody -> STEP/AP242 artifact
```

The first production target should prefer face-boundary chamfer because the existing prototype already maps cleanly:

```text
PrismaticTopFaceLoopChamferPrototype
  + FaceBoundaryLoop selection
  + chamfer distance
  -> BrepBody
```

Named single-edge chamfer by face pair is a follow-up slice, not the first slice, unless A0 proves it is already trivial to promote without widening scope.

## 2. Why chamfer, why now?

A single robust chamfer path is a serious geometric-kernel credibility proof.

Chamfer is strategically useful because it touches real topology rather than only metadata. A valid chamfer path creates or updates faces, edges, loops, and coedges. It proves AIR can materialize geometry through a bounded constructive route and complements the completed Phase 1 and Phase 2 manufacturing-intent/AP242 path.

This contract does not claim a complete CAD kernel. It claims one bounded proof that Aetheris can go from authored modeling intent to real topology and then to a deterministic STEP/AP242 artifact.

## 3. Existing prototype status

Current known pieces in the repository are:

| Prototype or seam | Actual repo location | What it does | Returns `BrepBody` | STEP output proven | Stability/hash evidence | Status |
| --- | --- | --- | --- | --- | --- | --- |
| `AirChamferRealBodyPrototype` | `Aetheris.Firmament.FrictionLab/CIRLab/AirChamferRealBodyPrototype.cs` | Takes a real `BrepBody` plus target edge start/end and adjacent face normals, runs Judgment-backed admission via the convex planar prototype chain, and can produce a candidate body plus topology summary. | yes, as `CandidateBody` on success | yes, through closed-witness step smoke summary | indirect stability only through the shadow/artifact corpus lane | prototype-only, non-authoritative |
| `AirChamferStepArtifactLab.WriteControlledCubeOneEdgeStep` | `Aetheris.Firmament.FrictionLab/CIRLab/AirChamferStepArtifactLab.cs` | Writes a controlled one-edge AirChamfer STEP artifact through `AirChamferShadowRoute -> AirChamferRealBodyPrototype`. | yes, via shadow candidate before export | yes | yes, corpus/stability evidence exists in EDGE-X11 and EDGE-X12 | prototype/lab-only |
| `PrismaticTopFaceLoopChamferPrototype` | `Aetheris.Kernel.Core/Brep/Prismatic/PrismaticTopFaceLoopChamferPrototype.cs` | Validates a top-face outer boundary-loop chamfer request for a rectangular prism, builds a three-section prismatic stack, and emits a constructive body through `PrismaticSectionTransitionEmitter`. | yes, as `FaceLoopChamferResult.Body` | yes, when `ExportStep: true` | deterministic topology/step checks exist in kernel and lab tests; no separate repeated-run hash corpus found for this route | strongest Phase 3 MVP candidate |
| `PrismaticTopEdgeChamferPrototype` | `Aetheris.Kernel.Core/Brep/Prismatic/PrismaticTopEdgeChamferPrototype.cs` | Emits a controlled single top-edge chamfer for the `TopPositiveXSide` rectangular-prism case through the same prismatic section-transition lane. | yes, as `PrismaticTopEdgeChamferResult.Body` | yes, when `ExportStep: true` | deterministic topology/step checks exist in kernel tests | production-adjacent prototype, but narrower and more selection-specific than the face-loop route |
| `FaceLoopChamferSelection` | `Aetheris.Kernel.Core/Brep/Prismatic/PrismaticTopFaceLoopChamferPrototype.cs` | Actual repo type exists as the `FaceLoopChamferSelection` record, with enum-like selection state carried by `FaceLoopChamferSelectionKind`. Default path is `FaceBoundaryLoop`. | n/a | n/a | n/a | real selection input type for the first MVP |
| `PrismaticTopEdgeChamferSelection` | `Aetheris.Kernel.Core/Brep/Prismatic/PrismaticTopEdgeChamferPrototype.cs` | Enum selecting the single-edge lane. Current admitted production-adjacent case is `TopPositiveXSide`; others reject. | n/a | n/a | n/a | follow-up-oriented selector, not first MVP target |
| `SelectAirChamferExperimentalRouteOrLegacy` | `Aetheris.Kernel.Firmament/Execution/FirmamentPrimitiveExecutor.cs` | Current gated chooser between legacy bounded chamfer output and an opt-in experimental AIR candidate provider. | yes, returns selected body through internal wrapper | not by itself | no separate stability proof; depends on candidate provider | keep as gate/fallback seam, not MVP authority by default |
| `AirRouteSelector` | `Aetheris.Kernel.Core/Air/AirRouteSelection.cs` | AIR-side bounded route decision seam that currently admits the top-face-loop chamfer class and rejects arbitrary graph edge finishes. | no | no | deterministic tests exist | useful Phase 3 route doctrine seam |
| `AirTopFaceLoopChamferWrapper` | `Aetheris.Kernel.Core/Air/AirWrappers.cs` | Lowers a canonical AIR top-face-loop chamfer case through `PrismaticTopFaceLoopChamferPrototype` and preserves AIR provenance/summary metadata. | yes, indirectly via wrapped result summary source | yes, via wrapped step summary | deterministic tests exist | closest AIR-facing bridge for the first production slice |
| `AirChamferCorpusStability_Repeated_Cli_Runs_Produce_Stable_Json_Markers_Topology_And_Step_Hashes` | `Aetheris.CLI.Tests/AirChamferCorpusStabilityTests.cs` | Repeated-run gated stability check for the experimental artifact corpus. | n/a | yes | yes, explicit repeated-run STEP hash and JSON stability gate | evidence-only; does not imply production authority |

Important naming note:

- The requested name `FaceLoopChamferSelection` does exist, but it is a record, not the enum-like selector by itself.
- The enum-like values for that route are `FaceLoopChamferSelectionKind`, `FaceLoopChamferOwningFaceKind`, `FaceLoopChamferLoopKind`, and `FaceLoopChamferRuleKind`.
- No missing-name gap was found for the other requested search targets.

Current production interpretation:

- The face-boundary loop prototype is the most directly usable Phase 3 MVP seed because it already expresses the preferred target: top face boundary loop plus uniform distance produces a `BrepBody`.
- The single-edge prismatic prototype is real and useful, but it is a narrower selection seam and should not pull Phase 3 L0 into face-pair edge resolution unless A0 proves that promotion is trivial.
- The `AirChamferRealBodyPrototype` and STEP artifact corpus are valuable evidence, but today they remain experimental or shadow-route proof rather than the first production authority.

## 4. Firmament casing doctrine for Phase 3

Phase 3 should introduce a C#-convention Firmament style for new modeling syntax:

```text
Block/declaration keywords: PascalCase
Named objects: PascalCase by default
Properties/fields: PascalCase
Enum-like values: PascalCase
Physical units: lowercase suffixes, e.g. mm, deg
Axis literals: +X, -Z
```

Preferred Phase 3 style:

```firmament
Model Bracket mm

Box Base {
    Size: [80mm, 50mm, 25mm]
}

Modify Base {
    EdgeFinish TopBreak {
        Face: +Z
        Target: Boundary
        Kind: Chamfer
        Distance: 1.5mm
    }
}
```

Preferred over:

```firmament
model bracket mm

box base { size: [80, 50, 25] }

modify base {
    edge_finish topBreak {
        face: +Z
        target: boundary
        kind: chamfer
        distance: 1.5
    }
}
```

Rationale:

- Aetheris is C#-native.
- Phase 2 made C# the logic surface.
- PascalCase aligns source declarations with the host ecosystem.
- New modeling syntax should look deliberate, not like a loose macro DSL.

This casing doctrine applies to new Phase 3 modeling syntax first. Do not rewrite old fixtures or Phase 1/2 syntax globally in L0.

## 5. MVP syntax target

The intended first syntax target is illustrative only in L0. It is not implemented by this milestone.

```firmament
Model Bracket mm

Box Base {
    Size: [80mm, 50mm, 25mm]
}

Modify Base {
    EdgeFinish TopBreak {
        Face: +Z
        Target: Boundary
        Kind: Chamfer
        Distance: 1.5mm
    }
}
```

Meaning:

```text
Chamfer the boundary loop of the +Z face of Base by 1.5mm.
```

Field semantics:

- `Face: +Z` selects the face direction.
- `Target: Boundary` selects the face boundary loop.
- `Kind: Chamfer` identifies the edge finish type.
- `Distance: 1.5mm` gives symmetric chamfer distance.

## 6. Follow-up syntax target: named face-pair edge chamfer

Deferred follow-up target:

```firmament
Modify Base {
    EdgeFinish LongEdgeBreak {
        Faces: [+X, +Z]
        Kind: Chamfer
        Distance: 1.5mm
    }
}
```

Meaning:

```text
Chamfer the edge shared by the +X and +Z faces.
```

This is follow-up because it requires edge selection by face-pair resolution. It must not block the first face-boundary MVP.

## 7. Explicitly deferred syntax: feature entry chamfer

Deferred:

```firmament
Modify Base {
    EdgeFinish HoleEntry {
        Feature: LeftHole
        Target: EntryEdge
        Kind: Chamfer
        Distance: 0.5mm
    }
}
```

Reason:

- semantic hole-entry chamfer overlaps with existing chamfered-entry hole variants;
- it requires feature-level edge resolution;
- it is not needed for the first AIR chamfer proof.

## 8. Expected implementation ladder

Initial milestone ladder:

```text
AIR-CHAMFER-A0:
  Audit existing FrictionLab/AIR chamfer prototypes, STEP artifact labs, gates, and tests.

AIR-CHAMFER-A1:
  Add AST/parser support for PascalCase Phase 3 EdgeFinish syntax inside Modify blocks.

AIR-CHAMFER-A2:
  Lower EdgeFinish Face:+Z Target:Boundary Kind:Chamfer into an AIR chamfer node.

AIR-CHAMFER-A3:
  Materialize the AIR chamfer node through PrismaticTopFaceLoopChamferPrototype.

AIR-CHAMFER-A4:
  Add STEP smoke/stability tests for V2 syntax -> AIR -> BRep -> STEP.

AIR-CHAMFER-A5:
  Add demo fixture: Box + holes + top face boundary chamfer -> STEP/AP242.
```

A0 should refine this ladder if the current repo structure reveals a cleaner split. The governing rule is that the ladder must stay bounded to the first production target rather than broadening into general chamfer infrastructure.

## 9. AIR/BRep materialization boundary

Phase 3 MVP boundary:

- input body shape for the MVP is a box or equivalent rectangular prismatic body;
- body dimensions must be extracted from existing build/model state or an equivalent canonical model state;
- the materializer should call the proven prismatic prototype seam;
- the result must be a `BrepBody`;
- the STEP artifact must be stable and deterministic for the admitted MVP fixture;
- the legacy path may remain as fallback outside the admitted MVP route.

Phase 3 must not require:

- arbitrary imported STEP geometry chamfering;
- arbitrary topology selection;
- a full BRep Boolean or editing framework;
- blend or fillet surfaces;
- multi-distance chamfers;
- asymmetric chamfers;
- vertex or chamfer networks.

## 10. Gating and fallback policy

Current gate seam:

```text
SelectAirChamferExperimentalRouteOrLegacy
```

Policy:

- Phase 3 should promote the AIR route only for the bounded box-root plus face-boundary chamfer case.
- Legacy route remains fallback outside that admitted case.
- Failure should produce explicit diagnostics.
- Phase 3 should not silently fallback when that would hide a regression in the admitted MVP route.
- Stability tests should assert that the AIR route is actually used for the MVP fixture.

The current experimental gate in `FirmamentPrimitiveExecutor` is the right policy shape: explicit opt-in, explicit supported-case admission, explicit candidate rejection, explicit legacy fallback. Phase 3 should narrow and harden that behavior for the first production slice rather than widening it.

## 11. Success criteria

Phase 3 MVP is successful when a Firmament document equivalent to:

```firmament
Model Bracket mm

Box Base {
    Size: [80mm, 50mm, 25mm]
}

Modify Base {
    EdgeFinish TopBreak {
        Face: +Z
        Target: Boundary
        Kind: Chamfer
        Distance: 1.5mm
    }
}
```

can produce:

- a `BrepBody`;
- deterministic STEP/AP242 output;
- a valid topology summary;
- visible chamfer evidence in build or analyze reporting, if available;
- no legacy-only fallback claim for the admitted MVP route.

Stretch demo:

```firmament
Model Bracket mm

Box Base {
    Size: [80mm, 50mm, 25mm]
}

Modify Base {
    Hole H1 {
        Type: Shaft
        On: Face(+Z)
        Center: [15mm, 25mm]
        Diameter: 8.5mm
        End: Through
    }

    Hole H2 {
        Type: Shaft
        On: Face(+Z)
        Center: [65mm, 25mm]
        Diameter: 8.5mm
        End: Through
    }

    EdgeFinish TopBreak {
        Face: +Z
        Target: Boundary
        Kind: Chamfer
        Distance: 1.5mm
    }
}
```

The stretch demo must not block the first MVP.

## 12. Non-goals

Phase 3 explicitly defers:

- general CAD modeling;
- arbitrary BRep surgery;
- arbitrary imported STEP chamfering;
- general edge selection;
- full fillet framework;
- full chamfer framework;
- multi-edge arbitrary chain selection;
- asymmetric chamfers;
- variable chamfers;
- vertex blends;
- robust face-healing framework;
- feature-history recovery;
- automatic feature recognition;
- hole-entry chamfer syntax;
- modifying the Phase 1 or Phase 2 PMI workflow;
- changing Forge or C# concept-pack behavior;
- global syntax or casing rewrite of existing Firmament fixtures.

## 13. Documentation links

Relevant prior contracts and notes:

- `docs/development/scope-contracts/v2/aetheris-v2-phase1-closeout.md`
- `docs/development/scope-contracts/v2/aetheris-v2-phase2-csharp-interop-scope-contract.md`
- `docs/development/implementation/v2-phase1-p2-record-pmi-ap242-export.md`
- `docs/development/implementation/forge-cs-a5-trusted-external-concept-packs.md`
- `docs/development/milestones/frictionlab/edge-loop-x1-top-face-loop-chamfer-prismatic-lab.md`
- `docs/development/milestones/frictionlab/edge-x11-airchamfer-step-artifact-corpus.md`
- `docs/development/milestones/frictionlab/edge-x12-airchamfer-corpus-stability.md`

This contract intentionally leaves those older documents in place. It adds the bounded Phase 3 production doctrine without rewriting Phase 1 or Phase 2 history.
