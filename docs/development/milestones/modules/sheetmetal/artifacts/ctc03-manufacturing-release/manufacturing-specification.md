# CTC-03 metric manufacturing specification

## Release assumptions

- Material: ASTM B209 5052-H32 aluminium sheet, mill finish.
- Nominal thickness: 2.0 mm; commercial thickness tolerance +/-0.12 mm.
- Blank process: CNC laser cut or CNC punch; all cut features made before forming.
- Forming: CNC press brake, K-factor 0.42 for the released flat calculation.
- Inside bend radius: 6.0 mm unless explicitly stated; bend angles 90 degrees except the 45-degree service flange.
- Unspecified cut-profile dimensions: +/-0.5 mm. Unspecified formed linear dimensions: +/-0.8 mm. Unspecified bend angles: +/-1 degree.
- Feature-size tolerances: ordinary laser/punch capability, generally +/-0.15 mm on released hole and slot sizes.
- Edges: remove burrs and sharp edges; break 0.2-0.5 mm. No gouges or raised burrs on datum A.
- Grain direction: orient major 90-degree bend axes across the rolling direction where practical; reject visible bend cracking.

## Metric normalization

| Feature/requirement | Reconstructed/source value | Final manufacturing value | Why |
| ------------------- | -------------------------: | ------------------------: | --- |
| Sheet thickness | 1.905 mm | 2.0 +/-0.12 mm | Commercial metric gauge and ordinary 5052 availability. |
| Main deck | 241.3 x 368.3 mm | 240 x 370 mm | Removes exact inch lineage while preserving envelope and layout character. |
| Common inside bend radius | 6.35 mm | 6.0 mm | Ordinary metric tooling; preserves the intentionally generous bend. |
| Front/rear wall height | 61.59254 mm | 62 mm | Reconstruction/formed consequence normalized to a controlled nominal. |
| Left/right wall height | 44.45 / 31.75254 mm | 45 / 32 mm | Metric production nominals. |
| Mounting flanges | 43.47972 / 44.45 mm | 44 mm both | One symmetric manufacturable interface definition. |
| Deck fastener holes | diameter 15.875 mm, 44.45 mm pitch | 4x diameter 16.0 +/-0.15 mm, 45 mm pitch | Metric cut size and pitch; pattern remains recognizable. |
| Large deck openings | diameter 50.8 / 38.1 mm | diameter 51.0 / 38.0 +/-0.20 mm | Metric nominal access openings. |
| Vent slots | 19.05 x 88.9 mm, 63.5 mm pitch | 20 x 90 +/-0.20 mm, 65 mm pitch | Metric laser-cut ventilation geometry. |
| Formed mounting holes | diameter 11.1252 mm, 203.2 mm pitch | 2x per flange, diameter 11.0 +/-0.15 mm, 200 mm pitch | Normal M10 clearance and symmetric 20 mm end offsets on a 240 mm width. |
| Service clearance | diameter 27.051 mm | diameter 27.0 +/-0.15 mm | Removes reconstruction precision without changing interface character. |
| Service attachment holes | diameter 4.7625 mm, 38.1 mm pitch | 4x diameter 5.0 +/-0.15 mm, 38 mm pitch | Practical metric attachment clearance. |
| Service attachment span/tab | 127 / 101.6 mm | 125 / 100 mm | Metric interface envelope; preserves partial-span topology. |

## Datum and GD&T rationale

Datum `A` is the main deck support plane, `B` is the formed front wall, and `C` is the formed left wall. This three-plane frame is sufficient to orient and locate the product without datum proliferation. A 0.8 mm position tolerance to `A|B|C` controls the front/rear mounting patterns, reflecting laser-cut accuracy plus accumulated press-brake placement. A 0.6 mm position tolerance to `A|B|C` controls the service-interface hole pattern. Other openings use size tolerances and basic semantic locations; additional profile/flatness frames would add inspection cost without demonstrated function.

## Planned annotations

- Whole part: material, thickness, general tolerance, cut-before-form, grain, deburr, and mill-finish requirements.
- Datum A / `MainDeck`: protect the support surface from forming/tooling gouges.
- Mounting patterns: do not enlarge or rework holes after forming without engineering approval.
- Service interface: cut the complete five-hole interface before forming and keep the flange free of bend distortion at the interface.

