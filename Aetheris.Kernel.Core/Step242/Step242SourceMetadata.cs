namespace Aetheris.Kernel.Core.Step242;

public sealed record Step242SourceMetadata(
    string? FileName,
    string? Description,
    string? Author,
    string? Organization,
    string? CreationTimestamp,
    string? OriginatingSystem,
    string? Authorization,
    string? ProductName,
    string? ProductDescription)
{
    public static Step242SourceMetadata Empty { get; } = new(null, null, null, null, null, null, null, null, null);
}
