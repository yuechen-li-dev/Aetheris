using Aetheris.Kernel.Firmament.CompiledModel;
using Aetheris.Kernel.Firmament.Execution;
using Aetheris.Kernel.Firmament.Materializer;
using Aetheris.Kernel.Firmament.Diagnostics;
using Aetheris.Kernel.Firmament.Lowering;
using Aetheris.Kernel.Firmament.ParsedModel;

namespace Aetheris.Kernel.Firmament;

public sealed record FirmamentCompilationArtifact(
    string ArtifactKind = "firmament-placement-executed",
    FirmamentParsedDocument? ParsedDocument = null,
    FirmamentCompiledSchema? CompiledSchema = null,
    FirmamentPrimitiveLoweringPlan? PrimitiveLoweringPlan = null,
    FirmamentPrimitiveExecutionResult? PrimitiveExecutionResult = null,
    FirmamentValidationExecutionResult? ValidationExecutionResult = null);
