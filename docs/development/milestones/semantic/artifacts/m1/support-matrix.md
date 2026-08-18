# Semantic-value support matrix

| Origin | Expose member | Concept Path | Selection | Profile | Compose | Modify | FEA Region | Inspection |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Direct Firmament | Supported | Supported where exposed | capability | capability | capability | exact targets only | boundary capability | Supported |
| Concept Struct | Supported | Supported | capability | if exact profile bound | if profile bound | exact targets only | if boundary bound | Supported |
| Template output | Supported | Supported | capability | capability | capability | exact targets only | boundary capability | Supported |
| Table/Record Template | Supported | Supported | capability | capability | capability | exact targets only | boundary capability | Supported |
| InlineStep/Recognize | recognized members only | Supported for exposed member | Supported | future recognition needed | future recognition needed | exact face/region only | Supported | Supported |
| Forge extension | validated members only | Supported for exposed member | Supported | if exact profile bound | if profile bound | exact targets only | Supported | Supported by host descriptor |

“Capability” means supported only when the producer proves and attaches that
specific contract. Raw BRep and mesh identifiers are intentionally unsupported.
