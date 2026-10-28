using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242StrictImportedExportTests
{
    [Theory, MemberData(nameof(Step242PmiTransferTests.NistFiles), MemberType = typeof(Step242PmiTransferTests)), Trait("Category", "SlowCorpus")]
    public void NistExactPartsPassStrictOutputWithoutRequiringCompleteSourceUvRecovery(string path)
    {
        var body = Step242Corpus.Body(path);
        var transfer = Step242PmiTransfer.Read(Step242Corpus.Text(path), body);
        Assert.True(transfer.IsSuccess, string.Join(";", transfer.Diagnostics.Select(d => d.Message)));
        var result = Step242Exporter.ExportBody(body, new Step242ExportOptions {
            ImportedPmi = transfer.Value, ImportedRecoveryToleranceMillimetres = .1,
            BrepExportPreflightMode = BrepExportPreflightMode.Enforce,
            BrepExportPreflightPolicy = BrepExportPreflightPolicy.TrustedProductionRoute });
        Assert.True(result.IsSuccess, path + ": " + string.Join(";", result.Diagnostics.Select(d => d.Message)));
        Assert.DoesNotContain("RATIONAL_B_SPLINE_CURVE", result.Value, StringComparison.Ordinal);
        // Any rational surface emission must originate in a native ellipsoid patch.
        var parsed = Step242SubsetParser.Parse(result.Value).Value;
        Assert.Equal(body.Geometry.Surfaces.Count(s => s.Value.Ellipsoid is not null),
            parsed.Entities.Count(e => Step242SubsetDecoder.TryGetConstructor(e.Instance, "RATIONAL_B_SPLINE_SURFACE") is not null));
        var roundtrip = Step242Importer.ImportBody(result.Value);
        Assert.True(roundtrip.IsSuccess, string.Join(";", roundtrip.Diagnostics.Select(d => d.Message)));
        Assert.Equal(body.Topology.Faces.Count(), roundtrip.Value.Topology.Faces.Count());
        Assert.Equal(body.Topology.Edges.Count(), roundtrip.Value.Topology.Edges.Count());
        var before = Step242SemanticPmiInspector.Inspect(Step242Corpus.Text(path)); var after = Step242SemanticPmiInspector.Inspect(result.Value);
        Assert.Equal(before.DatumCount, after.DatumCount); Assert.Equal(before.DimensionCount, after.DimensionCount);
        Assert.Equal(before.GeometricToleranceCount, after.GeometricToleranceCount);
        if (result.Diagnostics.Any(d => d.Message.StartsWith("step-export-recovered-pcurves-omitted:", StringComparison.Ordinal)))
            Assert.DoesNotContain(parsed.Entities, e => e.Name is "SURFACE_CURVE" or "SEAM_CURVE");
    }

    [Fact]
    public void InvalidPhysicalBoundaryStillFailsStrictExport()
    {
        var box = BrepPrimitives.CreateBox(10, 10, 10).Value;
        var moved = box.Topology.Vertices.First().Id;
        var points = box.Topology.Vertices.ToDictionary(v => v.Id, v => { box.TryGetVertexPoint(v.Id, out var p); return v.Id == moved ? p + new Aetheris.Kernel.Core.Math.Vector3D(1, 0, 0) : p; });
        var damaged = new BrepBody(box.Topology, box.Geometry, box.Bindings, points);
        var result = Step242Exporter.ExportBody(damaged, new Step242ExportOptions {
            BrepExportPreflightPolicy = BrepExportPreflightPolicy.TrustedProductionRoute });
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("brep-preflight-", StringComparison.Ordinal));
    }
    [Fact]
    public void InvalidOptionalUvTraceIsOmittedWithoutMutatingTheBody()
    {
        var stock = BrepPrimitives.CreateBox(10, 10, 10).Value;
        var source = Step242Exporter.ExportBody(stock, [new Step242SemanticPmiGeometricTolerance("Seat", "Seat", "Flatness", .03, [])
            { GeometricFaceIds = [stock.Topology.Faces.First().Id.Value] }]).Value;
        var original = Step242Importer.ImportBody(source).Value;
        var transfer = Step242PmiTransfer.Read(source, original).Value;
        var face = original.Topology.Faces.First(); var coedge = original.Topology.GetLoop(face.LoopIds[0]).CoedgeIds[0];
        var surface = original.Bindings.GetFaceBinding(face.Id).SurfaceGeometryId;
        var bad = new CoedgePcurveBinding(coedge, face.Id, surface, PcurveGeometry.Line(new(0, 1), new(999, 999), new(1000, 999)));
        var bindings = new BrepBindingModel();
        foreach (var binding in original.Bindings.EdgeBindings) bindings.AddEdgeBinding(binding);
        foreach (var binding in original.Bindings.FaceBindings) bindings.AddFaceBinding(binding);
        foreach (var binding in original.Bindings.PcurveBindings) bindings.AddPcurveBinding(binding.CoedgeId == coedge ? bad : binding);
        var points = original.Topology.Vertices.ToDictionary(v => v.Id, v => { original.TryGetVertexPoint(v.Id, out var p); return p; });
        var box = new BrepBody(original.Topology, original.Geometry, bindings, points, shellRepresentation: original.ShellRepresentation,
            faceOrientationReport: original.FaceOrientationReport) { ImportQualification = original.ImportQualification };
        var result = Step242Exporter.ExportBody(box, new Step242ExportOptions { ImportedPmi = transfer,
            BrepExportPreflightMode = BrepExportPreflightMode.Enforce, BrepExportPreflightPolicy = BrepExportPreflightPolicy.TrustedProductionRoute });
        Assert.True(result.IsSuccess, string.Join(";", result.Diagnostics.Select(d => d.Message)));
        Assert.Contains(result.Diagnostics, d => d.Message.StartsWith("step-export-recovered-pcurves-omitted:", StringComparison.Ordinal));
        Assert.DoesNotContain(Step242SubsetParser.Parse(result.Value).Value.Entities, e => e.Name == "PCURVE");
        Assert.Contains(bad, box.Bindings.PcurveBindings);
        var control = Assert.Single(Step242SemanticPmiInspector.Inspect(result.Value).Items, item => item.Kind == "Flatness");
        Assert.Equal(.03, control.Value); Assert.Single(control.GeometricFaceEntityIds);
    }
    [Fact]
    public void NativeEllipsoidHasTheExplicitRationalProjectionException()
    {
        var body = EllipsoidPrimitive.Create(10, 8, 6).Value;
        var result = Step242Exporter.ExportBody(body, new Step242ExportOptions {
            BrepExportPreflightMode = BrepExportPreflightMode.Enforce, BrepExportPreflightPolicy = BrepExportPreflightPolicy.TrustedProductionRoute });
        Assert.True(result.IsSuccess, string.Join(";", result.Diagnostics.Select(d => d.Message)));
        var parsed = Step242SubsetParser.Parse(result.Value).Value;
        Assert.Equal(8, parsed.Entities.Count(e => Step242SubsetDecoder.TryGetConstructor(e.Instance, "RATIONAL_B_SPLINE_SURFACE") is not null));
        var again = Step242Importer.ImportBody(result.Value);
        Assert.True(again.IsSuccess); Assert.All(again.Value.Geometry.Surfaces, surface => Assert.NotNull(surface.Value.Ellipsoid));
    }
    [Fact]
    public void AnnotationDependenciesCannotBypassTheRationalGeometryGuard()
    {
        var box = BrepPrimitives.CreateBox(10, 10, 10).Value;
        var source = Step242Exporter.ExportBody(box, [new Step242SemanticPmiGeometricTolerance("Seat", "Seat", "Flatness", .03, [])
            { GeometricFaceIds = [box.Topology.Faces.First().Id.Value] }]).Value;
        var parsed = Step242SubsetParser.Parse(source).Value;
        var control = parsed.Entities.Single(e => Step242PmiTransfer.GeometricTolerance(e) is not null);
        var aspect = ((Step242EntityReference)Step242PmiTransfer.GeometricTolerance(control)!.Arguments[3]).TargetId;
        var points = parsed.Entities.Where(e => e.Name == "CARTESIAN_POINT").Take(2).ToArray();
        var fresh = parsed.Entities.Max(e => e.Id) + 1;
        var context = parsed.Entities.First(e => Step242SubsetDecoder.TryGetConstructor(e.Instance, "GLOBAL_UNIT_ASSIGNED_CONTEXT") is not null).Id;
        source = source.Insert(source.LastIndexOf("ENDSEC;", StringComparison.Ordinal),
            $"#{fresh}=PROPERTY_DEFINITION('auxiliary',$,#{aspect});\n#{fresh+1}=PROPERTY_DEFINITION_REPRESENTATION(#{fresh},#{fresh+2});\n"
            + $"#{fresh+2}=REPRESENTATION('',(#{fresh+3}),#{context});\n#{fresh+3}=(B_SPLINE_CURVE(1,(#{points[0].Id},#{points[1].Id}),.UNSPECIFIED.,.F.,.F.)B_SPLINE_CURVE_WITH_KNOTS((2,2),(0.,1.),.UNSPECIFIED.)CURVE()GEOMETRIC_REPRESENTATION_ITEM()RATIONAL_B_SPLINE_CURVE((1.,2.))REPRESENTATION_ITEM('auxiliary'));\n");
        var body = Step242Importer.ImportBody(source).Value; var transfer = Step242PmiTransfer.Read(source, body);
        Assert.True(transfer.IsSuccess, string.Join(";", transfer.Diagnostics.Select(d => d.Message)));
        var result = Step242Exporter.ExportBody(body, new Step242ExportOptions { ImportedPmi = transfer.Value,
            BrepExportPreflightMode = BrepExportPreflightMode.Enforce, BrepExportPreflightPolicy = BrepExportPreflightPolicy.TrustedProductionRoute });
        Assert.False(result.IsSuccess); Assert.Contains(result.Diagnostics, d => d.Message.Contains("pmi-source-rational-geometry-not-canonical", StringComparison.Ordinal));
    }
}
