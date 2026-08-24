# Section chains

A SectionChain builds a surface or closed body from an ordered sequence of framed profiles. It is the low-level ruled-loft substrate for cases where the profile itself changes from station to station.

```text
Section0 -> Section1 -> Section2 -> Section3
```

Each `Section` owns a stable `SectionId`, a complete right-handed frame (`Origin`, `XAxis`, `YAxis`, `Normal`), and one closed outer `SectionProfile`. A profile is an ordered loop of semantic spans such as `South`, `East`, `North`, and `West`; its declared seam identifies the first span. X3's qualified profile curves are line, circular arc, and non-rational polynomial B-spline.

Adjacent sections use one-to-one semantic span correspondence. Matching IDs may establish strong semantic identity, while explicit `AdjacentSectionCorrespondence` records the mapping in the flagship. Aetheris does not silently use nearest points, nearest edges, or tessellated vertex indices to define topology. The X3 lane requires the same span count and seam-relative order on both sides of a transition.

`Ruled` is the only qualified transition policy. Each matched pair is sent through the existing `RuledSurfaceIr` lowering path, so there is one mathematical ruled implementation. Exact planar line pairs become `PLANE`; compatible line/B-spline and B-spline/B-spline pairs become exact non-rational `B_SPLINE_SURFACE_WITH_KNOTS`; coaxial compatible circles retain existing cylinder/cone recognition. Rational product surfaces are prohibited.

Every internal section edge is allocated once. The preceding and following transition faces use that same edge, curve, vertices, and parameter direction. Editing one section therefore rebuilds only its two adjacent transition identities; distant transitions and terminal conditions remain preserved in `SectionChainEditDelta`.

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
    Transition: Ruled
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

Equal ordered span identities infer correspondence. When identities differ, author a `Correspond` block with `From`, `To`, and explicit `Source -> Target` rows. Missing, incomplete, reordered, or duplicate mappings fail before BRep construction. `Ruled`, `Cap`, and `Open` are the only currently qualified transition/termination vocabulary.

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
aetheris section-chain validate fixtures/Canonical/SectionChain/eight-section-ergonomic.firmament --json
aetheris section-chain inspect fixtures/Canonical/SectionChain/eight-section-ergonomic.firmament --json
aetheris section-chain build fixtures/Canonical/SectionChain/eight-section-ergonomic.firmament --out artifacts/local/surf-x3a/fairing.step --json
```

On a source checkout where the tool is not installed, replace `aetheris` with `dotnet run --project Aetheris.CLI -c Release --`.

The compatibility `section-chain build` command also supports the generated `flagship`, `twist`, and `two-profile` witnesses. That standalone command writes sibling `.evidence.json` containing section frames, spans, correspondence, transition classes, terminations, topology, pcurve qualification, conservative intersection evidence, timings, SHA-256, rational-surface count, and STEP reimport evidence. Ordinary X3b BodyState `build` instead writes the sculpting `.delta.json` construction/delta evidence described in the sculpting guide; it does not write both sibling formats.

SURF-X3b adds a bounded `BodyState` composition lane. `AddSectionChain` retains the chain, terminal Section, semantic support, span correspondence, authorized envelope, preservation contracts, and requirements as one typed replay operation. `RemoveSectionChain` retains the changing-profile tool and both explicit penetration supports. Neither lowers to public `Union` or `Difference`; a SectionChain-specific builder emits the known shared topology directly.

The admitted additive lane attaches an `Open` first terminal to the complete planar `HousingSideEast` boundary, progresses in support-relative +X, and caps the free end. The admitted subtractive lane runs an `Open/Open` four-line-span chain monotonically from `HousingSideWest` to `HousingSideEast`, strictly inside the housing Y/Z boundary. These restrictions make intended attachment, remote intersection, connectedness, and opening topology explicit and deterministic.

Canonical BodyState sources are [`surf-x3b-add-section-chain-grip.firmament`](../../../fixtures/Canonical/BodyState/surf-x3b-add-section-chain-grip.firmament) and [`surf-x3b-remove-section-chain-duct.firmament`](../../../fixtures/Canonical/BodyState/surf-x3b-remove-section-chain-duct.firmament). Build them with the ordinary command:

```powershell
aetheris build fixtures/Canonical/BodyState/surf-x3b-add-section-chain-grip.firmament --out artifacts/local/surf-x3b-add-section-chain-grip.step --json
aetheris build fixtures/Canonical/BodyState/surf-x3b-remove-section-chain-duct.firmament --out artifacts/local/surf-x3b-remove-section-chain-duct.step --json
```

The current canonical grip qualifies 24 faces, 50 edges, 28 vertices, and 100 pcurves after STEP reimport. The canonical duct qualifies 24 faces, 54 edges, 32 vertices, and 108 pcurves. These are regression evidence for the named fixtures, not general formulas for arbitrary chains.

## Current limits

The standalone lane has one closed outer loop, no holes, same-topology one-to-one correspondence, ruled transitions, and Cap/Open terminals. It checks frame handedness, loop closure and orientation, sampled profile self-intersection, section spacing, semantic mapping order, sampled transition Jacobian foldover, non-neighbour transition crossings, and remote cap penetration using a deterministic validation-only triangle proxy. The proxy is conservative detection evidence, not a global proof. BodyState composition is additionally limited to the planar housing support lanes described above; arbitrary supports, rotated/non-four-line profiles, crown/patch predecessors, branches, G1/G2 continuity, and arbitrary freeform Boolean composition fail closed. Invalid chains never produce a faceted product fallback.

Stable diagnostic families include `section-chain-correspondence-missing`, `section-chain-correspondence-duplicate`, `section-chain-profile-orientation-mismatch`, `section-chain-transition-foldover`, `section-chain-self-intersection`, and `section-chain-pcurve-error`. `section-chain validate` returns a nonzero exit status and emits no STEP when any of these blocks materialization. Transition identities are formatted `SourceSection->TargetSection` in structured evidence and diagnostic detail.
