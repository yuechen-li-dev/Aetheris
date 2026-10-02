namespace Aetheris.Kernel.Firmament.FirmamentV2;

// Editor metadata for implemented authoring forms. Execution stays with their
// existing profile, point, section and wire binders; these are not new parsers.
[FirmamentConstruct("WireRoute", "WireRoute", Context = "Model", Entry = "WireRoute Lead { Diameter: 1mm; MinimumBendRadius: 10mm; Path { Start { At: Point3(0mm,0mm,0mm); } Corner Bend { At: Point3(40mm,0mm,0mm); Radius: 10mm; } End { At: Point3(40mm,40mm,0mm); } } }", Description = "A bounded point route lowered to exact WireForm straights and circular bends.")]
[FirmamentField("Diameter", "Diameter", FirmamentSchemaValueKind.Length, Required = true)]
[FirmamentField("MinimumBendRadius", "MinimumBendRadius", FirmamentSchemaValueKind.Length, Required = true)]
[FirmamentField("Material", "Material", FirmamentSchemaValueKind.ConstructReference)]
public static class WireRouteSemanticDeclaration { }

[FirmamentConstruct("WireFollow", "Follow", Context = "WireRoute Path", Description = "Lock a finite Concept Line3 interval; the first lane derives a circular entry and requires a tangent exit.")]
[FirmamentField("Curve", "Curve", FirmamentSchemaValueKind.ConstructReference, Required = true)]
[FirmamentField("From", "From", FirmamentSchemaValueKind.Length, Required = true)]
[FirmamentField("Distance", "Distance", FirmamentSchemaValueKind.Length, Required = true)]
[FirmamentField("Direction", "Direction", FirmamentSchemaValueKind.Choice, Choices = ["Forward", "Reverse"])]
public static class WireFollowSemanticDeclaration { }

[FirmamentConstruct("Section", "Section", Context = "SectionChain or keyed Pattern", Description = "A closed profile at a Concept frame, with stable span correspondence.")]
[FirmamentField("Frame", "Frame", FirmamentSchemaValueKind.ConstructReference, Required = true)]
[FirmamentField("Profile", "Profile", FirmamentSchemaValueKind.ConstructReference, Required = true)]
[FirmamentField("Seam", "Seam", FirmamentSchemaValueKind.ConstructReference, Description = "Optional explicit first span; omission uses the first authored span.")]
public static class SectionSemanticDeclaration { }

[FirmamentConstruct("ConceptPoints", "Points", Context = "Concept Struct", Description = "Finite keyed Point2 scaffolding from Linear, Series, or explicit Stations.")]
[FirmamentField("Keys", "Keys", FirmamentSchemaValueKind.Vector)]
public static class ConceptPointsSemanticDeclaration { }

[FirmamentConstruct("PointLinear", "Linear", Context = "Concept Points", Description = "Use Start and Step with Keys, or Start and Direction with explicit keyed Stations.")]
[FirmamentField("Start", "Start", FirmamentSchemaValueKind.Point, Required = true)]
[FirmamentField("Step", "Step", FirmamentSchemaValueKind.Vector)]
[FirmamentField("Direction", "Direction", FirmamentSchemaValueKind.Vector)]
public static class PointLinearSemanticDeclaration { }

[FirmamentConstruct("PointSeries", "Series", Context = "Concept Points", Description = "A bounded compile-time function-driven sequence; no runtime or Comptime keyword.")]
[FirmamentField("Index", "Index", FirmamentSchemaValueKind.String, Required = true)]
[FirmamentField("First", "First", FirmamentSchemaValueKind.Scalar, Required = true)]
[FirmamentField("Count", "Count", FirmamentSchemaValueKind.Scalar, Required = true)]
[FirmamentField("KeyPrefix", "KeyPrefix", FirmamentSchemaValueKind.String, Required = true)]
[FirmamentField("Position", "Position", FirmamentSchemaValueKind.Point, Required = true)]
public static class PointSeriesSemanticDeclaration { }
