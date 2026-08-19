# Standard Paperclip Template

`Standard.Products.Office.Paperclip` is the first Standard Products Template. It specializes a typed `PaperclipPolicy`, derives a readable seven-segment Concept Path, and lowers it through circular Sweep.

The default `StandardPaperclip` is metric and intentionally described as a recognizable office paperclip, not as compliance with a commercial or ISO dimensional standard.

| Parameter | Default |
|---|---:|
| `WireDiameter` | 0.8 mm |
| `OverallLength` | 33 mm |
| `OuterWidth` | 9 mm |
| `InnerWidth` | 5 mm |
| `BendRadius` | 1 mm |
| `LoopGap` | 1 mm |
| `Material` | `Standard.Materials.StainlessSteel.304_Annealed` |

The canonical authoring form is `Record → Static defaults → with overrides → Template<PaperclipPolicy>`. Start with [`paperclip.firmament`](../../../fixtures/Canonical/Templates/paperclip.firmament); variants should override policy data instead of rewriting the path.

Forge Host Protocol v1 publishes the template through ordinary `list`, `describe`, and `invoke` operations. It has no paperclip-specific RPC. Cadmata's **MAXIMUM PAPERCLIPS** tab calls the same specialization/compiler path, imports the returned AP242, and displays the generated engineering geometry.

The bounded maximizer reports centerline length, mass from catalog density, and paperclips per metre of wire. It does not perform unbounded optimization.
