using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242ExporterTests
{
    [Fact]
    public void ExportBody_BoxBody_ReturnsStepTextWithExpectedSections()
    {
        var boxResult = BrepPrimitives.CreateBox(4d, 6d, 8d);
        Assert.True(boxResult.IsSuccess);

        var export = Step242Exporter.ExportBody(boxResult.Value);

        Assert.True(export.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(export.Value));

        Assert.Contains("ISO-10303-21;", export.Value, StringComparison.Ordinal);
        Assert.Contains("HEADER;", export.Value, StringComparison.Ordinal);
        Assert.Contains("DATA;", export.Value, StringComparison.Ordinal);
        Assert.Contains("ENDSEC;", export.Value, StringComparison.Ordinal);
        Assert.Contains("END-ISO-10303-21;", export.Value, StringComparison.Ordinal);
        Assert.Contains("MANIFOLD_SOLID_BREP", export.Value, StringComparison.Ordinal);
        Assert.Contains("ADVANCED_FACE", export.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportBody_BoxBody_IsDeterministicForSameInput()
    {
        var boxResult = BrepPrimitives.CreateBox(10d, 12d, 14d);
        Assert.True(boxResult.IsSuccess);

        var first = Step242Exporter.ExportBody(boxResult.Value);
        var second = Step242Exporter.ExportBody(boxResult.Value);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value, second.Value);

        var entityLines = first.Value
            .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => line.StartsWith('#', StringComparison.Ordinal))
            .Take(5)
            .ToArray();

        Assert.Equal("#1=CARTESIAN_POINT($,(-5,-6,-7));", entityLines[0]);
        Assert.Equal("#2=VERTEX_POINT($,#1);", entityLines[1]);
    }

    [Fact]
    public void ExportBody_Sphere_ReturnsNotImplementedDiagnostic_InsteadOfThrowing()
    {
        var sphereResult = BrepPrimitives.CreateSphere(3d);
        Assert.True(sphereResult.IsSuccess);

        var export = Step242Exporter.ExportBody(sphereResult.Value);

        Assert.False(export.IsSuccess);
        var diagnostic = Assert.Single(export.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.NotImplemented, diagnostic.Code);
        Assert.Equal(KernelDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("Face:1", diagnostic.Source);
        Assert.Contains("boundary loops", diagnostic.Message, StringComparison.Ordinal);
    }
}
