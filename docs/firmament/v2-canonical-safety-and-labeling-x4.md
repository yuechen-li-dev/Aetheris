# CANONICAL-SAFETY-AND-LABELING-X4

> Semantic Value M1 preserves a successful canonical `Recognize` region as an
> exposed `SemanticValue` member. Its existing STEP entity/FaceId association is
> the proof for boundary, selection, exact-geometry, and analysis capabilities.
> Recognition metadata alone never creates a profile capability or new geometry.

Round 2 found two frontend integrity faults: canonical Counterbore/Countersink
fields were omitted before lowering and an unknown `Hole<Variant>` could be
ignored. Canonical Hole headers now bind any candidate identifier and reject
unknown variants with `firmament-v2-hole-variant-unknown`. Variant contracts
bind `CounterboreDiameter`/`CounterboreDepth` or
`CountersinkDiameter`/`CountersinkAngle` before a semantic hole is created;
invalid or incomplete values produce the existing variant diagnostics.

Lowering no longer dereferences nullable user fields. Parser validation is the
source-to-AIR boundary: an invalid declaration never creates a document that
can reach the AIR materializer. Canonical Hole blocks also reject unknown field
names rather than silently ignoring a misspelling.

The canonical V2 root now admits `InlineStep`, `Recognize`, and `Replace`.
They produce the same normalized `FirmamentV2InlineStepRecord`,
`FirmamentV2RecognizedRegion`, and `FirmamentV2ReplacementDecl` used by the
lowercase compatibility adapter. Sequential face IDs resolve via the imported
topology map; raw STEP entities remain an explicit advanced form.

The repository fixture
`fixtures/FirmamentV2/Canonical/valid/inline-step-recognize-replace.firmament`
is the replay path: analyze face 7, recognize it by `Faces: [7]`, and execute
the bounded verified hole replacement. It does not require raw STEP grep.
