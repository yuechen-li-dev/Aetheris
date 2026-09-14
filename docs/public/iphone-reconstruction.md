# iPhone exterior reconstruction experiment

The [editable blockout](../../fixtures/Experiments/IPhone17ProMax/blockout.firmament) is Pass A of IPHONE-17-PRO-MAX-RECON-X0. It is not a finished reconstruction or a manufacturing model. See the [release evidence](../release/IPHONE-17-PRO-MAX-RECON-X0.md).

`PhoneSpec` and `Standard` own the principal dimensions. Ordinary typed Templates specialize the parts, and an Assembly preserves their names in AP242. Coordinates use the rear-view top-left envelope corner: X points right, Y points up (the phone lies at negative Y), and Z points toward the rear. Body thickness excludes the raised cameras.

```powershell
dotnet build Aetheris.CLI -c Release
$cli = Resolve-Path Aetheris.CLI/bin/Release/net10.0/aetheris.exe
& $cli asm export-ap242 fixtures/Experiments/IPhone17ProMax/blockout.firmament --out artifacts/local/iphone-recon-x0/blockout.step --json
& $cli analyze compound artifacts/local/iphone-recon-x0/blockout.step --json
& $cli mesh fixtures/Experiments/IPhone17ProMax/blockout.firmament --format assembly-json --output artifacts/local/iphone-recon-x0/blockout.mesh.json --json
python scripts/qualify-iphone-recon-x0.py
```

The preview script needs numpy, Pillow, and matplotlib. Its only geometry input is the CLI's derived mesh. It does not construct or modify product geometry. Front slabs overlap in this blockout; the front preview resolves coplanar faces in the documented display order. STEP remains the geometric artifact.

Hard references are the body envelope, glass/display extents, Detail D camera/sensor centers and diameters, and the 2.55/1.88 mm rear stack. The plateau's 72.76 x 43 mm footprint and 11.5 mm radius are explicit proxies. Body R13.9 is a circular baseline. The notebook does not establish a plateau blend width. Front slabs and rear feature inserts are visual component proxies, not resolved material partitions. Bottom openings are named dark solid markers, not machined cavities or accessory keepouts. No RF, magnetic, or sensor keepout is product material.

Side-control and bottom-detail placements are explicit assembly transforms. For a length variation, update the bottom-detail Y translations along with `ProductLength`; for a width variation update the right-control X translations with `ProductWidth`. This is a known residual coupling, so changing the Record alone does not yet regenerate every exterior detail. Major plate profiles derive directly from the Record.

The [refined plateau](../../fixtures/Experiments/IPhone17ProMax/refined-plateau.firmament) now uses the generic [G2 planar plateau operation](firmament/plateaus.md). Its body and plateau form one trimmed manifold solid; the other component proxies remain separate. The 72.76 x 43 mm outer contact footprint uses `SmoothRoundedRect2` with 11.5 mm corner extent. The explicit 2 mm homothetic inset preserves the outer envelope and leaves a verified camera clearance above 0.273 mm. The width and corner extent are design assumptions, not recovered Apple spline parameters. See [SURF-G2-LOOP-X1 evidence](../release/SURF-G2-LOOP-X1.md) for continuity, identical-camera comparisons, and variations.

Use `aetheris asm inspect ... --json`, `asm export-ap242 ...`, and `mesh ... --format assembly-json` for this assembly, substituting the refined fixture in the commands above. The earlier [smooth extrusion witness](../../fixtures/Experiments/IPhone17ProMax/smooth-plateau-witness.firmament) remains a footprint-only regression.

The supplied PDF remains local. The validated Drawing Notes project is under `artifacts/local/drawing-notes-x1/fresh-agent/notebook/`. Use its canonical `drawing-notes.json` and refreshed Markdown, not stale filtered handoffs. PDF page 2 Detail A supplies corner evidence; Detail D owns the rear layout. Do not infer optical functions from camera numbering.

The [bounded concave fillet profile mode](firmament/fillet-profiles.md) supplies the reused quintic quarter law. The generic Plateau operation now carries that law around polynomial footprints with exact contacts and face-space trims. Exact Apple hidden splines, lens rings, body side-edge refinement, forward sensor partitioning, material keepout union, and manufacturing equivalence remain deferred.
