# CTC-03 metric manufacturing release

## Release decision

This directory is the manufacturing-authoritative successor to the M8 reconstruction in `../m8/ctc03-final.firmament`. The primary shop exchange artifact is `ctc03-manufacturing-ap242.step`; `ctc03-manufacturing.firmament` remains the source of design and manufacturing intent. The flat STEP and SVG are derived fabrication/review artifacts, not competing product definitions.

Within the assumed general-purpose CNC laser/punch and press-brake capability, the release is suitable to manufacture and inspect without relying on the NIST source PMI. The recognizable M8 topology is retained: 15 regions, seven bends, 17 cut features, both mounting interfaces, the deck openings/slots, and the partial-span five-hole service interface.

## Engineering assumptions

- ASTM B209 5052-H32 aluminium sheet, mill finish.
- Nominal thickness 2.0 mm, commercial tolerance +/-0.12 mm.
- CNC laser cut or CNC punch all profiles before forming.
- CNC press brake with 6 mm inside radius and K-factor 0.42.
- General cut-profile tolerance +/-0.5 mm, formed linear tolerance +/-0.8 mm, and bend-angle tolerance +/-1 degree unless specifically controlled.
- Hole/slot size tolerances are ordinary production values: +/-0.15 mm for mounting/service holes and +/-0.20 mm for access openings and slots.
- Remove burrs and sharp edges; break edges 0.2-0.5 mm. Protect datum A from raised burrs, gouges, and tooling damage.

See [engineering-interpretation.md](engineering-interpretation.md) for feature purpose and recovered-value classification, and [manufacturing-specification.md](manufacturing-specification.md) for the released process specification.

## Metric normalization decisions

| Feature/requirement | Reconstructed/source value | Final manufacturing value | Why |
| ------------------- | -------------------------: | ------------------------: | --- |
| Sheet thickness | 1.905 mm | 2.0 +/-0.12 mm | Commercial metric sheet; source value is exact 0.075-inch lineage. |
| Main deck | 241.3 x 368.3 mm | 240 x 370 mm | Sensible metric envelope without changing the product character. |
| Common inside bend radius | 6.35 mm | 6.0 mm | Available metric press-brake tooling; still a generous 3t radius. |
| Front/rear wall height | 61.59254 mm | 62 mm | Removes reconstructed formed-coordinate precision. |
| Side-wall heights | 44.45 / 31.75254 mm | 45 / 32 mm | Metric production nominals. |
| Front/rear mounting flange | 43.47972 / 44.45 mm | 44 mm both | One coherent symmetric interface value. |
| Deck fastener pattern | 4x dia 15.875 mm at 44.45 mm pitch | 4x dia 16.0 +/-0.15 mm at 45 mm pitch | Practical metric cut size and pitch. |
| Large deck openings | dia 50.8 / 38.1 mm | dia 51.0 / 38.0 +/-0.20 mm | Metric access-opening sizes. |
| Vent slots | 19.05 x 88.9 mm at 63.5 mm pitch | 20 x 90 +/-0.20 mm at 65 mm pitch | Metric laser-cut ventilation geometry. |
| Formed mounting holes | dia 11.1252 mm at 203.2 mm pitch | 2x per flange, dia 11.0 +/-0.15 mm at 200 mm pitch | Normal M10 clearance and symmetric metric layout. |
| Service interface | dia 27.051 and 4x dia 4.7625 mm at 38.1 mm pitch | dia 27.0 and 4x dia 5.0 +/-0.15 mm at 38 mm pitch | Removes reconstruction/conversion precision while preserving fit intent. |
| Service attachment span/tab | 127 / 101.6 mm | 125 / 100 mm | Metric interface envelope with unchanged partial-span topology. |

The retained base origin is explicitly provenance/comparison state, not controlled PMI. Tangency coordinates, flat extents, and BRep placement coordinates are derived and deliberately absent from the manufacturing contract.

## Datum and GD&T rationale

Datum A is the broad `MainDeck` support plane. Datum B is the formed `FrontWall`, establishing the longitudinal end orientation. Datum C is the formed `LeftWall`, establishing transverse orientation. This single A|B|C frame is enough to locate the installation interfaces.

The front/rear mounting pairs carry 0.8 mm position to A|B|C. The five-hole service interface carries 0.6 mm position to A|B|C. These values account for cut accuracy plus realistic press-brake accumulation. Other cuts receive size tolerances and ordinary semantic locations; extra flatness/profile frames were not justified by known function.

## PMI and annotation inventory

| PMI / Annotation | Target | Engineering purpose | AP242 representation | Export verified |
| ---------------- | ------ | ------------------- | -------------------- | --------------- |
| Datums A, B, C | MainDeck, FrontWall, LeftWall | Establish one coherent inspection frame | `DATUM_FEATURE`, `DATUM`, face `GEOMETRIC_ITEM_SPECIFIC_USAGE` | Yes |
| Sheet thickness | Whole part | Control purchased material | semantic thickness dimension + bilateral tolerance | Yes |
| Deck width/length | MainDeck | Control the primary envelope | semantic linear dimensions + bilateral tolerance, associated to datum-A face | Yes |
| 4x base fastener diameter | BaseFastenerPattern | Attachment clearance and repeated quantity | semantic diameter, tolerance, quantity, four cylindrical-face associations | Yes |
| Large opening diameters | LargeAccess51, LargeAccess38 | Access clearance | semantic diameters + tolerances, cylindrical-face associations | Yes |
| 2x vent slot width/length | VentSlots | Ventilation/cable clearance | semantic linear dimensions, tolerances, quantity, slot-wall associations | Yes |
| Front/rear 2x mounting diameters | FrontMountHoles, RearMountHoles | Installation clearance | semantic diameters, tolerances, quantities, cylindrical-face associations | Yes |
| Service-interface diameters | ServiceHole, ServiceInnerHoles, ServiceOuterHoles | Connector clearance and attachment | semantic diameters, tolerances, quantities, cylindrical-face associations | Yes |
| 0.8 position A|B|C | FrontMountHoles, RearMountHoles | Locate formed installation patterns | `POSITION_TOLERANCE`, datum system/reference compartments, face associations | Yes |
| 0.6 position A|B|C | Three service-interface patterns | Preserve connector fit after forming | `POSITION_TOLERANCE`, datum system/reference compartments, face associations | Yes |
| Material/thickness note | Whole part | Purchasing requirement | semantic target-bearing shape aspect/property text | Yes |
| General-tolerance note | Whole part | Default manufacturing capability | semantic target-bearing shape aspect/property text | Yes |
| Cut/deburr note | Whole part | Sequence and edge safety | semantic target-bearing shape aspect/property text | Yes |
| Forming note | Whole part | Tool/radius and crack acceptance | semantic target-bearing shape aspect/property text | Yes |
| Grain-direction note | Whole part | Reduce bend-cracking risk | semantic target-bearing shape aspect/property text | Yes |
| Protect datum A | MainDeck | Preserve support surface | semantic note associated to actual deck face | Yes |
| Mount-hole rework restriction | FrontMountHoles | Prevent uncontrolled post-form alteration | semantic note associated to both hole-wall faces | Yes |
| Cut service interface before form | AngledServiceFlangeBend | Avoid distorted/reworked connector interface | semantic note associated to both bend-cylinder faces | Yes |

## AP242 support matrix

| Capability | Release support | Evidence |
| ---------- | --------------- | -------- |
| Formed exact BRep | Supported | Reimports as one enclosed manifold: 129 faces, 306 edges, 198 vertices |
| Millimetre length unit | Supported | `(LENGTH_UNIT() ... SI_UNIT(.MILLI.,.METRE.))` |
| Datum feature/identification | Supported | 3 formal datum records reinspected |
| Linear/thickness/diameter dimension | Supported subset | 13 dimension records reinspected with values and bilateral tolerances |
| Repeated-feature quantity | Supported | Pattern quantities 2 or 4 survive reinspection |
| Position tolerance with datum references | Supported subset | 5 formal position records retain A, B, C |
| Feature/face association | Supported | `GEOMETRIC_ITEM_SPECIFIC_USAGE` links semantic targets to exported `ADVANCED_FACE` entities |
| Engineer annotations | Supported | 8 semantic target-bearing notes reinspected |
| Graphical PMI orientation/layout | Intentionally absent | No `ANNOTATION_PLANE`; presentation is left to a later projector |

## DFM and validation results

- Firmament validation: valid, zero fatal diagnostics, zero warnings.
- Formed model: 15 regions, seven bends, 17 features; BRep export preflight valid.
- Flat model: one connected exact analytic blank, seven bend lines, 17 cut loops, no region overlap.
- DFM: overall `Pass`; minimum radius/flange, corner resolution, cut-to-bend, cut-to-edge, exact-blank, and overlap rules pass.
- Formed AP242 reimport: one enclosed manifold, 129 faces, 306 edges, 198 vertices.
- Flat STEP reimport: one enclosed manifold, 116 faces, 342 edges, 228 vertices.
- Semantic AP242 reinspection: 3 datums, 13 dimensions, 5 position controls, 8 annotations; no inspector diagnostics.
- AP242 deterministic rebuild: identical SHA-256 `29A02DBDCFF8CED5C33AFA953D2C61321D6547578BA22B661D0621D025201E7E`.
- Full solution validation: zero build warnings/errors and 2,942 tests passed when test projects were run serially (the FrictionLab test assembly currently contains no discoverable tests).
- M8 normalization comparison: all seven bend identities/adjacencies/angles and all 17 feature pairings survive. Formed p95 deviation is 2.883 mm; flat-contour p95 is 3.220 mm; maximum feature-centre shift is 3.137 mm; no flat overlap was introduced.
- Automated release tests cover binding/DFM, deterministic AP242/reimport/reinspection, geometric face association, generic invalid-target rejection, and bounded M8 normalization.

The general reconstruction comparator reports `Fail` because its 0.1 mm reconstruction-fidelity threshold intentionally rejects the authorized metric normalization. That status is not treated as a manufacturing defect; the bounded topology and residual checks above are the release acceptance criteria.

## Remaining limitations

- This is a bounded AP242 production subset, not a universal ASME Y14.5/ISO 1101 engine. Position is supported; modifier stacks, composite frames, profile, flatness, and projected tolerance zones are not implemented here.
- Semantic notes carry target identity and resolved faces where applicable, but no drawing view, leader, glyph orientation, or annotation layout. This is deliberate.
- Whole-part notes and thickness are associated to the product/part target rather than an arbitrary face.
- STEP import currently reports the length-unit basis as assumed even though the exporter explicitly writes the millimetre SI unit entity. Preserving imported unit provenance in the in-memory BRep remains separate infrastructure work.
- The source does not establish coating, cosmetic class, hardware, or final assembly interfaces. This release therefore specifies mill finish and does not invent them.

## Deliverable hashes

| Artifact | SHA-256 |
| -------- | ------- |
| `ctc03-manufacturing-ap242.step` | `29A02DBDCFF8CED5C33AFA953D2C61321D6547578BA22B661D0621D025201E7E` |
| `ctc03-flat.step` | `19D961A647C83BE767253074519D95511E60AF21799C5053BF2684F66B53A741` |
| `ctc03-flat.svg` | `931F760D6EE3A646621C06CE9E6C168FD0451A5AFA33C7D738D2D422CAEBC257` |
| `ctc03-flat-preview.png` | `2B5B71A09121C6D28C65E339F3046733BA1EC0D7E96C9B23203F2E5A9807692C` |
