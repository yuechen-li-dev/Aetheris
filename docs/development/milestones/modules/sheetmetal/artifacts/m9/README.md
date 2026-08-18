# Sheet Metal M9 — attachment paths and analytic profile rounds

## Verdict

**Meaningful progression.** Physical free edges and bounded attachment paths
are now separate authored/IR concepts, including inset and partial paths,
explicit release-to-carrier material semantics, ordinary bend/flat lowering,
stable formed/flat correspondence, inspection paths, validation, and generic
dogfood. Analytic `Round` corner descendants now survive region, formed BRep,
flat `PlanarContour2`, and STEP-capable geometry.

CTC-03 is not complete. Its service bend now uses
`RightWall.ServiceFlangeAttachment` at the source axis while `RightWall.Outer`
is 2.6416 mm farther out. Both R12.7 wall-end rounds are analytic. The remaining
right-wall mismatch is the two source-specific 15.24 x 15.24 diagonal entries
and adjacent 63.5 mm deep-recess runs around the shallow service release. The
current generic release produces vertical release cuts directly from the outer
carrier. Encoding the missing compound approach profile as unrelated raw
coordinates would be brittle, so this milestone does not claim completion.

## Measured CTC comparison

Recovery-M2 baseline to M9 native flat:

| direction | baseline RMS / p95 / max (mm) | M9 RMS / p95 / max (mm) |
|---|---:|---:|
| source to native | 4.413550 / 9.554765 / 17.448908 | 4.310304 / 9.554765 / 19.478294 |
| native to source | 3.769455 / 8.898990 / 14.341517 | 3.452142 / 8.315259 / 17.168461 |

Width/height residual remains 0.002447/0.007820 mm; 17 openings and seven bend
lines remain present. The worse maximum is localized evidence that the compound
right-wall approach profile is still absent, not grounds to call the part done.

Recovery-M2 formed baseline to M9:

| direction | baseline RMS / p95 / max (mm) | M9 RMS / p95 / max (mm) |
|---|---:|---:|
| source to native | 8.500190 / 12.478269 / 52.816066 | 7.636925 / 9.141310 / 49.263886 |
| native to source | 3.607855 / 8.940881 / 12.735724 | 2.865211 / 8.940881 / 12.735724 |

All seven bend comparisons and all 17 opening comparisons pass. The service
bend axis residual is 0.00254 mm and service-hole center residuals are at most
0.000527 mm. Construction remains source-independent.

## Architecture

```text
SheetRegionIr
  +- physical Boundary3D / ExactContour
  +- SheetAttachmentPathIr[]
       +- carrier identity, owner, start/end, tangent
       +- in-plane/region normals, inset, span offset
       +- FlangeAttachable/BendAttachable/FeatureAttachable
  +- ordinary SheetBendIr and child SheetRegionIr
```

Known debt: the regex-backed dialect pre-scans attachment declarations for
capability admission before the semantic layout pass. It is localized and
deterministic, but should eventually become one typed parser pass.
