# Era 2: STEP and the NIST corpus

**3 – 13 March 2026 · about 200 commits · milestones M31–M45, Mx-series**

## In one paragraph

A kernel that only reads its own files proves nothing. This era pointed the AP242
importer at real CAD output, above all the NIST MBE PMI test models (the CTC,
FTC and STC parts), and made it work through them one blocker at a time. The
method mattered as much as the result: a manifest-driven corpus, a published
support matrix, deterministic diagnostics for every expected failure, and an
audit snapshot that was refreshed every time a part got further than before. The
viewer gained a name, Cadmata, along the way.

## What was built

- **A confidence gate.** `v0.corpus.json` sorted files into `passRequired`
  (parse → import → validate → tessellate → pick → export → hash must all
  succeed), `expectedFail` (must fail with exactly the recorded first
  diagnostic) and `deferred`. Runs were byte-stable across repeats.
- **A published support matrix.** v0 accepted a single manifold solid, lines and
  circles, and planar, cylindrical, conical and spherical surfaces. Assemblies,
  PMI, NURBS and healing were listed non-goals.
- **The long tail of real STEP.** Part 21 complex entity instances, typed
  parameter wrappers, inline surface constructors, `BOUNDED_CURVE` edge curves,
  trim-aware tessellation of cylindrical faces, periodic seams and degenerate
  loops on cones and tori, B-spline loops on revolved surfaces, coedge direction
  composition on planar loops. Each fix moved a named NIST part past its current
  blocker, and the commit refreshed the audit snapshot to prove it. Over thirty
  commits in ten days are snapshot refreshes.
- **Export validity probes.** M45 checked that every exported edge's vertices lie
  on its curve within tolerance, the first of many "the file must be correct, not
  just accepted" checks.
- **Cadmata.** The viewer was restyled and named, with a charcoal sketch-style
  grid.

## Decisions that stuck

- **Corpus-driven development.** Real parts, a manifest and a recorded expected
  outcome for each file. This became `Step242Corpus` and the `SlowCorpus` test
  lane, which `AGENTS.md` still requires before any change counts as verified.
- **Deterministic failure is a feature.** An expected failure that changes its
  diagnostic is a regression, even if nothing crashes.
- **Support matrices are public.** What is supported is written down, and the
  test suite enforces the document.

## Claude's commentary

This was the first contact with reality, and it set the project's temperament.
Real STEP files are full of things that are technically legal and practically
hostile: reused seams, degenerate edges, loops wound the "wrong" way, rational
B-splines standing in for circles. The team's answer was neither to write a
general healing pass nor to give up at the first odd entity. It was to take the
next blocker on a named part, fix that, and record the new blocker. That rhythm
of blocker, fix, snapshot and next blocker is visible in every later era.

The STEP importer work also quietly established that importing is not
understanding. A face read from a file has no intent attached. That distinction
drives the much later "imported feature recognition" work in era 9, where
recognizing a hole in an imported part is treated as a separate, provable claim.

## What survives

- `Aetheris.Kernel.Core/Step242` (importer, exporter, canonical hashing)
- `testdata/step242` and its manifests; the NIST audit snapshots
- The corpus discipline in `AGENTS.md` and `CONTRIBUTING.md`

Previous: [Era 1](01-kernel-bootstrap.md) · Next: [Era 3: Firmament V1 and the safe Booleans](03-firmament-v1-and-safe-booleans.md)
