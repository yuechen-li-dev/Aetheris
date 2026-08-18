# OCCT as1 flattened extracted assembly fixtures (ASM-A2.5b)

Source assembly: `testdata/step242/OCCT/as1.step`.

Artifacts:
- `_part_001_bolt_extract.step`: transitive-closure extraction of `#585 = MANIFOLD_SOLID_BREP`.
- `_part_002_l_bracket_extract.step`: transitive-closure extraction of `#943 = MANIFOLD_SOLID_BREP`.
- `_part_003_nut_extract.step`: transitive-closure extraction of `#76 = MANIFOLD_SOLID_BREP`.
- `_part_004_plate_extract.step`: transitive-closure extraction of `#1658 = MANIFOLD_SOLID_BREP`.
- `_part_005_rod_extract.step`: transitive-closure extraction of `#370 = MANIFOLD_SOLID_BREP`.
- `_part_001_bolt.step`: canonicalized via `aetheris canon` from `_part_001_bolt_extract.step`.
- `_part_002_l_bracket.step`: canonicalized via `aetheris canon` from `_part_002_l_bracket_extract.step`.
- `_part_003_nut.step`: canonicalized via `aetheris canon` from `_part_003_nut_extract.step`.
- `_part_004_plate.step`: canonicalized via `aetheris canon` from `_part_004_plate_extract.step`.
- `_part_005_rod.step`: canonicalized via `aetheris canon` from `_part_005_rod_extract.step`.
- `as1-assembly.firmasm`: flattened `.firmasm` manifest with rigid world transforms and deduplicated foreign STEP parts.
- `_extraction-summary.json`: extraction metadata (dedup key, root id, counts).

Bounded extraction summary:
- unique part families: 5 (`bolt`, `l-bracket`, `nut`, `plate`, `rod`)
- flattened instances: 18 (`bolt x6`, `l-bracket x2`, `nut x8`, `plate x1`, `rod x1`)
- hierarchy flattened from max depth 3 to one instance list with composed transforms
- part dedup key: `PRODUCT_DEFINITION` identity (+ shape-representation lineage)
