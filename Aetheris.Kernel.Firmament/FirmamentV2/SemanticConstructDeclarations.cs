namespace Aetheris.Kernel.Firmament.FirmamentV2;

// These are opt-in public Firmament surfaces. The parser remains the execution authority.
[FirmamentConstruct("Box", "Box", Context = "Model or Struct", Entry = "Box Body { Size: [10mm, 10mm, 10mm] }", Description = "Rectangular solid.")]
[FirmamentField("Size", "Size", FirmamentSchemaValueKind.Vector, Unit = FirmamentUnitKind.Length, Description = "Three lengths for the box dimensions; supply Size or Bounds.")]
[FirmamentField("Bounds", "Bounds", FirmamentSchemaValueKind.Box3, Description = "A semantic Box3 bound instead of Size.")]
public static class BoxSemanticDeclaration { }

[FirmamentConstruct("Hole", "Hole", Context = "Modify body", Entry = "Modify Body { Hole<Shaft> H { On: +Z; Center: Point2(0mm, 0mm); Diameter: 6mm; End: ThroughAll } }", Description = "Semantic drilled hole.")]
[FirmamentField("On", "On", FirmamentSchemaValueKind.FaceSelector, Required = true, Description = "Entry face of the hole.")]
[FirmamentField("Center", "Center", FirmamentSchemaValueKind.Point, Required = true, Description = "Center in the support face.")]
[FirmamentField("Diameter", "Diameter", FirmamentSchemaValueKind.Length, Required = true, Description = "Shaft diameter.")]
[FirmamentField("End", "End", FirmamentSchemaValueKind.String, Required = true, Description = "ThroughAll or a blind termination expression.")]
[FirmamentField("PatternIdentity", "PatternIdentity", FirmamentSchemaValueKind.String, Description = "Optional pattern identity.")]
[FirmamentOutput("Wall", "Wall", "Face", SourceAddressable = true, SourceRole = nameof(Materializer.SemanticTopologyRole.HoleWallFace))]
public static class HoleSemanticDeclaration { }

[FirmamentConstruct("Helix", "Helix", Context = "WireForm", Entry = "WireForm Spring { Diameter: 1mm; Origin: [0mm, 0mm, 0mm]; Tangent: [0, 0, 1]; Up: [0, 1, 0]; Helix Winding { Radius: 5mm; Turns: 4; Pitch: 2mm } }", CompatibilityAlias = "AxisCoil", Description = "WireForm centerline helix.")]
[FirmamentField("Radius", "Radius", FirmamentSchemaValueKind.Length, Required = true, Description = "Centerline radius.")]
[FirmamentField("Turns", "Turns", FirmamentSchemaValueKind.Scalar, Required = true, Description = "Number of winding turns.")]
[FirmamentField("Pitch", "Pitch", FirmamentSchemaValueKind.Length, Description = "Axial distance per turn; supply Pitch or Height.")]
[FirmamentField("Height", "Height", FirmamentSchemaValueKind.Length, Description = "Total axial span; supply Height or Pitch.")]
[FirmamentField("Handedness", "Handedness", FirmamentSchemaValueKind.Choice, Default = "RightHanded", Choices = ["RightHanded", "LeftHanded"], Description = "Winding direction.")]
[FirmamentField("StartPhase", "StartPhase", FirmamentSchemaValueKind.Angle, Default = "0deg", Description = "Starting radial rotation.")]
public static class HelixSemanticDeclaration { }

[FirmamentConstruct("Loft", "Loft", Context = "Model with two authored profiles and frames", Entry = "Loft Body { RearProfile: Rear; RearFrame: RearPlane; FrontProfile: Front; FrontFrame: FrontPlane; Rule: Ruled; Correspondence: CommonRay; Reference: [1, 0, 0] }", Description = "Two-section ruled loft.")]
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


