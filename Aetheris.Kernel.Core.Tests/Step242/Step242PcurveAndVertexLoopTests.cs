using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242PcurveAndVertexLoopTests
{
    [Fact]
    public void UnsupportedPcurveForm_IsDiagnosedInsteadOfDropped()
    {
        var path = Path.Combine(Step242CorpusManifestRunner.RepoRoot(), "fixtures", "Canonical", "Surfacing", "g2-planar-plateau.step");
        var source = File.ReadAllText(path);
        var index = source.IndexOf("POLYLINE(", StringComparison.Ordinal);
        Assert.True(index >= 0);
        var hostile = string.Concat(source.AsSpan(0, index), "PARABOLA(", source.AsSpan(index + "POLYLINE(".Length));

        var imported = Step242Importer.ImportBody(hostile);

        Assert.False(imported.IsSuccess);
        Assert.Contains(imported.Diagnostics, diagnostic => diagnostic.Source == "Importer.Pcurve.UnsupportedCurve");
    }

    [Fact]
    public void ImportedPlateauPcurves_AreFirstClassAndRoundTrip()
    {
        var imported = Step242Importer.ImportBody(File.ReadAllText(Path.Combine(
            Step242CorpusManifestRunner.RepoRoot(), "fixtures", "Canonical", "Surfacing", "g2-planar-plateau.step")));
        Assert.True(imported.IsSuccess, string.Join(" | ", imported.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.Equal(144, imported.Value.Bindings.PcurveBindings.Count());
        Assert.Contains(imported.Value.Bindings.PcurveBindings, binding => binding.Pcurve.Kind == PcurveGeometryKind.Polynomial);
        Assert.All(imported.Value.Bindings.PcurveBindings, binding => Assert.NotNull(binding.SourceStepPcurveEntityId));

        var exported = Step242Exporter.ExportBody(imported.Value);
        Assert.True(exported.IsSuccess, string.Join(" | ", exported.Diagnostics.Select(diagnostic => diagnostic.Message)));
        var reimported = Step242Importer.ImportBody(exported.Value);
        Assert.True(reimported.IsSuccess, string.Join(" | ", reimported.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.Equal(144, reimported.Value.Bindings.PcurveBindings.Count());
    }

    [Fact]
    public void CylinderSeam_PreservesTwoUsesAndExactRationalPcurve()
    {
        var source = AddCylinderPcurves(BrepPrimitives.CreateCylinder(2d, 10d).Value);
        var exported = Step242Exporter.ExportBody(source);
        Assert.True(exported.IsSuccess, string.Join(" | ", exported.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.Contains("SEAM_CURVE", exported.Value, StringComparison.Ordinal);
        Assert.Contains("RATIONAL_B_SPLINE_CURVE", exported.Value, StringComparison.Ordinal);

        var reimported = Step242Importer.ImportBody(exported.Value);
        Assert.True(reimported.IsSuccess, string.Join(" | ", reimported.Diagnostics.Select(diagnostic => diagnostic.Message)));
        var cylinderFace = Assert.Single(reimported.Value.Topology.Faces,
            face => reimported.Value.GetFaceSurface(face.Id).Kind == SurfaceGeometryKind.Cylinder);
        var seamEdge = Assert.Single(reimported.Value.GetEdges(cylinderFace.Id),
            edgeId => reimported.Value.GetEdgeCurve(edgeId).Kind == CurveGeometryKind.Line3);
        var seamBindings = cylinderFace.LoopIds.SelectMany(loopId => reimported.Value.Topology.GetLoop(loopId).CoedgeIds)
            .Where(coedgeId => reimported.Value.Topology.GetCoedge(coedgeId).EdgeId == seamEdge)
            .Select(reimported.Value.Bindings.GetPcurveBinding).ToArray();
        Assert.Equal(2, seamBindings.Length);
        var firstU = seamBindings[0].Pcurve.Evaluate(seamBindings[0].Pcurve.Domain.Start).U;
        var secondU = seamBindings[1].Pcurve.Evaluate(seamBindings[1].Pcurve.Domain.Start).U;
        Assert.NotEqual(firstU, secondU);
        Assert.True(SurfacePeriodicity.EquivalentModuloPeriod(firstU, secondU, double.Tau, 1e-10d));
        var seamUses = cylinderFace.LoopIds.SelectMany(loopId => reimported.Value.Topology.GetLoop(loopId).CoedgeIds)
            .Select(reimported.Value.Topology.GetCoedge).Where(coedge => coedge.EdgeId == seamEdge).ToArray();
        Assert.Contains(seamUses, use => use.IsReversed);
        Assert.Contains(seamUses, use => !use.IsReversed);
        var rational = Assert.Single(seamBindings, binding => binding.Pcurve.RationalWeights is not null);
        Assert.Equal([1d, 1d], rational.Pcurve.RationalWeights);
        Assert.Equal(1, rational.Pcurve.PolynomialCurve!.Value.Degree);
        Assert.Contains(reimported.Value.Bindings.PcurveBindings, binding => binding.SourceCurveType == "CIRCLE");
        Assert.Contains(reimported.Value.Bindings.PcurveBindings, binding => binding.SourceCurveType == "TRIMMED_CURVE");
    }

    [Fact]
    public void SubTolerancePcurveDrift_IsAcceptedButStillMeasured()
    {
        var source = AddCylinderPcurves(BrepPrimitives.CreateCylinder(2d, 10d).Value, 1e-7d);
        var exported = Step242Exporter.ExportBody(source);
        Assert.True(exported.IsSuccess);
        var reimported = Step242Importer.ImportBody(exported.Value);
        Assert.True(reimported.IsSuccess, string.Join(" | ", reimported.Diagnostics.Select(diagnostic => diagnostic.Message)));
        var evidence = BrepPcurveValidator.Validate(reimported.Value, 1e-6d);
        Assert.True(evidence.IsValid, string.Join(" | ", evidence.Diagnostics));
        Assert.InRange(evidence.MaximumReconstructionDeviation, 0d, 1e-6d);
    }

    [Fact]
    public void SpherePoleVertexLoop_RoundTripsWithoutFakeEdge()
    {
        var imported = Import("testdata/step242/handcrafted/baseline/sphere.step");
        var exported = Step242Exporter.ExportBody(imported);
        Assert.True(exported.IsSuccess, string.Join(" | ", exported.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.Contains("VERTEX_LOOP", exported.Value, StringComparison.Ordinal);
        var reimported = Step242Importer.ImportBody(exported.Value);
        Assert.True(reimported.IsSuccess, string.Join(" | ", reimported.Diagnostics.Select(diagnostic => diagnostic.Message)));
        var loop = Assert.Single(reimported.Value.Topology.Loops);
        Assert.Equal(LoopKind.Vertex, loop.Kind);
        Assert.Empty(reimported.Value.Topology.Edges);
        Assert.True(reimported.Value.Bindings.TryGetVertexLoopParameterBinding(loop.Id, out var binding));
        Assert.NotNull(binding.Parameter);
    }

    [Fact]
    public void ConeApexVertexLoops_ArePreservedAndStc06RemainsAmbiguous()
    {
        var imported = Import("testdata/step242/nist/STC/nist_stc_06_asme1_ap242-e3.stp");
        var apexLoops = imported.Topology.Loops.Where(loop => loop.Kind == LoopKind.Vertex).ToArray();
        Assert.Equal(2, apexLoops.Length);
        Assert.All(apexLoops, loop =>
        {
            var binding = imported.Bindings.GetVertexLoopParameterBinding(loop.Id);
            Assert.Equal(SurfaceGeometryKind.Cone, imported.GetFaceSurface(binding.FaceId).Kind);
        });
        Assert.Contains(imported.FaceOrientationReport!.Shells,
            shell => shell.Qualification == FaceOrientationQualification.Ambiguous);

        var exported = Step242Exporter.ExportBody(imported);
        Assert.True(exported.IsSuccess, string.Join(" | ", exported.Diagnostics.Select(diagnostic => diagnostic.Message)));
        var reimported = Step242Importer.ImportBody(exported.Value);
        Assert.True(reimported.IsSuccess, string.Join(" | ", reimported.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.Equal(2, reimported.Value.Topology.Loops.Count(loop => loop.Kind == LoopKind.Vertex));
    }

    private static BrepBody Import(string relativePath)
    {
        var result = Step242Importer.ImportBody(File.ReadAllText(Path.Combine(
            Step242CorpusManifestRunner.RepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar))));
        Assert.True(result.IsSuccess, string.Join(" | ", result.Diagnostics.Select(diagnostic => diagnostic.Message)));
        return result.Value;
    }

    private static BrepBody AddCylinderPcurves(BrepBody source, double seamOffset = 0d)
    {
        var bindings = new BrepBindingModel();
        foreach (var binding in source.Bindings.EdgeBindings) bindings.AddEdgeBinding(binding);
        foreach (var binding in source.Bindings.FaceBindings) bindings.AddFaceBinding(binding);
        var cylinderFace = source.Topology.Faces.Single(face => source.GetFaceSurface(face.Id).Kind == SurfaceGeometryKind.Cylinder);
        var surfaceBinding = bindings.GetFaceBinding(cylinderFace.Id);
        var cylinder = source.GetFaceSurface(cylinderFace.Id).Cylinder!.Value;
        var lineUseIndex = 0;
        foreach (var coedgeId in source.Topology.GetLoop(cylinderFace.LoopIds.Single()).CoedgeIds)
        {
            var coedge = source.Topology.GetCoedge(coedgeId);
            var edgeBinding = bindings.GetEdgeBinding(coedge.EdgeId);
            var domain = edgeBinding.TrimInterval!.Value;
            var curve = source.GetEdgeCurve(coedge.EdgeId);
            PcurveGeometry pcurve;
            if (curve.Kind == CurveGeometryKind.Line3)
            {
                var u = lineUseIndex++ == 0 ? seamOffset : double.Tau;
                var start = Project(cylinder, curve.Line3!.Value.Evaluate(domain.Start));
                var end = Project(cylinder, curve.Line3.Value.Evaluate(domain.End));
                if (u == 0d)
                {
                    var spline = new BSpline3Curve(1,
                        [new Point3D(u, start.V, 0d), new Point3D(u, end.V, 0d)],
                        [2, 2], [domain.Start, domain.End], "UNSPECIFIED", false, false, "UNSPECIFIED");
                    pcurve = PcurveGeometry.RationalPolynomial(domain, spline, [1d, 1d]);
                }
                else pcurve = PcurveGeometry.Line(domain, new(u, start.V), new(u, end.V));
            }
            else
            {
                var circle = curve.Circle3!.Value;
                var start = Project(cylinder, circle.Evaluate(domain.Start));
                var quarter = Project(cylinder, circle.Evaluate(domain.Start + (domain.End - domain.Start) * 0.25d));
                var direction = SurfacePeriodicity.EquivalentModuloPeriod(quarter.U, start.U + double.Pi / 2d, double.Tau, 1e-8d) ? 1d : -1d;
                pcurve = PcurveGeometry.Line(domain, start, new(start.U + direction * double.Tau, start.V));
            }
            bindings.AddPcurveBinding(new CoedgePcurveBinding(coedgeId, cylinderFace.Id, surfaceBinding.SurfaceGeometryId, pcurve));
        }
        foreach (var capFace in source.Topology.Faces.Where(face => source.GetFaceSurface(face.Id).Kind == SurfaceGeometryKind.Plane))
        {
            var capSurfaceBinding = bindings.GetFaceBinding(capFace.Id);
            var plane = source.GetFaceSurface(capFace.Id).Plane!.Value;
            var coedgeId = source.Topology.GetLoop(capFace.LoopIds.Single()).CoedgeIds.Single();
            var coedge = source.Topology.GetCoedge(coedgeId);
            var edgeBinding = bindings.GetEdgeBinding(coedge.EdgeId);
            var domain = edgeBinding.TrimInterval!.Value;
            var circle = source.GetEdgeCurve(coedge.EdgeId).Circle3!.Value;
            SurfaceParameterPoint ProjectPlane(Point3D point)
            {
                var offset = point - plane.Origin;
                return new(offset.Dot(plane.UAxis.ToVector()), offset.Dot(plane.VAxis.ToVector()));
            }
            var center = ProjectPlane(circle.Center);
            var cosinePoint = ProjectPlane(circle.Evaluate(0d));
            var sinePoint = ProjectPlane(circle.Evaluate(double.Pi / 2d));
            var cosine = new SurfaceParameterPoint(cosinePoint.U - center.U, cosinePoint.V - center.V);
            var sine = new SurfaceParameterPoint(sinePoint.U - center.U, sinePoint.V - center.V);
            var pcurve = PcurveGeometry.Ellipse(domain, center, cosine, sine);
            bindings.AddPcurveBinding(new CoedgePcurveBinding(coedgeId, capFace.Id, capSurfaceBinding.SurfaceGeometryId, pcurve));
        }
        var points = source.Topology.Vertices.ToDictionary(vertex => vertex.Id, vertex =>
        {
            source.TryGetVertexPoint(vertex.Id, out var point);
            return point;
        });
        return new BrepBody(source.Topology, source.Geometry, bindings, points, source.SafeBooleanComposition, source.ShellRepresentation, source.FaceOrientationReport);
    }

    private static SurfaceParameterPoint Project(CylinderSurface cylinder, Point3D point)
    {
        var offset = point - cylinder.Origin;
        var axial = offset.Dot(cylinder.Axis.ToVector());
        var radial = offset - cylinder.Axis.ToVector() * axial;
        var u = double.Atan2(radial.Dot(cylinder.YAxis.ToVector()), radial.Dot(cylinder.XAxis.ToVector()));
        if (u < 0d) u += double.Tau;
        return new(u, axial);
    }
}
