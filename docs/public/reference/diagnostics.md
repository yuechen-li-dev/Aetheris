# Diagnostics and failure recovery

## Circular Sweep (X0)

- `firmament-sweep-section-invalid`: circular diameter is missing, non-finite, or non-positive.
- `firmament-sweep-path-disconnected`: a segment does not continue from its predecessor.
- `firmament-sweep-path-not-tangent`: adjacent segments meet at a sharp corner.
- `firmament-sweep-path-nonplanar`: the path is outside X0's planar XY boundary.
- `firmament-sweep-bend-radius-too-small`: an arc radius does not exceed the section radius.
- `firmament-sweep-self-intersection`: nonadjacent centerline regions violate required clearance.
- `firmament-sweep-material-unresolved`: the material identity is absent from the deployed catalog.

## Formed Wire (WIRE-X0)

- `wireform-diameter-invalid`: stock diameter is missing, non-finite, or non-positive.
- `wireform-start-frame-invalid`: Origin/Tangent/Up is missing, the tangent is zero, or Up is parallel to the tangent.
- `wireform-straight-length-invalid:<operation>`: Straight length is not finite and positive.
- `wireform-bend-radius-invalid:<operation>`: centerline bend radius is not positive or does not exceed `Diameter / 2`. This local geometric check runs before body construction.
- `wireform-bend-angle-invalid:<operation>`: Bend angle is zero or exceeds ±180°.
- `wireform-bend-plane-invalid:<operation>`: Plane is not current local `Up` or `Right`.
- `wireform-material-unresolved`: the material identity is absent from the deployed catalog.
- `wireform-self-intersection:<opA>:<opB>`: nonadjacent wire portions violate diameter clearance. This later global check uses conservative 3D chord/sagitta bounds and rejects intentional contact.

The Standard Paperclip template has stricter product policy (`InnerBendRadius > WireDiameter`) than generic WireForm's geometric `Radius > Diameter / 2`; named Template `Require` clauses can therefore reject a Paperclip specialization before WireForm lowering.

## Surface trim closure (SURF-X1a)

- `surf-intersection-none`: the qualified supports do not intersect inside the bounded domains.
- `surf-intersection-ambiguous`: multiple branches exist without an admissible seed/reference boundary.
- `surf-pcurve-invalid`: a face-local UV curve is missing, outside its domain, misoriented, or reconstructs away from the shared 3D edge.
- `surf-trim-loop-open`: ordered trim coedges do not close through shared topology vertices.
- `surf-inner-loop-invalid`: an opening loop is not a valid bounded hole in the replacement face.
- `surf-extension-unsupported`: the surface family, degree, stability envelope, or requested extension is outside the qualified matrix.
- `surf-imported-selector-unresolved`: a STEP `ADVANCED_FACE` identity does not resolve to exactly one current face.
- `surf-association-target-removed`: PMI or an Assembly Interface has no explicit `Preserved` correspondence; Aetheris refuses name- or proximity-based rebinding.
- `surf-association-current-geometry-missing`: an explicitly preserved association references a face absent from the output `BodyState`.
- `surf-blend-g2-unsatisfied`: a constructible blend candidate fails the hard transverse normal-curvature contract and is rejected before scoring.
- `surf-blend-candidate-rejected`: compact per-candidate evidence emitted when the complete request has no selectable candidate.
- `surf-blend-no-eligible-candidates`: representation, continuity, topology, locality, or preservation gates leave no candidate at the declared minimum continuity.
- `surf-blend-override-ineligible`: `UseCandidate` names an absent candidate or one that fails the active hard requirements.
- `surf-blend-locality-violation`: a candidate exceeds the authorized transition envelope and cannot enter utility judgment.
- `surf-boundary-g2-violation`: realized exact boundary second-difference evidence exceeds the planar-shoulder normal-curvature tolerance.
- `surf-certified-bounds-invalid`: a claimed exact polynomial bounds certificate does not contain deterministic surface samples.
- `surf-selector-target-replaced`: a historical native or imported selector has an explicit successor and cannot be reused.

## Construction-state replay and SectionChain sculpting (SURF-X3b)

- `bodystate-operation-replay-failed`: replay stopped atomically at the named typed operation; the reported predecessor remains authoritative.
- `bodystate-operation-order-invalid`: an operation's authored predecessor does not equal the preceding operation's authored output; replay does not reorder or guess intent.
- `bodystate-operation-version-unsupported`: the retained operation payload version is not admitted.
- `bodystate-operation-support-missing`: a semantic support read by an operation is absent; Aetheris does not guess a nearest face.
- `bodystate-preserved-region-modified`: realized construction touched a protected semantic region.
- `section-chain-add-not-attached`: the additive terminal does not exactly correspond to its support, or no positive connected volume resulted.
- `section-chain-add-remote-intersection`: a non-terminal additive section crosses the predecessor outside the intended attachment.
- `section-chain-remove-no-material`: the admitted removal corridor does not remove positive bounded material.
- `section-chain-remove-disconnects-body`: a duct reaches the protected outer boundary and would sever the admitted housing result.
- `section-chain-housing-base-unsupported`: an upstream crown/patch changed the predecessor outside the currently qualified planar composition lane.

Start with `aetheris validate source.firmament --json`; use `build` when geometry, AP242, assertions, or artifacts are involved. A diagnostic code is the stable automation key, while its message identifies the value/target and expected category where useful.

Common recovery patterns:

| Failure | What to check |
|---|---|
| unknown keyword/Template | spelling, `Use` declaration, and qualified Template name |
| missing Template argument | the Template's typed parameter list or Forge `describe` output |
| unit mismatch | use the required dimension (`mm`, `deg`, `N`) rather than a bare/wrong-dimension value |
| unknown material | one of the four exact catalog references in the materials guide |
| unresolved PMI target | named hole/face selector exists in the same semantic domain |
| invalid tolerance | `PlusMinus(plus, minus)` uses Length values for a diameter |
| Sheet Metal region mismatch | use a named planar region such as `Base`, not `face(+Z)` |
| `sheetmetal-hole-domain-syntax` | replace Model `Hole<Shaft>` with Sheet Metal `Hole Name` syntax |
| `sheetmetal-pmi-domain-syntax` | use `Manufacturing` plus `DatumFeature` targeting a named Sheet Metal region |
| inlineSTEP file/face failure | resolve the file relative to the source and use an existing AP242 face identity |
| empty FEA selection | selected face exists and intersects occupied cut cells at the requested lattice |
| unsupported constitutive model | Preview 3 production scope is linear elastic isotropic |

Successful builds enforce PMI/AP242 parity. `firmament-v2-pmi-export-evidence-mismatch` means a supported record failed independent export reinspection; no artifact is written. See [targets](targets.md) for cross-domain forms.
