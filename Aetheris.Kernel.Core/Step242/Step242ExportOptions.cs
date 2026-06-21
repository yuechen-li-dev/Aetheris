namespace Aetheris.Kernel.Core.Step242;

public sealed class Step242ExportOptions
{
    public string ProductName { get; init; } = "AetherisBody";

    public string ProductId { get; init; } = "AETHERIS";

    public string ProductDescription { get; init; } = "";

    public string ApplicationName { get; init; } = "Aetheris";

    public string AuthoringSystem { get; init; } = "Aetheris.Kernel";

    public Step242HeaderMetadata HeaderMetadata { get; init; } = Step242HeaderMetadata.Deterministic;

    public static Step242ExportOptions FromSourceMetadata(Step242SourceMetadata metadata)
    {
        static string Coalesce(string? value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value!;

        return new Step242ExportOptions
        {
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
