# GarmentCode reference witnesses

These are sewing-pattern outputs from the real MIT-licensed GarmentCode
`Shirt(body, design, fitted=False)` program at commit
`d449629979028123a5c4dc9e732a2ec19b7fce31`, not patterns inferred from a picture.
Credit: Maria Korosteleva and Olga Sorkine-Hornung, *GarmentCode: Programming
Parametric Sewing Patterns*, SIGGRAPH Asia 2023.
Source: https://github.com/maria-korosteleva/GarmentCode.
Keep `LICENSE.txt` with these derivatives; see root `THIRD_PARTY_NOTICES.md`.

- `shirt.json`: upstream t-shirt preset, length multiplier 1.2, sleeves enabled.
- `dress.json`: same recipe, length multiplier 2.2, sleeveless enabled.
- `*-curves.json`: lengths and five samples per edge from upstream's own curve reader.
- `*-design.yaml`, `body-measurements.yaml`: reproducible upstream inputs, centimetres/degrees.
- `provenance.json`: exact revision, modifications and source hashes.

`scripts/research-garmentcode.py` generates these witnesses from an external
checkout. It canonicalizes component/seam presentation order because upstream
component discovery uses a Python set; curve geometry and seam references are
unchanged. The generated JSON repeats byte-for-byte across runs. Python and its
dependencies belong to the ignored research environment, not Aetheris runtime.

Import through `aetheris garment import-garmentcode`. The sibling
`garmentcode-dress.firmament` is its exact editable translation. The sibling
`garmentcode-dress-template.firmament` adds a pure `hemDrop` parameter to each
panel recipe. Zero preserves the original compiled cloth; 100mm extends the
hem without changing armholes, collar curves or panel placement.

These fixtures validate **cut geometry and interchange**, not completed drape
quality or suitability for arbitrary figures. Matching-body and Anatolia
experiments are retained under ignored `artifacts/local/garment/garmentcode-dress/`.
Body meshes are external inputs, not bundled fixtures or invented humanoid rigs.
