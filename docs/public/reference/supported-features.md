# Preview 3 supported features

`Supported` means a qualified public path. `Bounded` means the documented subset is production-tested. `Experimental` is usable evidence without a general product promise. `Not in Preview 3` is intentionally deferred.

| Area | Status | Preview 3 boundary |
|---|---|---|
| Firmament V2 native primitives/profiles | Bounded | Named Box/Cylinder/Frustum/RoundedBox and admitted line/arc profile construction/composition |
| Firmament V2 analytic `model` / `solid` primitives | Bounded | Parser-backed single-solid Sphere, Cone (including zero-radius pointed end), and Torus routes round-trip through AP242; this is not a general boolean/composition promise |
| Profile-composed pads and pockets | Bounded | Admitted prismatic `Compose` `Add`/`Remove` profile operations; no universal named Boss/Pocket feature or arbitrary solid boolean |
| Semantic holes, slots, patterns | Bounded | Qualified shaft/counterbore/countersink and finite static pattern routes; no stable generated-instance selector |
| Edge finishes/hollow/lattice | Bounded | Documented admitted chamfer/fillet, hollow, and cubic-truss routes; not arbitrary topology |
| Templates/Records/Static/Tables | Supported | Typed compile-time specialization, finite data, `with`, and `Require` |
| STEP AP242 export | Bounded | Deterministic single-body routes plus semantic PMI; unsupported intent fails loudly |
| STEP import / inlineSTEP | Bounded | Canonical single-body topology and recognized identities; arbitrary containment and multi-root bodies are not native-part promises |
| PMI authoring | Bounded | Plane Datum and toleranced shaft HoleDiameter AP242 export; broader PMI families are deferred |
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
