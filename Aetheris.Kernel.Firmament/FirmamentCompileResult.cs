using Aetheris.Kernel.Core.Results;

namespace Aetheris.Kernel.Firmament;

public sealed record FirmamentCompileResult(KernelResult<FirmamentCompilationArtifact> Compilation);
