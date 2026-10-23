# The history of Aetheris

Aetheris was written between February and October 2026 by one person, Yuechen
Li, working with AI coding agents: mostly OpenAI's Codex/GPT models, with
Claude and Gemini at various points. Almost every change landed as a numbered,
scoped task with a written report. By October there were more than a thousand
of those reports. They were accurate, exhaustive and almost unreadable.

This folder replaces them with the story they tell. It is meant to be read by
people and by agents who need to know why the system is shaped the way it is.
The original reports are still in git; [ORIGINALS.md](ORIGINALS.md) lists every
one and how to get it back.

## The short version

Aetheris started as a conventional B-rep kernel: build primitives, then make 3D
Booleans work. Booleans turned out to be the whole problem. Three months went
into making them reliable, first with ever-narrower "safe families", then by
evaluating geometry as implicit fields and trying to recover exact B-rep from
the fields afterwards. Both approaches worked for the cases they were built for
and stopped generalizing.

The way out was to stop discovering topology and start declaring it. If the
source says "a 6 mm counterbored hole through this plate", the compiler already
knows every face, loop and edge the result must have. It never needs to
intersect a cylinder with a box and guess. That idea became the **sweep-first**
architecture and an intermediate representation called **AIR**, and it turned
the project into what it is now: a compiler, with Firmament as the source
language, AIR as the middle and exact B-rep and STEP AP242 as the output.

Once the compiler shape was right, the project moved fast: a typed second
version of the Firmament language, three public previews in ten days, then an
expansion into sheet metal, FEA, assemblies, drawings, piping, wire forming,
surfacing and more. The latest era is consolidation: making the language
consistent, the compiler honest about everything it does not support, and the
editors (Cadmata and Helios) usable.

## Eras

| # | Era | Dates | What changed |
|---|---|---|---|
| 1 | [Kernel bootstrap](eras/01-kernel-bootstrap.md) | 25 Feb – 2 Mar | Topology, analytic geometry, primitives, first Booleans, a web viewer |
| 2 | [STEP and the NIST corpus](eras/02-step-and-the-nist-corpus.md) | 3 – 13 Mar | AP242 import and export, measured against real-world test parts |
| 3 | [Firmament V1 and the safe Booleans](eras/03-firmament-v1-and-safe-booleans.md) | 14 Mar – 3 May | The first language, bounded Boolean families, JudgmentEngine, FrictionLab |
| 4 | [CIR and the recovery wall](eras/04-cir-and-the-recovery-wall.md) | 4 – 22 May | Implicit-field geometry, and the attempt to recover exact B-rep from it |
| 5 | [AIR and sweep-first](eras/05-air-and-sweep-first.md) | 23 May – 17 Jun | Declared topology instead of discovered topology |
| 6 | [Firmament V2 and the AP242 MVP](eras/06-firmament-v2-and-the-ap242-mvp.md) | 18 Jun – 2 Aug | A typed language, semantic holes and PMI, inline STEP, Forge |
| 7 | [Previews 1 to 3](eras/07-previews-1-to-3.md) | 3 – 18 Aug | Productization: assemblies, FEA, drawings, sheet metal, three releases |
| 8 | [Domain expansion](eras/08-domain-expansion.md) | 19 Aug – 30 Sep | Wire, piping, surfacing, gears, mechanisms, showcases, humanoids |
| 9 | [Preview 4 and consolidation](eras/09-preview-4-and-consolidation.md) | 1 Oct – | Telos display, compiler honesty, one AST, Helios authoring |

The commit history also shows long pauses: two weeks after the Boolean work in
April, two more after the chamfer labs in late May, and nearly four weeks in
July. The author has said he walked away from the project more than once during
the Boolean period. Each restart came with a change of approach.

## Themes that run through every era

**Honest diagnostics.** From the first milestone, a failed operation returned a
named diagnostic instead of throwing or returning something plausible. That rule
hardened into the central product promise: a supported request is built, an
unsupported one is rejected by name, and nothing is silently skipped.

**Bounded before general.** Almost every capability started as one explicitly
admitted case ("a box with one through-hole along Z") and grew by naming the next
case, never by claiming generality first. This is why so many reports are titled
X0, X1, X2: each was one more bounded step.

**Evidence over assertion.** Reports end in one of three verdicts: success,
meaningful progression (one blocker removed, the next one isolated), or an honest
stop. That convergence rule is in `AGENTS.md` and it shaped how agents worked:
they were asked to stop and report rather than keep patching.

**Exact analytic geometry.** Aetheris prefers planes, cylinders, cones, spheres
and tori over NURBS wherever the shape allows. Exact geometry is what lets the
compiler know topology in advance, and it is why exported STEP files do not need
healing.

**Judgment, not heuristics in disguise.** When a subsystem chooses among several
strategies, it uses JudgmentEngine, so that admissibility, scoring and the
reason for rejecting each candidate are explicit and inspectable.

## How to use this folder

- **To understand a design decision**, start with [DECISIONS.md](DECISIONS.md).
  Each decision links to the era where it was made.
- **To understand a subsystem's oddities**, read the era where it was built.
  Most surprising behavior is a fossil of the constraint that was true then.
- **To find an original report**, search [ORIGINALS.md](ORIGINALS.md) by name or
  milestone code, then restore it from git.
- **For current behavior**, do not use this folder. Use the
  [public documentation](../../public/README.md) and the compiler itself.

Sections marked **Claude's commentary** are my own reading of the record (I am
Claude, an AI model by Anthropic, and I wrote these summaries in October 2026).
Everything else is drawn from the reports and the commit history. Where the two
disagree, trust the commit history.
