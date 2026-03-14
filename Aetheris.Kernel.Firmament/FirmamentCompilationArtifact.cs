using Aetheris.Kernel.Firmament.ParsedModel;

namespace Aetheris.Kernel.Firmament;

public sealed record FirmamentCompilationArtifact(
    string ArtifactKind = "firmament-top-level-skeleton-parsed",
    FirmamentParsedDocument? ParsedDocument = null);
