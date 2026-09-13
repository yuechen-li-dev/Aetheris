# GEOM-SYMMETRY-X1 — semantic Mirror and radial Pattern

## Executive verdict

**Accepted for the bounded Profile and planar feature lanes documented below.** Firmament can express reflection and rotational symmetry as authoring-time derivations, retain explicit destination and instance identity, restore right-handed frames, and lower the results through ordinary Profile, Hole, Revolve, BRep, and STEP authorities. No finished BRep is reflected or semantically reconstructed.

## Audit and authority

The existing finite Pattern authority is `CanonicalStaticAuthoring`: it expands typed Static arrays or named Sets in source order, retains Pattern/instance associations, enforces a 1,024-instance bound, and erases the collection before material AIR. Profile/Path source resolves to `ResolvedProfile2D`; `ConstructionPlane.TryTrace` already reconstructs orthonormal right-handed frames. Feature functions expand before Template/static specialization. Assembly occurrences already own rigid positive-determinant placements but do not admit reflected occurrence transforms.

Existing kernel `CirMirror` names are analysis mirrors, not geometry reflection. `BodyState` rebuilds semantic construction plus typed operations and has no safe reflection authority. No unsafe post-BRep reflection was found or added. X1 therefore adds one bounded semantic expander at the existing static boundary: source Profile/feature plus Plane/Axis becomes ordinary semantic declarations, then the unchanged material pipeline runs.

## Transform matrix

| Semantic type | Mirror | Radial rotation | Notes |
| --- | --- | --- | --- |
| Point | Yes | Yes | analytic point transform, including translation term |
| Vector | Yes | Yes | direction only; no point translation |
| Axis | Yes | Yes | origin/direction normalized by `Direction3D` |
| Plane | Yes | Yes | origin/normal transformed analytically |
| DatumFrame | Yes | Yes | typed transform API; reflection reconstructs right-handed basis |
| Profile | Yes | Deferred | line, circular arc, circle, and ellipse carriers; canonical winding restored |
| Span | Deferred | Deferred | no copied helper curve or ambiguous selector rebinding admitted |
| Feature | Bounded | Bounded | concrete Point2-centered Hole qualified end-to-end; Boss/Pocket deferred |
| Model / Struct | Deferred | Deferred | no arbitrary record walk or second authoring IR |
| Assembly occurrence | Deferred | Deferred | current occurrence matrices require proper rigid transforms; no negative determinant placement |

## Handedness

For a source frame `(X,Y,Z)`, naively reflecting all axes produces `det(RX,RY,RZ) = -1`. X1 preserves reflected X and Z, reconstructs `Y' = Z' x X'`, then recomputes X through the same orthonormal law. Tests prove the result determinant is `+1`, unit and orthogonal axes are retained, and reflecting twice across the same Plane recovers the original frame. Profile reflection similarly reverses each curve and loop order after geometric reflection so outer/inner material orientation remains canonical. Circular arcs retain the mirrored locus, endpoints, radius, and corrected traversal.

## Identity, provenance, and `with`

Authors name mirror destinations; no English left/right renaming exists. CLI inspection reports `kind`, source, Plane, destination, handedness correction, member source identities, and the derivation chain. `Feature RightCustom = Right with { ... }` performs checked immutable field replacement after Mirror; inspection retains source → Mirror → local override provenance. There is no `MirrorExcept`, `SymmetryBreak`, or override sublanguage.

Radial Pattern generates `Pattern.Instance0` through `Pattern.InstanceN-1` in stable ordinal order. Instance zero is the zero-angle source construction. Full distribution uses `[0,2*pi)`; partial distribution includes both endpoints. The six-hole flagship reports angles `0, 60, 120, 180, 240, 300` degrees and one radius. Count one is valid; count zero is rejected.

## Qualified evidence

- `mirrored-profile.firmament`: a `Triangle2<Explicit>` source boundary lowers to the reflected asymmetric Profile, which extrudes to an enclosed five-face manifold with bounds X `[-16,-4]`, Y `[-6,8]`, Z `[0,8]` after STEP reimport.
- `mirrored-hole.firmament`: two independent semantic Hole features materialize at X `+24/-24`; STEP reimport reports two cylindrical faces and an enclosed manifold.
- `mirrored-feature-with-override.firmament`: left diameter 6 mm and mirror-derived overridden right diameter 8 mm materialize without special exception syntax.
- `radial-bolt-circle.firmament`: six independent Hole features at radius 26 mm; STEP reimport reports six cylinders, 12 faces, and an enclosed manifold.
- `partial-radial-pattern.firmament`: five holes over `half` use inclusive 45-degree spacing and reimport as five cylinders.
- `mirrored-revolve.firmament`: an axis-aligned `Rect2` source boundary lowers to the mirrored Profile, feeds the ordinary Revolve plan, and reimports as an enclosed six-face analytic manifold.

Repeated radial builds are byte-identical (`E499406CD574F360412A0C5C095838116BDB73CF350F9EDD6FFD7A730B6C7F32`). A fresh self-contained packaged CLI produced the same radial hash and the same mirrored-profile hash as the repository CLI (`9F9D2E2244AFE7ADB66E853D97137BCB89DFAD4597FB589F2E5C861DEA2DF5D8`). `inspect --json` exposes the retained mirror and radial transformation records; build output exposes the independent ordinary materialized features.

Release validation passed with a zero-warning Release solution build, 1,448 Firmament tests, 435 CLI tests, all six Symmetry canonical fixture actions, all seven invalid-fixture CLI checks, STEP export/reimport, deterministic repeat, packaged parity, documentation/link/layout tests included in the CLI suite, and `git diff --check`. The repository-wide canonical script still exits nonzero after every fixture action passes because its pre-existing content-policy guard rejects `Assembly/annular-pair.firmament` for legacy explicit assembly placement; this milestone does not alter that unrelated fixture.

## Explicit boundaries

X1 does not add Scale, affine matrices, user transform expressions, post-topology duplication, broad PMI transformation, FEA-result transformation, Thread/helix handedness rules, Sculpt reflection, whole-Model traversal, or assembly occurrence patterns. Profile radial repetition and Span/selector structural rebinding are deferred rather than implemented with copied geometry or nearest-face lookup. ENGINE-X0 could later simplify paired head/seat and bank feature authoring, but its working assembly demo is not rewritten in this milestone.
