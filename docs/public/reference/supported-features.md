# Preview 3 supported features

`Supported` means a qualified public path. `Bounded` means the documented subset is production-tested. `Experimental` is usable evidence without a general product promise. `Not in Preview 3` is intentionally deferred.

| Area | Status | Preview 3 boundary |
|---|---|---|
| Firmament V2 native primitives/profiles | Bounded | Named Box/Cylinder/Frustum/RoundedBox and admitted line/arc profile construction/composition |
| Firmament V2 analytic primitives | Bounded | Direct named Sphere, Cone (including zero-radius pointed end), and Torus routes round-trip through AP242; legacy `solid` declarations remain compatibility inputs, not canonical authoring |
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
| New geometry/PMI/FEA/Forge families | Not in Preview 3 | Feature-frozen until after Preview 3 |
| General loft, helix, and freeform surface features | Not in Preview 3 | No public native V2 authoring and qualification route |
| Through profile removal | Bounded semantic operations | Use qualified `Hole`, `Slot`, or another documented opening feature; Pocket never means through-all |
| Arbitrary solid Boolean subtraction / hemispherical cavity | Not in Preview 3 | No public `Union`, `Subtract`, `Intersect`, CSG tree, Sphere-from-Block subtraction, or hemisphere special case |
