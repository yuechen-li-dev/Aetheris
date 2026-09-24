using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Xunit;
using Xunit.Abstractions;

namespace Aetheris.Kernel.Core.Tests.Brep;

public sealed class BrepDiametralCrossHoleTests
{
    private readonly ITestOutputHelper _output;

    public BrepDiametralCrossHoleTests(ITestOutputHelper output) => _output = output;

    [Theory]
    [InlineData(2d, 0.2d)]
    [InlineData(2d, 0.6d)]
    [InlineData(2d, 1.8d)]
    [InlineData(0.79375d, 0.5953125d)]
    public void OneSolidCrossHole_BuildsQualifiedManifoldTopology(double radius, double holeRadius)
    {
        var z = Direction3D.Create(new Vector3D(0, 0, 1));
        var x = Direction3D.Create(new Vector3D(1, 0, 0));
        var stock = new CylinderSurface(new Point3D(0, 0, -5), z, radius, x);
        var result = BrepDiametralCrossHole.Build(stock, 10d, x, holeRadius, 5d);
        Assert.True(result.IsSuccess, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        var body = result.Value;
        Assert.Equal(4, body.Topology.Faces.Count());
        Assert.Equal(8, body.Topology.Edges.Count());
        Assert.Equal(6, body.Topology.Vertices.Count());
        Assert.Equal(2, body.Bindings.FaceBoundaryRoleBindings.Count(binding => binding.Role == FaceBoundaryRole.Inner));
        Assert.Equal(16, body.Topology.Coedges.Count());
        Assert.All(body.Topology.Edges, edge => Assert.Equal(2,
            body.Topology.Coedges.Count(coedge => coedge.EdgeId == edge.Id)));
        var intersectionEdges = body.Bindings.EdgeBindings
            .Where(binding => body.Geometry.GetCurve(binding.CurveGeometryId).CertifiedIntersection is not null)
            .ToArray();
        Assert.Equal(4, intersectionEdges.Length);
        Assert.Equal(2, intersectionEdges.Select(binding => binding.CurveGeometryId).Distinct().Count());
        Assert.True(BrepPcurveValidator.Validate(body, requireEveryCoedge: true).IsValid);
        Assert.True(BrepExportPreflight.Validate(body).IsValid);
        var mass = BrepMassProperties.Evaluate(body);
        Assert.True(mass.IsEnclosed, string.Join("; ", mass.Topology.Messages));
        Assert.True(mass.IsOrientationConsistent, string.Join("; ", mass.Topology.Messages));
        Assert.True(mass.AbsoluteVolume > 0d);
        Assert.True(mass.SignedVolume > 0d);
        Assert.True(mass.AbsoluteVolume < double.Pi * radius * radius * 10d);
    }

    [Theory]
    [InlineData(0d, 0d, 1d, 1d, 0d, 0d, 3d)]
    [InlineData(0d, 0d, 1d, 0d, 1d, 0d, 7d)]
    [InlineData(0d, 1d, 0d, 1d, 0d, 0d, 5d)]
    public void AxialPositionRotationAndRigidFrame_StayClosed(
        double zx, double zy, double zz, double xx, double xy, double xz, double position)
    {
        var axis = Direction3D.Create(new Vector3D(zx, zy, zz));
        var cutter = Direction3D.Create(new Vector3D(xx, xy, xz));
        var stock = new CylinderSurface(new Point3D(7, -3, 11), axis, 2d,
            Direction3D.Create(axis.ToVector().Cross(cutter.ToVector())));
        var result = BrepDiametralCrossHole.Build(stock, 10d, cutter, 0.6d, position);
        Assert.True(result.IsSuccess, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        var body = result.Value;
        Assert.True(BrepPcurveValidator.Validate(body, requireEveryCoedge: true).IsValid);
        Assert.True(BrepExportPreflight.Validate(body).IsValid);
        var mass = BrepMassProperties.Evaluate(body);
        Assert.True(mass.IsEnclosed && mass.IsOrientationConsistent);
        Assert.True(mass.SignedVolume > 0d);
    }

    [Fact]
    public void InvalidPlacement_FailsExplicitly()
    {
        var z = Direction3D.Create(new Vector3D(0, 0, 1));
        var x = Direction3D.Create(new Vector3D(1, 0, 0));
        var stock = new CylinderSurface(new Point3D(0, 0, -5), z, 2d, x);
        Assert.Contains(BrepDiametralCrossHole.Build(stock, 10d, x, 0d, 5d).Diagnostics,
            diagnostic => diagnostic.Source == "Brep.CrossHole.InvalidDimension");
        Assert.Contains(BrepDiametralCrossHole.Build(stock, 10d, x, 0.6d, 0.6d).Diagnostics,
            diagnostic => diagnostic.Source == "Brep.CrossHole.EndCollision");
        Assert.Contains(BrepDiametralCrossHole.Build(stock, 10d,
            Direction3D.Create(new Vector3D(1, 0, 1)), 0.6d, 5d).Diagnostics,
            diagnostic => diagnostic.Source == "Brep.CrossHole.ObliqueAxis");
        Assert.False(BrepDiametralCrossHole.Build(stock, 10d, x, 2d, 5d).IsSuccess);
        Assert.False(BrepDiametralCrossHole.Build(stock, 10d, x, 2.1d, 5d).IsSuccess);
    }

    [Fact]
    public void CanonicalBody_TessellatesAndExportsRoundTrip()
    {
        var z = Direction3D.Create(new Vector3D(0, 0, 1));
        var x = Direction3D.Create(new Vector3D(1, 0, 0));
        var body = BrepDiametralCrossHole.Build(
            new CylinderSurface(new Point3D(0, 0, -5), z, 2d, x), 10d, x, 0.6d, 5d).Value;
        var display = BrepDisplayTessellator.TessellateBounded(body, executionTimeout: TimeSpan.FromSeconds(10));
        Assert.True(display.IsSuccess, string.Join("; ", display.Diagnostics.Select(d => d.Message)));
        Assert.Equal(4, display.Value.FacePatches.Count);
        Assert.All(display.Value.FacePatches, patch => Assert.NotEmpty(patch.TriangleIndices));
        var step = Step242Exporter.ExportBody(body);
        Assert.True(step.IsSuccess, string.Join("; ", step.Diagnostics.Select(d => d.Message)));
        Assert.Equal(step.Value, Step242Exporter.ExportBody(body).Value);
        var debugOutput = Environment.GetEnvironmentVariable("AETHERIS_CROSSHOLE_STEP_OUTPUT");
        if (!string.IsNullOrWhiteSpace(debugOutput)) File.WriteAllText(debugOutput, step.Value);
        var cylinderLoops = new List<Step242Importer.LoopRoleCylinderProjectionDiagnostic>();
        using var diagnostics = Step242Importer.CaptureLoopRoleCylinderProjectionDiagnostics(cylinderLoops);
        var imported = Step242Importer.ImportBody(step.Value);
        foreach (var loop in cylinderLoops)
            _output.WriteLine($"face={loop.FaceEntityId} loop={loop.LoopId} points={loop.PointCount} unique={loop.UniquePointCount} area={loop.SignedArea:G17} spanU={loop.AngularSpan:G17} spanV={loop.AxialSpan:G17} seam={loop.SeamCrossings} degenerate={loop.Degeneracy}");
        Assert.True(imported.IsSuccess, string.Join("; ", imported.Diagnostics.Select(d => d.Message)));
        var importedMass = BrepMassProperties.Evaluate(imported.Value);
        Assert.True(importedMass.IsEnclosed);
        Assert.True(importedMass.IsOrientationConsistent);
        Assert.True(importedMass.SignedVolume > 0d);
        Assert.True(BrepPcurveValidator.Validate(imported.Value, requireEveryCoedge: true).IsValid);
        Assert.Equal(2, imported.Value.Bindings.FaceBoundaryRoleBindings.Count(binding => binding.Role == FaceBoundaryRole.Inner));
        var cylinderRadii = imported.Value.Bindings.FaceBindings
            .Select(binding => imported.Value.Geometry.GetSurface(binding.SurfaceGeometryId))
            .Where(surface => surface.Kind == SurfaceGeometryKind.Cylinder)
            .Select(surface => surface.Cylinder!.Value.Radius)
            .OrderBy(radius => radius).ToArray();
        Assert.Equal(2, cylinderRadii.Length);
        Assert.Equal(0.6d, cylinderRadii[0], 10);
        Assert.Equal(2d, cylinderRadii[1], 10);
    }
}
