using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Import;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class StepLegacyQualificationTests
{
    [Fact]
    public void ExactImportedBox_GainsAnalyticallyDerivedPcurvesWithoutGeometryRecovery()
    {
        var imported = Step242Importer.ImportBody(Step242FixtureCorpus.CanonicalBoxGolden);
        Assert.True(imported.IsSuccess);
        var body = imported.Value;
        Assert.Equal(0, body.ImportQualification!.MissingPcurves);
        Assert.True(BrepPcurveValidator.Validate(body, 1e-3, requireEveryCoedge: true).IsValid);
        Assert.All(body.Bindings.PcurveBindings, binding => Assert.Equal(PcurveBindingOrigin.DerivedAnalytic, binding.Qualification!.Origin));
        Assert.All(body.Geometry.Surfaces, support => Assert.Null(support.Value.RecoveryProvenance));
        Assert.All(body.Geometry.Curves, curve => Assert.Null(curve.Value.RecoveryProvenance));
        Assert.Equal(Step242FixtureCorpus.CanonicalBoxGolden, Step242Exporter.ExportBody(body).Value);
    }

    [Fact]
    public void DisplayRefinementWarning_DoesNotDegradeAuthoritativeGeometry()
    {
        var body = Box.Value;
        var mesh = BrepDisplayTessellator.TessellateBoundedPartial(body) with {
            FaceDiagnostics = [new(null, "BSplineSurfaceWithKnots", "FaceTessellation", "Viewer.Tessellation.RefinementIncomplete", "Display budget reached.")]
        };
        var report = BrepImportQualification.WithDisplay(body, mesh);
        Assert.Equal(ImportQualificationStatus.Qualified, report.Status);
        Assert.Equal(ImportDisplayStatus.RefinementLimited, report.DisplayStatus);
        Assert.Empty(report.Reasons);
        Assert.Single(report.DisplayReasons);
    }
    private static readonly Lazy<BrepBody> Box = new(() => {
        var authored = BrepPrimitives.CreateBox(2, 2, 2).Value;
        Assert.True(BrepPcurveRecovery.Populate(authored.Topology, authored.Geometry, authored.Bindings, 1e-3).IsSuccess);
        var imported = Step242Importer.ImportBody(Step242Exporter.ExportBody(authored).Value);
        Assert.True(imported.IsSuccess);
        return imported.Value;
    });

    [Fact]
    public void ClosedQualifiedGeometry_DoesNotClaimDisplayUntilActualMeshIsAssessed()
    {
        var body = Box.Value;
        Assert.Equal(ImportQualificationStatus.Qualified, body.ImportQualification!.GeometryStatus);
        Assert.Equal(ImportQualificationStatus.Qualified, body.ImportQualification.Status);
        Assert.Equal(ImportDisplayStatus.NotAssessed, body.ImportQualification.DisplayStatus);
        Assert.Null(body.ImportQualification.DisplayedFaces);
        var mesh = BrepDisplayTessellator.TessellateBoundedPartial(body);
        var report = BrepImportQualification.WithDisplay(body, mesh);
        Assert.Equal(ImportQualificationStatus.Qualified, report.Status);
        Assert.Equal(6, report.DisplayedFaces);
        Assert.Empty(report.Reasons);
    }

    [Fact]
    public void MissingDisplayPatch_IsSeparateFromEngineering_WithSourceFaceIdentity()
    {
        var body = Box.Value;
        var mesh = BrepDisplayTessellator.TessellateBoundedPartial(body);
        var omitted = mesh.FacePatches[0];
        var partial = mesh with { FacePatches = mesh.FacePatches.Skip(1).ToArray() };
        var report = BrepImportQualification.WithDisplay(body, partial);
        Assert.Equal(ImportQualificationStatus.Qualified, report.Status);
        Assert.Equal(ImportDisplayStatus.Partial, report.DisplayStatus);
        Assert.Equal(6, report.BoundFaces);
        Assert.Equal(5, report.DisplayedFaces);
        var reason = Assert.Single(report.DisplayReasons, r => r.Code == "missing-display-face");
        Assert.Equal(omitted.FaceId.Value, reason.FaceId);
        Assert.Equal(body.Bindings.GetFaceBinding(omitted.FaceId).SourceStepEntityId, reason.StepEntityId);
        Assert.Equal(ImportQualificationStatus.Qualified, body.ImportQualification!.Status);
        Assert.Equal(6, body.Topology.Faces.Count());
    }

    [Fact]
    public void ClosedGeometryWithoutPcurves_RemainsInspectableAfterCompleteDisplay()
    {
        var original = Box.Value;
        var bindings = new BrepBindingModel();
        foreach (var e in original.Bindings.EdgeBindings) bindings.AddEdgeBinding(e);
        foreach (var f in original.Bindings.FaceBindings) bindings.AddFaceBinding(f);
        var points = original.Topology.Vertices.ToDictionary(v => v.Id, v => { original.TryGetVertexPoint(v.Id, out var p); return p; });
        var body = new BrepBody(original.Topology, original.Geometry, bindings, points, faceOrientationReport: original.FaceOrientationReport);
        body.ImportQualification = BrepImportQualification.Evaluate(body, 6, null, 1e-3);
        Assert.Equal(24, body.ImportQualification.MissingPcurves);
        var report = BrepImportQualification.WithDisplay(body, BrepDisplayTessellator.TessellateBoundedPartial(body));
        Assert.Equal(ImportQualificationStatus.Inspectable, report.Status);
        Assert.Contains(report.Reasons, r => r.Code == "missing-pcurve");
    }

    [Fact]
    public void SolidRootWithOpenShell_IsRetainedButNeverQualified()
    {
        var result = Step242Importer.ImportBody(Step242FixtureCorpus.PlanarFaceWithRectangularHole);
        Assert.True(result.IsSuccess);
        var report = result.Value.ImportQualification!;
        Assert.Equal(ImportQualificationStatus.Degraded, report.Status);
        Assert.Equal(8, Assert.Single(report.Shells).BoundaryEdges);
        Assert.Contains(report.Reasons, r => r.Code == "incomplete-shell");
        Assert.Equal(ImportQualificationStatus.Degraded,
            BrepImportQualification.WithDisplay(result.Value, BrepDisplayTessellator.TessellateBoundedPartial(result.Value)).Status);
    }
}
