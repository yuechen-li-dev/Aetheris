# FIRMAMENT-HOLE-SELECTOR-X0 — canonical Hole wall selection

Verdict: **Accepted for the bounded shaft-wall contract.** A compiler-owned Hole wall formats as `face(H.Wall)`, parses into a typed `FirmamentV2HoleWallSelector`, binds `H` to the semantic Hole declaration, and resolves the `HoleWallFace` role against current construction correspondence. `HoleDiameter` PMI is the existing legal use site. Helios copies the Aetheris-provided selector and rebuilds source containing that PMI record with zero diagnostics.

## Grammar audit

The older `Selection WallH { Target: HoleWall Source: Hole(H) Require: NonEmptyFaceSet }` is part of the advanced profile/compose selection grammar. The `Selection` keyword dispatches a simple Box + Hole file into the advanced adapter, where that body fails with `firmament-v2-phase3-edge-finish-syntax-invalid`. It was a named selection declaration, not a short target expression consumable by native PMI. X0 adds only `face(<HoleName>.Wall)` as a Hole-wall target for `HoleDiameter`; it does not change or alias the advanced declaration grammar. Existing axis `face(+Z)`, imported `face(#id)`, and named-hole PMI targets remain on their existing paths.

## Round trip

| Stage | Evidence |
| --- | --- |
| Construction | `Hole<Shaft> H` lowers to feature `Body.H`; wall descendant has `HoleWallFace`, source `hole:Body.H`, and a BRep face ID. |
| Format | The formatter requires that role, source feature, face, and stable addressability; it emits `face(H.Wall)` from the Hole name. |
| Parse/bind | Native PMI parsing records typed `FirmamentV2HoleWallSelector(H, Body)`. Unknown, non-Hole, and ambiguous symbols have distinct diagnostics. |
| Resolve | `SemanticTopologySelectionResolver` resolves source `hole:Body.H` plus `HoleWallFace` to the current face set. It permits one semantic role to have several faces. |
| Use | `HoleDiameter WallDiameter { Target: face(H.Wall) Value: 8mm }` exports normal AP242 diameter PMI bound to the selected Hole. |

For two shaft Holes, the bounded Boolean builder reports each wall's feature ID and face ID **when the face is constructed**. The composite materializer carries both roles into the direct display map; `face(H1.Wall)` and `face(H2.Wall)` stay distinct. No cylinder recognition or STEP reimport provides source identity.

## Helios and verification

The browser witness picks the inside wall, sees `Body.H`, copies `face(H.Wall)`, navigates to the Hole declaration, selects that declaration, edits the diameter, picks the wall again, uses `Reference Hole Wall in Source`, pastes the copied selector into its `HoleDiameter` target, and rebuilds with zero diagnostics. Helios withholds its planar `Reference Face in Source` action for the cylindrical wall. Web SDK selection exposes the selector, source range, role, origin feature, and build revision.

A fresh UI-only run created a Box + Hole project, clicked the inside wall, and saw `Body.HeliosHole1`, `HoleWallFace`, and `face(HeliosHole1.Wall)` in the Inspector. `Reference Hole Wall in Source` inserted `Pmi { HoleDiameter SelectedWall1 { Target: face(HeliosHole1.Wall) Value: 6mm } }`; rebuild completed with zero diagnostics. The tester used only the UI and did not inspect the source grammar or parser. The first blind run had exposed the missing discoverable use-site action, which the contextual Inspector action now supplies.

Focused native tests cover format/parse/bind/resolve, STEP reimport, diameter and position edits, rename, deletion, non-Hole diagnostics, and two named Holes. Browser Box and Hole tests cover the actual pick/copy/paste/rebuild path. Existing PMI/FEA/SheetMetal suites are regression gates.

## Limits

Blind-hole Bottom, entry/exit rims, counterbore sections, patterned-hole instance selectors, and imported Hole-like topology are outside X0. The typed PMI target is qualified only for `HoleDiameter`; a cylindrical wall is not admitted as a planar Datum. The current direct source map has one primary origin per wall and does not claim universal topological naming.
