# PREVIEW3-HARDEN-A3b

## Executive result

Yes. Users can author ordinary connected bosses and finite, floored pockets directly with first-class `Boss` and `Pocket` declarations. Both retain semantic identity while lowering through the existing profile-composition section stack. No general Boolean authoring was added.

## Boss contract

- Target: the active `Compose` body.
- Support: `On: Top` (equivalently the admitted `+Z` support).
- Profile: an existing admitted closed line/arc Profile; A3b did not widen profile vocabulary.
- Height: positive and finite.
- Connectivity: the profile must make proper region contact with the host support. Disjoint and point/tangent-only results fail before materialization.
- DFM: positive height, admitted profile, valid support, and connected material.
- Lowering: `PrismaticBossFeature` -> semantic `PrismaticProfileOperation(Add)` -> existing `PrismaticSectionStackConstruction` -> BRep -> AP242.

## Pocket contract

- Target: the active `Compose` body.
- Entry/support: `On: Top`; the feature is accessible along the admitted inward `-Z` route.
- Profile: an admitted closed line/arc Profile fully enclosed by stock; edge-breaking notches are not Pocket.
- Depth: positive and finite. Pocket cannot silently become through-all.
- Remaining floor: `Base.To - Depth - Base.From`, equivalently `hostThickness - Depth` for the admitted support direction.
- Minimum floor: explicit Pocket value, then template `minimumFloorThickness`, then existing template `minimumWallThickness`, then the documented bounded `1mm` Preview 3 default.
- DFM: invalid target/profile/depth, through depth, and insufficient floor all fail with stable `firmament-pocket-*` diagnostics containing engineering quantities.
- Lowering: `PrismaticPocketFeature` -> semantic `PrismaticProfileOperation(Remove)` -> the same section-stack/BRep/AP242 path as existing bounded Compose removal.

## Why no arbitrary Booleans

Firmament exposes engineering intent, not arbitrary CSG. `Sphere` remains an analytic standalone solid; `Pocket` is a finite prismatic feature. Sphere-from-Block subtraction, hemispherical special casing, and public `Union` / `Subtract` / `Intersect` remain outside Preview 3. A future spherical-seat or tool-profile feature requires its own manufacturability contract.

## Combination matrix

| Part/combination | Build | STEP reimport | DFM | Result |
| ---------------- | ----- | ------------- | --- | ------ |
| Cylindrical Boss + through shaft Hole | Pass | Enclosed, orientation-consistent | Pass | Qualified in canonical mounting-block witness |
| Boss + Counterbore + Pocket | Pass | Enclosed | Pass | Qualified by focused semantic test |
| Rectangular Boss + Pocket + EdgeFinish | Pass | Enclosed | Pass | Qualified on admitted line-only top boundary |
| Boss + Hole + Pocket | Pass | Enclosed, expected `[-20,-12,0]..[20,12,16]` bounds | Pass | Canonical practical witness |
| Pocket depth 4 mm in 10 mm stock, local floor 2 mm | Pass | Same practical witness | 6 mm floor | Accepted |
| Pocket depth 9.5 mm in 10 mm stock, required floor 2 mm | Reject | Not emitted | 0.5 mm floor | `firmament-pocket-minimum-floor-thickness` |
| Pocket depth 10/11 mm in 10 mm stock | Reject | Not emitted | 0/-1 mm floor | `firmament-pocket-through-depth` |
| Compose-host Countersink | Not admitted | Not emitted | Explicit boundary | Existing composed section-stack hole family remains Shaft/Counterbore; general Model countersink is unchanged |

The canonical circular witness has exact section-stack analytic volume `9570.194671058465 mm^3`. AP242 reimport reports one body, one shell, 32 faces, 72 edges, 44 vertices, 16 planes, 16 cylinders, enclosed-manifold structure, and bounds `[-20,-12,0]..[20,12,16]`. The generic tessellated mass verifier cannot triangulate one planar face in this combined circular witness and reports `Unavailable`; enclosure, orientation, analytic volume, surface families, and bounds are therefore recorded separately rather than overstating independent mass verification.

Structured build output reports `featureCount: 3`: `Hole<Shaft> MountHole`, `Boss MountBoss`, and `Pocket ElectronicsRecess`. Boss/Pocket reports include stable semantic IDs, host/support/profile, extent, material effect, floor policy where applicable, and the reused section-stack route.

## Fresh-user friction

Three isolated agents received only `docs/public/firmament/geometry.md` and the support matrix. None inspected implementation, tests, or fixtures.

| Task | Initial attempt | Documentation-driven correction | Second attempt |
|---|---|---|---|
| A: plate + cylindrical Boss + through Hole | Compose/Boss intent correct; guessed invalid profile and `Through: true` syntax | Added complete wrapper, circle Profile, and `End: ThroughAll` example | Parsed and built first try; AP242 reimport enclosed-manifold, 1 reported Boss |
| B: 4 mm rectangular Pocket, floor >= 2 mm | Pocket contract/floor correct; guessed invalid profile syntax and omitted wrapper | Added complete Rect2/Profile/Model example | Parsed and built first try; 6 mm floor, AP242 reimport enclosed-manifold, 1 reported Pocket |
| C: mounting block with Boss + Pocket | Feature declarations correct; guessed invalid profile syntax and omitted wrapper | Added complete profile construction plus combination guidance | Parsed and built first try; AP242 reimport enclosed-manifold, 2 reported features |

The initial failure was documentation friction, not diagnostic-driven source correction: agents could not write a parser-grounded full file from the original public page. The docs were corrected in place. All three second attempts required zero compiler-diagnostic corrections and zero internal source/test inspection. Exact second-attempt sources are retained under `docs/release/artifacts/a3b/`.

## Public docs delta

- `docs/public/firmament/geometry.md`: first-class Boss/Pocket contracts, syntax, floor precedence, diagnostics, canonical example, and Boolean boundary.
- `docs/public/reference/supported-features.md`: separate Boss, Pocket, through-removal, lower-level Compose, and arbitrary-Boolean support rows.
- `fixtures/Canonical/Features/Boss/boss-pocket-block.firmament`: canonical practical public witness.

## Remaining limitations

- Preview 3 Boss/Pocket support is the trustworthy world-XY / `On: Top` Compose-host class only; arbitrary support orientations are rejected.
- Profiles remain the pre-A3b admitted line/arc vocabulary.
- Pocket is enclosed and finite; edge-breaking notches and through cuts use other semantics.
- Compose-host Countersink is not admitted by the existing section-stack hole route. Model-domain countersink remains supported and unchanged.
- The generic mass tessellator limitation described above remains verification friction, not a topology or STEP enclosure failure.
- No general machining-accessibility, tool-reach, draft, molding, or additive-support solver was added.

## Finding classification

- **MustFix (completed):** public Boss/Pocket semantics, stable identity, connected boss admission, finite pocket depth, minimum floor diagnostics, truthful build inventory.
- **DocsFix (completed):** geometry philosophy and supported-features boundary.
- **DocumentForPreview:** combined circular witness generic mass-tessellation limitation; Compose-host Countersink boundary.
- **DeferredPostPreview3:** non-top support faces, broader profile families, specialized spherical/tool-profile cavities, general accessibility analysis.
- **ReleaseBlocker:** none identified by A3b validation.

## Validation

- Full `dotnet build Aetheris.slnx -c Release --no-restore -m:1`: pass, 0 warnings, 0 errors.
- Full serial `dotnet test Aetheris.slnx -c Release --no-build -m:1 -- RunConfiguration.MaxCpuCount=1`: 3,011 passed, 0 failed, 0 skipped. `Aetheris.FrictionLab.Tests` has no discoverable tests (pre-existing assembly state).
- Explicit A3 analytic primitive + hole-family and A2 PMI filter: 11 passed.
- Explicit profile-composition/BRep/EdgeFinish/PMI filter: 48 passed.
- Explicit Sheet Metal manufacturing-release regression: 4 passed.
- Focused Boss/Pocket semantic, DFM, identity, combination, and STEP tests: 19 passed.
- Public canonical Boss/Pocket CLI build/analyze qualification: pass; 3 semantic features reported and enclosed-manifold reimport.
- Three fresh-agent sources: all parse/build on the first post-doc-correction attempt and reimport enclosed-manifold.
- VS Code grammar/core tests: 13 passed; Boss/Pocket keywords and snippets included.
- `git diff --check`: pass.

## Feature freeze

A3b was a bounded exception for first-class Boss/Pocket semantics and associated DFM.

No general Boolean authoring was added.

Feature freeze is restored.
