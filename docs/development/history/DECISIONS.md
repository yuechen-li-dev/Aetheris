# Decisions

The architectural decisions that still shape Aetheris, with when they were made
and why. Each links to the era that explains it. Superseded decisions are listed
at the end, because knowing what was tried and abandoned saves people from
trying it again.

## Architecture

**Aetheris is a compiler: Firmament → AIR → BRep → STEP.** *(Era 5, 14 June)*
Source intent is lowered through Feature AIR and Constructive AIR to an explicit
B-rep plan, then to B-rep and STEP. Each stage has one owner. Why: lowering
preserves intent that a geometry-first kernel throws away and must later
rediscover. → [Era 5](eras/05-air-and-sweep-first.md)

**Declared topology over discovered topology.** *(Era 5, 23 May)* "Boolean union
composes. Boolean subtraction discovers." Prefer extrude, revolve, sweep and
section transitions, where the result's faces and loops are known in advance;
use 3D subtraction only when intent cannot be declared. Why: runtime
surface/surface intersection, trimming and re-stitching was the cost centre of
every failure in eras 3 and 4. → [Era 5](eras/05-air-and-sweep-first.md)

**The kernel starts at resolved 2D profiles, not sketches.** *(Era 5)* No
constraint solver in the kernel. Profiles arrive as explicit loops with
deterministic inside and outside. Why: 2D region topology is cheap and
predictable; constraint solving is an authoring concern. Firmament fixes
dimensions at compile time instead. → [Era 5](eras/05-air-and-sweep-first.md)

**CIR is an analysis mirror, never the topology authority.** *(Eras 4–5)* The
implicit-field representation answers containment, volume, maps and GPU display.
It never produces exported topology. Why: recovering B-rep from a field is
decompilation, and it only works for families you could have built directly. →
[Era 4](eras/04-cir-and-the-recovery-wall.md)

**`BrepBoolean` is frozen; new topology goes through recipes and Surgery.**
*(Era 7, 13 August)* Geometry queries supply evidence, a recognized recipe states
the expected topology, and a small BRep Surgery layer realizes and validates it.
Why: the Boolean facade looked uniform while each family needed its own topology
contract, and central dispatch did not scale. →
[Era 7](eras/07-previews-1-to-3.md), [Boolean lessons](../architecture/kernel/brep-boolean-lessons.md)

**Exact analytic geometry first.** *(Era 1 onward)* Planes, cylinders, cones,
spheres and tori wherever the shape allows; rational and B-spline geometry only
where needed, and recovered to analytic form on import when it provably is one.
Why: exact geometry is what makes topology predictable and exported STEP clean.

**C# and .NET, with a dependency-free core.** *(Era 1)* Chosen for tooling and
ecosystem. `Aetheris.Kernel.Core` has no project or package dependencies.

## The language

**Firmament V2 is the only canonical language.** *(Era 6, 18 June; confirmed era
7)* V1 and legacy JSON `.firmasm` are compatibility inputs on an explicit lane.
→ [Era 6](eras/06-firmament-v2-and-the-ap242-mvp.md)

**Everything is compile time.** Records, Statics, Tables, `with`, Templates,
Features, Patterns and `match` are expanded before geometry. No mutable state, no
loops, no `if` statements, no runtime objects. Why: a CAD artifact must be fully
determined by its source, and finite expansion keeps that checkable. →
[Era 6](eras/06-firmament-v2-and-the-ap242-mvp.md)

**Features are semantic.** A hole is a hole, with its variant and requirements,
not a subtracted cylinder. Intent flows into AIR, STEP labels and PMI. →
[Era 6](eras/06-firmament-v2-and-the-ap242-mvp.md)

**The source-intent invariant.** *(Era 9, October)* A successful build means
every authored request was consumed. Unknown fields, statements and unsupported
combinations reject with a diagnostic. Silent acceptance is a bug. Why: agents
and humans both trust a successful build, so a build that quietly drops intent
is worse than a failure. → [Era 9](eras/09-preview-4-and-consolidation.md)

**One source-located AST for every domain.** *(Era 9)* Domains keep their own
semantics and admission, but share parsing, spans and diagnostics. →
[Era 9](eras/09-preview-4-and-consolidation.md)

**Casing: PascalCase constructs, camelCase fields and names, lowercase linking
words.** *(Era 9)* Older PascalCase field spellings remain aliases; user symbols
are never renamed. → [Era 9](eras/09-preview-4-and-consolidation.md)

**Tolerances are typed values; PMI is explicit.** *(Eras 6 and 9)* `tol` travels
with a value, geometry uses the nominal, and only `Pmi` publishes. Arithmetic does
not invent tolerance propagation.

## How work is done

**Diagnostics are part of every result.** *(Era 1, M01)* Success never carries
errors; failure always carries at least one, with a stable code.

**Bounded first.** Capabilities enter as one explicitly admitted case and grow by
naming the next. Unsupported cases are listed, not implied.

**Three verdicts.** Every substantial task ends in success, meaningful
progression (one blocker removed, the next isolated) or an honest stop.
*(`AGENTS.md`)*

**JudgmentEngine for competing strategies.** *(Era 3, 13 April)* Admissibility,
utility scoring, tie-breaking and rejection reasons are explicit. Not for simple
deterministic dispatch. → [Era 3](eras/03-firmament-v1-and-safe-booleans.md)

**Corpus-driven verification.** *(Era 2)* Real parts with recorded outcomes;
the `SlowCorpus` lane gates every change. → [Era 2](eras/02-step-and-the-nist-corpus.md)

**"Verified" means the real path.** *(Era 6)* Real build, real exporter, reimport
and measured topology. Planned or parsed is not done.

**Claims follow evidence.** *(Era 7)* Release notes and public docs state only
what tests and qualification prove; limits are written down.

**Agents are users.** *(Eras 6–9)* LLM-oriented summaries, authoring burn-ins
using only public docs, and preserved agent specimens as regression fixtures.

**Documentation.** *(Era 7, revised era 9)* User documentation lives only in
`docs/public`. Development history is summarized here; working reports for the
current era live in `docs/development` until the era closes. Generated output
goes to ignored `artifacts/local/`.

## Tried and superseded

| Idea | Era | Why it was dropped |
|---|---|---|
| General 3D Booleans as the modeling foundation | 1–3 | Every working case was a recognized family with known topology; generalization cost grew with each family. |
| Safe Boolean families as the growth path | 3 | Admission depended on whole operation histories (the stepped-hole cliff). |
| Fall-forward to CIR with B-rep recovery | 4 | Recovery is decompilation; the generic executor only handled already-supported families. |
| `AirChamfer` patching of sharp bodies | 5 | Replaced by constructing the final body directly (profile chamfers, section transitions). |
| Firmament V1 indentation, TOON and JSON documents | 3–6 | Replaced by typed, declarative V2; kept only as compatibility input. |
| JSON-shaped `.firmasm` | 3–7 | Replaced by the V2 assembly profile. |
| Shadow-parser migration of domains | 9 | Superseded by migrating production admission directly onto the shared AST. |
| Non-goals "no FEA" and "no early fillets" | 1 | Reversed by real-part pressure in eras 3 and 7. The "no sketch solver in the kernel" non-goal stands. |
