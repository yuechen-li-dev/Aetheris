using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

public sealed record FirmamentProjectedValue(string ValueKind, string Text, double? Number = null,
    IReadOnlyList<double>? Components = null, string? Unit = null);
public sealed record FirmamentFieldProjection(FirmamentFieldId FieldId, string Name, FirmamentSchemaValueKind Kind,
    FirmamentUnitKind Unit, FirmamentProjectedValue? EffectiveValue, string? AuthoredValue,
    string Origin, FirmamentV2SourceSpan? DeclarationSpan, FirmamentV2SourceSpan? ValueSpan,
    bool Editable, string? ReadOnlyReason);
public sealed record FirmamentConstructProjection(FirmamentConstructId ConstructId, string SemanticId,
    string SourceDocument, string SourceRevision, int BuildRevision, FirmamentV2SourceSpan? ConstructSpan,
    IReadOnlyList<FirmamentFieldProjection> Fields);
public sealed record FirmamentFieldRewrite(bool Success, string? NewSource, string? NewSourceRevision,
    FirmamentV2SourceSpan? ReplacedSpan, string? Replacement, string? Code, string? Message);

/// <summary>Static, typed projection of canonical bound Box/Hole values. Parser-owned spans are the
/// only rewrite authority; compatibility and noncanonical forms remain source-only.</summary>
public static class FirmamentFieldProjector
{
    public const string Version = "firmament-field-projection/1";
    public static string RevisionOf(string source)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))).ToLowerInvariant();

    public static IReadOnlyList<FirmamentConstructProjection> Project(FirmamentV2Document document,
        string source, string sourceDocument, int buildRevision)
    {
        var revision = RevisionOf(source);
        var result = new List<FirmamentConstructProjection>();
        foreach (var solid in document.Solids)
        {
            if (solid.Box is not { } box || solid.SourceSpan is null) continue;
            var schema = FirmamentSemanticSchemas.Get("Box")!;
            result.Add(new(schema.Id, solid.Name, sourceDocument, revision, buildRevision, solid.SourceSpan,
                schema.Fields.Select(field => ProjectBoxField(field, box, solid.AuthoredFields, source)).ToArray()));
        }
        foreach (var block in document.ModifyBlocks ?? [])
        foreach (var hole in block.SemanticHoles)
        {
            if (hole.SourceSpan is null) continue;
            var schema = FirmamentSemanticSchemas.Get("Hole")!;
            result.Add(new(schema.Id, $"{block.TargetSolid}.{hole.Name}", sourceDocument, revision, buildRevision, hole.SourceSpan,
                schema.Fields.Select(field => ProjectHoleField(field, hole, source)).ToArray()));
        }
        return result;
    }

    public static IReadOnlyList<FirmamentConstructProjection> ProjectWireForm(WireFormFeatureAir feature,
        string source, string sourceDocument, int buildRevision)
    {
        var schema = FirmamentSemanticSchemas.Get("Helix")!;
        var revision = RevisionOf(source);
        var result = new List<FirmamentConstructProjection>();
        foreach (var coil in feature.Operations.OfType<WireAxisCoilAir>())
        {
            var provenance = feature.AuthoredOperations?.ElementAtOrDefault(coil.Ordinal - 1);
            if (provenance is null || provenance.Name != coil.Name) continue;
            result.Add(new(schema.Id, coil.StableId(feature.Name), sourceDocument, revision, buildRevision, provenance.ConstructSpan,
                schema.Fields.Select(field => ProjectHelixField(field, coil, provenance.Fields, source)).ToArray()));
        }
        return result;
    }

    public static FirmamentConstructProjection? ProjectLoft(SectionChainAuthoringResult result,
        string source, string sourceDocument, int buildRevision)
    {
        if (!result.IsSuccess || result.LoftBinding is not { } binding) return null;
        var schema = FirmamentSemanticSchemas.Get("Loft")!;
        return new(schema.Id, binding.Name, sourceDocument, RevisionOf(source), buildRevision,
            binding.ConstructSpan,
            schema.Fields.Select(field => ProjectLoftField(field, binding, source)).ToArray());
    }

    public static FirmamentFieldRewrite Rewrite(string source, string sourceRevision,
        FirmamentConstructProjection construct, FirmamentFieldId fieldId, FirmamentProjectedValue next)
    {
        if (sourceRevision != RevisionOf(source) || construct.SourceRevision != sourceRevision)
            return Refuse("stale_revision", "The source changed since this field was projected.");
        var field = construct.Fields.SingleOrDefault(item => item.FieldId == fieldId);
        if (field is null) return Refuse("field_not_found", "The selected semantic field was not projected.");
        if (!field.Editable || field.ValueSpan is null)
            return Refuse("field_readonly", field.ReadOnlyReason ?? "This field has no safe source literal.");
        if (construct.ConstructId.Value != "Hole" || fieldId.Value != "Hole.Diameter")
            return Refuse("field_readonly", "This field has no supported source rewrite.");
        if (next.ValueKind != "Length" || next.Unit != "mm" || next.Number is not { } value ||
            !double.IsFinite(value) || value <= 0d)
            return Refuse("invalid_value", "Diameter requires a positive finite length in mm.");
        var span = field.ValueSpan;
        if (span.Start < 0 || span.Length <= 0 || span.Start + span.Length > source.Length ||
            source.Substring(span.Start, span.Length) != field.AuthoredValue)
            return Refuse("stale_revision", "The projected literal no longer matches the source.");
        var replacement = value.ToString("R", CultureInfo.InvariantCulture) + "mm";
        var rewritten = source[..span.Start] + replacement + source[(span.Start + span.Length)..];
        return new(true, rewritten, RevisionOf(rewritten), span, replacement, null, null);
    }

    private static FirmamentFieldProjection ProjectBoxField(FirmamentFieldSchema field,
        FirmamentV2BoxRecord box, IReadOnlyList<FirmamentV2AuthoredField>? fields, string source)
    {
        var effective = field.Id.Value == "Box.Size"
            ? new FirmamentProjectedValue("Vector", string.Join(" × ", box.Size.Select(Format)) + " mm", Components: box.Size, Unit: "mm")
            : null;
        return Make(field, effective, fields, source, false, "Aggregate Box values are source-only in X1.");
    }

    private static FirmamentFieldProjection ProjectHoleField(FirmamentFieldSchema field,
        FirmamentV2SemanticHoleDecl hole, string source)
    {
        FirmamentProjectedValue? effective = field.Id.Value switch
        {
            "Hole.On" => new("FaceSelector", hole.EntryFace.Source),
            "Hole.Center" => new("Point", $"Point2({Format(hole.Center.U)}mm, {Format(hole.Center.V)}mm)"),
            "Hole.Diameter" => new("Length", Format(FirmamentSemanticSchemas.ProjectHoleDiameter(hole)) + " mm",
                FirmamentSemanticSchemas.ProjectHoleDiameter(hole), Unit: "mm"),
            "Hole.End" => new("String", hole.EndCondition.Kind == FirmamentV2SemanticHoleEndKind.ThroughAll
                ? "ThroughAll" : $"{hole.EndCondition.Kind} {Format(hole.EndCondition.Depth ?? 0)} mm"),
            _ => null
        };
        return Make(field, effective, hole.AuthoredFields, source, field.Id.Value == "Hole.Diameter",
            field.Id.Value == "Hole.Diameter" ? "No direct diameter literal." : "This field is source-only in X1.");
    }

    private static FirmamentFieldProjection ProjectHelixField(FirmamentFieldSchema field,
        WireAxisCoilAir coil, IReadOnlyList<FirmamentV2AuthoredField> fields, string source)
    {
        FirmamentProjectedValue? effective = field.Id.Value switch
        {
            "Helix.Radius" => new("Length", Format(coil.RadiusMm) + " mm", coil.RadiusMm, Unit: "mm"),
            "Helix.Turns" => new("Scalar", Format(coil.Turns), coil.Turns),
            "Helix.Pitch" => new("Length", Format(coil.PitchMm) + " mm", coil.PitchMm, Unit: "mm"),
            "Helix.Height" => new("Length", Format(coil.HeightMm) + " mm", coil.HeightMm, Unit: "mm"),
            "Helix.Handedness" => new("Choice", coil.Handedness.ToString()),
            "Helix.StartPhase" => new("Angle", Format(coil.StartPhaseRadians * 180d / Math.PI) + " deg",
                coil.StartPhaseRadians * 180d / Math.PI, Unit: "deg"),
            _ => null
        };
        var projected = Make(field, effective, fields, source, false, "Helix fields are source-only in X1.");
        return projected.AuthoredValue is null && effective is not null
            ? projected with { Origin = field.Default is null ? "Derived" : "Defaulted" } : projected;
    }

    private static FirmamentFieldProjection ProjectLoftField(FirmamentFieldSchema field,
        LoftAuthoredBinding binding, string source)
    {
        FirmamentProjectedValue? effective = field.Id.Value switch
        {
            "Loft.RearProfile" => new("Profile", binding.RearProfile),
            "Loft.RearFrame" => new("ConstructionPlane", binding.RearFrame),
            "Loft.FrontProfile" => new("Profile", binding.FrontProfile),
            "Loft.FrontFrame" => new("ConstructionPlane", binding.FrontFrame),
            "Loft.Rule" => new("Choice", "Ruled"),
            "Loft.Correspondence" => new("Choice", "CommonRay"),
            "Loft.Reference" => new("Vector", "[" + string.Join(", ", binding.Reference.Select(Format)) + "]",
                Components: binding.Reference),
            "Loft.Twist" => new("Angle", Format(binding.TwistDegrees) + " deg", binding.TwistDegrees, Unit: "deg"),
            _ => null
        };
        var projected = Make(field, effective, binding.Fields, source, false, "Loft fields are source-only in X1.");
        return projected.AuthoredValue is null && effective is not null && field.Default is not null
            ? projected with { Origin = "Defaulted" } : projected;
    }

    private static FirmamentFieldProjection Make(FirmamentFieldSchema schema, FirmamentProjectedValue? effective,
        IReadOnlyList<FirmamentV2AuthoredField>? fields, string source, bool allowRewrite, string reason)
    {
        var matches = fields?.Where(item => item.Name == schema.Name).ToArray() ?? [];
        var authored = matches.Length == 1 && InRange(matches[0].ValueSpan, source)
            ? source.Substring(matches[0].ValueSpan.Start, matches[0].ValueSpan.Length) : null;
        var literal = allowRewrite && effective?.Number is { } number && authored is not null &&
            authored.EndsWith("mm", StringComparison.Ordinal) &&
            double.TryParse(authored[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && parsed == number;
        var editable = literal && matches.Length == 1;
        var origin = authored is not null ? "Authored" : effective is not null ? "Derived" : "Unavailable";
        return new(schema.Id, schema.Name, schema.Kind, schema.Unit, effective, authored, origin,
            authored is null ? null : matches[0].DeclarationSpan,
            authored is null ? null : matches[0].ValueSpan, editable,
            editable ? null : matches.Length > 1 ? "Duplicate field declarations cannot be edited safely." :
                authored is not null && allowRewrite ? "Authored expression is not a direct matching literal." : reason);
    }

    private static bool InRange(FirmamentV2SourceSpan span, string source)
        => span.Start >= 0 && span.Length > 0 && span.Start + span.Length <= source.Length;
    private static string Format(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    private static FirmamentFieldRewrite Refuse(string code, string message) => new(false, null, null, null, null, code, message);
}
