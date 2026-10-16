namespace Aetheris.Kernel.Firmament.FirmamentV2;

/// <summary>One thing a visual editor can add. <c>Placement</c> is <c>model</c> (a body in a Model or
/// Struct, inserted with <c>insertDeclaration</c>) or <c>feature</c> (inside the Modify of a body,
/// inserted with <c>insertFeature</c>). <c>Source</c> is one declaration named <c>NameStem</c>; editors
/// pass a unique name with the insert.</summary>
public sealed record FirmamentPaletteItem(string Id, string Label, string Category, string Description,
    string Placement, string NameStem, string Source);

public sealed record FirmamentLanguagePalette(string Version, IReadOnlyList<FirmamentPaletteItem> Items);

/// <summary>
/// The constructs a visual editor offers to add, with starting values. This is a curated catalog,
/// not the schema: every item builds as written (Box host for features), which
/// FirmamentLanguagePaletteTests proves through the same build path the editor uses.
/// Add an item only with that proof.
/// </summary>
public static class FirmamentLanguagePaletteService
{
    public const string Version = "firmament-palette/1";

    private static readonly FirmamentPaletteItem[] Catalog =
    [
        new("box", "Box", "Bodies", "A rectangular block, centered on the origin.", "model", "Block",
            "Box Block { Size: [40mm, 30mm, 10mm] }"),
        new("cylinder", "Cylinder", "Bodies", "A round bar standing on the XY plane.", "model", "Bar",
            "Cylinder Bar { Radius: 10mm Height: 20mm }"),

        new("hole", "Hole", "Holes", "A drilled through hole from the top face.", "feature", "Hole",
            "Hole<Shaft> Hole { On: +Z Center: Point2(0mm, 0mm) Diameter: 6mm End: ThroughAll }"),
        new("hole-blind", "Blind hole", "Holes", "A drilled hole to a depth.", "feature", "BlindHole",
            "Hole<Shaft> BlindHole { On: +Z Center: Point2(0mm, 0mm) Diameter: 6mm End: Blind 5mm }"),
        new("counterbore", "Counterbored hole", "Holes", "A through hole with a flat-bottomed recess for a cap screw head.", "feature", "Counterbore",
            "Hole<Counterbore> Counterbore { On: +Z Center: Point2(0mm, 0mm) Diameter: 4mm CounterboreDiameter: 7mm CounterboreDepth: 3mm End: ThroughAll }"),
        new("countersink", "Countersunk hole", "Holes", "A through hole with a conical recess for a flat-head screw.", "feature", "Countersink",
            "Hole<Countersink> Countersink { On: +Z Center: Point2(0mm, 0mm) Diameter: 6mm CountersinkDiameter: 12mm CountersinkAngle: 90deg End: ThroughAll }"),
        new("perforation", "Perforation", "Holes", "A field of round vents across the top face.", "feature", "Vents",
            "Perforation Vents { On: +Z Diameter: 6mm Layout: Hex Pitch: 9mm Margin: 8mm }"),

        new("chamfer", "Chamfer top edges", "Edges", "Breaks the edges around the top face with a 45° chamfer.", "feature", "TopChamfer",
            "EdgeFinish TopChamfer { Face: +Z Target: Boundary Kind: Chamfer Distance: 1mm }"),
        new("fillet", "Round top edges", "Edges", "Rounds the edges around the top face.", "feature", "TopRound",
            "EdgeFinish TopRound { Face: +Z Target: Boundary Kind: Fillet Radius: 1mm }"),
    ];

    public static FirmamentLanguagePalette Palette() => new(Version, Catalog);
}
