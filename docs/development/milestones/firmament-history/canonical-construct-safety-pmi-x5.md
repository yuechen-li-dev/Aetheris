# CANONICAL-CONSTRUCT-SAFETY-PMI-X5

Round 3 established a useful Profile/Compose CNC route, but also exposed a
language-integrity hole: an unclaimed canonical block could be skipped without
diagnostic, and `Pmi` was only reachable through the lowercase compatibility
reader.

## Declaration safety

Canonical root scanning now inspects declaration-shaped blocks at root brace
depth only. Claimed declarations continue to use their owning parser; nested
fields and nested feature blocks are therefore left to their specific
diagnostics. An unclaimed block reports
`firmament-v2-canonical-declaration-unknown:<Keyword>`. A known
compatibility-only top-level family reports
`firmament-v2-canonical-construct-not-yet-supported:<Keyword>`.

| V2 declaration family | Status |
| --- | --- |
| Units, Box, Cylinder, RoundedBox, Frustum, Modify, InlineStep | canonical |
| Concept/Struct, Construction Plane, Profile, Compose, Selection | canonical |
| Record, Static, Template, Pattern, Require, Match | canonical bounded static route |
| Recognize, Replace | canonical |
| Pmi | canonical and normalized through the existing V2 PMI records |
| Solid, Let, Fill, Manufacturing, Feature, Expose | compatibility-only or separately scoped; canonical root reports explicit port status |

The scanner intentionally does not claim nested `EdgeFinish`, `Hole`, PMI
records, or fields: those must retain their specialized diagnostics.

## PMI

Canonical `Pmi`, `Datum`, `HoleDiameter`, `Target`, `Value`, `Tolerance`, and
`DatumRefs` normalize to `FirmamentV2PmiRecord` and
`FirmamentV2BoundPmiRecord`. No duplicate lowering exists. Existing PMI target
binding validates semantic hole IDs and admitted face selectors; the AP242
exporter consumes the bound values and tolerance.

The canonical production fixture is
`fixtures/Canonical/valid/box-hole-pmi.firmament`.

## Profile authoring and EdgeFinish admission

`fixtures/Canonical/valid/profile-compose-l-bracket.firmament`
shows a six-segment L loop from two `Rect2` guides and one named `Point2`.
`Segment.From` and `Segment.To` must be named points (including named Rect2
corners); literal coordinates produce
`ProfileSegmentEndpointMustReferenceNamedPoint`.

The paired invalid fixture records the current honest limitation:
`EdgeFinishProfileComposeBoundaryUnsupported`. The semantic boundary exists,
but arbitrary Profile/Compose polygon-boundary chamfer and fillet
materialization remains deferred. Primitive rectangular and explicitly
admitted semantic-chain routes remain unchanged.
