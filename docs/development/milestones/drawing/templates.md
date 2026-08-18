# Drawing concepts and templates

M0B Drawing Templates may accept both the authoritative Product and a structured metadata Record:

```firmament
Record DrawingInfo {
    Company: String Author: String PartNumber: String
    Revision: Version Date: Date Description: String Title: String Material: String
}
Static Defaults: DrawingInfo = DrawingInfo { /* complete immutable value */ }
Static Release = Defaults with { Revision: 1.1.0 Date: 2026-08-10 }

Template < Item: Product, Metadata: DrawingInfo >
Drawing StandardAssemblyDrawing: AssemblyProductionDrawing {
    Source: Item
    Metadata: Metadata
    BOM: true
}
Drawing Released = StandardAssemblyDrawing<Product: Machine, Metadata: Release>
```

`Version` accepts strict `major.minor.patch` without leading zeroes. `Date` accepts a real calendar date in ISO `yyyy-MM-dd`. Both are compile-time types; malformed values fail before product projection. Normalized metadata retains Static identity and `with` provenance.

Revision policy is author-supplied, never inferred from geometry: Major denotes an incompatible product-definition change, Minor a compatible engineering revision or added capability, and Patch a corrective non-breaking update.

There is no separate template subsystem. A Drawing Template is a Firmament Template whose result kind is `Drawing`.

```firmament
Concept Drawing MachinedPartDrawing {
    Require PrimaryView
    Require ManufacturingPmi
    Require Material
    Require DesignTable
    Require RevisionMetadata
}

Template < Item: Product >
Drawing StandardMachinedDrawing: MachinedPartDrawing {
    Source: Item
    Orientation: Landscape
    Material: "6061-T6 aluminium"
    Table: BearingStandards

    View Front {
        Direction: +Z
        HiddenLines: VisibleOnly
        PMI: [MountDiameter, A]
    }
    View ISO {
        Direction: [1, 1, 1]
        Projection: Isometric
        PMI: []
    }
}

Drawing BearingBlockProduction = StandardMachinedDrawing<Product: BearingBlock>
```

The application records source product identity, concept identity, template identity, argument map, Static Table sources, and a stable specialization identity. Concept `Require` checks communication structure rather than pixels. PMI lists contain names bound by the authoritative product `Pmi` block; they never contain replacement numeric values.

The supported M0 concept requirements are `PrimaryView`, `ManufacturingPmi`, `Material`, `DesignTable`, and `RevisionMetadata`. Unknown requirements fail closed.
