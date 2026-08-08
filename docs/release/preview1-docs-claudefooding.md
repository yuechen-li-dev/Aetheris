# Preview 1 documentation-only authoring review

Date: 2026-08-07

Context boundary: public manual content plus ordinary `aetheris --help` output.
No parser, compiler, or geometry source was consulted while authoring the
four-hole case. Existing canonical fixtures supply the other first-attempt
sources so the review remains tied to executable release truth.

| Task | First-attempt source | Build result | Retries | Public pages used | Invented grammar / compatibility input |
| --- | --- | --- | ---: | --- | --- |
| Rectangular plate | `fixtures/FirmamentV2/Canonical/valid/bare-box.firmament` | Success | 0 | Getting started; Language tour | None |
| Four patterned shaft holes | `fixtures/FirmamentV2/Canonical/valid/docs-four-hole-pattern.firmament` | Success | 0 | Static authoring; Slots and patterns; Holes | None |
| Low-level Profile/Compose L-bracket | `fixtures/FirmamentV2/Canonical/valid/profile-compose-l-bracket.firmament` | Success | 0 | Low-level Profiles/Compose | `Concept Path`-derived Profiles are Extrude-only in Preview 1. |
| Counterbored plate | `fixtures/FirmamentV2/Canonical/valid/counterbore-hole.firmament` | Success | 0 | Holes; Language reference | None |
| Supported Profile chamfer | `fixtures/FirmamentV2/Canonical/valid/profile-chamfer-mixed-convex-reflex-loop-top.firmament` | Success | 0 | Edge finishes | None |
| Supported bounded fillet | `fixtures/FirmamentV2/Canonical/valid/profile-fillet-reflex-two-segment-top.firmament` | Success | 0 | Edge finishes | None |
| Static/Require/projected PMI | `fixtures/FirmamentV2/Canonical/valid/pmi-projected-hole-diameter.firmament` | Success | 0 | Require/PMI; Language reference | None |
| Intentional ConvexSmall fillet | `fixtures/FirmamentV2/Canonical/invalid/profile-edgefinish-convex-small-fillet-invalid.firmament` | Rejected with `ProfileBoundaryFilletConvexArcSpindleUnsupported` | 0 | Edge finishes; Diagnostics | None |
| InlineStep/Recognize/Replace | `fixtures/FirmamentV2/Canonical/valid/inline-step-recognize-replace.firmament` | Success | 0 | Existing STEP | None |

Normal supported first-attempt success: **8/8 (100%)**. The unsupported case
was correctly identified without fallback. There were no retries,
compatibility forms, invented fields, or source-code archaeology attempts.

The unsupported diagnostic was useful: it names the spindle/self-intersection
policy and the manual gives three valid responses (reduce the finish, increase
the source radius, or leave the corner sharp).

One documentation gap appeared. `validate` accepts the intentionally invalid
ConvexSmall source because it does not materialize geometry; `build` emits the
advertised policy diagnostic. The public manual was corrected to distinguish
language/semantic validation from materialization-policy checking.
