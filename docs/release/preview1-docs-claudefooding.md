# Preview 1 documentation-only authoring review

> Historical Preview 1 evidence. P2-CONSOLIDATION-M1 later closed the
> Concept Path-derived Profile -> Compose limitation recorded below.

Date: 2026-08-07

Context boundary: public manual content plus ordinary `aetheris --help` output.
No parser, compiler, or geometry source was consulted while authoring the
four-hole case. Existing canonical fixtures supply the other first-attempt
sources so the review remains tied to executable release truth.

| Task | First-attempt source | Build result | Retries | Public pages used | Invented grammar / compatibility input |
| --- | --- | --- | ---: | --- | --- |
| Rectangular plate | `fixtures/Canonical/Basics/box.firmament` | Success | 0 | Getting started; Language tour | None |
| Four patterned shaft holes | `fixtures/Canonical/Patterns/four-hole-pattern.firmament` | Success | 0 | Static authoring; Slots and patterns; Holes | None |
| Low-level Profile/Compose L-bracket | `fixtures/Regression/CanonicalGeometry/profile-compose-l-bracket.firmament` | Success | 0 | Low-level Profiles/Compose | `Concept Path`-derived Profiles are Extrude-only in Preview 1. |
| Counterbored plate | `fixtures/Regression/CanonicalGeometry/counterbore-hole.firmament` | Success | 0 | Holes; Language reference | None |
| Supported Profile chamfer | `fixtures/Regression/CanonicalGeometry/profile-chamfer-mixed-convex-reflex-loop-top.firmament` | Success | 0 | Edge finishes | None |
| Supported bounded fillet | `fixtures/Regression/CanonicalGeometry/profile-fillet-reflex-two-segment-top.firmament` | Success | 0 | Edge finishes | None |
| Static/Require/projected PMI | `fixtures/Regression/CanonicalGeometry/pmi-projected-hole-diameter.firmament` | Success | 0 | Require/PMI; Language reference | None |
| Intentional ConvexSmall fillet | `fixtures/Invalid/Geometry/profile-edgefinish-convex-small-fillet-invalid.firmament` | Rejected with `ProfileBoundaryFilletConvexArcSpindleUnsupported` | 0 | Edge finishes; Diagnostics | None |
| InlineStep/Recognize/Replace | `fixtures/Regression/CanonicalGeometry/inline-step-recognize-replace.firmament` | Success | 0 | Existing STEP | None |

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
