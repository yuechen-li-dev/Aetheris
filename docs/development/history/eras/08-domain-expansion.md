# Era 8: Domain expansion

**19 August – 30 September 2026 · about 100 commits · the X-series**

## In one paragraph

With three releases out, the project went wide. Each new capability was run as a
named "X" milestone, a bounded experiment with its own report and an honest
verdict, and many of them were domains a mechanical CAD kernel does not usually
touch: formed wire and mathematical knots, piping with local autorouting, molded
plastic shells, weldments, gears, threads, text, a playable V8 engine, Babbage's
Difference Engine, virtual sculpture and, most ambitiously, humanoid bodies. A
browser editor called Helios appeared. The language was stress-tested by agents
writing real parts, and the results fed straight back into the compiler.

## What was built

**Manufacturing domains.**

- **X0 "Maximum Paperclips"**: a parametric, manufacturable paperclip from typed
  Firmament through semantic Sweep AIR to analytic STEP, invoked through Forge
  Host and regenerated in Cadmata. The default build makes exactly one paperclip.
- **MOLD-X0/X0a**: molded plastic shells with wall policy, tooling direction,
  parting plane, gates, standoffs, ribs and exact molded-feature B-rep.
- **WIRE-X0 to X2**: formed wire and rod (`StartFrame`, ordered straights and
  bends, derived cut length), coils and surface coils, and trefoil, figure-eight
  and torus knots.
- **X2 Structural and X3 Piping**: nodes, straight members and weldments; logical
  piping connections, equipment nozzles, keep-out clearance and local
  autorouting, with BOM and cut list.
- **X1 Standard Library**, gears (GEAR-LIB-X0), external threads, circular
  perforation, planar text and a maker's mark, cross holes, helical ribs, hollow
  radial cuts, and a sheet-metal shell FEA experiment (FEA-SHELL).

**Surfacing.** SURF-X0 to X4 introduced immutable geometric states with locality
contracts, bounded patch replacement, trim and intersection closure, blend
fairness judgment, section chains, lofts and a generalized `BodyState`. SURF-G2
qualified polynomial contacts and a trimmed G2 plate.

**Mechanisms and showcases.**

- **ENGINE-X0, the editable V8**: 171 occurrences and 170 mates solved from named
  datum interfaces, slider-crank closure and prescribed valve lift, with a browser
  view that consumes exported geometry rather than recreating it.
- **DIFF-ENGINE-X0, the Difference Engine**: three registers and four digits of
  real hierarchical CAD, 409 occurrences, animated in the browser. It was designed
  by GPT and corrected after human visual review.
- **ART-X0, Sol 1**: a phyllotaxis sculpture in exact tori, spheres and cylinders.
- **BENCHCAD**, guitar and Industrial ATLAS reconstructions, OpenUSD export and
  STEP assembly import.

**Language burn-in.** LANG-BURN-X1 (12 September) had a frontier agent author 34
parts using only the public docs. The verdict: useful parts in the qualified
lanes, but not yet one coherent language. The same day brought pure `Feature`
functions, `|>` profile pipelines, explicit `schema` headers, named `Set`s with
Pattern mapping, geometric `Span`s, stock-frame authority and unified admission.
HARDEN-X1 earlier made every semantic concern resolve through its strongest
representation, for example Boss support from current material and flat-pattern
area from the validated blank contour.

**Interchange.** STEP orientation authority (derived face orientation and
`same_sense`, periodic and degenerate boundaries), NURBS recovery and recovered
pcurves, Rhino 3DM inspection, intent ports and B-rep binding.

**Helios and the Web SDK (16 September).** Helios X1 showed a semantic editor
built only on the public `@aetheris/cad` package: compile, select both ways
between tree and viewport, edit properties, keep the last valid revision visible
when an edit is invalid.

**Humanoids (16–17 September).** HUMANOID-X0 to X5, RECON, RERIG and REST: a
canonical adult body domain with stable topology identity, a skeleton, semantic
regions, landmarks, skin weights, a reference rig, semantic poses and
judgment-driven deformation. Every report ends in "meaningful progression, not
accepted". No body was promoted, and the module is experimental to this day.

## Decisions that stuck

- **One named, bounded milestone per capability, with a verdict.** The X-series
  convention.
- **Showcases are real CAD.** Demos must go through the production compiler,
  assembly solver and exporter. No hand-built meshes.
- **Agents are test users.** Burn-ins with public docs only became a recurring
  method.
- **Humanoid and garment work stays experimental** until it passes its own gates.

## Claude's commentary

This era reads like a stress test of the architecture by breadth. It mostly
passed. Wire, piping, plastic shells and gears all landed as new front ends over
the same AIR and B-rep machinery rather than as parallel kernels, which is the
strongest evidence that the era 5 pivot was right.

The burn-in report is the honest counterweight. An agent could build real parts,
but it had to learn command routing, wrapper forms and per-domain exceptions,
and in one case a compatibility path quietly exported an unchanged imported body.
Breadth had outrun consistency. Era 9 is largely the bill for that, paid
deliberately.

The humanoid reports are worth reading as a model of how to run a moonshot
inside an engineering project: each one states the question, says "partly",
names the next narrow blocker with a number attached, and refuses to promote
anything that did not pass.

## What survives

- `Aetheris.PlasticShell`, `Aetheris.Piping`, `Aetheris.Surfacing`,
  `Aetheris.Sculpture`, `Aetheris.ThreeDm`, `Aetheris.Humanoid`, and the WireForm,
  Sweep, SectionChain, Structural and Gear front ends
- The V8, Difference Engine, guitar and ATLAS fixtures and demos
- Public guides for each domain; the humanoid pages under `docs/public/experimental`
- `HUMANOID-X0.md`, `PAT-DOGFOOD-X0.md` and `DCSD-DOGFOOD-X0.md` in `docs/release`,
  kept because code and scripts reference them

Previous: [Era 7](07-previews-1-to-3.md) · Next: [Era 9: Preview 4 and consolidation](09-preview-4-and-consolidation.md)
