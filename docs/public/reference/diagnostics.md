# Diagnostics and failure recovery

## Circular Sweep (X0)

- `firmament-sweep-section-invalid`: circular diameter is missing, non-finite, or non-positive.
- `firmament-sweep-path-disconnected`: a segment does not continue from its predecessor.
- `firmament-sweep-path-not-tangent`: adjacent segments meet at a sharp corner.
- `firmament-sweep-path-nonplanar`: the path is outside X0's planar XY boundary.
- `firmament-sweep-bend-radius-too-small`: an arc radius does not exceed the section radius.
- `firmament-sweep-self-intersection`: nonadjacent centerline regions violate required clearance.
- `firmament-sweep-material-unresolved`: the material identity is absent from the deployed catalog.

## Surface trim closure (SURF-X1a)

- `surf-intersection-none`: the qualified supports do not intersect inside the bounded domains.
- `surf-intersection-ambiguous`: multiple branches exist without an admissible seed/reference boundary.
- `surf-pcurve-invalid`: a face-local UV curve is missing, outside its domain, misoriented, or reconstructs away from the shared 3D edge.
- `surf-trim-loop-open`: ordered trim coedges do not close through shared topology vertices.
- `surf-inner-loop-invalid`: an opening loop is not a valid bounded hole in the replacement face.
- `surf-extension-unsupported`: the surface family, degree, stability envelope, or requested extension is outside the qualified matrix.
- `surf-imported-selector-unresolved`: a STEP `ADVANCED_FACE` identity does not resolve to exactly one current face.
- `surf-association-target-removed`: PMI or an Assembly Interface has no explicit `Preserved` correspondence; Aetheris refuses name- or proximity-based rebinding.
- `surf-association-current-geometry-missing`: an explicitly preserved association references a face absent from the output `BodyState`.
- `surf-selector-target-replaced`: a historical native or imported selector has an explicit successor and cannot be reused.

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
