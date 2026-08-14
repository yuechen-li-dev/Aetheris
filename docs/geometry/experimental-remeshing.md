# Experimental structured remeshing

`SurfaceReconstruction.Remesh` converts an ASCII triangle PLY surface into an approximate, predominantly quad `SurfaceMeshDocument`. It is a bounded shrink-wrap/structured-remesh operation, not CAD feature recognition or design-intent recovery.

```csharp
TriangleSurfaceMesh source = PlyTriangleSurfaceLoader.LoadAscii(stream, "scan.ply");
SurfaceReconstructionResult result = SurfaceReconstruction.Remesh(source, ReconstructionPolicy.Fast);
```

```powershell
aetheris reconstruct mesh scan.ply --mode fast --out scan-remesh.obj --report scan-report.json --error-ply scan-error.ply
```

## Fast policy

Fast uses a geometric tolerance of `max(0.001 × bounding-box diagonal, 1e-6 source units)`. It performs one face-local directional analysis, deterministic field-scored pairing, rejects catastrophic face pairings above the policy's 120° cutoff, applies at most 640 augmenting-path improvements of depth at most 5, and takes at most 4,096 quality samples in each direction. It performs no field-relaxation iterations, global blossom match, deep atlas coarsening, or runtime-sensitive optimization.

The mesh-first path retains authoritative shared vertex IDs, source connected components and open boundaries, and native bounded-parametric bilinear support for quads. Transition triangles are explicit and acceptable. Coarse `PanelIr` materialization is optional and is not performed by Fast solely to manufacture semantic structure.

Position quality reports source-to-result and result-to-source RMS, p95, and maximum distance. Normal quality reports mean, RMS, p95, and maximum angle. A shared correspondence cache records projection distance, normal/tangential residual, confidence, ambiguity, and boundary proximity; invalidation is per target region. The optional colored error PLY is lightweight sampled evidence, not a dense authoritative field.

Positional offset fields and differential-only normal corrections remain separate representation authorities, but Fast fits neither unless a coarse field would materially improve the scale-derived tolerance. The Bunny p95 errors are below tolerance, while sparse maxima remain above it; a global coarse residual map does not usefully address those local outliers, so Fast emits no residual maps for this case.

## Supported and unsupported cases

The current importer accepts ASCII PLY with triangle faces. Empty, invalid-index, non-finite, and non-manifold inputs return typed `Unsupported` results. Bounded cases that miss the predominantly-quad or topology target return `Partial` with a compact diagnostic rather than continuing indefinitely. OBJ is a visualization/export format; the reusable .NET result is the normal `SurfaceMeshDocument` front door.

Strong features influence local pairing through face normals and the directional field. Weak scan noise below tolerance is not forced into topology. The operation does not infer holes, bosses, fillets, datums, bunny anatomy, NURBS, or mechanical feature history.

## Known limitation and stop point

The main limitation is that local triangle pairing is a structured topological remesh, not a compact global quad atlas: irregular source tessellation can therefore leave transition triangles and high normal-error outliers, especially on thin, ambiguous, or poorly sampled geometry.

Further global atlas optimization is deferred until real consumers demonstrate that it is necessary. This experimental seam is intended to gather that pressure without continuing open-ended remeshing research.
