# Standard Paperclip Template

`Standard.Products.Office.Paperclip` specializes a typed `PaperclipPolicy`, derives a readable seven-segment Concept Path, and lowers it through the exact circular Sweep lane.

The default `StandardPaperclip` reconstructs the analytic design intent of the PAPERCLIP-X1 SolidWorks reference. Its ordered centerline is: 14 mm inner-right leg, R3 180° inner-top bend, 14 mm inner-left leg, R4 180° lower return, 15 mm outer-right leg, R5 180° outer-top bend, and 15 mm outer-left leg. Every join is tangent, the wire diameter is constant at 1 mm, and the resulting product envelope is 11 × 25 × 1 mm.

| Parameter | Default |
|---|---:|
| `WireDiameter` | 1 mm |
| `OuterLegLength` | 15 mm |
| `InnerLegLength` | 14 mm |
| `OuterBendRadius` | 5 mm |
| `InnerBendRadius` | 3 mm |
| `Material` | `Standard.Materials.StainlessSteel.304_Annealed` |

The canonical authoring form is `Record → Static defaults → with overrides → Template<PaperclipPolicy>`. Start with [`paperclip.firmament`](../../../fixtures/Canonical/Templates/paperclip.firmament); variants should override policy data instead of rewriting the path.

The X0 fields `OverallLength`, `OuterWidth`, `InnerWidth`, `BendRadius`, and `LoopGap` encoded the former incorrect silhouette and are no longer accepted. For example, “make the paperclip 15% longer” maps to `StandardPaperclip with { OuterLegLength: 17.25mm InnerLegLength: 16.1mm }`; a wider form can increase both bend radii while preserving `OuterBendRadius > InnerBendRadius` and adequate wire clearance.

Forge Host Protocol v1 publishes the template through ordinary `list`, `describe`, and `invoke` operations. It has no paperclip-specific RPC. Cadmata's **MAXIMUM PAPERCLIPS** tab calls the same specialization/compiler path, imports the returned AP242, and displays the generated engineering geometry.

The bounded maximizer reports centerline length, mass from catalog density, and paperclips per metre of wire. It does not perform unbounded optimization.
