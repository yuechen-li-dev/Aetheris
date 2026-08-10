# AETHERIS-MODULE-M0 ownership audit

| Area | Existing capability | M0/future owner | Pressure observed |
|---|---|---|---|
| Kernel.Core | exact curves/surfaces, topology, BRep, Boolean, STEP, tessellation, JudgmentEngine | Core | Correct shared geometric substrate; must not learn `PipeRoute` or Sheet Metal policy. |
| Firmament | central V2 parser/binder, feature declarations, AIR/materializer route selection | Core compiler + Module frontend adapters | A native domain keyword currently requires parser and central dispatch edits; M0 therefore uses explicit capability IDs/templates and adds no decorative import syntax. |
| Kernel.StandardLibrary | exact coaxial construction IR/materializers, bolts/reusable parts | StandardLibrary | Domain Templates previously had no typed owner; Module metadata now identifies their future home. |
| Forge | package descriptors, Concepts/Templates, extension capabilities | Forge infrastructure | Forge capability IDs and engineering Module capabilities were indistinguishable to tooling. Module catalog now provides separate typed ownership. |
| Forge Host | Template expansion, capability invocation, exact output admission | Forge Host | Host inspection previously exposed extension executors only. `EngineeringModules` now inspects built-ins without KernelSDK. |
| Semantics | `SemanticValue`, structural capabilities, exact bindings/provenance | Core semantics | Ad hoc string checks would conflate value evidence with compiler availability. `ModuleCapability` is a separate type. |
| Assembly | interfaces, mates, product structure, AP242 | Assembly | Domain endpoints can expose ordinary semantic axes/points without Assembly learning pipe semantics. |
| Drawing | DrawingIR, views, PMI, SVG/PDF | Drawing | Consumes ordinary semantic/BRep output; no Module-specific drawing branch is needed in M0. |
| Analysis/Continuum/FEA | exact regions, sampling, mechanics lowering | Analysis | Future domain analysis consumes exact bindings; it should not become capability discovery. |
| CLI | large manual top-level dispatch | Tooling | New domains would require bespoke commands. One generic `modules` catalog command now handles inspection and a bounded showcase path. |
| Surfacing-like | STEP ruled B-spline classification; prismatic transitions; extrusion/revolution surfaces | Surfacing intent over Core primitives | Mathematical ruled intent was recoverable but not first-class at authoring/lowering. |
| Piping-like | cylinders, torus, sweep architecture notes | Piping intent over Core primitives | Generic Sweep would lose diameter/bend/endpoints/frame policy. |
| SheetMetal-like | no coherent domain owner | SheetMetal (reserved) | Developability/flat-pattern work would otherwise leak into generic surface/kernel code. |

## Bad ownership examples

- Adding a new native Firmament feature currently touches central parser/binder/materializer selection.
- Forge extension admission branches on output origin and maintains a distinct capability registry.
- CLI top-level dispatch is a manual switch.
- Semantic capability checks answer what a value proves, but cannot answer whether a domain compiler capability is installed.
- STEP can classify exact ruled degree-(1,1) surfaces, but recovered representation does not establish the original engineering construction owner.

The M0 seam addresses discovery and ownership without moving stable code: explicit Module definitions, dependency/capability catalog, typed missing-capability diagnostics, Host/CLI inspection, and domain-owned lowering projects.
