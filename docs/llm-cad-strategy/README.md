# LLM CAD Strategy Notes

These notes collect practical CAD strategy in a form LLMs can use. They are not a claim of novelty. They are a translation layer between experienced CAD modeling habits and LLM reasoning for Aetheris, Firmament, AIR, and semantic decompilation work.

## What this folder is

This folder is a living internal wiki for CAD-native reasoning. It gives LLMs and human reviewers a shared vocabulary for thinking about robust CAD authoring strategy, not just final geometry.

The intended audience is:

- Codex, ChatGPT, Claude, and other LLMs working on Aetheris tasks.
- Humans supervising Firmament, AIR, decompilation, and kernel development.
- Future maintainers who need to distinguish a plausible feature strategy from a raw face inventory.

## What this folder is not

This folder is not:

- A language specification.
- A parser or lowering implementation plan by itself.
- A replacement for Firmament, AIR, BRep, DisplayIR, STEP, or kernel documentation.
- A claim that these modeling habits were invented here.
- A rule that every CAD model must be built the same way.

The notes describe practical CAD modeling strategy and resilient modeling strategy as working heuristics for LLMs.

## How LLMs should use these notes

Use these notes when you need to:

- Infer authoring intent from BRep, STEP, DisplayIR, screenshots, or CLI evidence.
- Propose a resilient feature tree rather than merely listing visible faces.
- Generate Firmament V2 candidates in a robust construction order.
- Identify missing Firmament, AIR, kernel, or display capabilities.
- Explain ambiguity and confidence instead of pretending that one feature tree is certain.

Treat the notes as guidance for reasoning. Do not treat them as permission to change product behavior.

## Current lesson list

- [00 — Purpose and scope](00-purpose-and-scope.md)
- [01 — Geometry inventory vs. modeling strategy](01-geometry-inventory-vs-modeling-strategy.md)
- [02 — Prismatic CAD first principles](02-prismatic-cad-first-principles.md)
- [03 — Semantic decompilation workflow](03-semantic-decompilation-workflow.md)
- [04 — Spatial decomposition first pass](04-spatial-decomposition-first-pass.md)
- [05 — CAD as semantic dependency graph](05-cad-as-semantic-dependency-graph.md)
- [06 — Profiles as constructive regions](06-profiles-as-constructive-regions.md)
- [07 — Holes are semantic features](07-holes-are-semantic-features.md)
- [99 — Glossary](99-glossary.md)

## How to add future lessons

Future lessons should be short, concrete, and field-manual style. Prefer examples that help an LLM choose between competing authoring strategies.

A new lesson should usually include:

- The core lesson in one or two sentences.
- A practical CAD pattern.
- The LLM failure mode it prevents.
- The evidence an LLM should look for.
- Boundaries: what the lesson does not imply.

Keep personal branding out of the folder. Attribute lessons to observed CAD practice, Aetheris experiments, and reusable modeling experience.

## Boundary with implementation docs

Implementation docs define syntax, lowering, AIR contracts, BRep behavior, STEP import/export, DisplayIR, tests, and product behavior. This folder informs how LLMs reason about those systems, but it does not change them.

If a note suggests that Aetheris needs a new capability, record that as a missing capability or future milestone. Do not silently treat the note as an implementation authorization.
