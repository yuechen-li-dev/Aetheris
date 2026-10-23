# Era 6: Firmament V2 and the AP242 MVP

**18 June – 2 August 2026 · about 115 commits · AIR-FIRMAMENT-A2, HOLE-X, STEP-V2, INLINE-STEP, ANALYZE-MAP, V2 Phase 1, FORGE-CS**

## In one paragraph

With the compiler architecture settled, the source language had to catch up.
Firmament V2 was designed in four documents on a single day and then built: a
PascalCase, typed, declaration-based language with Records, `with` derivation,
semantic references, Concepts, Templates and PMI. Holes became semantic features
rather than subtracted cylinders. A readiness contract defined a minimum viable
product (V2 source to verified AP242 with semantic PMI) and a closeout audit
graded the repository against it. In parallel the team gave LLMs a way to "see"
parts without images, began a strangler-fig path for migrating imported STEP to
semantic source, and wrote down how an experienced CAD modeler thinks.

## What was built

- **The V2 language design (18 June).** AIR-FIRMAMENT-A2 and its three companions
  covered source design, semantic references and admissibility, record
  derivation with `with`, and DFM Templates, Concepts and PMI. Tests still read
  these four documents, which is why they are kept.
- **A parser, one slice at a time (AIR-FIRMAMENT-X1 to X12).** A tiny V2 parser
  for Box, then `with`, `=>` exposure, a side-hole region, its variations along
  every axis, selector aliases and a route policy, each locked by artifact parity.
- **Semantic holes (HOLE-A0, HOLE-X1 to X4).** Holes as first-class features with
  stacked components, materialized through AIR, with a V2 source hook.
- **The AP242 MVP (STEP-V2-A1, X1 to X7, readiness contract, closeout).** Box,
  analytic primitives, semantic holes, derivation, multi-feature composition,
  derived variants, DFM concept enforcement and semantic PMI, each
  *step-verified*: real build, real B-rep, real exporter, reimport, topology and
  volume checks. The closeout audit graded every fixture against that bar. The
  `Demo` tag (21 June) marks the resulting PMI demonstration.
- **Values with tolerances (V2 L1–L4, P1–P2).** `let` literals, grouped records,
  an arithmetic expression graph, toleranced values, and record-shaped PMI
  exported to AP242.
- **Inline STEP (INLINE-STEP-A0, X1 to X7).** Carry an imported STEP body
  unchanged, then annotate it, label recognized regions, account for the residual,
  propose semantic replacements and finally replace a region with a native
  feature. A strangler fig for existing geometry, not a decompiler.
- **Seeing without pixels (ANALYZE-MAP-A0 to A6).** Ray and height maps with
  analytic hits on every surface family, a six-view summary written for LLMs,
  connected components and ranked probes, dogfooded on NIST CTC-01 and FTC-09.
- **LLM CAD strategy notes (20 June).** Ten short essays on modeling strategy:
  geometry inventory versus intent, prismatic first principles, semantic
  decompilation, profiles as constructive regions, holes as semantic features,
  edge-finish intent, manufacturing constraints. They are still in
  `architecture/llm-cad-strategy` and fed the public modeling-strategy skill.
- **Display architecture (DISPLAY-ARCH A0, X1 to X6).** DisplayIR with explicit
  partial display, bounded mesh lanes and wireframe fallback, retiring the old
  mesh-scene assumptions from the viewer.
- **Forge (FORGE-A0, X1–X2, FORGE-CS A0–A5 in July).** The semantic extension
  architecture: concept and Template descriptors, a standard concept pack,
  read-only C# interop facades, runtime concept validation, PMI obligations and
  trusted external packs.
- **Housekeeping.** .NET 10 only, a legacy-V1 test policy, testhost triage, and
  "house calls" on FTC-06 and FTC-07 import problems.

## Decisions that stuck

- **Firmament V2 is the language.** V1 became a compatibility input.
- **Holes, and later most features, are semantic**, carrying intent into AIR,
  STEP labels and PMI.
- **"Step-verified" means the real path end to end**, including reimport. Parsed
  or planned is not done.
- **Imported geometry migrates incrementally**, region by region, with the
  residual accounted for.
- **LLMs are first-class users.** Text summaries, maps and strategy notes were
  built for agents, not added for them later.

## Claude's commentary

The V2 design landed in a day because the hard thinking had already happened.
Records, `with`, Concepts and Templates are what you get when you take "declare
intent before topology" seriously at the source level: values are fixed before
lowering, choices are compile-time, and a Template is specialization, not a
macro. The language was designed by a compiler person, and it shows.

Two early bets from this era are worth noting because they look obvious now and
were not then. Writing a separate *LLM-oriented* text view of geometry
(ANALYZE-MAP) assumed agents would be doing real CAD work, months before that
was routine. And the MVP closeout's definition of done (real exporter, reimport,
measured topology) is stricter than many commercial CAD test suites. It is the
direct ancestor of the `Assert` blocks in today's language.

## What survives

- `Aetheris.Kernel.Firmament/FirmamentV2` (parser, binder, lowering)
- The four AIR-FIRMAMENT-A2 design documents and the Preview 2 language
  reference in `history/firmament/preview2-reference/` (kept because tests read
  them)
- `aetheris analyze` maps and summaries, inline STEP, the Forge descriptor model
- [LLM CAD strategy notes](../../architecture/llm-cad-strategy/README.md)

Previous: [Era 5](05-air-and-sweep-first.md) · Next: [Era 7: Previews 1 to 3](07-previews-1-to-3.md)
