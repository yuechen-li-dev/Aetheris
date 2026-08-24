# Standard Paperclip Template

`Standard.Products.Office.Paperclip` specializes a typed `PaperclipPolicy`. Standard Paperclip is now authored as a `WireForm` forming sequence and lowered through explicit line/arc centerline AIR to exact circular Sweep geometry.

The default `StandardPaperclip` reconstructs the analytic design intent of the PAPERCLIP-X1 SolidWorks reference. Its ordered centerline is: 14 mm inner-right leg, R3 180° inner-top bend, 14 mm inner-left leg, R4 180° lower return, 15 mm outer-right leg, R5 180° outer-top bend, and 15 mm outer-left leg. Every join is tangent, the wire diameter is constant at 1 mm, and the resulting product envelope is 11 × 25 × 1 mm.

| Parameter | Default |
|---|---:|
| `WireDiameter` | 1 mm |
| `OuterLegLength` | 15 mm |
| `InnerLegLength` | 14 mm |
| `OuterBendRadius` | 5 mm |
| `InnerBendRadius` | 3 mm |
| `Material` | `Standard.Materials.StainlessSteel.304_Annealed` |

The canonical authoring form is `Record → Static defaults → with overrides → Template<PaperclipPolicy>`. Start with [`paperclip.firmament`](../../../fixtures/Canonical/Templates/paperclip.firmament); variants should override policy data instead of rewriting the forming sequence or calculating tangent coordinates.

The X0 fields `OverallLength`, `OuterWidth`, `InnerWidth`, `BendRadius`, and `LoopGap` encoded the former incorrect silhouette and are no longer accepted. For this product, “make the paperclip 15% longer” canonically means scaling both authored straight-leg parameters: `StandardPaperclip with { OuterLegLength: 17.25mm InnerLegLength: 16.1mm }`. The bend radii remain unchanged, so this is not a claim that every envelope or cut-stock measure grows by exactly 15%. A wider form can increase both bend radii while preserving `OuterBendRadius > InnerBendRadius` and adequate wire clearance.

Export and independently inspect the default product with:

```text
aetheris build fixtures/Canonical/Templates/paperclip.firmament --output artifacts/local/wire-x0-paperclip.step --json
aetheris analyze artifacts/local/wire-x0-paperclip.step --json
```

The qualified product is an enclosed manifold with 4 cylindrical straight faces, 3 toroidal bend faces, and 2 planar terminal caps. Its 11 × 25 × 1 mm envelope, 95.6991118431 mm ideal cut-stock length, zero rational/B-spline product surfaces, and zero faceted fallback are preserved by the WireForm template route.

Forge Host Protocol v1 publishes the template through ordinary `list`, `describe`, and `invoke` operations. It has no paperclip-specific RPC. Cadmata's **MAXIMUM PAPERCLIPS** tab calls the same specialization/compiler path, imports the returned AP242, and displays the generated engineering geometry.

The bounded maximizer reports centerline length, mass from catalog density, and paperclips per metre of wire. It does not perform unbounded optimization.
