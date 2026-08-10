# Database-driven CAD with SQLite, LINQ, and Forge Host

The `samples/Aetheris.Samples.DatabaseDrivenCad` console application proves a deliberately ordinary architecture:

```text
SQLite -> EF Core -> LINQ -> typed C# record -> Forge Host -> Firmament Template -> exact AP242 STEP
```

SQLite is authoritative for catalog and application configuration data. The compile-tested `BearingBlock.firmament` Template is authoritative for engineering generation logic. AP242 is the exact compiled geometry artifact; the database does not store BRep blobs.

## Run it

From the repository root:

```powershell
dotnet run --project samples/Aetheris.Samples.DatabaseDrivenCad -- seed
dotnet run --project samples/Aetheris.Samples.DatabaseDrivenCad -- list
dotnet run --project samples/Aetheris.Samples.DatabaseDrivenCad -- query
dotnet run --project samples/Aetheris.Samples.DatabaseDrivenCad -- generate AB-204
dotnet run --project samples/Aetheris.Samples.DatabaseDrivenCad -- generate-all
```

Use `--database PATH` and `--output DIR` for explicit locations. Dimensions and tolerances are stored in millimetres; the names make that convention explicit. `generate-all` executes the real relational LINQ query for production aluminium configurations with bore diameter at least 20 mm, orders by bore then part number, and compiles every selected row. Output paths derive from the database part number.

The application maps its EF entity into `BearingBlockSpec`, whose dimensions use the public `Length` type and whose revision uses `System.Version`. `BearingBlockBinding.Descriptor` explicitly maps every field into a `ForgeRecord`; it uses no reflection, JSON, anonymous object serialization, or Firmament source construction. `ForgeInvocation.WithProvenance` records the database kind, entity, and key without a machine-local database path.

The Template constructs a real box and resolved through-bore, validates positive dimensions and bore/tolerance admissibility, and exports STEP through the normal compiler path. `ForgeCompilationResult` returns typed diagnostics and the stable Template specialization identity. Drawing/PDF generation is not included: the current Drawing M0 compiler consumes a complete source document rather than the Host Template result seam, and duplicating or source-generating a drawing document would violate this sample's boundary.

## LINQ is the configuration query language

The sample includes ordinary EF Core queries for:

- all configurations ordered by bore and part number;
- one SKU by `PartNumber`;
- production aluminium configurations joined through `Material` with a minimum bore;
- current configurations by semantic-version major component.

It intentionally does not wrap LINQ in an Aetheris query API.

## Static Tables versus SQLite

Firmament Static Tables remain appropriate for finite engineering standards and configuration that belongs with source and version control. SQLite is appropriate for runtime catalog, product, and business configuration queried by an application. One does not make the other obsolete.

## Scope

This sample demonstrates catalog data, variant querying, deterministic generation, and exact artifacts. It does not claim to implement approvals, permissions, audit/compliance, ECO workflows, supplier portals, ERP, or full PLM.

Compared with a spreadsheet configurator, responsibilities remain separate: SQLite stores relational data, LINQ expresses queries, C# owns application flow, Firmament owns engineering generation, Git owns source history, and STEP is a compiled artifact. No Excel, VBA, named ranges, proprietary query language, or KernelSDK dependency is involved.
