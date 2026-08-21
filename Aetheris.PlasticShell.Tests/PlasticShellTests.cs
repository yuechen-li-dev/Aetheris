using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Step242;
using Aetheris.PlasticShell;
using Xunit;

namespace Aetheris.PlasticShell.Tests;

public sealed class PlasticShellTests
{
    private static string Fixture(string name) => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "fixtures", name));

    [Fact]
    public void Flagship_lowers_to_first_class_ir_and_exact_closed_body()
    {
        var result = PlasticShellFirmament.CompileFile(Fixture(Path.Combine("Canonical", "PlasticShell", "plastic-shell-enclosure.firmament")));
        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics));
        Assert.NotNull(result.Intent); Assert.NotNull(result.State);
        Assert.Equal(4, result.Intent!.Standoffs.Count);
        Assert.Equal(4, result.Intent.Ejectors.Count);
        Assert.Equal(2, result.State!.Evidence.RibNetwork!.Candidates.Count);
        Assert.NotNull(result.State.Evidence.RibNetwork.SelectedCandidate);
        Assert.Empty(result.State.Evidence.Pullability.Undercuts);
        Assert.Equal(2.2, result.State.Evidence.WallThickness.Minimum, 10);
        Assert.Single(result.State.Body.Topology.Bodies);
        Assert.Single(result.State.Body.Topology.Shells);
        Assert.NotNull(result.State.Evidence.Materialization);
        Assert.Equal(0d, result.State.Evidence.Materialization!.ExteriorMaximumDeviation);
        Assert.False(string.IsNullOrWhiteSpace(result.State.Evidence.Materialization.ExteriorFingerprintBefore));
        Assert.Equal(result.State.Evidence.Materialization.ExteriorFingerprintBefore, result.State.Evidence.Materialization.ExteriorFingerprintAfter);
        Assert.Equal(7, result.State.Evidence.Materialization.Features.Count);
        Assert.Equal(4, result.State.Evidence.Materialization.Junctions.Count);
        Assert.All(result.State.Evidence.Materialization.Features, feature =>
        {
            Assert.NotEmpty(feature.FaceIds);
            Assert.Equal(PlasticEvidenceStrength.ExactAnalytic, feature.Strength);
            Assert.Equal(0d, feature.MinimumDraftAngleDegrees);
        });
        var ribs = result.State.Evidence.Materialization.Features.Where(f => f.Kind == "ConstantThicknessWallRib").ToArray();
        Assert.Equal(3, ribs.Length);
        Assert.All(ribs, rib =>
        {
            Assert.Equal(result.Intent.WallPolicy.NominalThickness, rib.BaseThickness, 10);
            Assert.Equal(result.Intent.WallPolicy.NominalThickness, rib.TopThickness, 10);
            Assert.Equal(3, rib.FaceIds.Count);
        });
        Assert.Equal(4, result.State.Evidence.Materialization.Features.Count(f => f.Kind == "AnalyticAnnularStandoff"));
        Assert.Contains(result.Diagnostics, d => d.Code == PlasticDiagnosticCodes.ConstantSectionFeatureZeroDraft && d.Severity == PlasticDiagnosticSeverity.Warning);
        Assert.All(result.State.Evidence.Materialization.Junctions, junction => Assert.True(junction.WithinLimit));
        Assert.InRange(result.State.Body.Topology.Faces.Count(), 20, 100);
        Assert.Contains("No product mesh", result.State.Evidence.Materialization.ConstructionMethod, StringComparison.Ordinal);
        Assert.True(BrepExportPreflight.Validate(result.State.Body).IsValid);
    }

    [Fact]
    public void Flagship_step_is_rational_free_and_reimports()
    {
        var result = PlasticShellFirmament.CompileFile(Fixture(Path.Combine("Canonical", "PlasticShell", "plastic-shell-enclosure.firmament")));
        var export = PlasticShellStepExporter.Export(result.State!, result.ModelName);
        Assert.True(export.IsSuccess, string.Join(Environment.NewLine, export.Diagnostics));
        Assert.Equal(0, export.Inventory.RationalProductSurfaces);
        var imported = Step242Importer.ImportBody(export.Step!);
        Assert.True(imported.IsSuccess, string.Join(Environment.NewLine, imported.Diagnostics));
        Assert.Single(imported.Value.Topology.Bodies);
        Assert.Single(imported.Value.Topology.Shells);
        var pmi = Step242SemanticPmiInspector.Inspect(export.Step!);
        Assert.True(pmi.Success, string.Join(Environment.NewLine, pmi.Diagnostics));
        var moldedItems = pmi.Items.Where(i => i.Name.StartsWith("standoff:", StringComparison.Ordinal) || i.Name.StartsWith("autorib:", StringComparison.Ordinal)).ToArray();
        Assert.All(moldedItems, item => Assert.NotEmpty(item.GeometricFaceEntityIds));
        var standoffAssociations = moldedItems.Where(i => i.Name.StartsWith("standoff:", StringComparison.Ordinal)).ToArray();
        Assert.Equal(4, standoffAssociations.Select(i => string.Join(',', i.GeometricFaceEntityIds)).Distinct(StringComparer.Ordinal).Count());
        var ribAssociation = moldedItems.Single(i => i.Name.StartsWith("autorib:", StringComparison.Ordinal)).GeometricFaceEntityIds;
        Assert.All(standoffAssociations, item => Assert.False(item.GeometricFaceEntityIds.SequenceEqual(ribAssociation)));
    }

    [Fact]
    public void Retired_height_field_is_available_only_as_explicit_art_export()
    {
        var result = PlasticShellFirmament.CompileFile(Fixture(Path.Combine("Canonical", "PlasticShell", "plastic-shell-enclosure.firmament")));
        Assert.True(result.IsSuccess);
        var art = PlasticShellHeightFieldArt.Export(result.State!, result.ModelName);
        Assert.True(art.IsSuccess, string.Join(Environment.NewLine, art.Diagnostics));
        Assert.NotNull(art.Step);
        Assert.True(art.BoundaryFaces > 9_000);
        Assert.Contains("height-field-art:happy-little-accident", art.Step!, StringComparison.Ordinal);
        Assert.DoesNotContain("height-field-art:happy-little-accident", PlasticShellStepExporter.Export(result.State!, result.ModelName).Step!, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("wall-collapse.firmament", PlasticDiagnosticCodes.WallOffsetCollapse)]
    [InlineData("draft-conflict.firmament", PlasticDiagnosticCodes.DraftConflict)]
    [InlineData("invalid-parting.firmament", PlasticDiagnosticCodes.InvalidParting)]
    [InlineData("invalid-gate.firmament", PlasticDiagnosticCodes.InvalidGate)]
    [InlineData("invalid-ejector.firmament", PlasticDiagnosticCodes.EjectorNotCoreAccessible)]
    [InlineData("undercut.firmament", PlasticDiagnosticCodes.Undercut)]
    [InlineData("no-valid-rib-network.firmament", PlasticDiagnosticCodes.AutoRibNoEligibleNetwork)]
    [InlineData("standoff-breaks-exterior.firmament", PlasticDiagnosticCodes.MaterializedFeatureOutsideAuthorizedRegion)]
    [InlineData("rib-undercut.firmament", PlasticDiagnosticCodes.AutoRibNoEligibleNetwork)]
    [InlineData("rib-ejector-collision.firmament", PlasticDiagnosticCodes.EjectorRibCollision)]
    [InlineData("rib-thick-junction.firmament", PlasticDiagnosticCodes.MaterialAccumulation)]
    [InlineData("rib-disconnected.firmament", PlasticDiagnosticCodes.AutoRibNoEligibleNetwork)]
    [InlineData("autorib-winner-not-materializable.firmament", PlasticDiagnosticCodes.MaterialAccumulation)]
    public void Invalid_fixture_has_focused_diagnostic(string fixture, string code)
    {
        var result = PlasticShellFirmament.CompileFile(Fixture(Path.Combine("Invalid", "PlasticShell", fixture)));
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == code && d.Severity == PlasticDiagnosticSeverity.Error);
    }

    [Fact]
    public void Autorib_is_deterministic()
    {
        var path = Fixture(Path.Combine("Canonical", "PlasticShell", "plastic-shell-enclosure.firmament"));
        var first = PlasticShellFirmament.CompileFile(path); var second = PlasticShellFirmament.CompileFile(path);
        Assert.Equal(first.State!.StateId, second.State!.StateId);
        Assert.Equal(first.State.Evidence.RibNetwork!.SelectedCandidate, second.State.Evidence.RibNetwork!.SelectedCandidate);
    }

    [Fact]
    public void Autorib_fan_root_depends_on_gate_location()
    {
        var source = File.ReadAllText(Fixture(Path.Combine("Canonical", "PlasticShell", "plastic-shell-enclosure.firmament")));
        var rear = PlasticShellFirmament.Compile(source);
        var front = PlasticShellFirmament.Compile(source.Replace("Position: [0, 57, 20]", "Position: [0, -57, 20]", StringComparison.Ordinal));
        var rearFan = rear.State!.Evidence.RibNetwork!.Candidates.Single(c => c.CandidateId == "gate-oriented-fan");
        var frontFan = front.State!.Evidence.RibNetwork!.Candidates.Single(c => c.CandidateId == "gate-oriented-fan");
        Assert.All(rearFan.Edges, edge => Assert.Equal("PcbC", edge.From));
        Assert.All(frontFan.Edges, edge => Assert.Equal("PcbA", edge.From));
    }
}
