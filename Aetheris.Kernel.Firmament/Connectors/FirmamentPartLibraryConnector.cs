using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.StandardLibrary;

namespace Aetheris.Kernel.Firmament.Connectors;

internal static class FirmamentPartLibraryConnector
{
    private const string StandardLibraryPrefix = "standard_library/";

    public static KernelResult<BrepBody> ResolvePart(string partReference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partReference);

        if (!partReference.StartsWith(StandardLibraryPrefix, StringComparison.Ordinal))
        {
            return KernelResult<BrepBody>.Failure([
                new KernelDiagnostic(
                    KernelDiagnosticCode.InvalidArgument,
                    KernelDiagnosticSeverity.Error,
                    $"Part reference '{partReference}' is not mapped by the Firmament connector. Expected prefix '{StandardLibraryPrefix}'.")
            ]);
        }

        var partName = partReference[StandardLibraryPrefix.Length..];
        return StandardLibraryReusableParts.TryCreate(partName);
    }
}
