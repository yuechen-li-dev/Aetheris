# BREP-HELICAL-RIB-X1 — manifold topology for bounded helical ribs

**Verdict: Accepted for the bounded complete-turn kernel feature.** `BrepHelicalRib.Create` converts the X0 exact rib authority into one closed, oriented, seam-aware BRep body. The canonical 24-turn body exports to AP242, reimports as one enclosed manifold shell, and tessellates with every face present. This is a kernel feature; public `Thread` syntax remains a later milestone.

## Admitted construction

The input is one right-hand, single-start, constant-pitch `HelicalRibGeometry` on cylindrical root stock. X1 admits 1–40 **complete turns** and requires exposed cylindrical stock before and after the rib footprint. Noninteger turn counts and rib contact with either stock end receive explicit diagnostics. The X0 authority still rejects overlapping adjacent turns (`root width >= pitch`) and invalid radii, pitch, profile widths, or support extent before topology construction.

The rib retains its exact axis, radial frame, pitch, profile, start phase, and angular domain in `BrepHelicalRibResult.Authority`. `QualifiedSideFaces` maps every side face back to its exact one-turn authority and finite-realization certificate. The BRep side faces are qualified non-rational degree-(3,1) surfaces. Their four boundary curve roles are realized directly from the corresponding surface control nets, so adjacent faces bind the same edge rather than independently fitting it. The X0 realization certificate bounds each side surface to the configured 1e-6 mm kernel tolerance, including its numerical allowance. The BRep pcurve validator checks 3D/pcurve agreement to 1e-6 mm.

## Direct topology

For `N` turns, the support cylinder is partitioned into `N+1` exposed skin faces: lower margin, `N-1` interturn gaps, and upper margin. No untrimmed support face remains beneath the rib. Each turn has a leading flank, finite-width crest, and trailing flank. Planar start/end rib caps close the feature; planar stock end disks close the cylinder. This is direct known topology: no generic sweep, Boolean union, or mesh-derived BRep.

The support seam is placed at the authored start phase, even when the entire axis/frame is transformed. Each wrap is one deterministic topological segment. Neighboring side surfaces and root gaps share the same edge IDs, and `EdgeRoles` retain the logical boundary name plus segment index. The result reports `N-1` internal seam splits. Each edge has exactly two coedges of opposite sense; loop and binding validation, export preflight, and pcurve checks pass. The 24-turn tessellation has positive signed volume of approximately 1,312.0 mm³, within 0.5% of the analytic stock-plus-trapezoid volume (1,317.4 mm³). This display-derived volume is an orientation/trim check, not a certified mass property.

| Turns | Faces | Edges | Vertices | Internal seam splits | Warm build |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | 9 | 17 | 10 | 0 | 0.7 ms |
| 4 | 21 | 41 | 22 | 3 | 2.4 ms |
| 24 | 101 | 201 | 102 | 23 | 14.1 ms |

The general counts are `4N+5` faces, `8N+9` edges, and `4N+6` vertices. The 24-turn STEP contains 25 cylinder faces, 72 B-spline side faces, and four planes. Its importer reports 392 pcurves and two seam pcurve pairs.

## Canonical witness and interchange

The kernel witness has a 6.4 mm root diameter, 8.0 mm crest diameter, 1.25 mm pitch, 1.1 mm root width, 0.1 mm crest width, and 30 mm centerline span: exactly 24 turns. The support extends from `z=-1` to `z=32.1` mm, leaving 1 mm exposed margin outside each full rib footprint. These are metric-like envelope dimensions, not a qualified thread standard.

The production AP242 file is generated at `artifacts/local/helical-rib-x1/helical-rib-24.step`. `aetheris analyze` reimported it and reported one body, one shell, 101 faces, 201 edges, 102 vertices, bounding box `[-4,-4,-1]` to `[4,4,32.1]` mm, and `enclosed-manifold` structural assessment. Its orientation report found 101 source agreements and no mismatches or ambiguous components. Roundtrip tests also check imported root/crest radius samples and the 1.25 mm axial displacement between consecutive leading-root seam vertices. The supplied McMaster STEP pair is dimensional and visual context only; this witness is authored from X0 geometry rather than reconstructed from imported topology.

The supported BRep display tessellator produced nonempty patches for all 101 faces and 375,078 triangles. The shaded [24-turn render](artifacts/helical-rib-x1.png) shows the continuous rib, support margins, and planar terminations without a visible missing wedge or exploding seam. The image is a derived display artifact; the STEP and exact rib parameters are the engineering authorities. One-loop cylinder faces with inclined pcurves now use trim-aware tessellation: the legacy bounding-rectangle display path had visibly overfilled the narrow interturn gaps and inflated display volume to 1,947 mm³. The volume regression assertion prevents that fallback from silently returning.

To regenerate the STEP, display OBJ, metrics, and render in PowerShell from the repository root (the Python renderer uses NumPy and Pillow):

```powershell
$env:AETHERIS_HELICAL_RIB_ARTIFACT_DIR = (Join-Path (Get-Location) 'artifacts/local/helical-rib-x1')
dotnet test Aetheris.Kernel.Core.Tests -c Release --filter FullyQualifiedName~WriteCanonicalWitnessWhenRequested
python scripts/render-helical-rib-x1.py artifacts/local/helical-rib-x1
```

The promoted PNG is the compact release visual; the STEP/OBJ and run metrics remain ignored local artifacts under the repository generated-artifact policy.

## Representative performance

One local Release run of the 24-turn witness measured 75 ms for initial BRep construction and validation, 4.51 s for supported display tessellation, 116 ms for AP242 export, and 4.52 s for Aetheris STEP reimport. The STEP file is 5,460,677 bytes. Warm in-process build times appear in the topology table. These are single-run measurements on the development machine, not a benchmark distribution. Face/edge counts scale linearly with turn count; display tessellation and reimport are the largest measured costs.

## Verification and limits

Focused tests cover 1, 4, and 24 turns; face/edge/vertex counts; every edge's two opposite coedges; pcurve consistency; display volume within 1% of the analytic envelope; STEP export/reimport; nonempty tessellation patches; pitch, crest-depth, start-phase, and transformed-axis changes; and explicit stock-end-contact rejection. X0 tests provide adjacent-turn self-intersection rejection and finite-realization bounds. The canonical artifact test is opt-in through `AETHERIS_HELICAL_RIB_ARTIFACT_DIR`, so normal test runs do not write output.

The phase-alignment test found and fixed an initial mismatch between the rib phase and support cylinder/cap reference frames. Start/end contact with stock end faces, partial turns, runout, arbitrary profile shapes, generic sweeps/booleans, and public Thread semantics remain outside X1. The imported STEP length unit is currently reported as assumed millimetres by the general importer; the exported witness coordinates and bbox were checked directly.

The Release solution build passed. The fast Core lane passed 1000/1000; the corpus-inclusive Core lane passed 1133/1133; Firmament passed 1649/1649. The conclusive full solution command exits nonzero only on the established baseline of two CLI tests and five SheetMetal tests. One intervening full run hit two existing Core tests' 10-second tessellation budgets; both passed immediately in isolation, and the subsequent corpus-inclusive Core and full solution runs passed 1133/1133. No X1 assertion failed.
