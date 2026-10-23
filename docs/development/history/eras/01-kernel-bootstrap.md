# Era 1: Kernel bootstrap

**25 February – 2 March 2026 · about 50 commits · milestones M00–M30**

## In one paragraph

Aetheris began as a textbook solid-modeling kernel, specified up front as a
numbered list of milestones and built one milestone per pull request. In under a
week it had typed topology, exact analytic curves and surfaces, primitive
solids, extrude and revolve, a deliberately narrow Boolean, display
tessellation, picking, an ASP.NET host, a React viewer, and a first AP242
exporter and importer that could round-trip its own output. Nothing here was
sophisticated. All of it was deliberate, and most of it is still in the code.

## What was built

The original plan, `milestones.md`, laid out about forty milestones, each with a
checklist and an explicit list of what it did *not* do.

- **M00–M03: foundations.** Project skeleton and CI; the `KernelResult<T>`
  envelope with structured diagnostics (success never carries errors, failure
  always carries at least one); tolerance and numeric primitives; immutable
  `Point3D`, `Vector3D`, `Direction3D` and affine transforms.
- **M04–M07: B-rep model.** Strongly typed IDs for body, shell, face, loop,
  coedge, edge and vertex; a topology graph with a reference validator; exact
  `Line3Curve`, `Circle3Curve`, plane, cylinder, sphere and cone surfaces; and the
  binding between topology and geometry, the minimal definition of a B-rep body.
- **M08–M11: construction.** Box, cylinder and sphere constructors, ray and
  point queries, polygon extrude and a minimal revolve, all validator-backed.
- **M12–M13: Booleans, narrowly.** A Boolean pipeline scaffold, then "real but
  narrow" Booleans for axis-aligned box/box cases only. The very first fix
  tightened a union predicate to *reject* L-shaped results it could not
  represent.
- **M14–M20: seeing it.** Display tessellation (explicitly "not simulation
  meshing"), picking contracts, a local ASP.NET API, versioned web protocol, a
  React viewer shell and a two-body Boolean UI.
- **M21–M30: interchange.** A hardening corpus, an AP242 subset exporter and
  importer, round-trip reliability tests, a lightweight definition/occurrence
  document model, a canonical STEP hash, and upload/download in the viewer.

## Decisions that stuck

- **C# and .NET**, chosen for tooling and ecosystem rather than theoretical
  performance. The kernel is still a dependency-free `Aetheris.Kernel.Core`.
- **B-rep is the source of truth** for solids. History and parametrics were
  declared outside the kernel. (They came back later as the compiler, above it.)
- **AP242 is the interchange target**, mapped at the boundary rather than
  mirrored as in-memory objects.
- **Diagnostics are part of the result type.** Every later "honest stop" and
  named rejection traces back to M01.
- **Non-goals were written down**: no sketch constraint solver, no FEA, no early
  fillets or chamfers, no all-in-one CAD application. Two of those were reversed
  within six months; the sketch-solver one never was.

## Claude's commentary

Reading the milestone file now is a little like reading a project charter after
the company has pivoted twice. The plan was right about the foundations and
wrong about where the difficulty lay. M13's narrow box/box Boolean was meant as
a first step toward general Booleans, but it turned out to be the shape of every
Boolean the project ever shipped: an explicitly recognized case with known
topology. That lesson cost three months to learn properly (eras 3 and 4).

The other thing that stands out is speed. Thirty milestones in six days is only
possible when each one is small, specified in advance and independently testable.
That working style, numbered, scoped, with a written report, never changed, and
it is the reason the later record is so large.

## What survives

- `Aetheris.Kernel.Core/{Topology,Geometry,Brep,Math,Numerics,Diagnostics,Results}`
- `Aetheris.Server` (the original API host) and the viewer that became
  `aetheris.client` and later Cadmata
- The M00 documents `vision.md`, `non-goals.md` and `milestones.md`, listed in
  [ORIGINALS.md](../ORIGINALS.md)

Next: [Era 2: STEP and the NIST corpus](02-step-and-the-nist-corpus.md).
