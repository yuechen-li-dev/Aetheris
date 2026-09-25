# TEXT-CODEX-FINISH-X2

## Verdict: Accepted for the bounded CODEX maker mark

The normalized X outline is valid. The former rejection came from an intersection split only 0.00000004535 mm from the cubic endpoint. The split parameter differed by 5.60898e-7, but its geometric separation was below the arrangement's existing 1e-7 mm tolerance. The section arrangement now deduplicates adjacent split parameters by both parameter and geometric distance. It retains the source cubic and removes only the tolerance-equivalent split; no glyph contour or font normalizer changed.

The ordinary section stack now lowers the full five-region CODEX profile set into a 0.2 mm Pocket. Its pocket-depth slab contains three material regions: the surrounding head material and the O and D counter islands. The threaded HexBolt comes from the existing `fixtures/Thread/hexbolt-threaded.firmament` build route. A bounded head-cap graft replaces its single planar top face with the section-stack top faces, pocket walls, and floor. The six exact circular rim edges are shared with the retained bolt shell. Thread geometry is unmodified. No general 3D Boolean or mesh fallback is involved.

The final body is enclosed and consistently oriented. STEP AP242 export and reimport pass the same mass-property checks. CLI STEP analysis reports one body, one shell, 295 faces, 691 edges, 408 vertices, and `enclosed-manifold`; all 76 linear-extrusion surfaces and 114 B-spline surfaces are supported. The imported STEP wireframe has no unsupported surface or curve families and pcurve recovery succeeds (maximum residual 7.81e-7 mm).

Reviewable artifacts are generated under ignored `artifacts/local/text-codex-finish-x2/`: `codex-threaded-hexbolt.step`, `codex-threaded-hexbolt-iso.png`, `codex-threaded-hexbolt-head.png`, and the corresponding SVGs. The head-on wireframe shows CODEX and both counters. Wireframe draws occluded bolt edges as well, so projected shank rings cross the lettering; the body and STEP checks, rather than that diagnostic image, establish manifold topology.

To regenerate the STEP, set `AETHERIS_MAKER_MARK_ARTIFACT_DIR` to that ignored directory and run `dotnet test Aetheris.Kernel.Firmament.Tests -c Release --filter FullyQualifiedName~CodexEngravesOnThreadedHexBoltAndRoundTripsStep -m:1`. Run `aetheris wireframe` on the resulting STEP with `--view iso` and `--view right` for the two SVGs.

No Firmament `Text` source syntax, schema, or LX change is introduced in this bounded finish. The maker mark is an explicit code-first construction over the existing compiled threaded HexBolt and normalized text regions.

## Verification

The Release solution build passed with zero errors. The fast Core lane passed 1,000 tests. The final full solution lane passed all 1,133 Core, 1,687 Firmament, and 99 SheetMetal tests. One CLI OBJ vertex-count baseline remained red (905 expected versus 908 actual); the full command therefore exited 1. The focused section suite passed 10 tests, including CODEX Pocket and threaded-bolt STEP roundtrip. The saved STEP SHA-256 is `E65B63D78C13F9B92B88810BA45B8D53126D1299DA4D1E1207474777EE6C4BC2`.
