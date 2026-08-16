# MAT-DB-X1 — Standard Library material catalog

## Architecture

```text
checked-in SQLite catalog
  -> EF Core entities and schema
  -> MaterialCatalog (composable C#/LINQ query surface)
  -> IMaterialResolver / MaterialResolver
  -> ResolvedMaterial domain record
  -> Firmament analysis lowering and engineering consumers
```

`Aetheris.Kernel.StandardLibrary.Materials` owns persistence, catalog queries, validation, stable identity, provenance, and domain mapping. EF entities stop at the catalog boundary. Firmament only carries a semantic material path. The FEA IR receives a complete `ResolvedMaterial` and scalar SI fields; neither the solver nor the Abaqus exporter knows about SQLite, EF Core, or query policy.

The database is [`aetheris-materials-x1.sqlite`](../../Aetheris.Kernel.StandardLibrary/Materials/Database/aetheris-materials-x1.sqlite). It is copied beside packaged output under `Materials/`. `MaterialCatalogDatabase.Recreate` deterministically creates the schema and seed rows from the checked-in EF model and seed definitions. `CatalogMetadata` records schema version 1 and seed version `MAT-DB-X1`.

## Schema

- `CatalogMetadata`: catalog ID, schema version, and seed version.
- `Materials`: stable semantic identity, Firmament path, family, designation, grade, temper/condition, standard, display name, constitutive class, and reference condition.
- `MaterialProperties`: one typed property kind per material, canonical SI value/unit, source ID/URI, authority class, condition, reference temperature, and qualification notes.

Unique indexes enforce `(CatalogId, StableId)`, Firmament path, property-per-material, and catalog metadata identity. The schema deliberately keeps identity and property context explicit without normalizing the initial catalog into many taxonomy tables.

## Seed catalog

| Stable ID | Material | Condition | Constitutive class | Structural data | Thermal data |
| --- | --- | --- | --- | --- | --- |
| `standard:aluminum/5052-h32` | Aluminum 5052-H32 | nominal room temperature, H32 | `LinearElasticIsotropic` | density, E, ν, yield, UTS | conductivity |
| `standard:aluminum/6061-t6` | Aluminum 6061-T6 | nominal room temperature, T6 | `LinearElasticIsotropic` | density, E, ν, independently tabulated G, yield, UTS | conductivity |
| `standard:steel/astm-a36` | ASTM A36 structural steel | nominal room temperature; bounded common plate/shape strength | `LinearElasticIsotropic` | density, E, ν, minimum yield, lower-bound UTS | deferred |
| `standard:stainless-steel/304-annealed` | 304 / S30400 stainless | annealed flat product, nominal room temperature | `LinearElasticIsotropic` | density, E, ν, minimum yield, minimum UTS | conductivity, specific heat, CTE |

All values are stored in canonical SI: kg/m³, Pa, dimensionless ratio, W/(m·K), J/(kg·K), and 1/K. Units remain explicit on every resolved property.

## Provenance strategy

Every property row records its own source ID, URI, authority (`ManufacturerTypical`, `StandardMinimum`, `IndustryReferenceNominal`, or future `SupplierCertified`), condition, optional reference temperature, and notes. This permits a future supplier-certified value or temperature-conditioned value to coexist conceptually with the bounded X1 nominal values without changing the meaning of the engineering record.

Seed sources are deterministic in [`MaterialSeedData.cs`](../../Aetheris.Kernel.StandardLibrary/Materials/Database/MaterialSeedData.cs):

- Hydro and thyssenkrupp 6061 alloy datasheets for mechanical/physical values and standard minima.
- Protolabs' AA-attributed 5052-H32 technical sheet, with MatWeb only for the explicitly labeled nominal Poisson ratio.
- ASTM A36/A36M and AISC 360 references for bounded strength minima and ordinary structural-steel elastic constants, with MatWeb only for nominal density.
- Outokumpu Core 304/4301 datasheet for ASTM A240 strength minima and room-temperature physical/thermal properties, with MatWeb only for the explicitly labeled nominal Poisson ratio.

Values are not supplier certificates. Reports should preserve the attached provenance and condition. X1 intentionally omits a property when the chosen sources do not support a defensible bounded value.

## Firmament usage

Firmament names one catalog material; it does not query the database:

```firmament
analysis LinearElastic CouponPull {
    body: coupon
    material: Standard.Materials.Aluminum.5052_H32
    // constraints and loads...
}
```

The complete executable witness is [`catalog-material-coupon.firmament`](../../fixtures/FirmamentV2/Materials/catalog-material-coupon.firmament). Unknown paths produce `firmament-material-unknown`; there is no fallback to a generic family.

## C# and LINQ usage

Database/query programming remains ordinary C#:

```csharp
using var catalog = MaterialCatalog.OpenDefault();

var sheetAluminum = catalog.Materials
    .Where(material => material.Family == "Aluminum")
    .OrderBy(material => material.Designation)
    .ThenBy(material => material.Temper)
    .ToArray();

var material = new MaterialResolver()
    .Resolve("Standard.Materials.Aluminum.5052_H32")
    .Material!;
```

The first query remains an EF-translated `IQueryable`; Firmament gains no SQL, predicates, comprehensions, ORM semantics, or selection scripting. `IMaterialResolver` is injectable at lowering time, allowing future Forge, company, or supplier federation behind the same semantic reference surface. Stable identity already consists of catalog ID plus stable material key.

## Validation and diagnostics

Seed generation rejects non-finite values, nonpositive supplied physical quantities, invalid isotropic Poisson ratios, UTS below yield, duplicate stable IDs, duplicate Firmament paths, and duplicate property kinds. Resolution rejects invalid entries while allowing a future thermal-only material to resolve with no structural block. The FEA lowerer explicitly rejects such an entry as missing structural data and distinguishes unknown, ambiguous, invalid, and missing-structural-data outcomes with deterministic diagnostic codes.

## FEA readiness

For every X1 entry, future FEA code can consume these values from `ResolvedMaterial` without database knowledge:

- structured identity and stable catalog ID;
- density;
- explicit constitutive class;
- Young's modulus;
- Poisson ratio;
- yield strength;
- ultimate tensile strength;
- per-property provenance and reference condition.

The existing linear-elastic FEA IR also carries the complete resolved record and direct SI scalars. The catalog coupon witness compiles and runs through the real solver.

## Limitations

- X1 supports only `LinearElasticIsotropic`; orthotropic, temperature-dependent, and elastic-plastic classifications are reserved but not implemented.
- Values are single bounded reference-condition observations, not temperature curves or a multidimensional materials-science model.
- A36 thermal values are intentionally absent. Some aluminum thermal fields remain absent where the bounded seed sources were incomplete.
- Manufacturing recommendations such as bend radii and stock thicknesses are deferred because they depend on thickness, tooling, process, grain direction, and supplier condition. X1 does not encode shop folklore as material physics.
- Catalog federation and override precedence are not implemented. `IMaterialResolver`, catalog-qualified stable IDs, and semantic paths provide the extension seam.
- The catalog has four deliberately useful entries; it is not a universal ontology, supplier inventory, or certification database.
