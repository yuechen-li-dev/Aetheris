# BREP-MULTIREGION-SLAB-X0

## Verdict: Meaningful progression

The planar Boss/Pocket stack now admits multiple material regions in a slab. A finite-depth O Pocket retains its center counter as a separate section region; both regions meet the plate below the pocket floor. D and two O counters also pass on a plate. A multi-island AETHERIS Boss produces one enclosed, consistently oriented body. These cases export and reimport through STEP AP242.

The complete normalized CODEX profile set still fails arrangement classification before solid lowering. The diagnostic is `arrangement-rejected:degenerate-fragment:compose:Mark4.Text.Glyph[4].Region[0].Outer.Outer.Segment[5].part0` on the X glyph. The threaded HexBolt was inspected with the CLI, but CODEX was not engraved into it. No bolt topology, bolt STEP, maker-mark render, or source-language Text claim is made.

## Section and connectivity model

The former slab held one `PrismaticSectionRegion` and `Compose` rejected two outer loops. Each slab now carries an ordered `MaterialRegions` set; each region still has one outer loop and zero or more inner loops. Region order is by descending signed area, then source stable ID. The first-region property remains for legacy single-region consumers; CLI inspection now reports the complete region set and aggregate area.

Transition subtraction works across both neighboring region sets. It creates separate planar caps at a counter floor and the surrounding pocket floor, and the emitter creates vertical walls for every region boundary. After topology construction, face adjacency through shared edges checks whether the actual shell is connected. A through-cut plate that yields two 3D solids is rejected as `compose-rejected:disconnected-3d-solid:components=2`; the former half-depth fixture was connected through its lower slab and was corrected to a through cut. No bridges, 3D Boolean path, or mesh representation were introduced.

## Evidence and remaining boundary

`BezierSectionStackTests` covers O, D, two O counters, and multi-island Boss, with enclosure, orientation, exact cubic surface, and STEP roundtrip checks. The existing one-region Boss/Pocket cases remain in that suite. The normalized CODEX X region is valid upstream; its arrangement creates a fragment whose midpoint tangent and endpoint chord are both below the section classifier's degeneracy tolerance. The classifier now tries the chord for a stationary cubic midpoint, then rejects the genuinely short fragment rather than silently dropping it. Resolving that tolerance and topology boundary is the next prerequisite to CODEX bolt integration.

The source Text syntax, generated schema, LX completion, performance breakdown, and O/CODEX/AETHERIS display renders remain pending because CODEX has not lowered through the production solid path.

## Verification

- Release solution build passed with 0 errors; the existing WebAssembly warnings remain.
- Fast Core lane: 1,000 passed. Full solution lane: 1,133 Core and 1,686 Firmament tests passed; overall exit code 1 reflects the same two CLI and five SheetMetal baseline failures recorded in the preceding milestones. The full log is in ignored `artifacts/local/multiregion-full-test.log`.
- After the final semantic probe and report changes, the focused Bézier section, disconnected-solid, and semantic Boss/Pocket tests passed (30 tests); Aetheris.CLI rebuilt with 0 warnings and 0 errors.
- CLI inspection of `fixtures/Thread/hexbolt-threaded.firmament` succeeded. It is inspection evidence only, not an engraving or STEP result.

## Downstream status (TEXT-CODEX-FINISH-X2)

The later bounded X2 work collapses the tolerance-equivalent X split during section arrangement, then engraves the normalized CODEX regions into the accepted threaded HexBolt head. The resulting body is enclosed and STEP-roundtrips; see `TEXT-CODEX-FINISH-X2.md`. The X0 verdict above remains its historical result.
