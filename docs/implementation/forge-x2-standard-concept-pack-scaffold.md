# FORGE-X2 Standard concept pack scaffold

FORGE-X2 adds the first built-in Standard semantic concept pack scaffold. The descriptor lives in `Aetheris.Forge/Standard/StandardConceptPack.cs` under the `Aetheris.Forge.Standard` namespace and exposes package `Aetheris.Standard` with display name `Aetheris Standard Concept Pack`.

## Concepts added

The pack is metadata-only and currently declares these concepts:

- `Standard.CNC` for CNC/prismatic manufacturing-process constraints.
- `Standard.Hole` for the base semantic hole contract.
- `Standard.ShaftHole` as a simple shaft-hole refinement of `Standard.Hole`.
- `Standard.CounterboreHole` as a stacked-hole concept with counterbore diameter/depth metadata.
- `Standard.CountersinkHole` as a stacked-hole concept with countersink diameter/angle metadata.
- `Standard.EdgeFinish` for fillet/chamfer/round edge-finish intent with deferred lowering.

No templates are added in FORGE-X2. That keeps the milestone focused on high-quality concept descriptors rather than template expansion semantics.

## Relationship to FORGE-A0 and FORGE-X1

FORGE-A0 described Forge as a future semantic CAD extension SDK made of concepts, templates, validation contracts, semantic feature schemas, examples, fixtures, and guidance rather than arbitrary runtime geometry plug-ins. FORGE-X1 introduced the descriptor abstractions and deterministic validation layer in `Aetheris.Forge.Abstractions`, including a descriptor-only `Standard.Hole` example fixture.

FORGE-X2 promotes that direction from a single example into an explicit built-in Standard concept pack descriptor. The pack is still hosted in `Aetheris.Forge` to avoid a new project or package-discovery mechanism.

## Metadata-only boundary

The pack uses `ForgeTrustTier.SemanticDocsOnly` and includes `MetadataOnly`/`NoPluginExecution` host requirements. Capabilities are documentation/contract metadata only. They do not load NuGet packages, scan assemblies, execute plugins, invoke lowerers, call materializers, or run Standard Library BRep helpers.

Existing parser, Firmament syntax, lowering, kernel, materializer, STEP, DisplayIR, frontend, product, and `Aetheris.Kernel.StandardLibrary` runtime behavior is unchanged.

## Hole and AIR relationship

`Standard.Hole`, `Standard.ShaftHole`, `Standard.CounterboreHole`, and `Standard.CountersinkHole` each include a lowering-contract descriptor whose target AIR feature family is `AirHoleFeature`. This is a metadata contract only: it names the conceptual AIR family that future tooling can validate against, but it does not execute lowering.

`Standard.ShaftHole` records `baseConceptId=Standard.Hole` in descriptor assumptions because the FORGE-X1 model does not yet have a first-class base-concept field. Counterbore and countersink descriptors also record stack metadata and diagnostics so tools can distinguish them from base shaft holes.

## Process and edge-finish concepts

`Standard.CNC` proves the pack can represent a manufacturing/process concept rather than only geometry features. It includes optional CNC/prismatic assumptions such as minimum tool radius, minimum wall thickness, preferred inside corner, and process family. It intentionally has no BRep or AIR lowering contract.

`Standard.EdgeFinish` captures target, kind, size, and scope metadata for edge-finish intent. Its lowering is explicitly deferred because FORGE-X2 does not add an AIR feature, BRep operation, or materializer extension point for edge finish descriptors.

## Deferred work

Deferred work includes package discovery, dynamic NuGet loading, assembly scanning, plugin execution, parser integration, new Firmament syntax, template expansion, standards/fit tables, thread/tap geometry, BRep/materializer extension APIs, and runtime migration of existing Forge or Standard Library geometry helpers.
