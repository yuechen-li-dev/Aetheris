# Era 7: Previews 1 to 3

**3 – 18 August 2026 · about 165 commits · Phase 3, CONCEPT-*, P1/P2/P3 milestones, ARCHAEOLOGY-M1–M6, PREVIEW3-HARDEN-A1–A5e**

## In one paragraph

After a month-long pause, the project came back and shipped three public
releases in ten days. Preview 1 (8 August) was the first release of the exact,
code-first CAD compiler. Preview 2 (10 August) was a feature freeze that
reconciled an enormous amount of new work: assemblies, FEA, drawings, the Forge
host, engineering modules. Preview 3 (17 August) hardened it into a qualified
Windows bundle with Cadmata, a NativeAOT Forge host and clients in four
languages. In the middle, an "archaeology" pass froze the old Boolean engine and
wrote down what it had taught.

## What was built

**Before Preview 1.** The Phase 3 AIR chamfer MVP and its compiler-architecture
addendum; edge-finish consolidation, junctions and trihedral fillet corners;
rounded boxes, thin-wall hollows and lattice fill; Concept expansion,
materialization and static logic (Enums and `Match`); compile-time Templates and
Patterns; the first `Assert Volume`; profile edge-finish "chimera" closures and a
fillet contact-shell composer; and a CTC-01 reconstruction pressure test written
for LLM authors.

**Preview 1, 7–8 August.** A feature freeze at commit `736a8e69`: Firmament V2
"canonical grammar X1", primitives, profiles, Compose, bounded mechanical
features, PMI, verification and bounded existing-STEP workflows, with Cadmata in
the Windows bundle and a VS Code extension. Known limits were listed in the notes,
not hidden.

**Preview 2, 8–10 August.** A consolidation release, marked Latest on GitHub:

- **Assemblies M0–M3**: interfaces and mates, executable product geometry, the
  V2 `.firmasm` profile, AP242 product structure round trips, template-produced
  assembly definitions and worst-case tolerance stack-up.
- **Continuum M0–M4C and FEA M5**: a dual representation with exact curved
  boundaries in cut cells, and bounded linear-elastic analysis on immersed
  (meshless) cut-cell bases, with Abaqus verification export.
- **Drawings and collaboration**: drawing Templates to SVG, vector PDF and
  editable PowerPoint, DFM review artifacts.
- **Forge**: the SDK split into Forge Host (consumers) and KernelSDK
  (extensions), safe-by-default extensions, and a SQLite/LINQ database-driven CAD
  sample.
- **Modules**: Panels, Piping, Sheet Metal and Surfacing as engineering modules
  with explicit status; SurfaceMeshIR M1–M7 for bounded mesh work; a HexBolt
  Standard Library foundation; Tables and Record derivation; typed Record
  Template parameters; Cadmata application-shell refactor and themes.

**Archaeology, 13 August.** Six milestones (M1–M6) mapped Firmament V1, legacy
JSON `.firmasm` and every `BrepBoolean` caller, then made decisions: Firmament V2
is the sole canonical language; `BrepBoolean` is frozen as a compatibility facade
over recognized families; future topology work splits into *evidence* (geometry
queries), *recipes* (which decide expected topology) and a small **BRep
Surgery** layer that realizes it. Geometry reasoning (contact, intersection,
distance) and the Stanford bunny remeshing experiments ran alongside.

**Preview 3, 14–18 August.** Sheet Metal M5–M11 (product families, enclosures,
assembly datums, bend terminations); semantic Profiles with exact planar
contours; Forge Host Protocol v1 with Python, Go, Rust and TypeScript clients; a
Standard Library material catalog; production FEA boundaries. Then the
PREVIEW3-HARDEN series: release topology, first-user documentation, a "normal
CAD" friction test, repository information architecture, language
reconciliation, Template metaprogramming review and fixture-corpus
qualification. Preview 3 shipped on 17 August, qualified for `win-x64` only,
with a factual claims list and a contributor licence agreement marked as a
candidate pending legal review.

## Decisions that stuck

- **Release claims are evidence-backed.** `PREVIEW3-CLAIMS.md` lists only what the
  test suite and release qualification prove.
- **`docs/public` is the only user documentation**, and generated output belongs
  in ignored `artifacts/local/`, under the generated-artifact policy.
- **Boolean frozen; Surgery for explicit topology.** New topology work states its
  expected graph first.
- **Win-x64 is the only qualified release binary.** Framework portability tests
  do not expand the release promise.

## Claude's commentary

Three releases in ten days sounds reckless. The record shows the opposite. Each
release had a freeze document, a capability manifest and a list of limitations,
and Preview 2's notes devote as much space to what is bounded or experimental as
to what is new. The speed came from the previous two months: once AIR made
features predictable, most of this era was connecting existing pieces and
proving the connections.

The archaeology pass deserves more attention than its size suggests. It is rare
for a project to stop, map its own legacy and write down which parts are frozen
and why. The [Boolean lessons](../../architecture/kernel/brep-boolean-lessons.md)
page that came out of it is the single most useful thing to read before touching
topology code, and it is short.

## What survives

- Release notes for Previews 1–3 in `docs/release/`
- `Aetheris.FEA`, `Aetheris.Continuum`, `Aetheris.Drawing`, `Aetheris.Forge.*`,
  `Aetheris.SheetMetal`, `Aetheris.Modules`, and the assembly IR
- The module architecture and module guides (kept because modules link to them)
- BRep Surgery and the frozen `BrepBoolean` facade

Previous: [Era 6](06-firmament-v2-and-the-ap242-mvp.md) · Next: [Era 8: Domain expansion](08-domain-expansion.md)
