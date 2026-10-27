namespace Aetheris.Kernel.Core.Step242;

/// <summary>The explicitly admitted semantic controls. They annotate geometry; they do not solve or modify it.</summary>
public static class Step242GeometricToleranceKinds
{
    public sealed record Descriptor(string Name, string Entity, bool RequiresDatum, bool AllowsDatum);
    public static IReadOnlyList<Descriptor> All { get; } = [
        new("Straightness", "STRAIGHTNESS_TOLERANCE", false, false), new("Flatness", "FLATNESS_TOLERANCE", false, false),
        new("Roundness", "ROUNDNESS_TOLERANCE", false, false), new("Cylindricity", "CYLINDRICITY_TOLERANCE", false, false),
        new("Parallelism", "PARALLELISM_TOLERANCE", true, true), new("Perpendicularity", "PERPENDICULARITY_TOLERANCE", true, true),
        new("Angularity", "ANGULARITY_TOLERANCE", true, true), new("Position", "POSITION_TOLERANCE", false, true),
        new("LineProfile", "LINE_PROFILE_TOLERANCE", false, true), new("SurfaceProfile", "SURFACE_PROFILE_TOLERANCE", false, true),
        new("CircularRunout", "CIRCULAR_RUNOUT_TOLERANCE", true, true), new("TotalRunout", "TOTAL_RUNOUT_TOLERANCE", true, true) ];
    public static Descriptor? Find(string kind) => All.FirstOrDefault(item => item.Name.Equals(kind switch { "Parallel" => "Parallelism", "Perpendicular" => "Perpendicularity", _ => kind }, StringComparison.OrdinalIgnoreCase));
    public static IReadOnlyDictionary<string, string> Modifiers { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["MaximumMaterial"] = "MAXIMUM_MATERIAL_REQUIREMENT", ["LeastMaterial"] = "LEAST_MATERIAL_REQUIREMENT",
        ["FreeState"] = "FREE_STATE", ["CommonZone"] = "COMMON_ZONE", ["Statistical"] = "STATISTICAL_TOLERANCE"
    };
    public static IReadOnlyDictionary<string, string> ZoneForms { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    { ["Cylindrical"] = "cylindrical or circular", ["Spherical"] = "spherical", ["BetweenSurfaces"] = "between two equidistant surfaces" };
    internal static string? Validate(Step242SemanticPmiGeometricTolerance tolerance, IReadOnlySet<string> datums)
    {
        var kind = Find(tolerance.ToleranceKind);
        if (kind is null) return $"Unknown control '{tolerance.ToleranceKind}'.";
        if (!double.IsFinite(tolerance.Value) || tolerance.Value < 0) return "Tolerance must be finite and nonnegative.";
        if (kind.RequiresDatum && tolerance.DatumReferences.Count == 0 || !kind.AllowsDatum && tolerance.DatumReferences.Count > 0) return $"Invalid datum references for {kind.Name}.";
        if (tolerance.DatumReferences.Count > 3 || tolerance.DatumReferences.Distinct(StringComparer.OrdinalIgnoreCase).Count() != tolerance.DatumReferences.Count
            || tolerance.DatumReferences.Any(label => !datums.Contains(label))) return "Datum references must resolve uniquely in precedence order (up to three).";
        if (tolerance.Modifiers.Any(m => !Modifiers.ContainsKey(m)) || tolerance.Modifiers.Distinct().Count() != tolerance.Modifiers.Count
            || tolerance.Modifiers.Contains("MaximumMaterial") && tolerance.Modifiers.Contains("LeastMaterial")) return "Unknown, duplicate or contradictory control modifiers.";
        if (tolerance.ZoneForm is { } form && !ZoneForms.ContainsKey(form)) return "Unknown tolerance zone form.";
        return null;
    }
}
