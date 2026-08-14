# BRep Boolean internals map

## Pipeline decomposition

| Category | Current components | Architectural reading |
|---|---|---|
| facade/orchestration | `BrepBoolean`, `BooleanRequest`, analysis/intersection/classification/rebuild records | compatibility pipeline; keep stable while internals migrate |
| recognition | `BrepBooleanBoxRecognition`, `CylinderRecognition`, `AnalyticSurfaceRecognition`, `PrismaticToolRecognition`, `PrismaticProfileRecognition`, `HoleFeatureRecognition` | evidence adapters that identify bounded families; belongs above Surgery |
| policy/routing | top-level and subtract/intersect `JudgmentEngine` candidate ladders, `BooleanGuards`, footprint containment, bounded cell classification | explicit family admission/tie-breaking/rejection; recipe selection, not topology mechanics |
| history/composition | `SafeBooleanComposition`, `SafeBooleanRootDescriptor`, `SupportedBooleanHole`, `SupportedThroughVoidSet`, `SupportedBlindPrismaticPocket`, open slots/occupied cells | accumulated recognized construction history and policy state; not generic BRep topology state |
| history validation | `BrepBooleanSafeCompositionGraphValidator` and its continuation `JudgmentEngine` | determines whether a next tool belongs to a supported continuation family |
| family classification | `BrepBooleanCoaxialSubtractStackFamily`, `BrepBooleanCoaxialCountersinkSubtractFamily` | explicit recognized recipe logic |
| family topology recipes | `BoxCylinderHoleBuilder`, `CylinderOpenSlotBuilder`, `OrthogonalUnionBuilder`, `PolygonalPrismThroughCutBuilder`, box wrapper, mixed-through-void builder | expected topology is hard-coded from known family facts; preserve as recipes/examples |
| generic low-level mechanics | `TopologyBuilder`; repeated edge-use -> cyclic coedge/loop construction; vertex/edge lookup; geometry/binding assembly; ID remap/copy code | extraction candidates for Surgery, subject to narrow contracts |
| validation | `BrepBindingValidator`, `BrepBooleanSafeCompositionGraphValidator`, output checks | structural binding validation is reusable; family admissibility remains above Surgery |
| diagnostics | `BooleanDiagnostic`, `BooleanDiagnosticContext`, kernel diagnostic mapping, Judgment rejection detail | keep family-aware diagnostics at facade/recipe level; Surgery emits structural diagnostics |
| generic geometry evidence | `SignedSideQuery`, `ClosestPointQuery`, `IntersectionQuery`, `ContactQuery` and bounded overlap calculations | observational only; never result-topology authority |

## What `SafeBooleanComposition` really is

`SafeBooleanComposition` is not a general history graph of arbitrary topology edits. It records an admitted root family (box, cylinder, polygonal extrusion), analytic holes with span/axis/radii, rectangular open slots, orthogonal occupied cells, through-void sets, and one blind-prismatic-pocket form. The graph validator then decides which next family can safely continue.

Therefore:

- `SafeBooleanRootDescriptor` is recognized root/construction metadata, not a generic shell descriptor;
- `SupportedBooleanHole` is an analytic hole-recipe segment, not a generic face/loop edit;
- `SupportedThroughVoidSet` is mixed-family continuation policy;
- `SupportedBlindPrismaticPocket` is a recognized pocket recipe descriptor;
- `SafeBooleanComposition` should move toward a compatibility/recipe history envelope, not into `Brep.Surgery`.

The stepped-hole `Holes.Count == 1` historical gate proves that behavior depended on the number and ordering of recognized prior operations, not only current operand geometry.

## Family builders versus surgery candidates

| Existing code | Keep as recipe | Candidate mechanical extraction |
|---|---|---|
| box/cylinder/cone/sphere hole body creation | expected walls, entry/exit trims, bottoms, shoulders, senses | cyclic loop construction; analytic curve/surface binding; shell/body assembly; validation |
| cylinder-root open slot | known cylindrical arc, floor and radial walls | edge reuse/orientation; face-with-loop construction; binding validation |
| orthogonal union from occupied cells | boundary-cell policy and coplanar rectangle merging | indexed vertex/edge reuse; oriented loop assembly; planar binding |
| polygonal/prismatic through cut | outer/inner footprint policy and reversed inner orientation | ring extrusion topology, inner-loop insertion, surface binding |
| mixed analytic + prismatic void | exact admitted coexistence/history rules | remap/copy of topology, geometry and bindings; shell stitching/assembly |

The repeated private `AddLoop`/`AddFaceWithLoop` implementations are the clearest immediate extraction pressure. The large `BoxCylinderHoleBuilder` also repeats body assembly and geometry/binding setup, but M3 should extract mechanics incrementally rather than design a universal edit API.

## Freeze marker

The existing class summary already calls the pipeline bounded and describes narrow support. M1 records the stronger policy here rather than adding attributes that could imply runtime enforcement. M4 should add concise architectural comments at dispatcher candidate construction and family builder entry points, once their destination namespaces are settled.
