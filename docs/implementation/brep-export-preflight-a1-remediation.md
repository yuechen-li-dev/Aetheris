# BREP-EXPORT-PREFLIGHT-A1 remediation

## Decision

**B. Keep global `Audit`; enforce trusted AIR chamfer production routes.**
`Step242ExportOptions` remains Audit/LegacyRoute by default. The two explicitly bounded
Firmament V2 AIR routes request Enforce/TrustedProductionRoute immediately before STEP
writer construction:

- `AirPrismaticTopFaceBoundaryChamfer` (rectangular top-loop chamfer)
- `AirRevolutionProfileTopRimChamfer` (circular top-rim chamfer)

Both routes export, reimport, and are manifold after preflight. Semantic-hole,
template-hole, ordinary box/extrusion, imported legacy, and bounded Boolean paths stay
Audit until their independent producer corpus is clean.

## Audit inventory and classification

| Code | Producer / fixture family | Classification | A1 result |
| --- | --- | --- | --- |
| `brep-preflight-trim-off-surface` | malformed plane/cylinder/cone trim fixtures | InvalidGeometry | Enforce blocks before STEP text; retained negative coverage. |
| `brep-preflight-coedge-disconnected` | `RevolvedProfileStackEmitter`, circular top-rim chamfer | InvalidTopology | Fixed: periodic rim now reuses the generating seam vertex. |
| `brep-preflight-coedge-disconnected` | `PrismaticSectionTransitionTopologyPlanner`, rectangular top-loop chamfer | InvalidTopology | Fixed: reversed top cap also reverses coedge order. |
| `brep-preflight-check-unsupported` | linear extrusion, surface-of-revolution, B-spline surface imports | UnsupportedCheck | Warning only; not an enforcement failure by itself. |
| periodic single-edge circle seam | cylinder/cone analytic fixtures | ValidLegacyRepresentation | Accepted when endpoint topology and closed loop agree. |
| older hole/profile-stack and Boolean topology | semantic holes, templates, bounded Boolean corpus | SuspiciousNeedsReview | Remains Audit; no serialization repair is applied. |

The report now carries the machine-readable `Classification` enum on every diagnostic.
Topology/edge contradictions are `InvalidTopology`; trim/support contradictions are
`InvalidGeometry`; unavailable containment coverage is `UnsupportedCheck`.

## Producer proof and artifacts

The circular producer changed from six coincident vertices to three shared seam vertices;
edge count (5), face count (4), analytic cylinder/cone/plane surfaces, and reimport remain
unchanged. Its representative artifact hash is
`B64A56F1ECD65324E3A2ED54302FFEF9188F4800E2D6DBE008E09C8F450C9699`.
The rectangular cap change alters only oriented top-cap coedge order; its representative
artifact hash is `DA592F28636D3F1D46A5BD3B798545F67E72CC79931DBCBE3B073DC91E8713A7`.
These are intentional topology corrections, not snapshot updates.

## Global Enforce blockers

Global Enforce is blocked by legacy semantic-hole/profile-stack and bounded-Boolean
producers, plus unsupported containment checks for imported non-analytic surface families.
Before changing the default, each producer needs a clean audit corpus, reimport proof, and
an explicit determination that remaining warnings are UnsupportedCheck or
ValidLegacyRepresentation. CAD Assistant was not available in this environment; CLI
reimport/manifold checks are the authoritative evidence recorded here.
