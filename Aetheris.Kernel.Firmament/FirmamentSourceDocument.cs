namespace Aetheris.Kernel.Firmament;

public sealed record FirmamentSourceDocument(
    string SourceText,
    string? SourceName = null,
    string SourceFamily = "firmament",
    string? LanguageVersion = null);
