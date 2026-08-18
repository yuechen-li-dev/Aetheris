# Pre-change semantic identity audit

| Area | Identity/type before M1 | Capability/binding | Span/provenance | Duplicate adapter or gap |
| --- | --- | --- | --- | --- |
| Concept IR / Struct | `ConceptIrValue.StableId`, nominal value kind; struct dictionaries | compile-time value or semantic-reference category | struct/member spans; string provenance | no common consumer value |
| Concept Path | path/guide/endpoint strings | directly produced `ResolvedProfile2D` | segment provenance | path-specific inspection and profile adapter |
| Template / Table / Record | specialization hash plus generated declarations | ordinary generated syntax | invocation, record origin, with-derived text | output identity separated only informally |
| Recognize / InlineStep | body + region name, STEP entity refs, kind/confidence | topology map proves exact imported face | recognition metadata; declaration span not carried in value | FEA/Selection could not consume a common reference |
| Selection | request ID, source stable IDs, topology role | `SemanticTopologyCorrespondence` | source string plus chain | source syntax-specific parsing |
| Modify | target strings and operation-specific exact checks | bounded body/face/profile logic | operation reports | no reusable capability diagnostic |
| Forge | capability/version and construction identity | ConstructionIR/ExactBRep output classification | dictionary plus host evidence | no exposed semantic output contract |
| FEA / AnalysisIR | body + path and optional face ID | separate `SemanticRegionBinding` | `AnalysisProvenance` | origin-specific region binding |
| CLI | subsystem-specific JSON sections | capabilities only for Concept Path inspection | partial | no unified descriptor |
| Cadmata | stable entity IDs, descendants, selections | topology DTOs | source span/metadata | retained; no M1 redesign |

M1 replaces the highlighted common gap with `SemanticValue` and retains bounded
producer validation where it proves real origin-specific facts.
