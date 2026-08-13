using Aetheris.Kernel.Core.Diagnostics;
using Xunit;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentPanelOnlyBuildTests
{
    [Fact]
    public void PanelOnlyBuildReturnsTypedUnsupportedResultInsteadOfCrashing()
    {
        var result = FirmamentBuildAndExport.CompileSource("""
            Model PanelOnly {
              Units: mm;
              Panel P { Surface: HyperbolicParaboloid { Width: 40mm; Depth: 30mm; Rise: 6mm; } }
            }
            """);
        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.NotImplemented, diagnostic.Code);
        Assert.Contains("firmament-panel-only-step-materialization-unsupported", diagnostic.Message);
    }
}
