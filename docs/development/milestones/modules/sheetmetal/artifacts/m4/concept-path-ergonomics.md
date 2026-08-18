# Concept Path ergonomics

Previous Sheet Metal parsing accepted `Main.Front` and `FrontWall.Outer` as strings but did not publish a domain public surface, inspect capabilities, map paths to flat state, or distinguish a hidden member from a capability mismatch.

M4 publishes `SheetMetalConceptPath` metadata:

- rectangular base: `Front`, `Right`, `Rear`, `Left`, `Center`;
- flange: `Root`, `Outer`, `Left`, `Right`, `LeftCorner`, `RightCorner`, `Bend`;
- formed/flat pairs such as `FrontWall.Bend` / `Flat.FrontWall.Bend`;
- capabilities including `FlangeAttachable`, `BendBoundary`, `FreeEdge`, `CornerAdjacent`, `Cuttable`, `PointCapable`, and `FlatCorrespondent`.

Examples of diagnostics:

```text
`Main.Center` has capability PointCapable; `Flange.From` requires FlangeAttachable SheetEdge.
Available public edge members: Front, Right, Rear, Left.

`FrontWall.InnerFace` is not a FlangeAttachable public member.
Available public members: Root, Outer, Left, Right, LeftCorner, RightCorner, Bend.
```

Inspection:

```text
aetheris sheetmetal paths fixtures/SheetMetal/m4-psu-enclosure.firmament
```

Path shape is tested stable across `FourWallTray` specializations with different dimensions. Normal Profile dogfood remains `Concept Path Outline` -> `Profile Plate From Outline`, and M4 tests convert/offset its ordinary `ResolvedProfile2D` line/arc semantics rather than introducing Sheet Metal path logic in the geometry kernel.
