# AIR-X4 — Box-as-AirExtrude lab-only EVT

## Purpose and scope

AIR-X4 introduces a **lab-only** evidence lane that compares:

1. baseline `BrepPrimitives.CreateBox(width, depth, height)` behavior, and
2. an AIR-style rectangle-profile linear extrusion built with existing `BrepExtrude` helpers.

No production primitive routing is changed in AIR-X4 itself.

> Postscript (AIR-V3): AIR-V3 consumed this evidence and migrated `BrepPrimitives.CreateBox` to the rectangle-profile extrude path in production.

## AIR-X3 reference

This lab follows the recommendation from `docs/development/milestones/general/air-x3-primitive-as-air-normalization-audit.md`:

- primitive-as-AIR normalization is viable for bounded analytic solids;
- box is the lowest-risk first candidate;
- primary risk is topology/seam/cap parity rather than geometric expressiveness.

## Existing `CreateBox` topology summary

For valid dimensions, baseline `CreateBox` emits a closed solid with expected box topology signatures:

- vertices, edges, faces
- planar face count
- loop/coedge totals
- STEP smoke markers

The lab captures those signatures per case and compares them against AIR-style extrude outputs.

## AIR-style rectangle-extrude construction attempted

For each valid case the lab builds:

- rectangle profile with centered extents `[-width/2,+width/2] x [-depth/2,+depth/2]`, and
- `BrepExtrude.Create` along +Z from origin `z=-height/2` for depth `height`.

If profile creation or extrusion fails, the lab records explicit `air-x4-extrude-box-failed:*` diagnostics and recommends parity follow-up instead of patching production emitters.

## Test cases and results

Covered cases:

- cube-like: `(10,10,10)`
- rectangular: `(12,8,6)`
- invalid dimensions: zero/negative inputs

Non-origin translated case is deferred in AIR-X4 because this EVT focuses on canonical centered parity and avoids introducing additional placement ambiguity.

## Topology parity findings

The lab emits deterministic diagnostics:

- `air-x4-box-extrude-lab-started`
- `air-x4-baseline-box-created`
- `air-x4-extrude-box-created` (or explicit failure)
- `air-x4-topology-parity-succeeded` (or explicit mismatch payload)

The recommendation is derived deterministically from topology and STEP smoke parity.

## STEP smoke findings

Both baseline and AIR-style bodies are exported via `Step242Exporter.ExportBody` and checked for:

- contains: `ISO-10303-21`, `MANIFOLD_SOLID_BREP`, `ADVANCED_FACE`, `PLANE`
- does not contain: `BREP_WITH_VOIDS`

The lab emits `air-x4-step-smoke-succeeded` or `air-x4-step-smoke-failed`.

## Final recommendation

Per case recommendation is one of:

- `box-air-extrude-ready-for-production-migration`, or
- `box-air-extrude-needs-emitter-parity-work`.

AIR-X4 remained lab-only at execution time; AIR-V3 later consumed this lane for production migration.

## Explicit non-goals

- no production migration is implemented inside AIR-X4 deliverables (migration occurs later in AIR-V3)
- no cylinder/cone/sphere/torus primitive-as-AIR expansion
- no STEP exporter/importer changes
- no Boolean core changes

## Test commands run

- `dotnet test Aetheris.FrictionLab.Tests/Aetheris.FrictionLab.Tests.csproj -c Release -f net10.0 --filter "AirBoxExtrude|AirProfileStack|ProfileStackExtrude|RecoveryPolicy|CIRLab"`
- `dotnet test Aetheris.Kernel.Core.Tests/Aetheris.Kernel.Core.Tests.csproj --filter "BrepPrimitives|CreateBox|Step242|Primitive"`
