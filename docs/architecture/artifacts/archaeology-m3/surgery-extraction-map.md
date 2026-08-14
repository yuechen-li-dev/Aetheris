# Surgery extraction map

| Old mechanic | New internal primitive | Current recipe callers | Reuse reason |
|---|---|---|---|
| repeated edge-use allocation and cyclic coedge links | `BrepLoopBuilder.CreateKnownLoop` + `BrepEdgeUse`; narrowly named legacy-sense adapter | orthogonal union, polygonal through cut, cylinder-root open slot; strict primitive fixtures | identical graph mechanic appeared in four Boolean builders and several feature emitters; caller supplies order/sense |
| private face-from-one/two-loop helpers | `BrepFaceBuilder.CreateKnownFace` / `CreateKnownFaceFromLoops` | orthogonal union, polygonal through cut, cylinder-root open slot | face ownership and outer/inner ordering are independent of feature recognition |
| repeated face set -> shell -> body | `BrepShellAssembler.CreateClosedBody` | polygonal through cut, cylinder-root open slot | caller supplies faces; assembler checks existence, duplicates, and two-use incidence |
| direct binding validation after rebuild | `BrepSurgeryValidation.ValidateBody` | all three migrated builders | reuses canonical validators and adds finite vertex geometry |

No remapper was extracted. The current mixed-through-void builder reuses an already rebuilt body and changes only composition metadata; inventing a general identity/provenance remapper from that single case would exceed evidence. Ring geometry also remains recipe-local because current analytic builders have materially different periodic seam and surface-sense conventions.
