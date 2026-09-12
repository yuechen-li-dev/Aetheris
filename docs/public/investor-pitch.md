# Reproducible investor demonstration

The investor demonstration uses the real CLI to produce three Standard Paperclip variants, two Standard Library HexBolt variants, G0/G1 eight-section ergonomic bodies, a CNC-policy coupon, and a loaded aluminum plate. All geometry sources live under `fixtures/InvestorPitch/`.

Run from the repository root with PowerShell 7 and the repository's .NET SDK:

```powershell
pwsh -NoProfile -File scripts/build-vc-pitch-evidence.ps1
```

The script builds the CLI, exports each model, analyzes and verifies STEP, generates previews, checks repeated STEP hashes, runs FEA on two lattices and through the native route, and verifies an expected CNC policy rejection. Generated files go to ignored `artifacts/local/vc-pitch/`. `-OutputRoot` changes that destination. The analysis source's imported path is rewritten to the chosen output root.

Paperclip edits specialize `Standard.Products.Office.Paperclip` using `with`. The longer variant scales both straight legs by 15%; bend lengths stay constant. HexBolt uses the existing compatibility template with an under-head length of 35 or 50 mm; the thread designation remains metadata on cylindrical geometry.

The plate is 120 x 40 x 8 mm with a 12 mm through-hole. The catalog supplies 6061-T6 material data. A 1000 N total load acts on the +X end and the -X end is fixed. The imported analysis uses inspected STEP face identities `#141` and `#170`; those are tied to this deterministic geometry, not general-purpose face selectors. Native analysis consumes the same dimensions and canonical shaft hole. The package includes a separate A36 cantilever sanity case.

CNC evidence checks the implemented minimum-tool-radius rule: the 4 mm diameter hole passes a 0.75 mm minimum and fails a 3 mm minimum. The coupon's other policy fields are not separate DFM proofs.

The presentation source, editable PPTX, PDF viewing copy, rendered assets, per-slide claim manifest, external-source metadata, engineering interpretation and reproduction README are delivered in the generated investor package. Market and commercial sections identify historical sourced figures and proposed business assumptions. They do not assert revenue or customer traction.

For source validation:

```powershell
dotnet build Aetheris.slnx -f net10.0 -m:1
dotnet test Aetheris.slnx -f net10.0 --no-build --filter 'Category!=SlowCorpus'
```
