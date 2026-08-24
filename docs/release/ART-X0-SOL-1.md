# ART-X0 — Sol 1 milestone report

**Verdict: Success**

Sol 1 exists through a dedicated non-manufacturing source, materializer, CLI, AP242 assembly export, Aetheris reimport, external FreeCAD import, deterministic rerun, authored preview, and focused/full regression coverage. The production PlasticShell and other engineering paths were not modified.

## Flagship artifact

- Source: `fixtures/Canonical/VirtualSculpture/sol-1.sculpture.json`
- STEP: `artifacts/local/virtual-sculpture/sol-1.step`
- Structured evidence: `artifacts/local/virtual-sculpture/sol-1.evidence.json`
- Authored preview: `artifacts/local/virtual-sculpture/sol-1.preview.svg`
- Preview raster used for review: `artifacts/local/virtual-sculpture/sol-1.preview.png`
- STEP SHA-256: `53034776a882cd49992741946d0c564819ace87a5651fa76fe2272ceae2d047a`
- STEP size: 1,149,628 bytes

Generated artifacts remain under ignored `artifacts/local/` by policy. The canonical source and this compact release record are tracked.

## Artistic construction

The interior begins with 233 golden-angle points distributed by equal-area annular phyllotaxis. Two deterministic connection laws, `n -> n + 13` and `n -> n + 21`, expose interleaved Fibonacci parastichy families. A primary fivefold radial/angular wave and a secondary thirteenfold interference wave move the field out of plane.

Three evenly distributed residue classes (`0`, `7`, and `14` modulo `21`) become controlled prominences after 62% radial growth. Their outer nodes rise through a cubic smoothstep ramp to 9 mm and their `+21` chords thicken. This produces three escaping gestures without disturbing the global order.

The center is reserved before field generation. An exact torus forms the eye, a fine exact torus delimits the corona, and a 58 mm-major-radius exact torus forms the calm outer boundary. The intended outer diameter is `2 * (58 + 2.25) = 120.50 mm`; FreeCAD's independent torus-face tessellation measured `120.50 mm` across and the imported assembly spans approximately `12.86 mm` in Z. FreeCAD's analytic-face `BoundBox` overestimates the XY torus extent, so it was not used as dimensional evidence.

## Semantic and architectural boundary

`Aetheris.Sculpture` is a new project with one bounded source contract and one materializer. `Mode: Virtual` is mandatory. The CLI exposes `aetheris sculpture build`; it does not route through ordinary Firmament manufacturing builds or the `experimental heightfield-art` command.

Sol 1 is intentionally an AP242 product-structure assembly of exact closed bodies. Perceptual joints overlap, but no manufacturing boolean-union claim is made. This is meaningful structured geometry rather than triangle soup: the final STEP contains only analytic toroidal, spherical, cylindrical, and planar surfaces.

No PlasticShell implementation file, manufacturing semantic, or exact production materialization route changed in ART-X0.

## Geometry and representation evidence

| Fact | Qualified value |
|---|---:|
| Phyllotaxis nodes | 233 |
| Fibonacci connection offsets | 13, 21 |
| Lattice connections | 432 |
| Prominent nodes | 13 |
| Prominent connections | 10 |
| Exact closed-body definitions | 220 |
| Exact closed-body occurrences | 668 |
| AP242 root + child occurrences | 669 |
| Planar surface definitions | 430 |
| Cylindrical surface definitions | 215 |
| Spherical surface definitions | 2 |
| Toroidal surface definitions | 3 |
| B-spline surfaces | 0 |
| Rational product surfaces | 0 |

Surface counts describe reusable STEP definitions, not multiplied occurrences. Aetheris STEP assembly reimport recovered 220 geometric definitions and 668 child occurrences. The extra imported root definition is the non-geometric Sol 1 assembly product.

## Qualification

The following real paths were run from the repository root:

```powershell
dotnet build Aetheris.slnx -c Release
dotnet test Aetheris.Sculpture.Tests/Aetheris.Sculpture.Tests.csproj -c Release --no-build
dotnet test Aetheris.CLI.Tests/Aetheris.CLI.Tests.csproj -c Release --no-build --filter FullyQualifiedName~Sol1CliTests
dotnet test Aetheris.slnx -c Release --no-build -m:1
dotnet run --project Aetheris.CLI -c Release --no-build -- sculpture build fixtures/Canonical/VirtualSculpture/sol-1.sculpture.json --out artifacts/local/virtual-sculpture/sol-1.step --evidence artifacts/local/virtual-sculpture/sol-1.evidence.json --preview artifacts/local/virtual-sculpture/sol-1.preview.svg --json
dotnet run --project Aetheris.CLI -c Release --no-build -- asm import-step artifacts/local/virtual-sculpture/sol-1.step --out artifacts/local/virtual-sculpture/reimport --json
tools/Validate-Step-FreeCAD.ps1 artifacts/local/virtual-sculpture/sol-1.step
git diff --check
```

The deterministic test builds the same source twice and requires byte-identical STEP plus equal SHA-256. The dedicated CLI test exercises source load, materialization, STEP/evidence/preview writes, and the explicit virtual-domain JSON marker.

The serial full-suite qualification passed all 3,161 discovered tests. An earlier solution-wide parallel run exposed one unrelated timing-test outlier in `RecognizedConstructionRecipeTests` while all ART-X0, CLI, and PlasticShell tests passed; that exact case passed immediately in isolation and the complete serial rerun passed all 961 Kernel.Core tests. `Aetheris.FrictionLab.Tests` remains an existing assembly with no discoverable tests.

FreeCAD 1.0.2 imported the assembly without healing: 676 document objects, 1,336 hierarchy-counted solids/shells, `shape_valid=true`, and analytic surface recognition of Cylinder, Plane, Sphere, and Toroid. The hierarchy exposes geometry at definition and occurrence levels, so the external traversal reports each placed solid twice. Its aggregate `closed=false` includes non-geometric assembly nodes; all emitted Aetheris component definitions pass exact closed-body preflight and import as solids.

## Manual visual review

Open the STEP itself for the acceptance review. Check:

- the overall solar/vortex silhouette;
- whether the outer toroidal frame reads as calm and pristine;
- whether the dual parastichy lattice reads as intentional spiral interference rather than a generic web;
- the cleanliness and authority of the central eye;
- the three rising, thickened outer prominences;
- whether close inspection rewards the viewer with ordered repetition;
- any ugly accidental spikes, overlaps, inverted placements, or display artifacts;
- whether the complexity feels beautiful rather than merely noisy.

The SVG is a deterministic composition preview, not substitute evidence for the STEP. Camera, material, and lighting choices in a CAD viewer will materially affect the prominence read.
