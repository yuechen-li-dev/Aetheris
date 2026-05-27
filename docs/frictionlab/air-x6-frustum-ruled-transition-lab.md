# AIR-X6 — Frustum-as-AirRuledTransition lab

## Purpose and scope

AIR-X6 is a lab-only experiment that evaluates representing a capped conical frustum as an `AirRuledTransition` between two compatible circular profiles, then compares that candidate against the current canonical revolve-produced frustum.

This work is intentionally lab scoped:
- no production routing changes,
- no public API changes,
- no STEP exporter/importer changes,
- no Boolean core changes.

## Doctrine references

Aetheris V2 doctrine (resolved-profile-first, sweep-first, bounded analytic construction) explicitly lists `AirRuledTransition` as a bounded direction for compatible profile transitions. See `docs/aetheris-v2-sweep-first-architecture.md` (section 6.6).

AIR-X5 documented that cone/frustum already has a viable revolve parity lane but remained conservatively marked for emitter-parity hardening before production migration. AIR-X6 provides first ruled-transition evidence for frustum specifically.

## Why frustum first

A frustum has clean bounded structure for ruled-transition evidence:
- two coaxial circular profiles,
- parallel end planes,
- one side analytic family (conical for unequal radii; cylinder-like when equal),
- deterministic cap policy.

That makes frustum a narrow, high-signal first experiment before any generic loft or freeform behavior.

## Mathematical model

For angular parameter `u` and transition parameter `t ∈ [0,1]`, AIR-X6 uses the ruled interpolation model:

`S(u,t) = (1 - t) P(u) + t Q(u)`

where:
- `P(u)` is the bottom circle (`z = -H/2`, radius `R0`),
- `Q(u)` is the top circle (`z = +H/2`, radius `R1`),
- compatibility is shared angular parameterization.

## Baseline vs candidate construction

- **Baseline**: canonical frustum through existing revolve path (`BrepRevolve` radial segment profile).
- **Candidate**: lab-only ruled-transition representation path that classifies admissible coaxial circle→circle transitions as an analytic conical surface and builds with existing kernel capability.

Apex cones (`topRadius=0` or `bottomRadius=0`) are explicitly deferred to revolve and are not forced through ruled-transition candidate construction.

## Test cases

AIR-X6 includes:
- frustum `(5,2,10)`
- frustum `(3,1,12)`
- inverted taper `(2,5,10)`
- cylinder-like `(4,4,10)`
- apex defer cases `(5,0,10)` and `(0,5,10)`
- invalid inputs: negative radius, zero/negative height, non-finite dimension

## Topology parity findings

For successful non-apex candidate rows, current lab reports deterministic topology parity with baseline on:
- vertex/edge/face counts,
- planar/conical face family counts,
- loop/coedge counts.

## STEP smoke findings

Successful candidate rows satisfy smoke markers:
- contains `ISO-10303-21`
- contains `MANIFOLD_SOLID_BREP`
- contains `ADVANCED_FACE`
- contains `CONICAL_SURFACE`
- contains `PLANE`
- does not contain `BREP_WITH_VOIDS`

## Apex-cone deferral rule

Rows with `topRadius=0` or `bottomRadius=0` emit explicit deferral diagnostic:
- `air-x6-apex-cone-deferred-to-revolve`

and recommendation:
- `frustum-apex-cone-defer-to-revolve`

## Recommendation

Given deterministic topology parity and STEP smoke success in this bounded lab, AIR-X6 recommendation is:

- `frustum-ruled-transition-ready-for-production-migration`

with caveat that this is still lab evidence and does not itself migrate production cone/frustum routing.

## Non-goals

- no production migration,
- no generic loft implementation,
- no NURBS/freeform,
- no sphere/torus changes,
- no STEP exporter changes.
