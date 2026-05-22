using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class StepSolidRootExportPlannerTests
{
    [Fact]
    public void Planner_SingleShell_SelectsManifoldSolidBrep()
    {
        var box = BrepPrimitives.CreateBox(4d, 4d, 4d);
        Assert.True(box.IsSuccess);

        var decision = StepSolidRootExportPlanner.Decide(box.Value);

        Assert.Equal(StepSolidRootExportKind.ManifoldSolidBrep, decision.Kind);
    }

    [Fact]
    public void Planner_OuterAndInnerShells_SelectsBrepWithVoids()
    {
        var body = LoadVoidFixture();

        var decision = StepSolidRootExportPlanner.Decide(body);

        Assert.Equal(StepSolidRootExportKind.BrepWithVoids, decision.Kind);
    }

    [Fact]
    public void Planner_MultiShellWithoutRoles_SelectsUnsupported()
    {
        var imported = LoadVoidFixture();
        var noRoles = new BrepBody(imported.Topology, imported.Geometry, imported.Bindings, vertexPoints: null, safeBooleanComposition: imported.SafeBooleanComposition, shellRepresentation: null);

        var decision = StepSolidRootExportPlanner.Decide(noRoles);

        Assert.Equal(StepSolidRootExportKind.Unsupported, decision.Kind);
        Assert.Contains(decision.Diagnostics, d => d.Contains("missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Planner_DiagnosticsIncludeRejectedPolicies()
    {
        var imported = LoadVoidFixture();
        var noRoles = new BrepBody(imported.Topology, imported.Geometry, imported.Bindings, vertexPoints: null, safeBooleanComposition: imported.SafeBooleanComposition, shellRepresentation: null);

        var decision = StepSolidRootExportPlanner.Decide(noRoles);

        var manifoldEval = decision.Evaluations.Single(e => e.PolicyName == "ManifoldSolidBrepExportPolicy");
        var voidEval = decision.Evaluations.Single(e => e.PolicyName == "BrepWithVoidsExportPolicy");
        Assert.False(manifoldEval.Admissible);
        Assert.False(voidEval.Admissible);
        Assert.NotEmpty(manifoldEval.RejectionReasons);
        Assert.NotEmpty(voidEval.RejectionReasons);
    }

    [Fact]
    public void Export_ExplicitVoidShells_StillEmitsBrepWithVoids()
    {
        var body = LoadVoidFixture();

        var export = Step242Exporter.ExportBody(body);

        Assert.True(export.IsSuccess);
        Assert.Contains("BREP_WITH_VOIDS", export.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void Export_ThroughHole_RemainsManifold()
    {
        var fixturePath = Path.Combine(Step242CorpusManifestRunner.RepoRoot(), "testdata", "firmament", "exports", "boolean_box_cylinder_hole.step");
        var import = Step242Importer.ImportBody(File.ReadAllText(fixturePath));
        Assert.True(import.IsSuccess);

        var export = Step242Exporter.ExportBody(import.Value);

        Assert.True(export.IsSuccess);
        Assert.Contains("MANIFOLD_SOLID_BREP", export.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("BREP_WITH_VOIDS", export.Value, StringComparison.Ordinal);
    }

    private static BrepBody LoadVoidFixture()
    {
        var fixturePath = Path.Combine(Step242CorpusManifestRunner.RepoRoot(), "testdata", "firmament", "exports", "boolean_box_sphere_cavity_basic.step");
        var import = Step242Importer.ImportBody(File.ReadAllText(fixturePath));
        Assert.True(import.IsSuccess, string.Join(Environment.NewLine, import.Diagnostics.Select(d => d.Message)));
        return import.Value;
    }
}
