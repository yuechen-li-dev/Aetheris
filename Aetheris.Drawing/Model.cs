using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Aetheris.Drawing;

public enum DrawingRegionType { View, Detail, Section, NoteBlock, DimensionCluster, Keepout, DatumArea, Other }
public enum DrawingAnnotationCategory { Datum, Dimension, Feature, View, Section, Keepout, Note, Relationship, Uncertain, Question }
public enum DrawingConfidence { High, Medium, Low, Unresolved }
public enum DrawingDimensionType { Overall, Offset, Diameter, Radius, Depth, Thickness, Angle, Coordinate, Keepout, AllAround, Unknown }
public enum DrawingAxis { X, Y, Z, Radial, Normal, Other }
public enum DrawingSemanticClass { ProductGeometry, AccessoryKeepout, SensorKeepout, MaterialRestriction, CosmeticReference, FunctionalReference, Unknown }
public enum DrawingRelationKind { Targets, ReferencedFrom, LocatedIn, DetailOf, SectionOf, KeepoutFor, CoordinateIn, RelatedTo }

/// <summary>PDF page-space rectangle in points (1/72 inch), with a top-left origin.</summary>
public sealed record DrawingBounds(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;

    public void Validate(string name)
    {
        if (!new[] { X, Y, Width, Height }.All(double.IsFinite) || X < 0 || Y < 0 || Width <= 0 || Height <= 0)
            throw new DrawingNotesException("drawing-bounds-invalid", $"{name} bounds must be finite positive page-space coordinates.");
    }

    public bool NearlyEquals(DrawingBounds other, double tolerance = 0.01) =>
        Math.Abs(X - other.X) <= tolerance && Math.Abs(Y - other.Y) <= tolerance &&
        Math.Abs(Width - other.Width) <= tolerance && Math.Abs(Height - other.Height) <= tolerance;

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"[{X:0.##},{Y:0.##},{Width:0.##},{Height:0.##}]");
}

public sealed record DrawingPage(int Number, double WidthPoints, double HeightPoints);

public sealed record DrawingSourceDocument(
    string FileName,
    string Sha256,
    int PageCount,
    IReadOnlyList<DrawingPage> Pages,
    string? Title = null,
    string? VisibleDate = null,
    string? VisibleRevision = null,
    IReadOnlyList<string>? SheetIdentifiers = null,
    string Renderer = "PDFium",
    string RendererVersion = "PDFtoImage-5.4.0");

public sealed record DrawingRegion(
    string Id,
    string Name,
    int Page,
    DrawingBounds Bounds,
    DrawingRegionType Type,
    string? ParentRegionId = null,
    string? Notes = null,
    DrawingConfidence Confidence = DrawingConfidence.High,
    string? CropFile = null);

public sealed record DrawingDimensionInterpretation(
    string ValueText,
    double? ParsedValue = null,
    string? Units = null,
    DrawingDimensionType Type = DrawingDimensionType.Unknown,
    DrawingAxis? Axis = null,
    string? DatumId = null,
    string? SourceFeatureId = null,
    string? TargetFeatureId = null,
    string? Interpretation = null);

public sealed record DrawingAnnotation(
    string Id,
    string Label,
    DrawingAnnotationCategory Category,
    int Page,
    DrawingBounds Bounds,
    string? RegionId = null,
    string? Note = null,
    string? OriginalText = null,
    DrawingConfidence Confidence = DrawingConfidence.High,
    DrawingSemanticClass SemanticClass = DrawingSemanticClass.Unknown,
    IReadOnlyList<string>? Tags = null,
    IReadOnlyList<string>? Aliases = null,
    DrawingDimensionInterpretation? Dimension = null,
    string? Pass = null);

public sealed record DrawingRelation(
    string Id,
    string FromId,
    string ToId,
    DrawingRelationKind Kind,
    string? Note = null,
    DrawingConfidence Confidence = DrawingConfidence.High);

public sealed record DrawingPass(string Id, string Name, string? Focus = null);

public sealed class DrawingNotesProject
{
    public string SchemaVersion { get; init; } = "aetheris-drawing-notes-1";
    public string Name { get; set; } = "Drawing Notes";
    public required DrawingSourceDocument Document { get; init; }
    public List<DrawingRegion> Regions { get; init; } = [];
    public List<DrawingAnnotation> Annotations { get; init; } = [];
    public List<DrawingRelation> Relations { get; init; } = [];
    public List<DrawingPass> Passes { get; init; } = [];
}

public sealed record DrawingNotesIssue(string Code, string Message, IReadOnlyList<string> ItemIds);

public sealed class DrawingNotesException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

public static partial class DrawingDimensionParser
{
    [GeneratedRegex(@"[-+]?(?:\d+(?:\.\d+)?|\.\d+)", RegexOptions.CultureInvariant)]
    private static partial Regex NumericValue();
    [GeneratedRegex(@"^\s*\d+\s*[xX]\s*", RegexOptions.CultureInvariant)]
    private static partial Regex LeadingMultiplicity();

    public static DrawingDimensionInterpretation Parse(string valueText, DrawingDimensionType type = DrawingDimensionType.Unknown, DrawingAxis? axis = null, string? interpretation = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(valueText);
        var normalizedValueText = LeadingMultiplicity().Replace(valueText.Replace(',', '.'), "");
        var match = NumericValue().Match(normalizedValueText);
        double? value = match.Success && double.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
        var units = valueText.Contains('°') || valueText.Contains("deg", StringComparison.OrdinalIgnoreCase) ? "deg"
            : valueText.Contains(" in", StringComparison.OrdinalIgnoreCase) ? "in"
            : valueText.Contains("mm", StringComparison.OrdinalIgnoreCase) ? "mm"
            : value is not null ? "mm" : null;
        if (type == DrawingDimensionType.Unknown)
        {
            if (valueText.Contains('Ø') || valueText.Contains("DIA", StringComparison.OrdinalIgnoreCase)) type = DrawingDimensionType.Diameter;
            else if (Regex.IsMatch(valueText, @"(^|\s)R\s*[-+]?\d", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) type = DrawingDimensionType.Radius;
            else if (units == "deg") type = DrawingDimensionType.Angle;
        }
        return new(valueText, value, units, type, axis, Interpretation: interpretation);
    }
}

public static class DrawingIds
{
    public static string Normalize(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var id = Regex.Replace(value.Trim(), @"[^A-Za-z0-9_.-]+", "-").Trim('-');
        if (id.Length == 0) throw new DrawingNotesException("drawing-id-invalid", "Stable ID must contain a letter or number.");
        return id;
    }

    public static string SourceFingerprint(int page, DrawingBounds bounds) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(FormattableString.Invariant($"p{page}:{bounds.X:0.###},{bounds.Y:0.###},{bounds.Width:0.###},{bounds.Height:0.###}"))))[..12].ToLowerInvariant();
}
