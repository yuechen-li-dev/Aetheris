using Aetheris.Kernel.Firmament.ParsedModel;

namespace Aetheris.Kernel.Firmament;

public sealed record FirmamentCompilationArtifact(
    string ArtifactKind = "firmament-ops-primitive-boolean-and-validation-required-fields-validated",
    FirmamentParsedDocument? ParsedDocument = null);
