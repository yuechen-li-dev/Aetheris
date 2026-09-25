# ORDNING-X0 — perforated utensil-holder witness

**Verdict: Meaningful progression.** The production Firmament path now creates a constant-thickness cylindrical Hollow body and exports/reimports it as an enclosed manifold STEP body. The presentation-quality perforated product is **not** accepted: cylindrical Hollow perforation and semantic circular edge finishing are not yet composed in this path. No hero render is claimed.

The visual reference suggests a compact, deep-drawn stainless utensil holder. This is an ORDNING-inspired design, not a dimensional or manufacturing-history reproduction. The intended lower blend would imply formed sheet, the softened rim would remove a sharp handling edge, and unperforated bands would keep holes clear of the formed regions. A 0.8 mm wall is a plausible design choice for a small stainless household vessel; it is not a measured IKEA gauge.

## Realized body

| Parameter | Value / status |
| --- | --- |
| Outer diameter | 112 mm |
| Height | 142 mm |
| Wall thickness | 0.8 mm, exact coaxial radial offset |
| Bottom thickness | 0.8 mm |
| Opening | Top, with planar annular rim |
| Lower blend | Not implemented; current bottom corner is sharp |
| Top edge finish | Not implemented; current rim edges are sharp |
| Perforations | 0; no cylindrical Hollow lowering yet |
| Material | Intended 304 stainless; no material assignment in this witness |

The proposed, **unrealized** presentation field is 8 rows × 20 columns = 160 local wall holes, 9 mm diameter, 15 mm vertical pitch, 18° angular pitch (about 17.6 mm arc pitch at the outside wall). Row centers would run from 22.5 to 127.5 mm, leaving 18 mm bottom and 10 mm top edge-to-hole margins. The intended lower outer blend radius is 8 mm, corresponding inner radius 7.2 mm, with a small rim finish constrained by the 0.8 mm sheet. These numbers are design targets only and have no current BRep or STEP witness.

The source is [ordning-utensil-holder-body-x0.firmament](../../fixtures/Canonical/Presentation/ordning-utensil-holder-body-x0.firmament):

```firmament
Struct OrdningInspiredUtensilHolderBody {
  Cylinder<Hollow> Body {
    Radius: 56mm
    Height: 142mm
    WallThickness: 0.8mm
    Openings: [Top]
  }
}
```

`Cylinder<Hollow>` is compiler-owned paired-boundary construction. The BRep has an outer cylinder, coaxial inner cylinder, two bottom planes, and an annular top face. The bottom is closed as material; the top is open into the cavity. No Boolean or mesh approximation is used.

## Qualification and reproduction

```powershell
dotnet run --project Aetheris.CLI -c Release -- build fixtures/Canonical/Presentation/ordning-utensil-holder-body-x0.firmament --output artifacts/local/ordning-x0/body.step --json
dotnet run --project Aetheris.CLI -c Release -- analyze artifacts/local/ordning-x0/body.step --json
```

The observed STEP reimport reports one enclosed-manifold body and shell, 5 faces, 4 edges, 4 vertices, 2 cylinders, 3 planes, and bounds `[-56,-56,0]` to `[56,56,142]` mm. The generated STEP is 3,811 bytes. The production build command took 1.29 s including CLI startup; this is not a kernel-only benchmark. Tessellation and render times are unavailable because the perforated presentation model does not exist.

Release build completed with 0 errors. Focused Hollow tests passed (7 Core and 4 Firmament); the fast Core lane passed 962/962. The serial solution gate did not pass: 2 CLI tests failed (`MeshObj_HexBolt...` quad count 905 vs 908, and `ValidateRoutesModuleShapedSheetMetal...`), and 5 CTC03 SheetMetal tests failed. Those failures are outside the new cylindrical Hollow route; their baseline status has not been established. The full test log is retained locally at `artifacts/local/ordning-x0/full-test.log`.

## Exact next boundary

The current `FirmamentPerforationPlanner` accepts only `+Z` on a planar Box. `BrepDiametralCrossHole.Build` handles a single diametral cut through a **solid** cylinder, which would create paired opposite wall openings and cannot be applied directly to this Hollow body. A local finite radial cutter must connect one opening on the outer cylinder to one on the inner cylinder, then repeat with deterministic instance topology and seam handling. This requires analytic cylinder-cylinder intersection trims and pcurves for both walls, plus one cutter wall per local hole; it is not a general Boolean.

The Hollow construction parser now rejects a following `Modify` block with `firmament-v2-hollow-modify-unsupported`, so it cannot silently export the unperforated body when a Perforation is requested. Circular lower-blend and rim chains also need compiler-owned edge identities and constant-thickness topology. Until those paths are qualified, the proposed 20-column by 8-row, 9 mm diameter field and 8 mm lower blend remain design intent, not realized geometry. STEP hole count, source provenance, seam quality, top safety, and hero visual quality therefore remain unverified.
