# AP242 assembly audit

Evidence: `Aetheris.Kernel.Core/Step242/Step242Exporter.cs`, `Step242Importer.cs`, and `Aetheris.Kernel.Firmament/Assembly/FirmasmAssemblyRoundtripExporter.cs`.

The current exporter supports a single exact `BrepBody`. It emits one PRODUCT, PRODUCT_DEFINITION_FORMATION, PRODUCT_DEFINITION, PRODUCT_DEFINITION_SHAPE, shape representation, and SHAPE_DEFINITION_REPRESENTATION. It does not emit `NEXT_ASSEMBLY_USAGE_OCCURRENCE`, `PRODUCT_DEFINITION_RELATIONSHIP`, `MAPPED_ITEM`, `ITEM_DEFINED_TRANSFORMATION`, `REPRESENTATION_RELATIONSHIP_WITH_TRANSFORMATION`, or `CONTEXT_DEPENDENT_SHAPE_REPRESENTATION`.

The legacy roundtrip exporter writes one transformed STEP file per instance plus a JSON package. This proves transformed geometry but is not native AP242 product structure and does not reuse definition geometry.

The importer returns one `BrepBody`; it does not expose product hierarchy or occurrence transforms. Consequently a native M1 AP242 assembly writer would need a bounded product-structure schema/writer first, followed by a structural parser/import result distinct from `ImportBody`. Boolean flattening would be incorrect. This is the exact remaining blocker; M1 does not paper over it with a fused body.

