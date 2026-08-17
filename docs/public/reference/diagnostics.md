# Diagnostics and failure recovery

Start with `aetheris validate source.firmament --json`; use `build` when geometry, AP242, assertions, or artifacts are involved. A diagnostic code is the stable automation key, while its message identifies the value/target and expected category where useful.

Common recovery patterns:

| Failure | What to check |
|---|---|
| unknown keyword/Template | spelling, `Use` declaration, and qualified Template name |
| missing Template argument | the Template's typed parameter list or Forge `describe` output |
| unit mismatch | use the required dimension (`mm`, `deg`, `N`) rather than a bare/wrong-dimension value |
| unknown material | one of the four exact catalog references in the materials guide |
| unresolved PMI target | named hole/face selector exists in the same semantic domain |
| invalid tolerance | `PlusMinus(plus, minus)` uses Length values for a diameter |
| Sheet Metal region mismatch | use a named planar region such as `Base`, not `face(+Z)` |
| `sheetmetal-hole-domain-syntax` | replace Model `Hole<Shaft>` with Sheet Metal `Hole Name` syntax |
| `sheetmetal-pmi-domain-syntax` | use `Manufacturing` plus `DatumFeature` targeting a named Sheet Metal region |
| inlineSTEP file/face failure | resolve the file relative to the source and use an existing AP242 face identity |
| empty FEA selection | selected face exists and intersects occupied cut cells at the requested lattice |
| unsupported constitutive model | Preview 3 production scope is linear elastic isotropic |

Successful builds enforce PMI/AP242 parity. `firmament-v2-pmi-export-evidence-mismatch` means a supported record failed independent export reinspection; no artifact is written. See [targets](targets.md) for cross-domain forms.
