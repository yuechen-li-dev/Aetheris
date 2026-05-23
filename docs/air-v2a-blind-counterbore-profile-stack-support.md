# AIR-V2A / V2A.1: cylindrical blind-hole and counterbore profile-stack stabilization

## Scope
AIR-V2A attempted to migrate cylindrical blind-hole and counterbore execution to the AIR/profile-stack route used by through and stepped holes.

AIR-V2A.1 stabilizes the branch and restores test gates.

## Final route status after V2A.1
- Through-hole: AIR/profile-stack route active and executable.
- Stepped-hole: AIR/profile-stack route active and executable.
- Blind-hole: AIR route explicitly deferred; legacy bounded placement-driven blind route remains executable.
- Counterbore: AIR route explicitly deferred; legacy bounded counterbore route remains executable.
- Countersink/chamfered-entry: conical route preserved; AIR conical deferral preserved.

## Why blind/counterbore are deferred in V2A.1
The attempted AIR layer normalization for blind/counterbore caused focused Firmament regressions (counterbore execution `BooleanFailed`, blind-bottom manifold/void regressions, and cascading rematerializer/matrix failures).

V2A.1 therefore restores explicit deferral diagnostics and preserves proven legacy execution for these two variants.

## Diagnostics
Preserved defer breadcrumbs:
- `air-profile-stack-v1-blind-deferred`
- `air-profile-stack-v1-counterbore-deferred`
- `air-profile-stack-v1-conical-deferred`

## Non-goals
- no conical AIR migration,
- no arbitrary profile/loop support,
- no STEP exporter behavior changes,
- no public API/CLI expansion.

## Recommended next step
AIR-V2B should investigate bounded emitter support for blind/counterbore interval topology with dedicated composition constraints and diagnostics before re-enabling AIR routing for these variants.


## AIR-V2B update
Blind and counterbore are no longer blanket deferred when admissible contiguous AIR interval semantics are present; see `air-v2b-blind-counterbore-interval-production.md` for exact accepted/rejected boundaries and diagnostics.
