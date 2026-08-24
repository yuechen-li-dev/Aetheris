# Section chains

A SectionChain builds a surface or closed body from an ordered sequence of framed profiles. G1 is the normal freeform authoring default; explicit G0/Ruled remains available when straight generators are intentional.

```text
Section0 -> Section1 -> Section2 -> Section3
```

Each `Section` owns a stable `SectionId`, a complete right-handed frame (`Origin`, `XAxis`, `YAxis`, `Normal`), and one closed outer `SectionProfile`. A profile is an ordered loop of semantic spans such as `South`, `East`, `North`, and `West`; its declared seam identifies the first span. X3's qualified profile curves are line, circular arc, and non-rational polynomial B-spline.

Adjacent sections use one-to-one semantic span correspondence. Matching IDs may establish strong semantic identity, while explicit `AdjacentSectionCorrespondence` records the mapping in the flagship. Aetheris does not silently use nearest points, nearest edges, or tessellated vertex indices to define topology. The X3 lane requires the same span count and seam-relative order on both sides of a transition.

Continuity intent and transition law are separate. `Continuity: G0` selects the existing `Ruled` law. `Continuity: G1` selects local `SmoothPolynomial` transitions. G0 makes adjacent surfaces meet. G1 also makes their tangent planes agree, so highlights and silhouettes flow smoothly across section boundaries.

For G1, Aetheris transforms corresponding polynomial controls into world space and derives a shared tangent field at every section. Interior tangents use a nonuniform three-section quadratic-derivative stencil; cumulative frame-origin chord length is the station metric, and endpoints use a bounded one-sided chord derivative. Three deterministic magnitude policies are qualified for foldover and overshoot before the Judgment Engine compares bending energy and normal variation. Continuity is a hard eligibility constraint, never an aesthetic score.

Each matched span becomes an exact non-rational cubic transition in the longitudinal direction. Lines are exactly degree-elevated to cubic when needed; compatible polynomial B-splines retain their boundary geometry and knot structure. Circular-arc G1 normalization and mixed incompatible knot structures currently fail with `section-chain-g1-degree-limit`; Aetheris never introduces rational weights or a faceted fallback. The G0 path still preserves exact plane/cylinder/cone recognition where applicable.

Every internal section edge is allocated once. The preceding and following transition faces use that same edge, curve, vertices, and parameter direction. For G0, editing one section rebuilds its two adjacent transitions. G1 has a bounded dependency neighborhood: editing section `i` recomputes tangent fields `i-1..i+1` and may rebuild transitions `i-2->i-1` through `i+1->i+2`. Distant transitions and terminal conditions remain preserved in `SectionChainEditDelta`; there is no global loft solve.

Every transition and cap coedge also owns a face-local pcurve. Aetheris builds these through the same bounded Plane/Cylinder/non-rational-B-spline machinery used by trimmed SURF faces, then independently checks reconstruction deviation, surface-domain containment, both oriented endpoints, and UV loop closure. The two faces meeting at an internal section therefore share one authoritative 3D edge while retaining separate local 2D representations.

The terminal vocabulary is `Cap` and `Open`. `Cap/Cap` closes the two planar profile loops and yields a closed solid. Any open terminal is truthfully classified as `OpenShell`; the general STEP command currently emits only the capped witnesses.

## Sweep relationship

A Sweep transports one profile through a trajectory. A SectionChain allows the profile itself to change at explicitly framed stations. Both use ordered geometry and stable parameter correspondence, but the current circular Sweep keeps its specialized line/arc-path materializer. That path preserves Paperclip's exact cylinders and tori, planar transported-frame policy, and sweep-specific clearance checks; forcing it through generic ruled faces would weaken the representation.

## Firmament authoring

SectionChain authoring reuses ordinary `Concept Path` and `Profile` declarations. A `Section` associates that reusable 2D profile with a named right-handed `Construction Plane`; it does not expose vertices, faces, or raw BRep surfaces.

```firmament
Concept Path NoseOutline {
    Start: Point2(-5mm, -3mm)
    Heading: 0deg
    Line Bottom { Length: 10mm }
    Line Right { Turn: 90deg; Length: 6mm }
    Line Top { Turn: 90deg; Length: 10mm }
    Close Left
}
Profile NoseProfile From NoseOutline

SectionChain Fairing {
    Continuity: G1
    Section Nose {
        Frame: NoseFrame
        Profile: NoseProfile
        Seam: Bottom
    }
    Section Front {
        Frame: FrontFrame
        Profile: FrontProfile
        Seam: Bottom
    }
    Start: Cap
    End: Cap
}
```

Equal ordered span identities infer correspondence. When identities differ, author a `Correspond` block with `From`, `To`, and explicit `Source -> Target` rows. Missing, incomplete, reordered, or duplicate mappings fail before BRep construction. Omit `Continuity` for the G1 default, or write `Continuity: G0` and `Transition: Ruled` for intentionally faceted/straight-generator construction. G2 is not claimed. `Cap` and `Open` remain separate termination intent; planar caps meet the transition at G0 unless separately designed.

```firmament
Correspond NoseToFront {
    From: Nose
    To: Front
    Bottom -> South
    Right -> East
    Top -> North
    Left -> West
}
```

Profile coordinates use the construction plane's local axes: profile X follows frame `XAxis`, profile Y follows `YAxis`, and the chain progresses between section origins/normals. If one Profile is referenced by several Sections, editing it changes each of those yielded states; locality is the union of their adjacent transitions. The current CLI reports one compiled state but does not yet compare two authored files or serialize `SectionChainEditDelta`.

A SectionChain is useful as a geometric generator: each framed profile is an ordered state, and Aetheris materializes the transition between adjacent yielded states. This is a mental model, not a general-purpose iterator language.

## CLI

Generate and inspect the eight-section flagship through the production BRep/STEP path:

```powershell
aetheris section-chain inspect flagship --json
aetheris section-chain build flagship-g0 --out artifacts/local/surf-x4/surf-x4-ergonomic-g0.step --json
aetheris section-chain build flagship --out artifacts/local/surf-x4/surf-x4-ergonomic-g1.step --json
```

On a source checkout where the tool is not installed, replace `aetheris` with `dotnet run --project Aetheris.CLI -c Release --`.

The compatibility command supports `flagship` (G1), `flagship-g0`, `twist`, and `two-profile`. Its structured inspection/evidence includes continuity intent, tangent derivation, candidate/rejection metrics, the selected policy, measured G0/G1 errors, representation, pcurves, topology, SHA-256, and STEP reimport. Its sibling preview uses the same general [BRep wireframe renderer](../reference/wireframe.md) exposed by `aetheris wireframe`; SectionChains no longer own a private visualization implementation.

SURF-X3b adds a bounded `BodyState` composition lane. `AddSectionChain` retains the chain, terminal Section, semantic support, span correspondence, authorized envelope, preservation contracts, and requirements as one typed replay operation. `RemoveSectionChain` retains the changing-profile tool and both explicit penetration supports. Neither lowers to public `Union` or `Difference`; a SectionChain-specific builder emits the known shared topology directly.

The admitted additive lane attaches an `Open` first terminal to the complete planar `HousingSideEast` boundary, progresses in support-relative +X, and caps the free end. The admitted subtractive lane runs an `Open/Open` four-line-span chain monotonically from `HousingSideWest` to `HousingSideEast`, strictly inside the housing Y/Z boundary. These restrictions make intended attachment, remote intersection, connectedness, and opening topology explicit and deterministic.

Canonical BodyState sources are [`surf-x3b-add-section-chain-grip.firmament`](../../../fixtures/Canonical/BodyState/surf-x3b-add-section-chain-grip.firmament) and [`surf-x3b-remove-section-chain-duct.firmament`](../../../fixtures/Canonical/BodyState/surf-x3b-remove-section-chain-duct.firmament). Build them with the ordinary command:

```powershell
aetheris build fixtures/Canonical/BodyState/surf-x3b-add-section-chain-grip.firmament --out artifacts/local/surf-x3b-add-section-chain-grip.step --json
aetheris build fixtures/Canonical/BodyState/surf-x3b-remove-section-chain-duct.firmament --out artifacts/local/surf-x3b-remove-section-chain-duct.step --json
```

The current canonical grip qualifies 24 faces, 50 edges, 28 vertices, and 100 pcurves after STEP reimport. The canonical duct qualifies 24 faces, 54 edges, 32 vertices, and 108 pcurves. These are regression evidence for the named fixtures, not general formulas for arbitrary chains.

## Current limits

The standalone lane has one closed outer loop, no holes, same-topology one-to-one correspondence, G0/G1 pairwise transitions, and Cap/Open terminals. It checks frame handedness, loop closure/orientation, profile self-intersection, physical spacing, semantic mapping order, transition Jacobian foldover, bounded overshoot, non-neighbour crossings, remote cap penetration, pcurves, and realized continuity. The triangle proxy is conservative detection evidence, not a global proof. BodyState composition remains limited to the planar housing support lanes; arbitrary supports, branches, G2, rail lofts, topology-changing correspondence, and arbitrary freeform Boolean composition fail closed.

Stable diagnostic families include `section-chain-correspondence-missing`, `section-chain-correspondence-duplicate`, `section-chain-profile-orientation-mismatch`, `section-chain-transition-foldover`, `section-chain-self-intersection`, and `section-chain-pcurve-error`. `section-chain validate` returns a nonzero exit status and emits no STEP when any of these blocks materialization. Transition identities are formatted `SourceSection->TargetSection` in structured evidence and diagnostic detail.
