using Aetheris.Kernel.Firmament.Lowering;
using Aetheris.Kernel.Firmament.Execution;
using Aetheris.Kernel.Firmament.ParsedModel;

namespace Aetheris.Kernel.Firmament;

public sealed record FirmamentCompilationArtifact(
    string ArtifactKind = "firmament-topology-exists-validation-executed",
    FirmamentParsedDocument? ParsedDocument = null,
    FirmamentPrimitiveLoweringPlan? PrimitiveLoweringPlan = null,
    FirmamentPrimitiveExecutionResult? PrimitiveExecutionResult = null,
    FirmamentValidationExecutionResult? ValidationExecutionResult = null);
