namespace Aetheris.Kernel.Firmament.FirmamentV2;

// These are opt-in public Firmament surfaces. The parser remains the execution authority.
[FirmamentConstruct("Box", "Box", Context = "Model or Struct", Entry = "Box Body { Size: [10mm, 10mm, 10mm] }", Description = "Rectangular solid.")]
[FirmamentField("Size", "Size", FirmamentSchemaValueKind.Vector, Unit = FirmamentUnitKind.Length, Description = "Three lengths for the box dimensions; supply Size or Bounds.")]
[FirmamentField("Bounds", "Bounds", FirmamentSchemaValueKind.Box3, Description = "A semantic Box3 bound instead of Size.")]
public static class BoxSemanticDeclaration { }

[FirmamentConstruct("Hole", "Hole", Context = "Modify body", Entry = "Modify Body { Hole<Shaft> H { On: +Z; Center: Point2(0mm, 0mm); Diameter: 6mm; End: ThroughAll } }", Description = "Semantic drilled hole.")]
[FirmamentField("On", "On", FirmamentSchemaValueKind.FaceSelector, Required = true, Description = "Entry face of the hole.")]
[FirmamentField("Center", "Center", FirmamentSchemaValueKind.Point, Required = true, Description = "Center in the support face.")]
[FirmamentField("Diameter", "Diameter", FirmamentSchemaValueKind.Length, Required = true, Description = "Shaft diameter.", SourceEditable = true, ProjectionMember = nameof(ProjectDiameter))]
[FirmamentField("End", "End", FirmamentSchemaValueKind.String, Required = true, Description = "ThroughAll or a blind termination expression.")]
[FirmamentField("PatternIdentity", "PatternIdentity", FirmamentSchemaValueKind.String, Description = "Optional pattern identity.")]
[FirmamentOutput("Wall", "Wall", "Face", SourceAddressable = true, SourceRole = nameof(Materializer.SemanticTopologyRole.HoleWallFace))]
public static class HoleSemanticDeclaration
{
    public static double ProjectDiameter(FirmamentV2SemanticHoleDecl instance) => instance.ShaftDiameter;
}

[FirmamentConstruct("Perforation", "Perforation", Context = "Modify body", Entry = "Modify Body { Perforation Vent { On: +Z; Diameter: 6mm; Layout: Hex; Pitch: 9mm; Margin: 8mm } }", Description = "One semantic field of circular through openings on a planar Box or cylindrical Hollow body.")]
[FirmamentField("On", "On", FirmamentSchemaValueKind.FaceSelector, Required = true, Description = "Semantic support face: +Z on Box or OuterWall on Cylinder<Hollow>.")]
[FirmamentField("Diameter", "Diameter", FirmamentSchemaValueKind.Length, Required = true, Description = "Circular opening diameter.")]
[FirmamentField("Layout", "Layout", FirmamentSchemaValueKind.Choice, Required = true, Choices = ["Grid", "Hex", "CylindricalGrid", "CylindricalStaggered"], Description = "Planar or cylindrical grid and staggered layouts.")]
[FirmamentField("Pitch", "Pitch", FirmamentSchemaValueKind.Length, Required = true, Description = "Center spacing; Hex row spacing is sqrt(3)/2 times Pitch.")]
[FirmamentField("Margin", "Margin", FirmamentSchemaValueKind.Length, Default = "0mm", Description = "Clear material from each support edge to opening rim.")]
[FirmamentField("MinimumLigament", "MinimumLigament", FirmamentSchemaValueKind.Length, Default = "0mm", Description = "Additional minimum material between opening rims.")]
[FirmamentField("OffsetX", "OffsetX", FirmamentSchemaValueKind.Length, Default = "0mm", Description = "Pattern shift in support X.")]
[FirmamentField("OffsetY", "OffsetY", FirmamentSchemaValueKind.Length, Default = "0mm", Description = "Pattern shift in support Y.")]
[FirmamentField("CircumferentialPitch", "CircumferentialPitch", FirmamentSchemaValueKind.Length, Description = "Nominal circumferential pitch; full wrap is resolved to an integer number of columns.")]
[FirmamentField("MarginTop", "MarginTop", FirmamentSchemaValueKind.Length, Description = "Minimum material between top opening rim and vessel top.")]
[FirmamentField("MarginBottom", "MarginBottom", FirmamentSchemaValueKind.Length, Description = "Minimum material between bottom opening rim and vessel base.")]
[FirmamentField("StartAngle", "StartAngle", FirmamentSchemaValueKind.Angle, Description = "Angular phase of the first cylindrical column.")]
[FirmamentOutput("InstanceCount", "InstanceCount", "Count")]
[FirmamentOutput("Wall", "Wall", "Face", SourceRole = nameof(Materializer.SemanticTopologyRole.HoleWallFace))]
public static class PerforationSemanticDeclaration { }

[FirmamentConstruct("Helix", "Helix", Context = "WireForm", Entry = "Model HelixWitness {\n    Units: mm\n    WireForm Spring {\n        Diameter: 2mm\n        Material: Standard.Materials.StainlessSteel.304_Annealed\n        StartFrame { Origin: [0mm, 0mm, 0mm]; Tangent: [1, 0, 0]; Up: [0, 0, 1] }\n        Helix Winding { Radius: 6mm; Turns: 1; Pitch: 5mm; Handedness: RightHanded; StartPhase: 0deg }\n    }\n}\n", CompatibilityAlias = "AxisCoil", Description = "WireForm centerline helix.")]
[FirmamentField("Radius", "Radius", FirmamentSchemaValueKind.Length, Required = true, Description = "Centerline radius.")]
[FirmamentField("Turns", "Turns", FirmamentSchemaValueKind.Scalar, Required = true, Description = "Number of winding turns.")]
[FirmamentField("Pitch", "Pitch", FirmamentSchemaValueKind.Length, Description = "Axial distance per turn; supply Pitch or Height.")]
[FirmamentField("Height", "Height", FirmamentSchemaValueKind.Length, Description = "Total axial span; supply Height or Pitch.")]
[FirmamentField("Handedness", "Handedness", FirmamentSchemaValueKind.Choice, Default = "RightHanded", Choices = ["RightHanded", "LeftHanded"], Description = "Winding direction.")]
[FirmamentField("StartPhase", "StartPhase", FirmamentSchemaValueKind.Angle, Default = "0deg", Description = "Starting radial rotation.")]
public static class HelixSemanticDeclaration { }

[FirmamentConstruct("Loft", "Loft", Context = "Model with two authored profiles and frames", Entry = "schema SectionChain\nModel RuledSolidLoft {\n    Units: mm\n    Concept Struct Stations {\n        Lower: Plane { Origin: [0mm, 0mm, 0mm]; Normal: [0, 0, 1]; Up: [0, 1, 0] }\n        Upper: Plane { Origin: [0mm, 0mm, 50mm]; Normal: [0, 0, 1]; Up: [0, 1, 0] }\n    }\n    Construction Plane LowerFrame { Trace: Stations.Lower }\n    Construction Plane UpperFrame { Trace: Stations.Upper }\n    Circle2 LowerCircle { Center: [0mm, 0mm] Radius: 25mm }\n    Ellipse2 UpperEllipse { Center: [0mm, 0mm] AxisLengths: [60mm, 40mm] Rotation: 25deg }\n    Profile LowerOutline { Loop Outer { LowerCircle |> TraceLoop } }\n    Profile UpperOutline { Loop Outer { UpperEllipse |> TraceLoop } }\n    Loft Body {\n        RearProfile: LowerOutline\n        RearFrame: LowerFrame\n        FrontProfile: UpperOutline\n        FrontFrame: UpperFrame\n        Rule: Ruled\n        Correspondence: CommonRay\n        Reference: [1, 0, 0]\n    }\n}\n", Description = "Two-section ruled loft.")]
[FirmamentField("RearProfile", "RearProfile", FirmamentSchemaValueKind.Profile, Required = true, Description = "Closed rear section profile.")]
[FirmamentField("RearFrame", "RearFrame", FirmamentSchemaValueKind.ConstructionPlane, Required = true, Description = "Placement of the rear section.")]
[FirmamentField("FrontProfile", "FrontProfile", FirmamentSchemaValueKind.Profile, Required = true, Description = "Closed front section profile.")]
[FirmamentField("FrontFrame", "FrontFrame", FirmamentSchemaValueKind.ConstructionPlane, Required = true, Description = "Placement of the front section.")]
[FirmamentField("Rule", "Rule", FirmamentSchemaValueKind.Choice, Required = true, Choices = ["Ruled"], Description = "Section interpolation rule.")]
[FirmamentField("Correspondence", "Correspondence", FirmamentSchemaValueKind.Choice, Required = true, Choices = ["CommonRay"], Description = "Point correspondence between sections.")]
[FirmamentField("Reference", "Reference", FirmamentSchemaValueKind.Vector, Required = true, Description = "Reference ray for section correspondence.")]
[FirmamentField("Twist", "Twist", FirmamentSchemaValueKind.Angle, Default = "0deg", Description = "Relative section rotation.")]
public static class LoftSemanticDeclaration { }

[FirmamentConstruct("Concept", "Concept", Context = "Document", Entry = "Concept MountingFrame { Bounds: Box3; TopPlane: Plane }", Description = "Named semantic requirements; member names and types are authored per Concept.")]
public static class ConceptSemanticDeclaration { }


