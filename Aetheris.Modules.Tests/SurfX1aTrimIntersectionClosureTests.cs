using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Surfacing;
using Xunit;

namespace Aetheris.Modules.Tests;

public sealed class SurfX1aTrimIntersectionClosureTests
{
    [Fact]
    public void FlagshipOwnsFourInnerLoopsAndQualifiedFaceLocalPcurves()
    {
        var state = CompileFlagship().OutputState!;
        var splineFace = Assert.Single(state.Body.Topology.Faces, face => state.Body.TryGetFaceSurfaceGeometry(face.Id, out var support)
            && support?.Kind == SurfaceGeometryKind.BSplineSurfaceWithKnots);
        Assert.Equal(5, splineFace.LoopIds.Count);
        Assert.Equal(5, Assert.Single(state.SurfacePatches).BoundaryLoops);
        var evidence = BrepPcurveValidator.Validate(state.Body, 1e-5, requireEveryCoedge: true);
        Assert.True(evidence.IsValid, string.Join(" | ", evidence.Diagnostics));
        Assert.Equal(state.Body.Topology.Coedges.Count(), evidence.PcurveCount);
        Assert.True(evidence.MaximumReconstructionDeviation <= 1e-5);
        Assert.Contains(state.ValidationEvidence, item => item.Check == "DerivedTrimIntersections" && item.Satisfied);
        foreach (var innerLoop in splineFace.LoopIds.Skip(1))
        {
            var coedge = Assert.Single(state.Body.Topology.GetLoop(innerLoop).CoedgeIds.Select(state.Body.Topology.GetCoedge));
            Assert.Equal(2, state.Body.Topology.Coedges.Count(candidate => candidate.EdgeId == coedge.EdgeId));
            Assert.Equal(2, state.Body.Bindings.PcurveBindings.Count(binding => state.Body.Topology.GetCoedge(binding.CoedgeId).EdgeId == coedge.EdgeId));
        }
    }

    [Fact]
    public void IntersectionClassificationAndBranchSelectionAreExplicitAndDeterministic()
    {
        var z0 = SurfaceGeometry.FromPlane(Plane(0));
        var z1 = SurfaceGeometry.FromPlane(Plane(1));
        var domain = new SurfaceParameterDomain(-10, 10, -10, 10);
        var none = BoundedSurfaceIntersector.Intersect(new("z0", z0, domain, "z1", z1, domain));
        Assert.Equal(SurfaceIntersectionClassification.NoIntersection, none.Classification);
        var coincident = BoundedSurfaceIntersector.Intersect(new("z0a", z0, domain, "z0b", z0, domain));
        Assert.Equal(SurfaceIntersectionClassification.CoincidentRegion, coincident.Classification);

        var patch = Assert.IsType<BSplineSurfacePatch>(CompileFlagship().OutputState!.Construction.ReplacementPatch);
        var supportPlane = SurfaceGeometry.FromPlane(new PlaneSurface(new(0, -40, 20), Dir(0, -1, 0), Dir(1, 0, 0)));
        var first = BoundedSurfaceIntersector.Intersect(new("south", supportPlane, domain, patch.PatchId, patch.Support, patch.ParameterDomain, SeedOnA: new(0, 0)));
        var second = BoundedSurfaceIntersector.Intersect(new("south", supportPlane, domain, patch.PatchId, patch.Support, patch.ParameterDomain, SeedOnA: new(0, 0)));
        Assert.True(first.IsSuccess); Assert.Equal(SurfaceIntersectionClassification.SingleCurve, first.Classification);
        Assert.Equal(first.Classification, second.Classification); Assert.Equal(first.SelectedBranch, second.SelectedBranch);
        Assert.Equal(first.Branches.Select(branch => branch.StableId), second.Branches.Select(branch => branch.StableId)); Assert.NotNull(first.SelectedBranch);
        Assert.Equal(CurveGeometryKind.BSpline3, Assert.Single(first.Branches).Curve3D.Kind);

        var coplanarBoundary = BoundedSurfaceIntersector.Intersect(new("base-plane", SurfaceGeometry.FromPlane(Plane(20)), domain,
            patch.PatchId, patch.Support, patch.ParameterDomain));
        Assert.Equal(SurfaceIntersectionClassification.MultipleCurves, coplanarBoundary.Classification);
        Assert.Null(coplanarBoundary.SelectedBranch); Assert.Contains("surf-intersection-ambiguous", coplanarBoundary.Diagnostics);
    }

    [Fact]
    public void AnalyticAndSplineExtensionsAreBoundedAndReportTheirLaw()
    {
        var plane = SurfaceGeometry.FromPlane(Plane(0));
        var analytic = SurfaceSupportExtension.Extend(plane, new(0, 1, 0, 1), new(-.1, 1.1, -.1, 1.1));
        Assert.True(analytic.IsSuccess); Assert.Equal(SurfaceExtensionMethod.AnalyticIdentity, analytic.Support!.Method);
        Assert.Equal(new Point3D(-.1, -.1, 0), analytic.Support.Evaluate(-.1, -.1));

        var patch = Assert.IsType<BSplineSurfacePatch>(CompileFlagship().OutputState!.Construction.ReplacementPatch);
        var extended = SurfaceSupportExtension.Extend(patch.Support, patch.ParameterDomain, new(-.1, 1.1, -.1, 1.1));
        Assert.True(extended.IsSuccess); Assert.Equal(SurfaceExtensionMethod.EndpointTangentContinuation, extended.Support!.Method);
        Assert.Contains("C1", extended.Support.ContinuityAtOriginalBoundary, StringComparison.Ordinal);
        var rejected = SurfaceSupportExtension.Extend(patch.Support, patch.ParameterDomain, new(-1, 2, 0, 1));
        Assert.False(rejected.IsSuccess); Assert.Contains(rejected.Diagnostics, item => item.Code == "surf-extension-unsupported");
    }

    [Fact]
    public void StepCarriesSurfaceCurvesPcurvesAndReimportsWithoutRationalSurfaces()
    {
        var export = SculptStepExporter.Export(CompileFlagship().OutputState!, "SURF-X1a");
        Assert.True(export.IsSuccess, string.Join(" | ", export.Diagnostics.Select(item => item.Message)));
        Assert.Contains("=PCURVE(", export.Step!, StringComparison.Ordinal);
        Assert.Contains("=SURFACE_CURVE(", export.Step!, StringComparison.Ordinal);
        Assert.DoesNotContain("RATIONAL_B_SPLINE_SURFACE", export.Step!, StringComparison.Ordinal);
        Assert.Equal(0, export.Inventory.RationalNurbs); Assert.Equal(1, export.Inventory.NonRationalBSpline);
        var imported = Step242Importer.ImportBody(export.Step!);
        Assert.True(imported.IsSuccess, string.Join(" | ", imported.Diagnostics.Select(item => item.Message)));
        Assert.True(BrepExportPreflight.Validate(imported.Value!).IsValid);
    }

    [Fact]
    public void PmiAndBottomInterfaceRemainGeometricallyAssociatedAfterReinspection()
    {
        var state = CompileFlagship().OutputState!;
        Assert.All(state.GeometryAssociations!, association => Assert.Equal(PersistentAssociationState.Preserved, association.State));
        Assert.All(state.GeometryAssociations!, association => Assert.NotEmpty(association.FaceIds));
        var export = SculptStepExporter.Export(state, "SURF-X1a associations");
        Assert.True(export.IsSuccess);
        var inspection = Step242SemanticPmiInspector.Inspect(export.Step!);
        Assert.True(inspection.Success, string.Join(" | ", inspection.Diagnostics));
        Assert.Equal(1, inspection.DatumCount); Assert.Equal(1, inspection.DimensionCount); Assert.Equal(1, inspection.GeometricToleranceCount);
        Assert.All(inspection.Items.Where(item => item.Kind is "Datum" or "Diameter" or "Position"), item => Assert.NotEmpty(item.GeometricFaceEntityIds));
        var assembly = Assert.Single(inspection.Items, item => item.Kind == "Annotation" && item.Name.Contains("assembly-interface", StringComparison.Ordinal));
        Assert.Equal(SculptedHousingFactory.BottomMountingInterface, assembly.Target);
        Assert.NotEmpty(assembly.GeometricFaceEntityIds);
    }

    [Fact]
    public void ImportedAdvancedFaceIsReplacedBySupportGraftWithoutRebuildingNeighbors()
    {
        var compile = CompileFlagship();
        var nativeBase = compile.States["Base"];
        var sourceStep = SculptStepExporter.Export(nativeBase, "SURF-X1a imported source");
        Assert.True(sourceStep.IsSuccess);
        var imported = Step242Importer.ImportBody(sourceStep.Step!);
        Assert.True(imported.IsSuccess, string.Join(" | ", imported.Diagnostics.Select(item => item.Message)));
        var topBinding = Assert.Single(imported.Value!.Bindings.FaceBindings, binding =>
            imported.Value.Geometry.GetSurface(binding.SurfaceGeometryId).Plane is { } plane && Math.Abs(plane.Origin.Z - 20d) < 1e-8d && plane.Normal.ToVector().Z > 0d);
        Assert.True(topBinding.SourceStepEntityId.HasValue);
        var adopted = ImportedFaceRegionReplacer.AdoptImportedBody(nativeBase, imported.Value, "ImportedBaseStep");
        var patch = Assert.IsType<BSplineSurfacePatch>(compile.OutputState!.Construction.ReplacementPatch);
        var replaced = ImportedFaceRegionReplacer.Apply(adopted, topBinding.SourceStepEntityId!.Value, patch, "ImportedTrimmedCrown");
        Assert.True(replaced.IsSuccess, string.Join(" | ", replaced.Diagnostics.Select(item => item.Message)));
        Assert.Same(imported.Value.Topology, replaced.OutputState!.Body.Topology);
        Assert.Equal(4, replaced.Evidence!.InnerLoopCount);
        Assert.NotEmpty(replaced.Evidence.PreservedNeighborSourceStepEntityIds);
        Assert.True(replaced.Evidence.Pcurves.IsValid);
        Assert.Equal(adopted.GeometryAssociations!.Select(item => item.FaceIds), replaced.OutputState.GeometryAssociations!.Select(item => item.FaceIds));
        Assert.All(replaced.OutputState.GeometryAssociations!, association => Assert.Contains("Explicit GeometricDelta Preserved", association.Evidence, StringComparison.Ordinal));
        Assert.Contains(replaced.OutputState.Body.Bindings.FaceBindings, binding => binding.FaceId == topBinding.FaceId
            && binding.SourceStepEntityId == topBinding.SourceStepEntityId
            && replaced.OutputState.Body.Geometry.GetSurface(binding.SurfaceGeometryId).Kind == SurfaceGeometryKind.BSplineSurfaceWithKnots);
        var stale = ImportedFaceRegionReplacer.Apply(replaced.OutputState, topBinding.SourceStepEntityId.Value, patch, "StaleImportedTarget");
        Assert.False(stale.IsSuccess); Assert.Contains(stale.Diagnostics, item => item.Code == "surf-selector-target-replaced");
    }

    [Fact]
    public void BrokenPcurveIsRejectedIndependentlyOfTopologyValidity()
    {
        var body = CompileFlagship().OutputState!.Body;
        var bindings = new BrepBindingModel();
        foreach (var edge in body.Bindings.EdgeBindings) bindings.AddEdgeBinding(edge);
        foreach (var face in body.Bindings.FaceBindings) bindings.AddFaceBinding(face);
        var first = body.Bindings.PcurveBindings.OrderBy(item => item.CoedgeId.Value).First();
        foreach (var pcurve in body.Bindings.PcurveBindings)
            bindings.AddPcurveBinding(pcurve.CoedgeId == first.CoedgeId
                ? pcurve with { Pcurve = PcurveGeometry.Line(pcurve.Pcurve.Domain, new(0, 0), new(0, 0)) }
                : pcurve);
        var points = body.Topology.Vertices.Where(vertex => body.TryGetVertexPoint(vertex.Id, out _)).ToDictionary(vertex => vertex.Id,
            vertex => { body.TryGetVertexPoint(vertex.Id, out var point); return point; });
        var corrupted = new BrepBody(body.Topology, body.Geometry, bindings, points, body.SafeBooleanComposition, body.ShellRepresentation);
        Assert.True(BrepExportPreflight.Validate(corrupted).IsValid);
        var evidence = BrepPcurveValidator.Validate(corrupted, 1e-5, requireEveryCoedge: true);
        Assert.False(evidence.IsValid); Assert.Contains(evidence.Diagnostics, message => message.StartsWith("surf-pcurve-invalid", StringComparison.Ordinal));
    }

    [Fact]
    public void RemovedPmiOrInterfaceTargetIsNotReboundByGeometryOrName()
    {
        var state = CompileFlagship().OutputState!;
        var removed = state.Delta! with
        {
            OutputState = new("state-association-removal-witness"),
            Correspondence = [new(SculptedHousingFactory.BottomMountingInterface, GeometricChangeKind.Removed, [], "Deliberate invalid witness.")]
        };
        var remap = SculptedHousingFactory.RemapPersistentAssociations(state, state.Body, removed);
        Assert.False(remap.IsSuccess);
        Assert.Contains(remap.Diagnostics, item => item.Code == "surf-association-target-removed"
            && item.Entity == SculptedHousingFactory.BottomMountingInterface);
    }

    private static SculptingCompileResult CompileFlagship() => SculptingAuthoring.CompileFile(Path.Combine(RepositoryRoot(), "fixtures", "Canonical", "Sculpting", "surf-x1a-trimmed-freeform-housing.firmament"));
    private static PlaneSurface Plane(double z) => new(new(0, 0, z), Dir(0, 0, 1), Dir(1, 0, 0));
    private static Direction3D Dir(double x, double y, double z) => Direction3D.Create(new Vector3D(x, y, z));
    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Aetheris.slnx"))) current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
