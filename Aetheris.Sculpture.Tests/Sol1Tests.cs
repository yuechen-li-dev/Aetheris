using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Sculpture.Tests;

public sealed class Sol1Tests
{
    [Fact]
    public void FlagshipSource_BuildsDeterministicExactVirtualSculptureAssembly()
    {
        var source = Sol1Source.Load(FixturePath());
        Assert.True(source.IsSuccess, string.Join(Environment.NewLine, source.Diagnostics.Select(d => d.Message)));
        Assert.Equal(SculptureMode.Virtual, source.Value.Mode);

        var first = Sol1Materializer.Build(source.Value);
        var second = Sol1Materializer.Build(source.Value);
        Assert.True(first.IsSuccess, string.Join(Environment.NewLine, first.Diagnostics.Select(d => d.Message)));
        Assert.True(second.IsSuccess, string.Join(Environment.NewLine, second.Diagnostics.Select(d => d.Message)));
        Assert.Equal(first.Value.Step, second.Value.Step);
        Assert.Equal(first.Value.Evidence.StepSha256, second.Value.Evidence.StepSha256);
        Assert.Equal(233, first.Value.Evidence.PhyllotaxisNodes);
        Assert.Equal([13, 21], first.Value.Evidence.FibonacciOffsets);
        Assert.Equal(432, first.Value.Evidence.LatticeConnections);
        Assert.True(first.Value.Evidence.ProminentNodes > 0);
        Assert.True(first.Value.Evidence.ProminentConnections > 0);
        Assert.True(first.Value.Evidence.StepAssemblyReimportSucceeded);
        Assert.False(first.Value.Evidence.IsManufacturingGeometry);
        Assert.Equal(0, first.Value.Evidence.SurfaceInventory.RationalProductSurfaces);
        Assert.Equal(0, first.Value.Evidence.SurfaceInventory.BSplineSurfaces);
        Assert.True(first.Value.Evidence.SurfaceInventory.Tori >= 3);
        Assert.True(first.Value.Evidence.SurfaceInventory.Spheres >= 2);
        Assert.True(first.Value.Evidence.SurfaceInventory.Cylinders > 0);
        Assert.DoesNotContain("RATIONAL_B_SPLINE_SURFACE", first.Value.Step, StringComparison.Ordinal);

        var imported = Step242AssemblyImporter.Import(first.Value.Step);
        Assert.True(imported.IsSuccess, string.Join(Environment.NewLine, imported.Diagnostics.Select(d => d.Message)));
        Assert.Equal(first.Value.Evidence.ReimportedDefinitions, imported.Value.Definitions.Count);
        Assert.Equal(first.Value.Evidence.ReimportedOccurrences, imported.Value.Occurrences.Count);
    }

    [Fact]
    public void SourceValidation_PreservesEyeAndFrameClearance()
    {
        var source = Sol1Source.Load(FixturePath());
        Assert.True(source.IsSuccess);
        var points = Sol1Materializer.GeneratePoints(source.Value);
        Assert.All(points, point => Assert.True(point.RadiusMm > source.Value.Eye.MajorRadiusMm + source.Value.Eye.MinorRadiusMm));
        Assert.All(points, point => Assert.True(point.RadiusMm + source.Value.Lattice.NodeRadiusMm < source.Value.OuterFrame.MajorRadiusMm - source.Value.OuterFrame.MinorRadiusMm));
    }

    private static string FixturePath()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Aetheris.slnx"))) current = current.Parent;
        return Path.Combine(current?.FullName ?? throw new DirectoryNotFoundException(), "fixtures", "Canonical", "VirtualSculpture", "sol-1.sculpture.json");
    }
}
