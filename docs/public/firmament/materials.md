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

The Standard Library catalog is backed by a deployed SQLite asset and resolved by .NET. Firmament does not expose SQL. FEA consumes catalog density and the properties required by its constitutive model; an unknown material is a named error, not a fallback.

Build/solve [`catalog-material-coupon.firmament`](../../../fixtures/Canonical/Materials/material-catalog-coupon.firmament) with `aetheris fea ... --out-dir artifacts/material --json`.
