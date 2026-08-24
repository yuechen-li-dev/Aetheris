# Preview 3 supported features

`Supported` means a qualified public path. `Bounded` means the documented subset is production-tested. `Experimental` is usable evidence without a general product promise. `Not in Preview 3` is intentionally deferred.

| Area | Status | Preview 3 boundary |
|---|---|---|
| Firmament V2 native primitives/profiles | Bounded | Named Box/Cylinder/Frustum/RoundedBox and admitted line/arc profile construction/composition |
| Bounded mathematical sculpting | Bounded (SURF-X3b subset) | Immutable single-predecessor `BodyState` reconstructed from versioned `BaseConstruction` plus typed replay operations; persistent `OffsetRegion`, `ReplaceRegion`, judged `BlendBoundary`, `HoleFeature`, east-attached `AddSectionChain`, and west/east through-duct `RemoveSectionChain`; atomic replay, semantic support invalidation, locality/preservation evidence, pcurves, AP242 associations, and rational-free STEP. General blend networks and arbitrary SectionChain supports remain unsupported. See the [sculpting guide](../firmament/sculpting.md). |
| Molded plastic shell | Bounded (MOLD-X0a) | First-class `PlasticShell` IR; exact drafted coaxial-frustum shell; analytic annular standoffs with retained core holes; constant-shell-thickness planar B-rep rib walls with flat tops; one closed product boundary; face-associated AP242 notes; and explicit zero-release-draft warning for constant-section vertical features. The retired polar height field is available only through the non-manufacturing experimental art command. Freeform exterior offsets and general junctions remain unsupported. See the [PlasticShell guide](../firmament/plastic-shell.md). |
| Virtual Sculpture | Bounded (ART-X0) | The explicit `Mode: Virtual` Sol 1 flagship: deterministic golden-angle/Fibonacci 3D lattice, analytic toroidal frame and eye, exact closed-body AP242 assembly, evidence/preview output, and zero rational product surfaces. This is not manufacturing geometry and does not extend Firmament engineering domains. See [Virtual Sculpture](../virtual-sculpture.md). |
| Firmament V2 analytic primitives | Bounded | Direct named Sphere, Cone (including zero-radius pointed end), and Torus routes round-trip through AP242; legacy `solid` declarations remain compatibility inputs, not canonical authoring |
| Circular Sweep | Bounded (X0) | Open planar XY Concept Path, tangent line/arc segments, constant circular diameter, analytic cylinder/torus faces, capped solid; no general 3D/variable/rail/twist sweep |
| SectionChain ruled loft substrate | Bounded (SURF-X3b subset) | Firmament authoring lowers into ordered framed, same-topology semantic SectionChain IR; one-to-one ruled transitions; shared edges; Cap/Open; strict pcurves; deterministic intersection checks; persistent typed BodyState Add/Remove through documented planar housing lanes; no generic CSG authoring, rational product surface, or faceted fallback. See [Section chains](../firmament/section-chains.md). |
| Structural / weldment | Bounded (X2) | Explicit 3D nodes and straight paths; identifiable members; square/rectangular/round tube, angle, flat/round bar; orientation; catalog material; two-member butt and polygonal miter joints; semantic fillet welds; AP242 member assembly and deterministic JSON Cut List. No coping, curved routing, multi-member miter, connection design, weld analysis, or structural FEA. See [guide](../firmament/structural.md). |
| Semantic piping / routing | Bounded (X3a) | Logical connections; equipment-owned port Interfaces and hollow nozzle stubs; target-port-scoped owner KeepOut exemptions; coincident endpoint mates; explicit orthogonal 3D routes; deterministic A* proposals; editable accepted routes; 90-degree elbows; bounded local rerouting; AP242 assembly, BOM, and pipe Cut List. No freeform, stress-, flow-, slope-, or plant-scale routing. See [guide](../firmament/piping.md). |
| Boss | Bounded | First-class connected finite `On: Top` profile addition on an admitted Compose host; positive height; lowers through existing `Add`; no arbitrary solid union |
| Pocket | Bounded | First-class enclosed finite-depth `On: Top` profile removal; positive depth, non-through termination, and minimum remaining floor enforced; lowers through existing `Remove` |
| Lower-level profile composition | Bounded | Existing prismatic `Compose` `Add`/`Remove` remains compatible for bounded blockout authoring |
| Semantic holes, slots, patterns | Bounded | Qualified shaft/counterbore/countersink and finite static pattern routes; no stable generated-instance selector |
| Edge finishes/hollow/lattice | Bounded | Documented admitted chamfer/fillet, hollow, and cubic-truss routes; not arbitrary topology |
| Templates/Records/Static/Tables | Supported | Typed compile-time specialization, finite data, `with`, and `Require` |
| STEP AP242 export | Bounded | Deterministic single-body routes plus semantic PMI; unsupported intent fails loudly |
| STEP import / inlineSTEP | Bounded | Canonical single-body topology and recognized identities; arbitrary containment and multi-root bodies are not native-part promises |
| PMI authoring and presentation | Bounded | Model authoring qualifies plane Datum and toleranced shaft HoleDiameter; manufacturing AP242 workflows additionally qualify documented dimensions, position controls, annotations, and geometry associations; this is not general PMI authoring |
| Sheet Metal authoring | Bounded | Base/flanges/bends and planar circular holes/cuts, formed STEP, flat STEP/SVG, K-factor, DFM, semantic regions; Model `Hole<Counterbore>` / `Hole<Countersink>` syntax is rejected |
| Sheet Metal reconstruction | Experimental | Bounded recognition/recovery with explicit partial status and evidence |
| Materials | Supported | Four deployed Standard Library catalog entries |
| FEA | Bounded | LinearElasticIsotropic cut-cell/vector-lattice, Fixed, total-resultant Force, four result families; no nonlinear/contact/dynamics |
| Forge Protocol v1 | Supported | List/describe/invoke embedded public templates; process JSON and file artifacts |
| Cadmata | Bounded | Geometry inspection, selection, semantic PMI presentation and filtering |
| Assemblies | Bounded | Typed Firmament assembly inspection plus explicitly identified legacy `.firmasm` compatibility |
| Platform qualification | Supported | Windows x64 bundle; NativeAOT Forge Host on `win-x64` |
| Linux/macOS release binaries | Not in Preview 3 | Framework logic tests do not constitute binary qualification |
| Post-Preview-3 feature families | X0 exception | Circular Sweep and Standard Products Paperclip are the bounded X0 additions; other new geometry/PMI/FEA/Forge families remain frozen |
| General smooth/rail/topology-changing loft, helix, and freeform surface features | Not in Preview 3 | The experimental ruled SectionChain substrate does not qualify arbitrary smooth loft, guide rails, topology-changing correspondence, arbitrary dumb-solid editing, or surface networks. |
| Through profile removal | Bounded semantic operations | Use qualified `Hole`, `Slot`, or another documented opening feature; Pocket never means through-all |
| Arbitrary solid Boolean subtraction / hemispherical cavity | Not in Preview 3 | No public `Union`, `Subtract`, `Intersect`, CSG tree, Sphere-from-Block subtraction, or hemisphere special case |

## Template capability matrix

| Capability | Status | Boundary |
|---|---|---|
| Typed value parameters and defaults | Supported | `Length`, `Angle`, `Int`, `Float`, `Bool`, `String`, enums, `Version`, `Date`, bounded `ImportedStep`/`ProfilePath` seams; lowercase scalar aliases remain accepted |
| Records and `with` | Supported | immutable checked values and finite derivation; no mutation or inheritance |
| DFM policy families | Supported | typed CNC/FDM/SheetMetal policy Records and Concept Structs; canonical CNC/Additive policies feed existing enforcement, lowercase process templates remain compatibility-only |
| Language Concept constraint | Supported | explicit `type T satisfies Concept`; distinct from an ordinary value parameter |
| `Require` | Supported | finite specialization-time boolean admissibility; no runtime scripting |
| Static arrays, Tables, Pattern | Bounded | finite checked data and admitted feature expansion; no query language |
| Materials | Bounded by consuming domain | semantic identity can flow through existing Firmament/Forge paths; no SQLite or catalog queries in Templates |
| Sheet Metal product families | Supported | five embedded public families and Forge Protocol v1 exposure |
| Nested Templates | Bounded | acyclic concrete specialization only; Templates are not values |
| Recursive or higher-order metaprogramming | Not in Preview 3 | cycles are rejected; Template parameters are unsupported |
| Direct array/Table parameters | Not in Preview 3 | select/derive a finite Record or use bounded feature Pattern data |
| FEA specialization | Bounded by concrete Model output | an admitted specialized Model can contain existing Analysis; no generic Analysis output family |
| PMI specialization | Bounded by existing concrete semantics | specialized dimensions can feed existing PMI; no new PMI kinds |
| Forge introspection | Supported | stable ID plus generic display signature, categories, units, defaults, enum cases, Record fields, constraints, output kind, artifacts |
