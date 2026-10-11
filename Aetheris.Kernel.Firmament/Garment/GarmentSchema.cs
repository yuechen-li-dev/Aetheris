using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Garment;

[FirmamentConstruct("Garment", "Garment", Context = "Document", Entry = "schema Garment\nRect2 cut { center: [0mm, 100mm]; size: [200mm, 200mm] }\nProfile pattern { Loop Outer { cut |> TraceLoop } }\nGarment Outfit {\n  meshSize: 60mm;\n  Fabric cotton { thickness: 2mm; arealDensity: 0.2; }\n  Panel front { profile: pattern; origin: [0mm, 160mm, 1000mm]; }\n}", Description = "An assembly of material-space panels and distributed stitch interfaces, compiled for cloth simulation.")]
[FirmamentField("MeshSize", "MeshSize", FirmamentSchemaValueKind.Length, Unit = FirmamentUnitKind.Length, Default = "60mm")]
public static class GarmentDeclaration { }

[FirmamentConstruct("Panel", "Panel", Context = "Garment", Entry = "Panel front { profile: cut; origin: [0mm, 160mm, 1000mm]; u: [1, 0, 0]; v: [0, 0, 1]; }", Description = "Resolved 2D Profile plus a separate initial 3D arrangement. Named profile boundary spans remain addressable as panel.edges.name.")]
[FirmamentField("Profile", "Profile", FirmamentSchemaValueKind.ConstructReference, Required = true)]
[FirmamentField("Origin", "Origin", FirmamentSchemaValueKind.Vector, Required = true, Unit = FirmamentUnitKind.Length)]
[FirmamentField("U", "U", FirmamentSchemaValueKind.Vector)]
[FirmamentField("V", "V", FirmamentSchemaValueKind.Vector)]
[FirmamentField("Grain", "Grain", FirmamentSchemaValueKind.Vector)]
[FirmamentField("Pin", "Pin", FirmamentSchemaValueKind.ConstructReference)]
[FirmamentField("WrapRadius", "WrapRadius", FirmamentSchemaValueKind.Length, Unit = FirmamentUnitKind.Length)]
[FirmamentField("WrapAngle", "WrapAngle", FirmamentSchemaValueKind.Angle, Unit = FirmamentUnitKind.Angle)]
[FirmamentField("WrapTopRadius", "WrapTopRadius", FirmamentSchemaValueKind.Length, Unit = FirmamentUnitKind.Length)]
[FirmamentField("WrapTopOrigin", "WrapTopOrigin", FirmamentSchemaValueKind.Vector, Unit = FirmamentUnitKind.Length)]
[FirmamentField("WrapTopAngle", "WrapTopAngle", FirmamentSchemaValueKind.Angle, Unit = FirmamentUnitKind.Angle)]
public static class GarmentPanelDeclaration { }

[FirmamentConstruct("InterfaceStitch", "Interface<Stitch>", Context = "Garment", Entry = "Interface<Stitch> side { a: front.edges.leftSide; b: back.edges.rightSide; orientation: Reversed; }", Description = "Distributed material-boundary correspondence, lowered to weighted cloth constraints rather than rigid frame mates.")]
[FirmamentField("A", "A", FirmamentSchemaValueKind.ConstructReference, Required = true)]
[FirmamentField("B", "B", FirmamentSchemaValueKind.ConstructReference, Required = true)]
[FirmamentField("Orientation", "Orientation", FirmamentSchemaValueKind.Choice, Choices = ["Same", "Reversed"], Default = "Reversed")]
[FirmamentField("Ease", "Ease", FirmamentSchemaValueKind.Scalar, Default = "0")]
[FirmamentField("Compliance", "Compliance", FirmamentSchemaValueKind.Scalar, Default = "0.00000001")]
public static class GarmentStitchDeclaration { }

[FirmamentConstruct("Fabric", "Fabric", Context = "Garment", Entry = "Fabric cotton { arealDensity: 0.2; thickness: 2mm; }", Description = "One shared fabric per garment. Density is kg/m2; compliance controls are authoring parameters, not calibrated textile moduli.")]
[FirmamentField("ArealDensity", "ArealDensity", FirmamentSchemaValueKind.Scalar)]
[FirmamentField("Thickness", "Thickness", FirmamentSchemaValueKind.Length, Unit = FirmamentUnitKind.Length)]
[FirmamentField("WarpCompliance", "WarpCompliance", FirmamentSchemaValueKind.Scalar)]
[FirmamentField("WeftCompliance", "WeftCompliance", FirmamentSchemaValueKind.Scalar)]
[FirmamentField("DiagonalCompliance", "DiagonalCompliance", FirmamentSchemaValueKind.Scalar)]
[FirmamentField("BendCompliance", "BendCompliance", FirmamentSchemaValueKind.Scalar)]
public static class GarmentFabricDeclaration { }

[FirmamentConstruct("Drape", "Drape", Context = "Garment", Entry = "Drape { figure: Anatolia; pose: Rest; clearance: 4mm; }", Description = "A named figure binding supplied by the host. The first lane sews and settles on the loaded static Rest-pose body.")]
[FirmamentField("Figure", "Figure", FirmamentSchemaValueKind.ConstructReference, Required = true)]
[FirmamentField("Pose", "Pose", FirmamentSchemaValueKind.Choice, Choices = ["Rest"])]
[FirmamentField("Clearance", "Clearance", FirmamentSchemaValueKind.Length, Unit = FirmamentUnitKind.Length)]
public static class GarmentDrapeDeclaration { }
