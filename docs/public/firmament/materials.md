# Materials

Firmament refers to catalog materials semantically, for example:

```firmament
Material: Standard.Materials.Aluminum.5052_H32
```

Preview 3 ships four catalog entries:

| Reference | Material |
|---|---|
| `Standard.Materials.Aluminum.5052_H32` | Aluminum 5052-H32 |
| `Standard.Materials.Aluminum.6061_T6` | Aluminum 6061-T6 |
| `Standard.Materials.Steel.ASTM_A36` | ASTM A36 structural steel |
| `Standard.Materials.StainlessSteel.304_Annealed` | 304 stainless, annealed |

The Standard Library uses the same versioned, managed catalog on desktop and in the browser. No database engine or catalog file is required for the four built-in materials. Identity, SI values and property provenance remain attached to each resolved material. Firmament does not expose SQL. FEA consumes catalog density and the properties required by its constitutive model; an unknown material is a named error, not a fallback.

Build/solve [`catalog-material-coupon.firmament`](../../../fixtures/Canonical/Materials/material-catalog-coupon.firmament) with `aetheris fea ... --out-dir artifacts/local/material --json`.

## Optional SQLite interoperability

Applications that maintain an external SQLite catalog can reference `Aetheris.Materials.Sqlite` and use `SqliteMaterialCatalog.Open(path)` in place of the former `new MaterialCatalog(path)`. It returns a validated in-memory `MaterialCatalog`; the database is closed before geometry compilation. `MaterialResolver(() => SqliteMaterialCatalog.Open(path))` retains the resolver injection seam. This optional package includes native SQLite and is not a dependency of the compiler or CLI.

For managed integrations, `new MaterialCatalog(entities)` accepts material rows with typed properties and provenance, validates the complete snapshot, and resolves it without database access. `MaterialCatalogDatabase.Recreate(path)` remains in the optional SQLite package for applications that generate the standard seed database.
