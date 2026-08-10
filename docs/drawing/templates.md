# Drawing concepts and templates

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
    View Iso {
        Direction: [1, 1, 1]
        Projection: Isometric
        PMI: []
    }
}

Drawing BearingBlockProduction = StandardMachinedDrawing<Product: BearingBlock>
```

The application records source product identity, concept identity, template identity, argument map, Static Table sources, and a stable specialization identity. Concept `Require` checks communication structure rather than pixels. PMI lists contain names bound by the authoritative product `Pmi` block; they never contain replacement numeric values.

The supported M0 concept requirements are `PrimaryView`, `ManufacturingPmi`, `Material`, `DesignTable`, and `RevisionMetadata`. Unknown requirements fail closed.
