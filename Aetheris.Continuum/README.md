# Aetheris.Continuum bounded reconstruction utilities

`Aetheris.Continuum.Reconstruction` contains two deliberately small geometry-decompilation aids:

- `PointTorusField` evaluates precomputed local analytic tori imported from an external fitter. It follows the query representation and 32-neighbor exponential blend observed in Feng et al., *Points as Tori: Fast Pointwise Signed Distance for Point Clouds* (SIGGRAPH 2026). The learned Flax model and its weights are not included. [Project](https://nzfeng.github.io/research/PointsAsTori/index.html) · [code](https://github.com/nzfeng/points-as-tori)
- `SharpDualContouring` converts a regular sampled signed-distance grid into an approximate explicit triangle mesh with sign-changing-edge Hermite constraints, finite-difference normals, per-cell QEF placement, and deterministic dual connectivity. It is a clean-room bounded implementation informed by Carrera et al., *Dual Contouring of Signed Distance Data*. [code](https://github.com/xianacarrera/dual-contouring-of-signed-distance-data)

## Authority warning

Both outputs are approximate, source-derived evidence for reconstruction, scan comparison, fitting, and geometry decompilation. They are not BRep, CIR topology, Firmament feature history, manufacturing geometry, or semantic authority. A mesh extracted here requires a separate explicit reconstruction/admission step before it can participate in stronger lanes.

PAT sign is qualified only for closed, globally oriented, locally single-sheet input and only inside the declared near-source validity band. Open, unoriented, thin, and multi-sheet inputs return unsigned values. Callers must inspect both `Qualification` and `IsSigned`: a query can be outside the validity band and also unsigned. Callers receive `PointTorusEvaluation`; there is intentionally no naked-double query API.

The contourer always reports boundary edges, nonmanifold edges, connected components, duplicate faces, and duplicate vertices. It does not repair them. These counters do not test self-intersection, consistent orientation, degenerate triangles, or source-distance fitness; downstream admission must add whichever checks its authority requires.

## Minimal CLI workflow

```powershell
dotnet run --project Aetheris.CLI -- continuum make-fixtures --out-dir artifacts/local/continuum-tools-x1
dotnet run --project Aetheris.CLI -- continuum pat-query artifacts/local/continuum-tools-x1/sphere-point-tori.json artifacts/local/continuum-tools-x1/sphere-queries.json --out artifacts/local/continuum-tools-x1/sphere-results.json
dotnet run --project Aetheris.CLI -- continuum contour artifacts/local/continuum-tools-x1/sharp-box-grid.json --out artifacts/local/continuum-tools-x1/sharp-box.obj --report artifacts/local/continuum-tools-x1/sharp-box-report.json
```

The interchange formats are intentionally boring JSON (`aetheris.point-torus-field.v1`, `aetheris.sampled-sdf-grid.v1`) and OBJ. Coordinates, radii, spacing, distances, and validity bands use millimetres. Grid values are flattened with X varying fastest, then Y, then Z: `values[(z * sizeY * sizeX) + (y * sizeX) + x]`. Query input is a JSON array of `[x,y,z]` millimetre triples. `make-fixtures` generates complete deterministic examples under ignored local artifacts.

The public upstream PAT path used 32 neighbors; `NeighborCount` remains explicit and may be smaller for tiny fixtures. The blend is the observed upstream exponential kernel with `shift = 0.5 * max(neighbor distance)` and `lambda = 64 / shift`.

## Licensing and provenance

The PAT upstream repository is MIT, but its pretrained weight file has no separately stated grant; this module redistributes neither code nor weights. The DCSD repository contains an MIT text whose copyright notice names Nick Sharp rather than the paper authors, while the paper PDF is CC BY-NC-ND; this implementation therefore copies no upstream source. Preserve upstream/model hashes in `PointTorusProvenance` when importing real fitted parameters.
