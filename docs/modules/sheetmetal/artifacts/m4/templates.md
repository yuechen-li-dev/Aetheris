# Templates and LLM authoring dogfood

## Reusable templates

`SheetMetalTemplates` exposes:

- `LBracketSpec(Width, Depth, FlangeHeight, Policy)`;
- `UChannelSpec(Width, Depth, WallHeight, Policy)`;
- `FourWallTraySpec(Width, Depth, WallHeight, Policy)`.

`SheetMetalTemplatePolicy` owns thickness, inside radius, K-factor, material, corner policy, and relief policy. Each generator emits ordinary Sheet Metal source and therefore uses normal lowering, DFM, Concept Paths, correspondence, STEP, and SVG. Tested path shape is identical between two tray specializations.

## PSU enclosure

[`m4-psu-enclosure.firmament`](../../../../../fixtures/FirmamentV2/SheetMetal/m4-psu-enclosure.firmament) is 18 noncomment, nonblank source lines with one base, five flanges, four mounting holes, and three vent cuts. It uses `Main.Front`, `Main.Right`, `Main.Rear`, `Main.Left`, and nested `FrontWall.Outer`; it contains no raw region/face/edge/BRep IDs.

Real-path result:

- 11 semantic regions, five bends, seven cuts, four base corners;
- both Mitered and Open corner policies;
- validated exact composed blank;
- valid formed and flat STEP;
- flat size `256.2957516778 x 208.9723886187 mm`;
- deterministic flat hash `da104ebd017d4915b3d10e3a372d59794823caa8aa4d96def08fbb1cd85f5d91`;
- DFM `Pass`.

## Repair dogfood

`m4-bad-tray-dfm.firmament` deliberately declares relief width `0.6 mm` and depth `1 mm` at thickness `1.2 mm`, radius `1.5 mm`. The semantic finding requires width `>= 1.2 mm` and depth `>= 2.7 mm` and supplies a numeric suggestion. `m4-fixed-tray-dfm.firmament` applies those values and changes overall DFM from `Warning` to `Pass` without any BRep inspection.
