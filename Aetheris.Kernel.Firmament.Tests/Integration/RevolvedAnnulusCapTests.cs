using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Firmament.Tests.Integration;

/// <summary>A full revolution of a profile off the axis has annular caps: the larger circle is their outer boundary
/// whichever way the profile runs, so a STEP reader keeps each cap facing out of the material.</summary>
public sealed class RevolvedAnnulusCapTests
{
    [Theory]
    [InlineData("[[5mm, 0mm], [10mm, 0mm], [10mm, 20mm], [5mm, 20mm]]", 4712.389)]
    [InlineData("[[5mm, 0mm], [5mm, 20mm], [10mm, 20mm], [10mm, 0mm]]", 4712.389)]
    public void Sleeve_ReimportsWithItsCapsFacingOut(string points, double volume)
    {
        var built = FirmamentBuildAndExport.CompileSource($"Model Sleeve {{ units: mm\n Axis A {{ Origin: [0mm, 0mm, 0mm]; Direction: [0, 1, 0] }}\n Polygon2 Section {{ Points: {points} }}\n Revolve Body {{ Profile: Section; About: A; Angle: full }} }}");
        Assert.True(built.IsSuccess, string.Join("\n", built.Diagnostics.Select(d => d.Message)));
        var imported = Step242Importer.ImportBody(built.Value.StepText);
        Assert.True(imported.IsSuccess);
        Assert.DoesNotContain(imported.Diagnostics, d => d.Message.StartsWith("step-orientation-source-mismatch", StringComparison.Ordinal));
        Assert.True(SurfaceMeshIrTessellator.TryBuild(imported.Value, SurfaceMeshPolicy.FromDisplayOptions(DisplayTessellationOptions.Default), out var document, out var failure), failure);
        Assert.True(SurfaceMeshIrTessellator.TryLowerToTriangleMesh(document, out _, out var mesh));
        Assert.True(mesh.IsWatertight && mesh.IsOutwardOriented);
        Assert.True(double.Abs(mesh.SignedVolume - volume) < 0.01 * volume, $"{mesh.SignedVolume} vs {volume}");
    }
}
