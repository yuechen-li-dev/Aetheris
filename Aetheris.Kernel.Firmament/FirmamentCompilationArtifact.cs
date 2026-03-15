using Aetheris.Kernel.Firmament.ParsedModel;

namespace Aetheris.Kernel.Firmament;

public sealed record FirmamentCompilationArtifact(
    string ArtifactKind = "firmament-validation-target-featureid-existence-checked",
    FirmamentParsedDocument? ParsedDocument = null);
