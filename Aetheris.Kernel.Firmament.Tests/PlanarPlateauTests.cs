using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Surfacing;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class PlanarPlateauTests
{
    [Fact]
    public void MixedFootprintProducesExactContactsAndG2PatchJoins()
    {
        var source = FirmamentCorpusHarness.ReadFixtureText("fixtures/Canonical/SectionChain/smooth-corner-sections.firmament");
        var input = SectionChainAuthoringParser.Compile(source, materialize: false);
        Assert.True(input.IsSuccess, string.Join("\n", input.Diagnostics));
        var section = input.Chain!.Sections[0];
        var result = PlanarPlateauContacts.Build("Plateau", section.Profile, section.Frame, 3, 2, "Body.Top", "Plateau.Top");
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics));
        Assert.Equal(12, result.Plan!.Spans.Count);
        Assert.All(result.Plan.Joins, j => Assert.True(j.Status == "G2WithinSampledTolerance", $"{j.BoundaryIdentity}: {j.Status} {j.MaximumNormalAngleDegrees} {j.MaximumShapeOperatorResidual}"));
    }
    [Fact]
    public void MixedPlateauGraftsTrimmedManifoldAndPreservesBaseTopology()
    {
        var input = SectionChainAuthoringParser.Compile(FirmamentCorpusHarness.ReadFixtureText("fixtures/Canonical/SectionChain/smooth-corner-sections.firmament"), false).Chain!.Sections[0];
        var source = BrepPrimitives.CreateBox(100, 80, 5).Value;
        var top = source.Bindings.FaceBindings.Single(b => source.Geometry.GetSurface(b.SurfaceGeometryId).Plane is { } p && p.Normal.ToVector().Z > .99);
        var frame = input.Frame with { Origin = new Point3D(0, 0, 2.5) };
        var plan = PlanarPlateauContacts.Build("Plateau", input.Profile, frame, 3, 2, "Body.Top", "Plateau.Top");
        Assert.True(plan.IsSuccess, string.Join("\n", plan.Diagnostics));
        var built = PlanarPlateauMaterializer.Apply(source, top.FaceId, plan.Plan!);
        Assert.True(built.IsSuccess, string.Join("\n", built.Diagnostics));
        Assert.True(built.Pcurves!.LoopClosureValid);
        Assert.True(built.Pcurves.MaximumReconstructionDeviation < 1e-9);
        foreach (var edge in source.Topology.Edges) Assert.Equal(edge, built.Body!.Topology.GetEdge(edge.Id));
        foreach (var face in source.Topology.Faces.Where(f => f.Id != top.FaceId)) Assert.Equal(face, built.Body!.Topology.GetFace(face.Id));
        var polynomialTrims = built.Body!.Bindings.PcurveBindings.Where(pc => pc.Pcurve.Kind == PcurveGeometryKind.Polynomial).ToArray();
        Assert.Equal(24, polynomialTrims.Length);
        foreach (var pc in polynomialTrims)
        {
            var edge = built.Body.Bindings.GetEdgeBinding(built.Body.Topology.GetCoedge(pc.CoedgeId).EdgeId);
            var curve = built.Body.Geometry.GetCurve(edge.CurveGeometryId).BSpline3!.Value;
            var uv = pc.Pcurve.PolynomialCurve!.Value;
            var plane = built.Body.Geometry.GetSurface(pc.SurfaceGeometryId).Plane!.Value;
            Assert.Equal(curve.Degree, uv.Degree);
            Assert.Equal(curve.KnotValues, uv.KnotValues);
            Assert.Equal(curve.KnotMultiplicities, uv.KnotMultiplicities);
            for (var i = 0; i < curve.ControlPoints.Count; i++)
            {
                var reconstructed = plane.Origin + plane.UAxis.ToVector() * uv.ControlPoints[i].X + plane.VAxis.ToVector() * uv.ControlPoints[i].Y;
                Assert.True((reconstructed - curve.ControlPoints[i]).Length < 1e-12);
            }
        }
        var exported = Step242Exporter.ExportBody(built.Body!);
        Assert.True(exported.IsSuccess, string.Join("\n", exported.Diagnostics.Select(d => d.Message)));

    }
    [Theory]
    [InlineData(0.1)]
    [InlineData(1)]
    [InlineData(10)]
    public void SemanticPlateauWorksAtSeveralScales(double scale)
    {
        var source = FirmamentCorpusHarness.ReadFixtureText("fixtures/Canonical/Surfacing/g2-planar-plateau.firmament");
        source = System.Text.RegularExpressions.Regex.Replace(source, @"([-+]?\d+(?:\.\d+)?)mm", m =>
            (double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) * scale).ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "mm");
        var built = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(built.IsSuccess, string.Join("\n", built.Diagnostics.Select(d => d.Message)));
        Assert.True(built.Value.Plateau!.ReimportManifold);
        Assert.All(built.Value.Plateau.Contacts.Joins, j => Assert.Equal("G2WithinSampledTolerance", j.Status));
        Assert.Equal(built.Value.StepText, FirmamentBuildAndExport.CompileSource(source).Value.StepText);
    }

    [Theory]
    [InlineData("Width: 2mm", "Width: 0mm", "plateau-width-must-be-positive")]
    [InlineData("Width: 2mm", "Width: -1mm", "plateau-width-must-be-positive")]
    [InlineData("Width: 2mm", "Width: 20mm", "plateau-width-collapses-footprint")]
    [InlineData("Height: 3mm", "Height: 0mm", "plateau-height-must-be-positive")]
    [InlineData("Size: [60mm,40mm]", "Size: [120mm,40mm]", "plateau-contact-outside-support")]
    public void InvalidPlateausFailBeforeExport(string from, string to, string error)
    {
        var source = FirmamentCorpusHarness.ReadFixtureText("fixtures/Canonical/Surfacing/g2-planar-plateau.firmament").Replace(from, to);
        var result = FirmamentBuildAndExport.CompileSource(source);
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Message == error);
    }

    [Fact]
    public void TightCornersAndStraightOnlyLoopsShareTheSamePath()
    {
        var source = FirmamentCorpusHarness.ReadFixtureText("fixtures/Canonical/Surfacing/g2-planar-plateau.firmament");
        var tight = FirmamentBuildAndExport.CompileSource(source.Replace("CornerExtent: 8mm", "CornerExtent: 1mm"));
        Assert.True(tight.IsSuccess, string.Join("\n", tight.Diagnostics.Select(d => d.Message)));
        Assert.All(tight.Value.Plateau!.Contacts.Joins, j => Assert.Equal("G2WithinSampledTolerance", j.Status));
        var straight = FirmamentBuildAndExport.CompileSource(source.Replace("SmoothRoundedRect2 Footprint", "Rect2 Footprint").Replace(" CornerExtent: 8mm", ""));
        Assert.True(straight.IsSuccess, string.Join("\n", straight.Diagnostics.Select(d => d.Message)));
        Assert.Equal(4, straight.Value.Plateau!.Contacts.Spans.Count);
        Assert.All(straight.Value.Plateau.Contacts.Joins.Where(j => j.BoundaryKind != "NeighboringFootprintSpans"), j => Assert.Equal("G2WithinSampledTolerance", j.Status));
        Assert.Contains(straight.Value.Plateau.Contacts.Joins, j => j.BoundaryKind == "NeighboringFootprintSpans" && j.Status == "G0");
    }

    [Theory]
    [InlineData("Size: [60mm,40mm]", "Size: [65mm,35mm]")]
    [InlineData("Height: 3mm", "Height: 2.5mm")]
    [InlineData("Width: 2mm", "Width: 1.2mm")]
    [InlineData("Rect2 BaseBoundary { Center: [0mm,0mm] Size: [100mm,80mm] }", "RoundedRect2 BaseBoundary { Center: [0mm,0mm] Size: [100mm,80mm] Radius: 18mm }")]
    public void ParametersRegenerateThroughOrdinaryPipeline(string from, string to)
    {
        var source = FirmamentCorpusHarness.ReadFixtureText("fixtures/Canonical/Surfacing/g2-planar-plateau.firmament");
        Assert.Contains(from, source);
        var result = FirmamentBuildAndExport.CompileSource(source.Replace(from, to));
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics.Select(d => d.Message)));
        Assert.True(result.Value.Plateau!.ReimportManifold);
        Assert.All(result.Value.Plateau.Contacts.Joins, j => Assert.Equal("G2WithinSampledTolerance", j.Status));
    }

    [Fact]
    public void DegenerateAndSelfCrossingFootprintsFailTyped()
    {
        var frame = SectionFrame.Create(Point3D.Origin, new(1, 0, 0), new(0, 1, 0));
        var points = new[] { new SectionPoint2D(-1, -1), new SectionPoint2D(1, 1), new SectionPoint2D(-1, 1), new SectionPoint2D(1, -1) };
        var spans = points.Select((p, i) => new SectionProfileSpan("S" + i, new SectionProfileCurve.Line(p, points[(i + 1) % 4]))).ToArray();
        var cross = PlanarPlateauContacts.Build("Bad", new("P", spans, "S0"), frame, 1, .1, "Base", "Top");
        Assert.False(cross.IsSuccess);
        Assert.Contains(cross.Diagnostics, d => d.StartsWith("plateau-footprint-"));
        spans[0] = new("S0", new SectionProfileCurve.PolynomialBSpline(3, [points[0], points[0], points[0], points[0]], [4, 4], [0, 1]));
        Assert.False(PlanarPlateauContacts.Build("Bad", new("P", spans, "S0"), frame, 1, .1, "Base", "Top").IsSuccess);
    }

    [Fact]
    public void PolynomialPcurvesKeepDegreeKnotsAndExactPlaneParameters()
    {
        var source = FirmamentCorpusHarness.ReadFixtureText("fixtures/Canonical/Surfacing/g2-planar-plateau.firmament");
        var built = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(built.IsSuccess);
        Assert.True(built.Value.Plateau!.Pcurves.MaximumReconstructionDeviation < 1e-9);
        // Export retains 2D B-spline entities; it no longer samples polynomial trims.
        Assert.Contains("B_SPLINE_CURVE_WITH_KNOTS", built.Value.StepText);
        Assert.Contains("DEFINITIONAL_REPRESENTATION", built.Value.StepText);
    }
}

// The 35-face display export takes about one second alone but can consume the five-second
// production budget under concurrent geometry tests. Keep its real-path budget and run this
// integration case without competing test collections.
[CollectionDefinition("PlanarPlateauDisplayExport", DisableParallelization = true)]
public sealed class PlanarPlateauDisplayExportCollection;

[Collection("PlanarPlateauDisplayExport")]
public sealed class PlanarPlateauDisplayExportTests
{
    [Fact]
    public void FreshAuthoredPhoneUsesGenericPlateauAndMeshesEveryFace()
    {
        var path = FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Experiments/IPhone17ProMax/refined-plateau.firmament");
        var compiled = new Aetheris.Kernel.Firmament.Assembly.AssemblyM1Pipeline().CompileFile(path);
        Assert.True(compiled.IsSuccess, string.Join("\n", compiled.Diagnostics.Select(d => d.Message)));
        var body = compiled.Geometry!.DefinitionBodies.Single(b => b.Key.StartsWith("BodyPart<")).Value;
        Assert.Equal(35, body.Topology.Faces.Count());
        Assert.All(body.Topology.Edges, e => Assert.Equal(2, body.Topology.Coedges.Count(c => c.EdgeId == e.Id)));
        var mesh = Aetheris.Kernel.Firmament.Assembly.AssemblyDisplayMeshExporter.Export(compiled);
        Assert.Equal(16, mesh.Definitions.Count);
        Assert.All(mesh.Definitions, d => Assert.NotEmpty(d.Indices));
    }
}
