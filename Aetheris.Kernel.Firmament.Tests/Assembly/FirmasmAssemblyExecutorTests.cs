using Aetheris.Kernel.Firmament.Assembly;

namespace Aetheris.Kernel.Firmament.Tests.Assembly;

public sealed class FirmasmAssemblyExecutorTests
{
    [Fact]
    public void Execute_OcctNutBoltAssembly_ComposesTwoWorldSpaceBodies()
    {
        var executor = new FirmasmAssemblyExecutor();
        var path = FirmamentCorpusHarness.ResolveFixtureFullPath("testdata/firmasm/examples/occt-nut-bolt/nut-bolt-assembly.firmasm");

        var result = executor.ExecuteFromFile(path);

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal(2, result.Value.Instances.Count);
        Assert.Equal(2, result.Value.ComposedBody.Topology.Bodies.Count());
        Assert.Equal(result.Value.Instances.Count, result.Value.ComposedBody.Topology.Bodies.Count());
    }

    [Fact]
    public void Execute_OcctAs1Assembly_ComposesAllInstances()
    {
        var executor = new FirmasmAssemblyExecutor();
        var path = FirmamentCorpusHarness.ResolveFixtureFullPath("testdata/firmasm/examples/occt-as1/as1-assembly.firmasm");

        var result = executor.ExecuteFromFile(path);

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal(18, result.Value.Instances.Count);
        Assert.Equal(18, result.Value.ComposedBody.Topology.Bodies.Count());
        Assert.True(result.Value.ComposedBody.Topology.Faces.Count() > 0);
        Assert.True(result.Value.ComposedBody.Topology.Vertices.Count() > 0);
    }

    [Fact]
    public void Execute_TransformsMoveInstancesAwayFromOrigin()
    {
        var executor = new FirmasmAssemblyExecutor();
        var path = FirmamentCorpusHarness.ResolveFixtureFullPath("testdata/firmasm/examples/occt-nut-bolt/nut-bolt-assembly.firmasm");

        var result = executor.ExecuteFromFile(path);

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));

        var minByInstance = result.Value.Instances
            .Select(instance =>
            {
                var points = instance.Body.Topology.Vertices
                    .Select(vertex => instance.Body.TryGetVertexPoint(vertex.Id, out var p) ? p : (Aetheris.Kernel.Core.Math.Point3D?)null)
                    .Where(p => p.HasValue)
                    .Select(p => p!.Value)
                    .ToArray();
                Assert.NotEmpty(points);
                return (instance.InstanceId, MinX: points.Min(p => p.X));
            })
            .OrderBy(x => x.InstanceId)
            .ToArray();

        Assert.Equal(2, minByInstance.Length);
        Assert.NotEqual(minByInstance[0].MinX, minByInstance[1].MinX);
        Assert.Contains(minByInstance, x => Math.Abs(x.MinX) > 1e-3d);
    }
}
