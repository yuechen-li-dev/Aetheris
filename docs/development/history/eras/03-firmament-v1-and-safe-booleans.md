# Era 3: Firmament V1 and the safe Booleans

**14 March – 3 May 2026 · about 250 commits · Firmament M0–M10, Boolean waves, E/F series, FORGE-M0, PMI-M0**

## In one paragraph

Aetheris got a language. Firmament V1 described a small ordered feature program:
a box, a cylinder, a subtract, a few `expect_*` checks. The compiler ran it
against the kernel and exported STEP. The language was the easy part. Every
interesting part needed Booleans the kernel could not do in general, so this era
became a long campaign of "safe Boolean families": blind holes, coaxial stacks,
keyways, prism cuts, each recognized explicitly and built with known topology.
Along the way it produced two tools that are still central, JudgmentEngine and
FrictionLab, and the first PMI, Forge and assembly code. It ended with a
two-week pause in the commit history.

## What was built

**Firmament V1.** A pre-M0 corpus and fixture harness came first, then the
language in numbered steps: required-field validation, Boolean lowering into an
execution plan, selector ports, topology-backed `expect_selectable`, schema
blocks for `cnc`, `additive` and `injection_molded` with a first DFM rule
(minimum tool radius), a canonical formatter, and STEP export of the single
resulting body. Placement went through several rounds, ending in "semantic-first"
`on_face` anchors with explicit offsets as a fallback. V1 source was
indentation-based; it later gained TOON- and JSON-shaped document forms.

**Loud failure.** Mid-March commits are titled "Fail loudly on unsupported
Firmament booleans" and "Enforce loud failure for box-torus booleans". An
unsupported combination became a named diagnostic, never a wrong solid.

**Safe Boolean families.** Through late March and April, each new kind of
feature got its own recognized family and rebuild: bounded blind cylinder and
cone subtracts, coaxial subtract stacks generalized to N levels, cylinder-root
re-entry for a centre bore, keyway subsets on shafts, principal-plane mirrored
patterns, prism-family box-root rebuilds, mixed through-voids, polygonal
extrusion roots. A deferred pile tracked everything else.

**Chamfers and fillets, against the plan.** The non-goals said no early edge
finishing. Real parts disagreed. The E-series added bounded chamfer rewrites (two
edges, concave straight edges, non-orthogonal corners routed to an explicit
reject), and the F-series bounded fillets: single cylindrical edges, chained
same-radius concave fillets, cross-radius terminations, with every unsupported
chain classified and rejected.

**JudgmentEngine (13 April).** Choosing among Boolean strategies had become a
tangle of conditions. JudgmentEngine made the choice explicit: candidates,
admissibility, utility scores, tie-breaking and a recorded reason for each
rejection. Boolean classification, safe-subtract continuation and chamfer
routing were ported to it within a week. `AGENTS.md` gained the rule to use it
whenever a subsystem selects among bounded strategies.

**FrictionLab (3 April).** A harness for measuring how hard it actually was to
author real parts: write candidates, run them, classify where they broke, rerun
after fixes (FL2a to FL2g). FRIC-B0, the "chimera" pressure test, mapped exactly
which mixed subtract sequences the Boolean families admitted and why the rest
failed.

**Firsts.** `analyze` and `canon` CLI commands with orthographic section and map
probes; a semantic PMI core with planar datums, explicit dimensions and
JudgmentEngine-placed hole PMI; FORGE-M0, a bounded library-part foundation with
a rounded-corner box golden path; and JSON-shaped `.firmasm` assemblies executed
against the OCCT `as1.step` sample.

## What went wrong

The Boolean families worked, and they did not compose. The stepped-hole case
became the canonical example: a small through hole, then a medium blind
continuation, then a large shallow one. The first two succeeded; the third
crossed a "one hole in history" gate before topology was even attempted.
Generalizing the builder was not enough, because admission depended on the
whole coaxial history. The cost of each new case was growing with the number of
cases already supported.

## Decisions that stuck

- **Unsupported means rejected, by name.** The compiler-honesty rule starts here.
- **JudgmentEngine** for every multi-strategy choice.
- **Measure authoring friction**, don't guess it. FrictionLab's method survives in
  the later burn-in and Claude authoring passes.
- **Semantic PMI** as part of the model, not a drawing afterthought.

## Claude's commentary

This is the era where the project learned its hardest lesson, though it did not
yet know how to act on it. The Boolean API (`Subtract(left, right)`) looks
uniform. The implementations behind it are not: a through hole, a blind pocket,
an open keyway and a counterbore each need different loop roles, face survival
rules and orientation. Every safe family was really a *construction recipe* with
known topology, disguised as a Boolean. The project spent six weeks adding
recipes behind a facade that pretended they were one operation.

Two things from this era aged very well. JudgmentEngine turned "the code picks
something" into "the code picks this, for these reasons, and rejected those".
And the loud-failure rule meant that even the failures were usable data. When
the architecture changed in era 5, the rejected cases were already catalogued.

## What survives

- Firmament V1 as a compatibility input (`FirmamentCompiler`, reached only
  through an explicit V1 lane), with fixtures under `fixtures/Compatibility`
- `BrepBoolean`, frozen in August as a facade over the recognized families
- `Aetheris.Kernel.Core/Judgment` and `Aetheris.Firmament.FrictionLab`
- The legacy JSON `.firmasm` codec and the semantic PMI core
- [BRep Boolean lessons](../../architecture/kernel/brep-boolean-lessons.md), the
  best single write-up of what these families taught

Previous: [Era 2](02-step-and-the-nist-corpus.md) · Next: [Era 4: CIR and the recovery wall](04-cir-and-the-recovery-wall.md)
