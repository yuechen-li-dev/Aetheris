using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests.Integration;

public sealed class ConstructionPlaneExtrudeTests
{
    [Fact]
    public void TracedPositiveXPlane_MapsLocalProfileAndEmitsExactWorldXCaps()
    {
        var plane = Trace("side", new(10, 2, 3), new(1, 0, 0), new(0, 0, 1));
        Assert.Equal(1d, plane.Determinant, 12);
        Assert.Equal((10d, 4d, 6d), Tuple(plane.ToWorld((2, 3))));
        Assert.Equal((2d, 3d, 0d), plane.ToLocal(plane.ToWorld((2, 3))));

        var result = LineArcProfileExtrudeEmitter.TryEmit(new LineArcProfileExtrudeRequest([Rectangle()], 4, plane));
        var body = Assert.IsType<Aetheris.Kernel.Core.Brep.BrepBody>(result.Body);
        var capNormals = body.Geometry.Surfaces.Select(x => x.Value).Where(x => x.Kind == SurfaceGeometryKind.Plane).Take(2).Select(x => x.Plane!.Value.Normal.ToVector()).ToArray();
        Assert.Contains(capNormals, n => Math.Abs(n.X - 1) < 1e-10);
        Assert.Contains(capNormals, n => Math.Abs(n.X + 1) < 1e-10);
        Assert.All(Points(body), point => Assert.InRange(point.X, 8d, 12d));
        var step = Step242Exporter.ExportBody(body);
        Assert.True(step.IsSuccess);
        Assert.Contains("AXIS2_PLACEMENT_3D", step.Value!);
        Assert.True(Step242Importer.ImportBody(step.Value!).IsSuccess);
    }

    [Fact]
    public void ArbitraryProperPlane_PreservesCircularRadiusAndAxis()
    {
        // Normal (0, 3/5, 4/5) and an X hint make a well-conditioned deterministic proper frame.
        var plane = Trace("rotated", new(7, -2, 11), new(0, .6, .8), new(1, 0, 0));
        var loop = new LineArcProfileLoop2D([
            new LineArcLineSegment2D((-3, 2), (3, 2)),
            new LineArcCircularArc2D((3, 0), 2, Math.PI / 2, -Math.PI),
            new LineArcLineSegment2D((3, -2), (-3, -2)),
            new LineArcCircularArc2D((-3, 0), 2, -Math.PI / 2, -Math.PI)], false);
        var result = LineArcProfileExtrudeEmitter.TryEmit(new LineArcProfileExtrudeRequest([loop], 5, plane));
        var body = Assert.IsType<Aetheris.Kernel.Core.Brep.BrepBody>(result.Body);
        var cylinders = body.Geometry.Surfaces.Select(x => x.Value.Cylinder).Where(x => x is not null).Select(x => x!.Value).ToArray();
        Assert.Equal(2, cylinders.Length);
        Assert.All(cylinders, cylinder =>
        {
            Assert.Equal(2d, cylinder.Radius, 12);
            Assert.Equal(1d, cylinder.Axis.ToVector().Dot(plane.AxisZ.ToVector()), 12);
        });
        Assert.All(Points(body), point => { var local = plane.ToLocal(point); Assert.Equal(point, plane.ToWorld((local.X, local.Y), local.Z), new Point3Comparer()); });
    }

    [Fact]
    public void ProfileUsingConstructionPlane_ParsesTraceAndRetainsProvenance()
    {
        const string source = """
            Concept Struct SideLayout {
                Datum: Plane { Origin: [10mm, 2mm, 3mm]; Normal: [1, 0, 0]; Up: [0, 0, 1] }
            }
            Construction Plane PositiveXWorkplane { Trace: SideLayout.Datum }
            Profile SideProfile Using PositiveXWorkplane {
                Rect2 Outline { Center: [0mm, 0mm]; Size: [4mm, 2mm] }
                Segment Bottom { Trace: Outline.Bottom; From: Outline.BottomLeft; To: Outline.BottomRight }
                Segment Right { Trace: Outline.Right; From: Outline.BottomRight; To: Outline.TopRight }
                Segment Top { Trace: Outline.Top; From: Outline.TopRight; To: Outline.TopLeft }
                Segment Left { Trace: Outline.Left; From: Outline.TopLeft; To: Outline.BottomLeft }
            }
            Extrude Solid { Profile: SideProfile; From: -2mm; To: 2mm }
            """;
        var parsed = ProfileAuthoringParser.Parse(source);
        var profile = Assert.IsType<ResolvedProfile2D>(parsed.Profile);
        Assert.Equal("construction:PositiveXWorkplane", profile.EffectiveConstructionPlane.StableId);
        var emitted = ResolvedProfile2DValidator.Extrude(profile, parsed.Height);
        Assert.Contains(emitted.Correspondence!.ProvenanceChain, x => x.StartsWith("ConceptPlane:", StringComparison.Ordinal));
        Assert.Contains(emitted.Correspondence.Descendants, x => x.Role == SemanticTopologyRole.LocalEndBoundary);
    }

    [Fact]
    public void M8_EquivalentFramesPreserveMassAndMapCentroid()
    {
        var frames = new[]
        {
            ConstructionPlane.WorldXY,
            Trace("x", new(10, 2, 3), new(1, 0, 0), new(0, 0, 1)),
            Trace("r", new(7, -2, 11), new(0, .6, .8), new(1, 0, 0)),
        };
        var results = frames.Select(frame => BrepMassProperties.Evaluate(Assert.IsType<Aetheris.Kernel.Core.Brep.BrepBody>(LineArcProfileExtrudeEmitter.TryEmit(new LineArcProfileExtrudeRequest([Rectangle()], 4, frame)).Body))).ToArray();
        Assert.All(results, mass => { Assert.NotEqual(BrepMassPropertiesStatus.Unavailable, mass.Status); Assert.True(mass.IsEnclosed); Assert.True(mass.IsOrientationConsistent); });
        Assert.All(results.Skip(1), mass => { Assert.Equal(results[0].AbsoluteVolume, mass.AbsoluteVolume, 6); Assert.Equal(results[0].SurfaceArea, mass.SurfaceArea, 6); });
        for (var i = 0; i < frames.Length; i++)
        {
            Assert.True(results[i].Centroid.HasValue);
            var centroid = results[i].Centroid!.Value;
            Assert.Equal(frames[i].Origin.X, centroid.X, 6); Assert.Equal(frames[i].Origin.Y, centroid.Y, 6); Assert.Equal(frames[i].Origin.Z, centroid.Z, 6);
        }
    }

    private static ConstructionPlane Trace(string id, ConceptIrPoint3 origin, ConceptIrVector3 normal, ConceptIrVector3 up)
    {
        Assert.True(ConstructionPlane.TryTrace("construction:" + id, new("concept:" + id, origin, normal, "fixture", up), "fixture", out var plane, out var diagnostic), diagnostic);
        return plane!;
    }

    private static LineArcProfileLoop2D Rectangle() => new([
        new LineArcLineSegment2D((-2, -1), (2, -1)), new LineArcLineSegment2D((2, -1), (2, 1)),
        new LineArcLineSegment2D((2, 1), (-2, 1)), new LineArcLineSegment2D((-2, 1), (-2, -1))], false);
    private static (double, double, double) Tuple(Aetheris.Kernel.Core.Math.Point3D p) => (p.X, p.Y, p.Z);
    private static IReadOnlyList<Aetheris.Kernel.Core.Math.Point3D> Points(Aetheris.Kernel.Core.Brep.BrepBody body) => body.Topology.Vertices.Select(vertex => { Assert.True(body.TryGetVertexPoint(vertex.Id, out var point)); return point; }).ToArray();
    private sealed class Point3Comparer : IEqualityComparer<Aetheris.Kernel.Core.Math.Point3D>
    { public bool Equals(Aetheris.Kernel.Core.Math.Point3D x, Aetheris.Kernel.Core.Math.Point3D y) => Math.Abs(x.X-y.X)<1e-10 && Math.Abs(x.Y-y.Y)<1e-10 && Math.Abs(x.Z-y.Z)<1e-10; public int GetHashCode(Aetheris.Kernel.Core.Math.Point3D obj) => 0; }
}
