# Era 4: CIR and the recovery wall

**4 – 22 May 2026 · about 170 commits · CIR-M/A/E/F/T, CIR-BREP, CIR-RECOVERY V1–V19, STEP-VOID**

## In one paragraph

If exact B-rep Booleans are hard, evaluate the shape another way. CIR, the
Constructive Intermediate Representation, held a part as a tree of primitives
and CSG operations that could be evaluated as an implicit field: point
containment, volume, sections and maps, all without building topology. Booleans
became trivial tree composition. The hard part moved to the other end: getting
exact B-rep *back out* of the field for STEP export. Three weeks and roughly a
hundred numbered steps later, the team could recover exact B-rep for a growing
catalogue of hole families, and had shown clearly why that would never become
general.

## What was built

- **CIR itself (M0–M4, A0–A1, E0).** A typed CSG tree with lazy evaluation, an
  experimental Firmament-to-CIR lowerer, differential CIR-versus-B-rep analysis
  to prove the two agreed, and a native CIR analysis service behind the analyzer.
- **Fall-forward (F0–F16).** The runtime contract: build exact B-rep where
  supported, and when a *valid* request hit an unsupported materialization, continue
  in CIR rather than fail. Then materializers to come back: planar faces, circular
  caps, inner loops, cylindrical faces, shell stitching dry runs, seam orientation
  proofs, emitted-topology identity maps. Each was gated and bounded.
- **The trim oracle (T0–T10).** Surface-restricted field sampling, marching-squares
  contours, contour stitching, analytic snap candidates (is this sampled loop
  really a circle?), a ranked acceptance gate and tiered trim representations.
- **CIR-BREP (S0, T0–T7, X0–X8).** Stitching candidates into shared edges,
  remapping combined bodies, duplicate-edge cleanup, and a pressure test merging a
  box and a cylinder shell. X8 tried a generic CIR-tree-to-B-rep executor.
- **Semantic recovery (V1–V19).** Instead of recovering arbitrary surfaces,
  recognize *intent*: a through hole, a counterbore, a countersink, a blind hole,
  a stepped hole, a chamfered-entry hole. A JudgmentEngine-mediated policy catalog
  chose a recovery variant and executor for each, ending in a capability manifest
  (V19) for the hole-family lane.
- **Side work that lasted.** BREP_WITH_VOIDS import and export with a
  JudgmentEngine solid-root planner (STEP-VOID A0–A2); curve, groove and
  surface-feature architecture audits; a CIR cone primitive.

## What went wrong

The generic executor (CIR-BREP-X8) was the decisive experiment. It mapped a
Boolean expression tree recursively onto the existing B-rep Booleans, and it
succeeded only for the families those Booleans already supported. A generic
syntax tree is a generic *traversal*; it is not a generic topology algorithm.

Meanwhile the recovery lane was reaching its own wall. Every hole variant needed
its own recognizer, plan and executor, and placement semantics had to be
harmonized across them twice (V17, V18). The V19 manifest says it plainly:
"FRep/CIR → BRep is semantic intent recovery, decompilation, not ordinary
lowering." The pipeline was compiling to a lossy form and then decompiling it.

## Decisions that stuck

- **CIR is an analysis side-channel, not a topology authority.** It is still
  used for containment, volume intervals, maps and, from Preview 4, GPU field
  rendering. It never became the source of exported topology.
- **Recognize intent, don't reconstruct surfaces.** The hole-family recovery work
  is the ancestor of semantic holes in Firmament V2 and of imported-feature
  recognition in era 9.
- **Volume claims need authority.** Display-mesh volume is not evidence; an exact
  recognizer or a qualified field interval is.

## Claude's commentary

It is tempting to call this era a detour. It was not. It was the experiment that
proved the hypothesis wrong, and the record of it is unusually clean: each step
says what it tried, what it measured and where it stopped. The key sentence, that
recovering B-rep from a field is decompilation, is the insight that made the next
era possible. If the compiler already knew it was building a counterbored hole,
why throw that away, evaluate a field, and then work out from the field that it
had been a counterbored hole?

The other lesson is about naming. "Fall-forward" sounds like graceful
degradation. In practice it meant carrying valid intent in a form the exporter
could not use. The project later adopted a stricter rule, that a supported
request is built and an unsupported one is rejected, partly because fall-forward
showed how a well-meant fallback can blur what was actually produced.

## What survives

- CIR evaluation in `Aetheris.Kernel.Core` and `Aetheris.Continuum`, used for
  analysis, volume assertions and display
- `BREP_WITH_VOIDS` support and the solid-root export planner
- The hole-family recognition ideas, reworked in eras 6 and 9
- The `cir-brep-x3` FrictionLab document, kept because a test reads it

Previous: [Era 3](03-firmament-v1-and-safe-booleans.md) · Next: [Era 5: AIR and sweep-first](05-air-and-sweep-first.md)
