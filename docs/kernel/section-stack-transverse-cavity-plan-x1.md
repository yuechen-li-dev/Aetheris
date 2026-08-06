# SECTION-STACK-TRANSVERSE-CAVITY-PLAN-X1 — plan-first host seam

The section-stack route separates construction planning from materialization. `PrismaticSectionStackEmitter.TryPlan` produces `PrismaticSectionStackTopologyPlan`; `PrismaticSectionStackBrepMaterializer` validates bindings and instantiates an independent snapshot of the `BrepBody` without deciding topology.

The topology plan preserves every section-stack side face as a `PrismaticSectionStackFacePlanMapping`, carrying its plan `FaceId`, source arrangement provenance, construction/slab identity, and Z interval. This is the required affected-host lookup seam for a later transverse-cavity planner: it must consume corridor and construction provenance to select planned faces, not search the emitted BRep.

The next planner boundary is:

```text
PrismaticSectionStackConstruction
  -> PrismaticSectionStackTopologyPlan
  -> SectionStackBlindDrillCavityPlan (face replacements and new cavity topology)
  -> PrismaticSectionStackBrepMaterializer
```

Corridor evidence proves that the drilling tool fits. The cavity plan proves that the host topology can represent it. A section-stack planner boundary may split a cavity face. It must never create a physical cap. The cavity is integrated into the authoritative host shell; it is not emitted as a detached negative tool body.

`SectionStackBlindDrillCavityPlanner` now implements the first narrow insertion:

- Its input contains the proven corridor and a construction-provenance `MouthHostFaceId`; it never searches final BRep geometry.
- It replaces that planar host side face with the retained loops plus one exact circular Mouth inner loop.
- It adds one exact cylindrical shaft wall, one shaft-to-DrillPoint loop, one exact conical wall, and one Tip to the same authoritative shell. There is no Exit or cap at the shaft/cone seam.
- The replacement map and plan-owned semantic descendants are published before `PrismaticSectionStackBrepMaterializer` consumes the final plan.

The planner additionally admits one exact circular Mouth across one internal
coplanar planning seam. It splits the Mouth into two owned circular arcs and
replaces the affected outer loops, retaining only the seam segments outside the
opening. The shaft and cone are not partitioned. Non-coplanar, physical-edge,
tangent, and arbitrary multi-face cases remain rejected at the planner boundary.

`SectionStackBlindDrillComposeBridge` is the source-facing seam used by the
compose exporter and selection inspection. It converts the source-owned
construction-plane blind declaration into AIR only after a section-stack plan
exists, proves `FullRadiusThroughTotalDepth`, obtains the Mouth face from plan
provenance and plan-bound surface identity (never an emitted BRep search), and
passes the fully decided replacement plan to the materializer.
