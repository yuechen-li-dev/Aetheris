# AETHERIS-BUNNY-M2 evidence

```text
TriangleSurfaceMesh → transported cross field → singularity/junction evidence
                    → deterministic field-scored quad layout → strict BoundaryPatch PanelIr
                    → SurfaceMeshIR (with explicitly typed unmatched transitions, if any)
```

Geometric chart segmentation and quadrilateral surface parameterization are separate problems. M0 solved the former approximately; M2 introduces the latter. Reproduce with `dotnet run --project tools/Aetheris.BunnyM2 -c Release -- .tmp/bunny-m0-source/bunny/reconstruction/bun_zipper.ply docs/geometry/artifacts/bunny-m2`.

Only compact summaries are checked in. No full mesh document, per-face dump, dense field dump, or candidate geometry is persisted.