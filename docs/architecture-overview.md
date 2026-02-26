# Architecture Overview (Provisional)

This package map is directional only and may evolve with milestone outcomes.

## Proposed package map

- `Aetheris.Kernel.Core` — shared kernel primitives, result/diagnostic contracts, numeric policy entry points, and kernel-internal math/transform substrate (not a public CAD math API promise).
- `Aetheris.Kernel.Modeling` — solid/B-rep modeling operations and topology-aware workflows.
- `Aetheris.Kernel.Tessellation` — mesh generation derived from authoritative B-rep.
- `Aetheris.Kernel.Step242` — AP242 mapping and translation at persistence/interchange boundaries.
- `Aetheris.Web.Contracts` — backend/frontend DTO contracts and API-facing schemas.
- `Aetheris.Demo.Web` (or existing frontend app) — browser UI for scenario validation and demos.

## Notes

- Boundaries are favored over deep abstraction in early milestones.
- Kernel remains independent from UI and persistence concerns.
- Kernel operations should return structured result/diagnostic envelopes for expected outcomes instead of throwing ad hoc exceptions.
