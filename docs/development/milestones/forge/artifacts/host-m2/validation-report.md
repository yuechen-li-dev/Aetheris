# Forge Host M2 validation evidence

Seeded four configurations, two materials, and one shared Aster Works drawing-metadata row in a real SQLite database. The production-aluminium LINQ query selected `AB-204` then `AB-305` by bore diameter.

| Part | Revision | Specialization | STEP SHA-256 |
|---|---:|---|---|
| AB-204 | 2.1.0 | `template:1d2e995f2be2e479` | `3ACD9BEA0CE21C5FDF7699A27128D8B11F05F4719C939746742AE1FC7C3F7259` |
| AB-305 | 3.0.1 | `template:eea80ba75fd5599f` | `5764A7273643806522A13864BAF28C331D10B40C6E82FE513FB7FDEBB90BD4AF` |

Two independent batch runs preserved query order, specialization identities, artifact names, and STEP hashes. Warm compilation for the second specialization was about 5 ms; first-process compiler/template initialization was about 230–252 ms. EF/SQLite first-query initialization was about 213–250 ms, while explicit DTO/Record mapping was 0.02–4.55 ms.

`aetheris inspect` on AB-204 reported one enclosed-manifold body and shell, 7 faces, 15 edges, 12 vertices, 6 planar faces, 1 cylindrical face, 2 circular curves, and bounds `[-50,-30,0]` to `[50,30,16]` mm. This is the expected 100 x 60 x 16 block with an exact cylindrical through-bore.

Focused validation:

- Database-driven sample tests: 8 passed (SQLite schema/seed/join/query, unknown SKU, malformed revision, primitive/unit/nested Record binding, unsupported mapping, invalid Require, deterministic generation, dependency boundary).
- Forge Host tests: 8 passed (native and extension invocation, generated binding, diagnostics, exact STEP round trip, CIR/provenance, resource handling).
- Firmament Template/Drawing tests: 38 passed; CLI tests: 360 passed; FEA host-consumer tests: 12 passed.
- `dotnet restore Aetheris.slnx` and `dotnet build Aetheris.slnx -f net10.0 --no-restore /m:1`: passed with 0 warnings and 0 errors.
- NuGet vulnerability audit: no vulnerable direct or transitive packages for the sample.
- Package boundary: KernelSDK absent directly and transitively from sample `project.assets.json`.
- Drawing/PDF: not generated; the Drawing M0 API is still complete-source oriented and was not bridged with source generation.
- Public docs site: no public-doc-site project is present in this repository; repository Forge docs were updated.
