namespace Aetheris.Kernel.Core.Step242;

using Aetheris.Kernel.Core.Topology;

/// <summary>Controls whether export preflight findings are only reported or block serialization.</summary>
public enum BrepExportPreflightMode
{
    Disabled,
    Audit,
    Enforce,
}

/// <summary>Named evidence tier for an export route; avoids fixture-specific exporter policy.</summary>
public enum BrepExportPreflightPolicy
{
    LegacyRoute,
    TrustedProductionRoute,
}

public sealed class Step242ExportOptions
{
    /// <summary>Source semantic PMI rebound to an unchanged imported body. Graphical glyphs are regenerated separately.</summary>
    public Step242PmiTransfer? ImportedPmi { get; init; }
    /// <summary>Declared distance accuracy of retained imported geometry, in mm.
    /// Null preserves the historical export. This is not a machining tolerance.</summary>
    public double? DistanceAccuracyMillimetres { get; init; }
    public string ProductName { get; init; } = "AetherisBody";

    public string ProductId { get; init; } = "AETHERIS";

    public string ProductDescription { get; init; } = "";

    public string ApplicationName { get; init; } = "Aetheris";

    public string AuthoringSystem { get; init; } = "Aetheris.Kernel";

    public Step242HeaderMetadata HeaderMetadata { get; init; } = Step242HeaderMetadata.Deterministic;

    private BrepExportPreflightMode? preflightMode;

    /// <summary>
    /// Production policy defaults to Enforce. Legacy interchange defaults to Audit;
    /// an explicit override is available for diagnostic callers.
    /// </summary>
    public BrepExportPreflightMode BrepExportPreflightMode
    {
        get => preflightMode ?? (BrepExportPreflightPolicy == BrepExportPreflightPolicy.TrustedProductionRoute
            ? BrepExportPreflightMode.Enforce : BrepExportPreflightMode.Audit);
        init => preflightMode = value;
    }

    /// <summary>
    /// TrustedProductionRoute rejects rational B-spline supports and pcurves in
    /// the native body. Native ellipsoid octants have exact rational STEP
    /// projection and recover to the same native quadric on import.
    /// LegacyRoute retains interchange/debug compatibility, including rational evidence.
    /// </summary>
    public BrepExportPreflightPolicy BrepExportPreflightPolicy { get; init; } = BrepExportPreflightPolicy.LegacyRoute;

    /// <summary>Explicit import-only geometric consistency budget; authored export uses kernel tolerance.</summary>
    public double? ImportedRecoveryToleranceMillimetres { get; init; }

    /// <summary>Source-declared vertex tolerances for an imported topology, capped by its binder.</summary>
    public IReadOnlyDictionary<VertexId, double>? ImportedSourceVertexTolerancesMillimetres { get; init; }

    /// <summary>
    /// Retained for source compatibility; no effect. Advanced-face edges use their
    /// underlying CIRCLE, with vertices, sense and pcurves defining the boundary.
    /// </summary>
    public bool EmitFullCircleTrimmedCurves { get; init; }

    /// <summary>Emit qualified coedge pcurves; false leaves STEP trim binding to the importer.</summary>
    public bool EmitQualifiedPcurves { get; init; } = true;

    public static Step242ExportOptions FromSourceMetadata(Step242SourceMetadata metadata,
        BrepExportPreflightPolicy preflightPolicy = BrepExportPreflightPolicy.LegacyRoute)
        => FromSourceMetadata(metadata, preflightPolicy, null);

    public static Step242ExportOptions FromSourceMetadata(Step242SourceMetadata metadata,
        BrepExportPreflightPolicy preflightPolicy, Step242PmiTransfer? importedPmi)
    {
        static string Coalesce(string? value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value!;

        return new Step242ExportOptions
        {
            ImportedPmi = importedPmi,
            BrepExportPreflightPolicy = preflightPolicy,
            ImportedRecoveryToleranceMillimetres = preflightPolicy == BrepExportPreflightPolicy.TrustedProductionRoute ? .1d : null,
            ProductName = Coalesce(metadata.ProductName, "AetherisBody"),
            ProductDescription = Coalesce(metadata.ProductDescription, string.Empty),
            ApplicationName = Coalesce(metadata.Organization, "Aetheris"),
            AuthoringSystem = Coalesce(metadata.OriginatingSystem, "Aetheris.Kernel"),
            HeaderMetadata = new Step242HeaderMetadata(
                Coalesce(metadata.FileName, "aetheris_export.step"),
                Coalesce(metadata.Description, "Aetheris AP242 subset export"),
                Coalesce(metadata.CreationTimestamp, "1970-01-01T00:00:00"),
                Coalesce(metadata.Author, "Aetheris"),
                Coalesce(metadata.Organization, Coalesce(metadata.Author, "Aetheris")),
                Coalesce(metadata.OriginatingSystem, "Aetheris.Kernel"),
                Coalesce(metadata.OriginatingSystem, "Aetheris.Kernel"),
                Coalesce(metadata.Authorization, string.Empty))
        };
    }
}

public sealed record Step242HeaderMetadata(
    string FileName,
    string Description,
    string CreationTimestamp,
    string Author,
    string Organization,
    string PreprocessorVersion,
    string OriginatingSystem,
    string Authorization)
{
    public static Step242HeaderMetadata Deterministic { get; } = new(
        "aetheris_export.step",
        "Aetheris AP242 subset export",
        "1970-01-01T00:00:00",
        "Aetheris",
        "Aetheris",
        "Aetheris.Kernel",
        "Aetheris.Kernel",
        string.Empty);
}
