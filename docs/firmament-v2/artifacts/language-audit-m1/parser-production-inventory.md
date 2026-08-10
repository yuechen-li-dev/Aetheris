# Parser production inventory

| Production family | Owning parser | Result |
|---|---|---|
| Model/Units/primitives/Modify/InlineStep/Recognize/Replace/PMI/let | `FirmamentV2Parser` | `FirmamentV2Document` AST/bound records |
| Concept/Concept Struct/Struct/Expose | `ConceptIrResolver` | `ConceptIrDocument` |
| Record/Static/Table/with/Require/Pattern | `CanonicalStaticAuthoring` | static authoring document + erased source |
| Template/Match/defaults/satisfies | `FirmamentV2TemplateExpansion` | specialization/source-map evidence + erased source |
| Concept Path/Profile/Compose/Selection | `ProfileAuthoringParser` plus V2 parser | validated profile/construction declarations |
| Assembly/Interface/Semantic datum/Relation/Assert stackup | `AssemblyM0Parser` | `AssemblySource` |
| Analysis/material/constraints/loads/results | `FirmamentAnalysisCompiler` | `AnalysisIR` |

Active AST nodes are those reachable from these results and a consumer. Historical `FirmamentV2TemplateDecl` manufacturing-process records coexist with canonical Template expansion; they are internal legacy representation, not a second public Template grammar.

