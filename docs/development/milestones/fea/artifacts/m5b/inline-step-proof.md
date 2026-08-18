# Rotated InlineStep proof

- Fixture SHA-256: `251DAB4CF985633CB97ABDD6AA429B958496335178D3FE95910715F75F11948D`
- Typed `ImportedStep`; no source-string generation
- Rotation: 17 degrees Z and 9 degrees X about exact box center
- Fixed `imported.face(-X)`; 100 N resultant `imported.face(+X)`
- STEP face ID -> recognition -> AnalysisIR -> CIR association -> planar quadrature
- `ForgeHost` -> `ForgeTemplate` -> `ForgeInvocation.Analyze()`
- Native solve converges; evidence retains a non-null exact face ID; validated Abaqus export contains full cells
- Scope remains bounded six-planar-face imported bodies

Reproduced by `ForgeInlineStepTemplate_UsesTypedImportedResourceInAnalysis`.
