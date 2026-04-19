using Aetheris.Kernel.Firmament.ParsedModel;

namespace Aetheris.Kernel.Firmament.Lanes;

internal static class FirmamentLaneOperationCatalog
{
    public static bool IsLaneRoutedOperation(FirmamentKnownOpKind kind) => kind != FirmamentKnownOpKind.LibraryPart;
}
