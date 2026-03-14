using Aetheris.Kernel.Firmament.ParsedModel;

namespace Aetheris.Kernel.Firmament;

public sealed record FirmamentCompilationArtifact(
    string ArtifactKind = "firmament-ops-structure-known-kind-family-parsed",
    FirmamentParsedDocument? ParsedDocument = null);
