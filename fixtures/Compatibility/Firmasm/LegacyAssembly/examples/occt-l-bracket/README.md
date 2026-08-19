# OCCT l-bracket extracted assembly fixtures (ASM-A2)

Source assembly: `testdata/step242/OCCT/l-bracket-assembly.step`.

Artifacts:
- `_part_001_bolt_extract.step`: transitive-closure extraction of `#72 = MANIFOLD_SOLID_BREP`.
- `_part_002_nut_extract.step`: transitive-closure extraction of `#351 = MANIFOLD_SOLID_BREP`.
- `_part_003_l_bracket_extract.step`: transitive-closure extraction of `#714 = MANIFOLD_SOLID_BREP`.
- `_part_001_bolt.step`: canonicalized via `aetheris canon` from `_part_001_bolt_extract.step`.
- `_part_002_nut.step`: canonicalized via `aetheris canon` from `_part_002_nut_extract.step`.
- `_part_003_l_bracket.step`: canonicalized via `aetheris canon` from `_part_003_l_bracket_extract.step`.
- `l-bracket-assembly.firmasm`: flattened `.firmasm` manifest with rigid transforms and deduplicated foreign STEP parts.

Bounded extraction summary:
- unique solids: 3 (`bolt`, `nut`, `l-bracket`)
- flattened instances: 7 (`3x bolt`, `3x nut`, `1x l-bracket`)
- hierarchy depth observed in source: 2 (top-level assembly -> nut-bolt subassembly -> part)
