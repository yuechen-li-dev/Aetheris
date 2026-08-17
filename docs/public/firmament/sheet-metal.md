# Sheet Metal

Authored Sheet Metal uses a specialized semantic model: a base region, named flanges and bend strips, and cuts on named planar regions. It can emit formed STEP, thickness-bearing flat STEP, and flat SVG; it also carries material, K-factor, DFM, semantic layout, and manufacturing definitions on qualified routes.

[`preview3-l-bracket-hole.firmament`](../../../fixtures/FirmamentV2/SheetMetal/preview3-l-bracket-hole.firmament) is the practical first example. Build the formed part and then flatten the same source:

```powershell
aetheris build fixtures/FirmamentV2/SheetMetal/preview3-l-bracket-hole.firmament --output artifacts/bracket-formed.step --json
aetheris sheetmetal flatten fixtures/FirmamentV2/SheetMetal/preview3-l-bracket-hole.firmament --step artifacts/bracket-flat.step --svg artifacts/bracket-flat.svg --json
```

Sheet Metal holes use `Hole Mount { On: Base; Center: (40mm, 20mm); Diameter: 8mm; }`. Model-domain `Hole<Shaft>` is deliberately rejected here with `sheetmetal-hole-domain-syntax`. `On` targets a named planar semantic region (`Base`, a named flange, or a verified exposed region/path), not `face(+Z)`. Manufacturing PMI likewise uses `DatumFeature A { Target: Base; }` inside `Pmi`, together with a named `Manufacturing` block; Model-domain `Datum`/`face(-Z)` syntax is rejected with `sheetmetal-pmi-domain-syntax`.

The JSON `part.features` count is the number of lowered cuts/openings in `SheetMetalPartIr`; bends are reported separately as `part.bends`. Authoring and imported reconstruction are separate workflows. Commands such as `sheetmetal recognize`, `recover-flat`, and `recover` operate on imported STEP and may be partial; they do not turn arbitrary geometry into authoritative authored intent.
