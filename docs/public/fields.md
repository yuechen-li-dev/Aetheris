# Aetheris.Fields

`Aetheris.Fields` owns analytic SDF expressions, their deterministic CPU evaluation
tape, capability declarations, and specialized Visual TypeScript field source.
Its only project dependency is `Aetheris.Kernel.Core`. It does not depend on
Continuum analysis, Firmament, Copeland, SQLite, the CAD application, or a renderer.

The existing `Aetheris.Continuum.Backends.Sdf` namespace is retained for source
compatibility. `Aetheris.Continuum` references this library and forwards the moved
public types for previously compiled callers. Broader CIR adapters, integration,
region planning and analysis remain in Continuum.

The source lowerer now exports `Field` as an importable VTS function. Its hash
domain is `cir-visual-ts/2`; rebuild retained v1 display/shader artifacts rather
than treating an old cache identity as a new program. The admitted exterior-step
flag is set by successful lowering, not by manually constructing a program.

Build or pack the small library directly:

```powershell
dotnet build Aetheris.Fields -c Release -m:1
dotnet pack Aetheris.Fields -c Release -o artifacts/local/field-packages
```

Building the complete Firmament/application stack still restores its explicitly
pinned `Copeland.TS.Backend.Wgsl` package family. That is a downstream display
compiler dependency, not a dependency of native fields. Applications consuming
only fields need no Copeland checkout or compiler packages.

The library is `AGPL-3.0-only`, as declared by Aetheris package metadata. Exported
field source specializes Aetheris implementation code; consumers must preserve its
AGPL provenance rather than treating generated shaders as a licence boundary.
Geometry data, third-party assets and application code retain their own licences.

`CirVisualTsLowerer` admits bounded primitives and rigid Boolean compositions.
It rejects nonrigid transforms explicitly. Exact Euclidean signed distance,
sign-correct occupancy and a conservative exterior step bound are different
properties: Boolean fields can provide safe exterior stepping without being
exact Euclidean distance everywhere. Floating-point error and ray-budget policies
belong to the consuming tracer and require validation in its coordinate range.
