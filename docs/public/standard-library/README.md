# Aetheris Standard Library

The Standard Library is a shipped, curated catalog of typed Firmament product families. Applications discover, describe, and invoke it through Forge Protocol v1; the same invocation produces the STEP loaded by Cadmata and offered for download. Product identities and policy field names are public API and should remain stable within a release line.

| Product family | Public Template ID | Main capabilities |
| --- | --- | --- |
| Paperclip | `Standard.Products.Office.Paperclip` | Path, circular Sweep, material, deterministic AP242 |
| Mounting Plate | `Standard.Products.Mechanical.MountingPlate` | prismatic stock, four counterbores, PMI |
| Bearing Block | `Standard.Products.Mechanical.BearingBlock` | base, Boss, bore, mounting holes, PMI |
| Machined Angle Bracket | `Standard.Products.Mechanical.MachinedAngleBracket` | prismatic L profile, mounting holes |
| Shaft Collar | `Standard.Products.Mechanical.ShaftCollar` | circular profile, bore |
| Flanged Adapter | `Standard.Products.Mechanical.FlangedAdapter` | circular flange, 4/6/8-hole semantic Pattern, bore PMI |
| Rack Panel | `Standard.Products.Electronics.RackPanel` | planar stock, symmetric mounting holes |
| Standoff | `Standard.Products.Mechanical.Standoff` | circular spacer, clearance bore |
| Welded Workbench | `Standard.Structural.WeldedWorkbench` | structure graph, generated Member/Joint/Weld tables, AP242 assembly, Cut List JSON |
| Generic Pipe / Elbow90 / Tee | `Standard.Piping` | dimensional, non-standards-claim piping product policies |
| Pump Cooling Skid | `Standard.Piping.PumpSkid` | equipment-owned nozzle ports, scoped KeepOut exemptions, endpoint mates, deterministic accepted route, AP242 assembly |
| Electronics Enclosure | `Standard.SheetMetal.ElectronicsEnclosure` | Sheet Metal, DFM, formed STEP, flat STEP, SVG |

The namespace taxonomy is intentionally shallow: `Standard.Products.Mechanical`, `.Electronics`, and `.Office` identify ordinary products; `Standard.Structural` owns structure-first weldments; the existing `Standard.SheetMetal` namespace remains stable for manufacturing Templates.

## Invoke a family

List and describe schemas before binding values:

```powershell
dotnet run --project Aetheris.Forge.Host -- list
dotnet run --project Aetheris.Forge.Host -- describe Standard.Products.Mechanical.MountingPlate
dotnet run --project Aetheris.Forge.Host -- invoke Standard.Products.Mechanical.MountingPlate --request fixtures/Canonical/Integration/standard-products/mounting-plate.forge.json --out artifacts/local/mounting-plate
```

The request changes policy data, not geometry logic. Lengths are metric strings such as `"120 mm"`. Forge `describe` is authoritative for fields, units, constraints, and artifacts. See the [Forge interop guide](../forge/interop.md).

Forge resolves each Record parameter's default `Static` through the Firmament binder. Each described field therefore carries its canonical default, engineering type, unit, and required status when the value is statically available. `Static` values derived with `with` are reported after overrides have been applied; unresolved compile-time forms have no invented default. This applies to both `Standard.Products.*` and the five existing `Standard.SheetMetal.*` gallery families, so a generic client never needs a parallel engineering-default table.

## Use a family from Firmament

Standalone source imports a bounded shipped namespace with `Use` and keeps family and default identities qualified:

```firmament
Model MyMountingPlate {
    Units: mm
    Use Standard.Products.Mechanical

    Static MyPolicy = Standard.Products.Mechanical.StandardMountingPlate with {
        Width: 120mm
        Height: 80mm
        Material: "Standard.Materials.Aluminum.6061_T6"
    }

    Struct Plate = Standard.Products.Mechanical.MountingPlate<P: MyPolicy>
}
```

X1a resolves only the embedded `Standard.Products.Mechanical`, `.Electronics`, and `.Office` modules. Resolution selects named exports from the semantic catalog; it does not read a path or paste an arbitrary file. Fully qualified symbols avoid shadowing. A local declaration colliding with a selected internal export is rejected, as are unknown modules, declarations, or references whose module was not explicitly used. User-defined modules and dependency cycles remain outside this bounded first release.

## Authoring model

The authoritative shipped module is `Aetheris.Kernel.Firmament/Standard/StandardProducts.firmament`. Every family follows `Record → Static default → Template<Policy> → Require → semantic geometry`. The same embedded source drives Firmament import, Forge discovery/defaults, tests, and Product Gallery controls.

Pattern intent remains in compiler semantic reporting through specialization: identity, source array, generator Template, specialized count, and generated-instance relationships are retained. `aetheris inspect <source> --json` exposes that inventory under `patterns`; `aetheris build <source> --json` includes the same semantic report beside its materialized features. The BRep plan is the deliberate boundary where finite instances become explicit topology. PMI inside one-level and nested Templates is lifted using specialization provenance, bound to the specialized semantic feature, exported to AP242, and checked by reinspection parity.

## Compatibility

Template IDs and policy field identities are public API. Additive fields require defaults before they can be non-breaking; removals, renames, type changes, and semantic reinterpretations require an announced compatibility break. Geometry may be corrected without renaming a family when the policy meaning is preserved.
