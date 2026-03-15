using Aetheris.Kernel.Firmament.Lowering;
using Aetheris.Kernel.Firmament.ParsedModel;

namespace Aetheris.Kernel.Firmament;

public sealed record FirmamentCompilationArtifact(
    string ArtifactKind = "firmament-primitive-lowering-plan-built",
    FirmamentParsedDocument? ParsedDocument = null,
    FirmamentPrimitiveLoweringPlan? PrimitiveLoweringPlan = null);
