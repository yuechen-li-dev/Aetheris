# SURF-X3a — SectionChain sculpt integration

## Executive verdict

**Meaningful progression.** SectionChain is now a real Firmament-authored, inspectable, pcurve-complete, deterministic standalone SURF primitive with conservative non-neighbour/cap intersection detection. It is not yet a first-class `AddSectionChain`/`RemoveSectionChain` BodyState sculpt primitive, so the requested final verdict cannot honestly be Accepted.

The remaining blocker is structural, not syntactic: `BrepBoolean` admits bounded axis-aligned/prismatic/analytic-hole families and classifies an arbitrary changing ruled/non-rational-B-spline SectionChain tool as an unsupported general case. `BodyState` also retains only a `HousingConstruction` recipe; every downstream safe feature rebuilds that recipe and would erase a grafted chain. A correct implementation therefore needs a retained SectionChain construction history plus a bounded shared-topology housing/attachment builder (or a general topology editor). Routing the request through public arbitrary Boolean authoring would violate the milestone's meaning and would not converge.

## X3 audit

X3 already guaranteed ordered full frames, closed single-loop semantic spans, stable seams, explicit or strong-identity one-to-one correspondence, Ruled transitions, Cap/Open terminations, shared internal section edges, deterministic local edit identities, exact planes, compatible polynomial ruled B-splines, foldover/profile checks, rational-free STEP, and deterministic reimport.

It deliberately did not guarantee smooth/rail-guided lofts, topology-changing correspondence, multiple loops, global intersection proof, or arbitrary BodyState composition. SectionChain bypassed BodyState by materializing directly from a generated C# template in the CLI. Its faces had 3D edge bindings but no face-local pcurves. Its intersection checks were limited to sampled profile crossings and per-patch Jacobian/normal foldover.

X3a reuses the SURF-X1a `BoundedPcurveBuilder`, `BrepPcurveValidator`, AP242 `SURFACE_CURVE`/`PCURVE` writer, and existing Concept Path/Profile binder. No second profile grammar, ruled formula, pcurve inverter, or mesh product representation was introduced.

## X3 blocker closure

| X3 blocker | X3a result | Evidence |
| --- | --- | --- |
| Firmament text authoring | Closed for standalone SectionChain | `SectionChainAuthoringParser`; Concept Path/Profile reuse; canonical 2/6/8-section and open fixtures; build/inspect/validate CLI |
| Fixture corpus | Partially closed | Four canonical fixtures plus typed foldover invalid fixture; Add/Remove and several requested negative fixtures remain blocked with the sculpt path |
| AddSectionChain | Blocked | Arbitrary changing ruled/B-spline union is outside admitted Boolean families; no retained construction history/shared-topology attachment builder |
| RemoveSectionChain | Blocked | Same Boolean boundary; no bounded one-body duct topology builder, no-op/split qualification, or retained history |
| Self-intersection qualification | Closed for the admitted standalone validation contract | profile test + foldover test + deterministic AABB/triangle proxy for non-neighbour transitions and remote caps; explicit non-proof evidence |
| Face-local pcurves | Closed | every transition/cap coedge populated; independent domain, endpoint, orientation, UV closure, max/RMS reconstruction qualification; STEP reimport |

## Firmament and CLI

The semantic route is:

```text
Concept Path / Profile / Construction Plane
  -> SectionChain semantic IR
  -> RuledSurfaceIr transition lowering
  -> shared-topology BRep with face-local pcurves
  -> AP242 STEP
```

Sections retain authored section, frame, profile, span, seam, transition, and termination identities. Equal ordered span identities produce `InferredSemanticIdentity`; otherwise a `Correspond` block provides explicit source/target rows. Ambiguous, reordered, missing, or duplicate correspondence fails before BRep construction.

`section-chain build|inspect|validate <file.firmament>` reports the semantic IR rather than only face counts, including resolved correspondence, transition surface classes, pcurve evidence, conservative intersection method, representation inventory, and reimport.

## Pcurves

The eight-section authored flagship has 28 transition faces and 2 planar caps. Its 60 shared edges create 120 face-local coedge pcurves. Independent qualification reports:

| Evidence | Result |
| --- | ---: |
| Pcurves / coedges | 120 / 120 |
| Maximum 3D reconstruction deviation | `1.475552811121985E-14 mm` |
| Tolerance | `1E-05 mm` |
| Surface-domain containment | passed |
| Oriented start/end reconstruction | passed |
| UV loop closure | passed |
| STEP reimport | passed |

An internal section still owns one 3D edge. Each adjacent transition face owns its own pcurve for that edge; no duplicate near-coincident seam is introduced. Planar cap pcurves use the same plane projection machinery.

## Self-intersection qualification

The admitted pipeline checks profile self-intersection, adjacent ruled Jacobian/normal foldover, non-neighbour transition crossings, coplanar overlap, and remote cap penetration. The latter checks use a deterministic validation-only `24 x 6` triangle proxy per ruled patch, expanded AABB broadphase, triangle/triangle narrowphase, and topology-aware exclusion of intended adjacent seams.

This is detection evidence, not a global proof or a certified Hausdorff enclosure. The structured method name is `DeterministicBroadphasePlusTriangleProxy`; the topology tolerance is `1E-06`. A four-section non-neighbour overlap witness is rejected as `section-chain-self-intersection`, and a 180-degree framed-profile inversion is rejected as `section-chain-transition-foldover`. Product geometry remains structured BRep; faceted product fallback is zero.

## Locality

`SectionChainEditor.ReplaceSection` still identifies only the two adjacent transition identities. The X3 witness that changes `Rise` rebuilds `PalmFront->Rise` and `Rise->Peak`, preserves five distant transitions, and preserves both terminations. This is semantic and realized deterministic-geometry preservation; the current implementation does not cache/splice BRep subgraphs and therefore does not claim that a full materializer invocation avoided recomputing distant patches. BodyState/GeometricDelta locality remains part of the blocked sculpt integration.

## Representation and artifact

The re-authored flagship is `fixtures/Canonical/SectionChain/eight-section-ergonomic.firmament` and produces:

| Evidence | Result |
| --- | ---: |
| SHA-256 | `03ba8872e1daea585d16c2a54ffb02aeead26633e4e6f3fc13e87c7ab1c41f97` |
| Sections / transitions | 8 / 7 |
| Bodies / shells | 1 / 1 |
| Faces / edges / vertices | 30 / 60 / 32 |
| Planes | 30 |
| Non-rational B-splines | 0 |
| Rational product surfaces | 0 |
| Faceted product fallback | 0 |
| Pcurves | 120 |
| Self-intersection proxy | passed |
| Structural reimport assessment | enclosed-manifold |
| STEP reimport | passed |

Two exports produced the same hash. The artifact is `artifacts/local/surf-x3a/surf-x3a-section-chain-ergonomic-body.step`; a sibling `.evidence.json` records the complete report.

The requested Add grip and Remove duct artifacts are not generated because no correct Add/Remove BodyState result exists. Producing standalone chain bodies with those filenames would misrepresent the capability.

## Fresh-agent qualification

- A passed for eight-profile authoring using only public docs and canonical fixtures.
- F passed through the real CLI path and independently reproduced the flagship hash and representation evidence.
- D could author a centered 8 mm middle-profile width increase and infer the union of adjacent transitions, but found no public predecessor/successor compare command.
- E could author a 180-degree frame inversion and now has a canonical invalid fixture plus stable foldover diagnostic documentation.
- B and C stopped correctly: public docs expose no Add/Remove syntax and explicitly prohibit substituting arbitrary Difference.

The exercise also found stale public statements and missing resolved inferred correspondence in CLI JSON; both were corrected in place.

## Validation

- Release solution build: passed, 0 warnings, 0 errors.
- Full serial .NET suite: passed, 3,179 tests across 17 test-bearing assemblies; FrictionLab reports no discoverable tests.
- SectionChain geometry suite: 9 passed.
- Firmament authoring/fixture suite: parser, lowering, termination, pcurve, invalid correspondence, and foldover cases passed.
- CLI authored build/inspect/validate: passed.
- Paperclip kernel and CLI semantic-route regressions: passed; Sweep remains specialized.
- Deterministic double export and STEP reimport: passed.
- Freshly packed/local-tool SectionChain validation smoke: passed.
- `git diff --check`: passed (line-ending notices only).

## Required next architecture

Acceptance requires a narrow planar-housing attachment/removal construction that directly emits one connected shared-topology shell, retains ordered SectionChain sculpt history in `BodyState`, verifies realized diffs against an authorized envelope, preserves existing semantic associations, proves volume direction and connectedness, and supports downstream safe features without erasing the chain. Only after that real path exists should `AddSectionChain` and `RemoveSectionChain` be exposed in Firmament and canonical Add/Remove artifacts be published.
