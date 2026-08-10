# Assembly M1: executable semantic product geometry

Assembly M1 keeps M0's product tree, independent Mate graph, typed Interfaces, and symbolic tolerance graph. It adds a definition/materialization seam and a deterministic transformed-instance artifact; transforms remain compiler results, never authoring inputs.

## Canonical path

```text
Static Record
  -> ordinary Firmament Template specialization
  -> ordinary Firmament exact BRep/AP242 build
  -> Part definition artifact (once per specialization)
  -> Assembly instance + Mate-derived rigid transform
  -> transformed exact instance BRep
  -> world-coordinate semantic bindings
  -> post-materialization Mate residual validation
```

The canonical proof is `fixtures/AssemblyM1/template-block-pair.firmament`. A Part tag may apply an existing Firmament Template:

```firmament
<Part Fixed = AssemblyBlock<Spec: FixedSpec>></Part>
```

The specialization is compiled by `FirmamentBuildAndExport.CompileSource`; Assembly does not own a parallel geometry generator. The canonical AP242 part result is reimported to the exact BRep path. Definition identity, specialization identity, Static Record provenance, instance identity, and transform are retained separately.

## Runtime and inspection

`AssemblyM1Pipeline` returns AssemblyIR plus an in-memory collection of source definition bodies and transformed instance bodies. The serializable `AssemblyGeometryArtifactIr` contains definition hashes, topology/bounds metrics, instance-to-definition mappings, world transforms, residuals, and a deterministic hash.

```powershell
dotnet run --project Aetheris.CLI -- asm inspect fixtures/AssemblyM1/template-block-pair.firmament --json
```

Human-readable inspection includes the product tree, Mates, residuals, a minimal derived BOM, and stackups. JSON distinguishes definition artifacts from instance artifacts.

## World semantics and residual authority

`AssemblyWorldQuery.Resolve` composes an instance's definition-local `ExactAxisBinding`, `ExactPlaneBinding`, `ExactPointBinding`, or exact body with the resolved rigid transform, using double precision. It does not clone authored definitions.

After both participant bodies exist, every admitted Axis/Plane/Point placement constraint is reevaluated in world coordinates. Position and angular residuals are recorded independently of the solver's abstract success. The M1 canonical fixture produces zero residual for its coincident axis and seating plane.

## Dimensional consequences

An Interface `Fit` now lowers a typed clearance transition into AssemblyIR with Mate and Interface identities. Its source provenance includes the endpoint Template specialization and Static Record rows, and that evidence is copied into stackup contributions. The canonical M1 assertion proves the automatic transition from `MovingSpec` to `FixedSpec`; explicit `Relation` remains the bounded way to author other signed dimensional transitions. M0 tolerance paths remain unchanged and green.

## Current boundary

- Template-generated Parts are supported. Template-authored Assembly/subassembly definitions are designed for by definition identity and the retained tree, but are not parsed/materialized in M1.
- Exact instance BReps are executable and inspectable. Cadmata has not yet gained an Assembly display endpoint.
- Native AP242 product structure is not emitted. The current exporter creates one PRODUCT/PRODUCT_DEFINITION and one shape representation per body and has no NAUO, mapped-item, or representation-relationship-with-transformation writer. See `docs/assembly/artifacts/m1/ap242-audit.md`.
- `.firmasm` remains a deprecated transform-first compatibility lane. Its executor/export package is preserved; raw transforms have no Interface meaning. Automated source migration is not implemented.
- InlineStep/Recognize semantic regions remain exact BRep regions, but current recognizers do not produce AxisCapable, PlaneCapable, or DimensionalCapable values, so they cannot yet satisfy these Assembly Roles.
