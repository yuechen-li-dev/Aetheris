# Sheet Metal M12 — semantic-local reconstruction comparison

Status: **meaningful progression**. Aetheris can now compare recovered and native
Sheet Metal by stable engineering feature rather than only by anonymous whole-body
samples. CTC-03 is still `NeedsReview`; the new report names every failing target,
and no new modeling primitive or CTC-specific compiler dispatch was added.

## Comparator audit and architecture

The previous flat comparator translated the two bounding-box minima, sampled each
outer curve, and measured nearest **sample point** globally. It did not use region
ownership, Concept Paths, semantic correspondence, analytic curve domains, or source
edge provenance. The formed comparator similarly compared anonymous region boundary
points, then separately matched bends and openings. Consequently it could not emit a
result per bend endpoint and mixed physical free edges, shared region boundaries,
reference-surface offsets, and segmentation density.

The M12 path is:

```text
recovered STEP edge -> recovered region chain + immutable edge provenance
                                            \
                                             SemanticGeometryTarget -> local metrics
                                            /
native Concept Path -> exact flat semantic descendant chain
```

`SemanticSheetMetalComparer` first pairs planar regions using formed position, area,
and plane evidence. It then maps native curves through native flat -> formed ->
recovered flat transforms. This removes local U/V orientation ambiguity. A target
keeps observed source IDs separate from the derived engineer interpretation.

Supported target kinds are region boundary, profile member, ProfileDelta member,
Profile corner, AttachmentPath, bend termination, opening, and bend. Stable ordering
and a deterministic report hash are part of the contract.

## Curve correspondence and metrics

Local source evidence is an ordered bounded chain selected by projected target
endpoints. The selected first/last source curves are analytically trimmed to the
local domain. Therefore one source arc may match multiple native descendants, and
several split source lines may match one native member. Entity count differences are
reported as `SegmentationOnly`, not geometry failure.

Each target reports bidirectional RMS, p95, maximum, chain length, endpoint residuals,
source/native curve counts and families, plus line direction/offset/span or arc
center/radius/domain differences when a direct analytic pair exists. Local frames use
chain-start, along-chain, and profile-normal axes. Classification is deliberately
bounded: `ParameterMismatch`, `WrongProfileOperation`, `WrongAttachment`, and
`WrongTermination` are evidence hints, not an automated engineering diagnosis.

Policy for this run is **0.1 mm** for local manufactured-profile position. Bend axis,
angle, and radius retain the existing 0.05 mm / 0.05 degree policy.

## CTC-03 endpoint proof

The accepted stitched source blank, not an individual face loop, is primary flat
authority. Formed residual is axial endpoint extent in the matched source bend frame.

| Semantic termination | Native treatment | Flat max (mm) | Formed axial (mm) | Verdict |
|---|---|---:|---:|---|
| `FrontWallBend.StartTermination` | Trimmed 8.89 | 0.0001 | <0.0001 | Pass |
| `FrontWallBend.EndTermination` | Trimmed 8.89 | 0.0001 | <0.0001 | Pass |
| `RearWallBend.StartTermination` | Trimmed 8.89 | 0.0001 | <0.0001 | Pass |
| `RearWallBend.EndTermination` | Trimmed 8.89 | 0.0001 | <0.0001 | Pass |

Visual source authority identified the four rectangles as the two endpoints of each
front/rear cylindrical bend strip. The semantic bend comparison independently measured
each native strip as 8.89 mm too long. Four 8.89 mm trims now remove those exact pieces.
Lowering was corrected so BendTermination shortens only the finite cylindrical strip;
it no longer shortens the adjacent planar wall or downstream mounting flange. All four
flat and formed endpoint probes now pass at numerical noise.

## Main-deck and right-wall forensics

The former `region-p-0065-0069` 8.945 mm result was not an anonymous missing deck
feature. It mixed a shared regional boundary with the physical stitched blank under
global nearest correspondence. In the shared semantic frame the MainDeck regional
boundary maximum is 1.9025 mm; all six deck circles and both slots independently pass
at numerical noise. The 8.945 mm value remains in the legacy whole-contour summary and
is now explicitly classified as coarse shared-boundary/correspondence evidence, not a
reason to invent geometry.

The right-wall aggregate is decomposed as follows:

| Target | Max (mm) | Status |
|---|---:|---|
| service AttachmentPath | <0.0001 | PassWithKnownDifference (segmentation) |
| LeadIn / LeadOut | <0.0001 | PassWithKnownDifference (segmentation) |
| LeftRun / AttachmentLand / step transitions | <0.0001 | PassWithKnownDifference (segmentation) |
| RightRun | <0.0001 | PassWithKnownDifference (segmentation) |
| rear end round | 0.9322 | NeedsReview |
| front end round | 1.9025 | NeedsReview |
| service-flange openings | <0.0001 | Pass |
| service connector crown | 0.0018 | Pass |

The service profile and attachment path are correct. Remaining right-wall residual is
confined to the end-round/corner family and the shared regional boundary.

## Worst semantic targets

| Target | Kind | RMS (mm) | p95 (mm) | max (mm) | Classification |
|---|---|---:|---:|---:|---|
| Front mounting outer rounds | Profile member | 1.24 | 2.42 | 2.632 | WrongProfileOperation |
| Rear mounting shoulders | Profile member | 0.63 | 1.82 | 2.628 | WrongProfileOperation |
| MainDeck shared boundary | Region boundary | 0.25 | 0.00 | 1.9025 | reference/shared-boundary mismatch |
| Left/Right wall bend domains | Bend | 0.09 | 0.001 | 1.9025 | ParameterMismatch |
| right-wall end rounds | Profile corner | 0.67–1.39 | bounded per member | 0.9322–1.9025 | WrongProfileOperation |

These are the exact remaining targets. There is no final “miscellaneous contour”
bucket.

## Parity and inventory

Flat semantic report: 71 stable targets. All 17 openings pass. Five of seven bend
lines pass the complete local-domain test; all seven still pass the independent
axis/angle/radius/adjacency test. All four terminations pass.
AttachmentPath passes. ProfileDelta and corner results are listed individually in the
JSON CLI output.

Legacy flat global summary remains `NeedsReview`:

| Direction | RMS / p95 / max (mm) |
|---|---:|
| source -> native | 1.39725 / 3.80101 / 3.80101 |
| native -> source | 1.37476 / 3.80101 / 3.80101 |

Bounds residual is 0.002447 x 0.007820 mm. These global values are summary only.

Formed global summary is source -> native 1.81177 / 2.12872 / 6.42141 mm and
native -> source 1.96634 / 6.41889 / 6.42356 mm. All seven bend parameter matches and
all 17 opening matches pass. Endpoint-local formed evidence is in the table above.

Final inventory remains seven bends, 15 circular holes, two slots, service flange and
rounded service tab, right-wall generic recess with bounded attachment path and
release geometry, front/rear mounting profiles, wall rounds/chamfers, and four named
bend terminations. Native compilation reads no STEP path, face ID, edge ID, recovered
polygon, or recovery provider.

## CLI, reuse, performance, and workflow

```text
aetheris sheetmetal recover-flat source.step --out-dir recovered --json
aetheris sheetmetal compare-flat recovered/recovered-flat.json native.firmament --semantic --json
aetheris sheetmetal compare source.step native.firmament --semantic --json
```

On this CTC run, target creation took about 8.8 ms, 71-target flat comparison about
333 ms, and formed semantic comparison about 19 ms. Reports and hashes are stable.

The non-CTC dogfood builds two authored coupons differing only in a generic
ProfileDelta depth. The report localizes the failure to `Wall.ServiceRecess.Land`
instead of reporting only global distance, and an identical rerun produces the same
hash.

The LLM workflow is now: recover flat -> reconstruct -> semantic compare -> inspect
the worst target and its source edge provenance -> revise an existing semantic
parameter/program -> repeat. This was substantially easier than screenshots/global
RMS: the previous “main-deck 8.945 mm missing geometry” diagnosis was too coarse, and
the right-wall AttachmentPath was shown to be correct. No genuinely new general
Profile primitive was needed. The bend-strip extent seam is now resolved by keeping
finite bend termination independent from adjacent planar-profile extent.

## Verdict

Code quality is an **acceptable bounded prototype**: typed records and deterministic
behavior are reusable, while local chain selection and mismatch classification remain
conservative heuristics. Aetheris can now prove local flat matches for individual
ProfileDelta members, AttachmentPaths, profile corners, openings, bends, and bend
terminations, and can report formed axial endpoint evidence per termination.

CTC-03 is **not fully reconstructed**. Exact remaining manufactured geometry includes
the front/rear mounting-profile transition families, finite Left/Right wall bend-line
domains, and the right-wall end-round family.
Calling it Complete would violate the 0.1 mm policy.
