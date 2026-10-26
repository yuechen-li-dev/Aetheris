namespace Aetheris.Kernel.Firmament.FirmamentV2;

// The everyday part workflow: a closed 2D Profile given to Extrude or Compose.
// These describe the ProfileAuthoringParser and Compose surfaces; those parsers
// remain the execution authority. See docs/public/reference/language/profiles.md.

[FirmamentConstruct("Profile", "Profile", Context = "Model", Entry = "Profile Outline = RoundedRect2 { center: [0mm, 0mm]; size: [50mm, 40mm]; radius: 3mm }",
    Description = "A closed 2D boundary for Extrude or Compose: one closed shape directly, or Loops traced from named construction guides with |>.")]
public static class ProfileSemanticDeclaration { }

[FirmamentConstruct("Extrude", "Extrude", Context = "Model", Entry = "Extrude Plate { profile: Outline; from: 0mm; to: 8mm }",
    Description = "A plain prism from a Profile along its layout normal. Holes and other Modify features are not admitted on an Extrude; use Compose with a Base for those.")]
[FirmamentField("Profile", "Profile", FirmamentSchemaValueKind.Profile, Required = true, Description = "Name of the Profile to extrude.")]
[FirmamentField("From", "From", FirmamentSchemaValueKind.Length, Required = true, Description = "Start height along the layout normal.")]
[FirmamentField("To", "To", FirmamentSchemaValueKind.Length, Required = true, Description = "End height; greater than From.")]
public static class ExtrudeSemanticDeclaration { }

[FirmamentConstruct("Compose", "Compose", Context = "Model", Entry = "Compose Plate {\n    Base Stock { Profile: Outline; From: 0mm; To: 8mm; Role: Stock }\n}",
    Description = "A Profile-based body built from a Base and optional Boss, Pocket and Hole operations. A later Modify on the Compose name admits holes and top-rim chamfers; Fillet finishes are rejected on Compose bodies, so round corners in the Profile instead.")]
public static class ComposeSemanticDeclaration { }

[FirmamentConstruct("ComposeBase", "Base", Context = "Compose", Entry = "Base Stock { Profile: Outline; From: 0mm; To: 8mm; Role: Stock }",
    Description = "The starting stock of a Compose body.")]
[FirmamentField("Profile", "Profile", FirmamentSchemaValueKind.Profile, Required = true, Description = "Name of the Profile that bounds the stock.")]
[FirmamentField("From", "From", FirmamentSchemaValueKind.Length, Required = true, Description = "Bottom of the stock.")]
[FirmamentField("To", "To", FirmamentSchemaValueKind.Length, Required = true, Description = "Top of the stock; greater than From.")]
[FirmamentField("Role", "Role", FirmamentSchemaValueKind.String, Default = "Stock", Choices = ["Stock"], Description = "Stock for starting material.")]
public static class ComposeBaseSemanticDeclaration { }

[FirmamentConstruct("Rect2", "Rect2", Context = "Model, Profile or layout", Entry = "Rect2 Outline { center: [0mm, 0mm]; size: [50mm, 40mm] }",
    Description = "Closed axis-aligned rectangle. Named sides and corners (Top, BottomLeft, ...) can be traced into Profiles.")]
[FirmamentField("Center", "Center", FirmamentSchemaValueKind.Point, Required = true, Description = "Center in the layout plane.")]
[FirmamentField("Size", "Size", FirmamentSchemaValueKind.Vector, Unit = FirmamentUnitKind.Length, Required = true, Description = "Two full lengths, X then Y.")]
public static class Rect2SemanticDeclaration { }

[FirmamentConstruct("RoundedRect2", "RoundedRect2", Context = "Model, Profile or layout", Entry = "RoundedRect2 Outline { center: [0mm, 0mm]; size: [50mm, 40mm]; radius: 3mm }",
    Description = "Closed rectangle with four circular corner arcs. Extruded, it gives a block with rounded vertical edges.")]
[FirmamentField("Center", "Center", FirmamentSchemaValueKind.Point, Required = true, Description = "Center in the layout plane.")]
[FirmamentField("Size", "Size", FirmamentSchemaValueKind.Vector, Unit = FirmamentUnitKind.Length, Required = true, Description = "Two full lengths, X then Y.")]
[FirmamentField("Radius", "Radius", FirmamentSchemaValueKind.Length, Required = true, Description = "Corner radius, at most half the shorter side.")]
public static class RoundedRect2SemanticDeclaration { }

[FirmamentConstruct("Circle2", "Circle2", Context = "Model, Profile or layout", Entry = "Circle2 Disc { center: [0mm, 0mm]; diameter: 20mm }",
    Description = "Closed circle. Supply exactly one of Radius or Diameter.")]
[FirmamentField("Center", "Center", FirmamentSchemaValueKind.Point, Required = true, Description = "Center in the layout plane.")]
[FirmamentField("Radius", "Radius", FirmamentSchemaValueKind.Length, Description = "Radius; omit when Diameter is given.")]
[FirmamentField("Diameter", "Diameter", FirmamentSchemaValueKind.Length, Description = "Diameter; omit when Radius is given.")]
public static class Circle2SemanticDeclaration { }

[FirmamentConstruct("Slot2", "Slot2", Context = "Model, Profile or layout", Entry = "Slot2 Track { center: [0mm, 0mm]; length: 30mm; width: 8mm }",
    Description = "Closed obround along X. Length is overall, including both round ends.")]
[FirmamentField("Center", "Center", FirmamentSchemaValueKind.Point, Required = true, Description = "Center in the layout plane.")]
[FirmamentField("Length", "Length", FirmamentSchemaValueKind.Length, Required = true, Description = "Overall length; at least Width.")]
[FirmamentField("Width", "Width", FirmamentSchemaValueKind.Length, Required = true, Description = "Slot width.")]
public static class Slot2SemanticDeclaration { }
