# CTC-03 manufacturing-release engineering interpretation

## Design basis and authority

The design basis is `../m8/ctc03-final.firmament` and its M8 formed and flat artifacts. The NIST AP242 file is reconstruction evidence only. The manufacturing successor will be an authored metric product definition; recovered values remain documented as provenance but do not drive production where they are unit-conversion or reconstruction artifacts.

The M8 flat is the authoritative visual basis for interpretation. It contains one connected exact blank, seven bend lines, all 17 cut contours, formed-flange relief profiles, and the partial-span 45-degree service flange. The earlier M1 preview is not representative of the final design basis.

## Functional interpretation

- `MainDeck` is the primary equipment-support deck. Its two long slots provide ventilation or cable/airflow clearance; the two large circular openings provide access or service clearance; the four-hole line is a repeated deck attachment pattern.
- `FrontWall`, `RearWall`, `LeftWall`, and `RightWall` stiffen and locate the deck after forming.
- `FrontMountingFlange` and `RearMountingFlange` provide two-hole mounting interfaces. Their central free-edge recesses are intentional clearance features, not incidental reconstruction noise.
- `AngledServiceFlange` is a local connector/service interface. The large central clearance and four small attachment holes form one functional interface and should be controlled together.
- The wall-end and flange reliefs are manufacturing clearances that permit the seven bends without overlapping material. They need valid blank geometry and deburred edges, but do not require independent dimensional PMI.

## Value classification

Likely inch-conversion lineage includes 1.905, 4.7625, 6.35, 8.89, 11.1252, 12.7, 15.875, 19.05, 25.4, 31.75, 38.1, 44.45, 50.8, 63.5, 88.9, 101.6, 114.3, 127, 203.2, and related sums. These are useful evidence of the source design family but are not automatically suitable metric production nominals.

Values such as 24.43226, 25.40254, 30.735796052, 43.47972, 48.514, and 61.59254 are treated as reconstruction, formed-placement, or bend/flattening consequences. They will not appear as controlled production dimensions.

Driving dimensions are the sheet thickness; deck envelope; flange/wall lengths; bend angles and inside radii; cut sizes; pattern centres, pitches, and quantities; and the service-interface layout. Derived BRep coordinates, bend tangencies, exact flat extents, and profile transition coordinates remain consequences of those drivers.

## Inspection-critical features and datum proposal

- Primary datum `A`: broad `MainDeck` planar region. It establishes the functional support plane.
- Secondary datum `B`: `FrontWall` planar region. It establishes the longitudinal end orientation after forming.
- Tertiary datum `C`: `LeftWall` planar region. It establishes the transverse side orientation.
- The front and rear mounting-hole patterns merit position control to `A|B|C` because they locate the formed part in an installation.
- The five-hole service interface merits a position control to the same frame because connector fit depends on the formed 45-degree flange.
- Deck ventilation and access openings require controlled size and ordinary location tolerance, but not decorative GD&T.

This deliberately avoids copying the source model's six-datum, multi-frame GD&T presentation.

## Process interpretation

The part is suitable for CNC laser cutting or punching from commercial aluminium sheet followed by CNC press-brake forming. All holes and profile cuts should be made in the flat. A modest inside radius, ordinary bend-angle tolerance, generous cut-to-bend distances, and the existing relief topology make the part compatible with a competent general sheet-metal shop. No machining, exotic tooling, or drawing-space PMI presentation is assumed.

