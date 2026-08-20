using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Surfacing;
using Xunit;

namespace Aetheris.Modules.Tests;

public sealed class SurfX1PatchSurfacingTests
{
    [Fact]
    public void FlagshipReplacesOnlyCrownWithInspectableNonRationalPatch()
    {
        var compile = CompileFlagship();
        Assert.True(compile.IsSuccess, string.Join(';', compile.Diagnostics.Select(x => $"{x.Code}:{x.Message}")));
        var state = compile.OutputState!; var patch = Assert.Single(state.SurfacePatches);
        Assert.Equal(SurfacePatchClass.NonRationalBSpline, patch.SurfaceClass);
        Assert.Equal((3, 3), (patch.DegreeU, patch.DegreeV)); Assert.Equal((6, 6), (patch.ControlCountU, patch.ControlCountV));
        Assert.Contains(state.Delta!.Correspondence, x => x.InputEntity == SculptedHousingFactory.CrownRegion && x.OutputEntities.SequenceEqual(["CrownPatch"]));
        Assert.Contains(state.ValidationEvidence, x => x.Check == "AuthorizedLocality" && x.Satisfied && x.MaximumObservedDeviation == 0d);
        Assert.Contains(state.ValidationEvidence, x => x.Check == "SharedBoundaryTopology" && x.Satisfied);
        Assert.Contains(state.ValidationEvidence, x => x.Check == "Boundary:BoundaryEast:G1" && x.Satisfied && x.MaximumObservedDeviation < .001d);
        Assert.Contains(state.ValidationEvidence, x => x.Check == "Boundary:BoundaryWest:G1" && x.Satisfied && x.MaximumObservedDeviation < .001d);
    }

    [Fact]
    public void TrimAndExtendRemainBoundedByExistingNonRationalSupport()
    {
        var patch = Assert.IsType<BSplineSurfacePatch>(CompileFlagship().OutputState!.Construction.ReplacementPatch);
        var trimmed = SurfacePatchOperations.TrimRegion(patch, new(.1, .9, .2, .8));
        Assert.True(trimmed.IsSuccess); Assert.Equal(new SurfaceParameterDomain(.1, .9, .2, .8), trimmed.Patch!.ParameterDomain);
        var extended = SurfacePatchOperations.ExtendRegion((BSplineSurfacePatch)trimmed.Patch, new(0, 1, 0, 1));
        Assert.True(extended.IsSuccess);
        var rejected = SurfacePatchOperations.ExtendRegion(patch, new(-.1, 1, 0, 1));
        Assert.False(rejected.IsSuccess); Assert.Contains(rejected.Diagnostics, x => x.Code == "surf-extend-law-unsupported");
    }

    [Fact]
    public void FlagshipStepIsEnclosedReimportsAndContainsNoRationalSurface()
    {
        var export = SculptStepExporter.Export(CompileFlagship().OutputState!, "SURF-X1");
        Assert.True(export.IsSuccess, string.Join(';', export.Diagnostics.Select(x => x.Message)));
        Assert.Equal(1, export.Inventory.NonRationalBSpline); Assert.Equal(0, export.Inventory.RationalNurbs);
        Assert.DoesNotContain("RATIONAL_B_SPLINE_SURFACE", export.Step!, StringComparison.Ordinal);
        var imported = Step242Importer.ImportBody(export.Step!);
        Assert.True(imported.IsSuccess, string.Join(';', imported.Diagnostics.Select(x => x.Message)));
        Assert.Single(imported.Value!.Geometry.Surfaces, x => x.Value.BSplineSurfaceWithKnots is not null);
        var incidence = imported.Value.Topology.Coedges.GroupBy(x => x.EdgeId).Select(x => x.Count()).ToArray();
        Assert.All(incidence, count => Assert.Equal(2, count));
    }

    [Fact]
    public void G1ViolationFailsAtomicallyAndLeavesPredecessorAccepted()
    {
        var source = File.ReadAllText(FlagshipPath()).Replace("[-20mm, -12mm, 20mm]", "[-20mm, -12mm, 23mm]", StringComparison.Ordinal);
        var compile = SculptingAuthoring.Compile(source);
        Assert.False(compile.IsSuccess); Assert.Contains(compile.Diagnostics, x => x.Code == "surf-boundary-g1-violation");
        Assert.True(compile.States.TryGetValue("Base", out var predecessor)); Assert.Null(predecessor.PredecessorStateId);
        Assert.False(compile.States.ContainsKey("FreeformCrown"));
    }

    [Fact]
    public void ReplacedHistoricalSelectorProducesActionableDiagnostic()
    {
        var first = CompileFlagship(); var input = first.OutputState!; var patch = input.Construction.ReplacementPatch!;
        var operation = new ReplaceRegionOperation("stale.ReplaceRegion", SculptedHousingFactory.CrownRegion, patch,
            [SculptedHousingFactory.CrownRegion], new(-30, -20, 20, 30, 20, 28), [], []);
        var result = ReplaceRegionSculptor.Apply(input, "Stale", operation);
        Assert.False(result.IsSuccess); Assert.Contains(result.Diagnostics, x => x.Code == "surf-selector-target-replaced" && x.Message.Contains("CrownPatch", StringComparison.Ordinal));
    }

    [Fact]
    public void SafeHoleResolvesCurrentStateAfterReplacementAndPreservesPatchAndExistingHoles()
    {
        var compile = CompileFlagship(); var sculpted = compile.States["FreeformCrown"]; var after = compile.States["CrownWithServiceHole"];
        Assert.Equal(sculpted.StateId, after.PredecessorStateId); Assert.Contains("H5", after.Delta!.Introduces);
        Assert.Contains("CrownPatch", after.Delta.Preserves);
        Assert.All(new[] { "CrownPatch", "H1", "H2", "H3", "H4" }, id => Assert.Contains(after.ValidationEvidence, x => x.Check == $"Preserve:{id}" && x.Satisfied));
        var export = SculptStepExporter.Export(after, "SURF-X1 downstream hole");
        Assert.True(export.IsSuccess); Assert.Equal(1, export.Inventory.NonRationalBSpline); Assert.Equal(5, export.Inventory.Cylinder); Assert.Equal(0, export.Inventory.RationalNurbs);
    }

    [Fact]
    public void IdenticalInputAndPatchAreDeterministic()
    {
        var first = CompileFlagship(); var second = CompileFlagship();
        Assert.Equal(first.OutputState!.StateId, second.OutputState!.StateId);
        Assert.Equal(SculptStepExporter.Export(first.OutputState, "SURF-X1").Step, SculptStepExporter.Export(second.OutputState, "SURF-X1").Step);
    }

    [Fact]
    public void LowAndHighCrownsBranchIndependentlyFromSamePredecessor()
    {
        var compile = CompileFlagship(); var predecessor = compile.States["Base"]; var low = compile.States["FreeformCrown"];
        var source = (BSplineSurfacePatch)low.Construction.ReplacementPatch!; var s = source.Spline;
        var controls = s.ControlPoints.Select(row => (IReadOnlyList<Aetheris.Kernel.Core.Math.Point3D>)row.Select(p => new Aetheris.Kernel.Core.Math.Point3D(p.X, p.Y, 20d + (p.Z - 20d) * 1.4d)).ToArray()).ToArray();
        var highSpline = new BSplineSurfaceWithKnots(s.DegreeU, s.DegreeV, controls, s.SurfaceForm, s.UClosed, s.VClosed, s.SelfIntersect, s.KnotMultiplicitiesU, s.KnotMultiplicitiesV, s.KnotValuesU, s.KnotValuesV, s.KnotSpec);
        var highPatch = new BSplineSurfacePatch("HighCrownPatch", highSpline, source.ParameterDomain, source.BoundaryLoop);
        var operation = new ReplaceRegionOperation("High.ReplaceRegion", SculptedHousingFactory.CrownRegion, highPatch,
            [SculptedHousingFactory.CrownRegion, SculptedHousingFactory.TransitionZone], new(-30, -20, 20, 30, 20, 30),
            [new(SculptedHousingFactory.BottomMountingInterface, PreservationMode.ExactGeometry), new(SculptedHousingFactory.MountingHolePattern, PreservationMode.PatternPlacementAndDiameter)],
            [SculptRequirement.ClosedManifold, SculptRequirement.OrientationConsistency, SculptRequirement.NoSelfIntersection]);
        var high = ReplaceRegionSculptor.Apply(predecessor, "HighCrown", operation);
        Assert.True(high.IsSuccess, string.Join(';', high.Diagnostics.Select(x => x.Message)));
        Assert.Equal(predecessor.StateId, low.PredecessorStateId); Assert.Equal(predecessor.StateId, high.OutputState!.PredecessorStateId);
        Assert.NotEqual(low.StateId, high.OutputState.StateId); Assert.Equal(20d, predecessor.Construction.FinalHeight);
        Assert.True(high.OutputState.Construction.FinalHeight > low.Construction.FinalHeight);
    }

    private static SculptingCompileResult CompileFlagship() => SculptingAuthoring.CompileFile(FlagshipPath());
    private static string FlagshipPath() => Path.Combine(RepositoryRoot(), "fixtures", "Canonical", "Sculpting", "surf-x1-freeform-housing.firmament");
    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Aetheris.slnx"))) current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
