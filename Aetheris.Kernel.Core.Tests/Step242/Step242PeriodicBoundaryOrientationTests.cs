using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242PeriodicBoundaryOrientationTests
{
    public static TheoryData<string> FormerRedCases => new()
    {
        "STC/nist_stc_10_asme1_ap242-e2.stp",
        "FTC/nist_ftc_08_asme1_ap242-e2.stp",
        "FTC/nist_ftc_11_asme1_ap242-e2.stp",
        "CTC/nist_ctc_02_asme1_ap242-e2.stp",
    };

    [Theory]
    [MemberData(nameof(FormerRedCases))]
    [Trait("Category", "SlowCorpus")]
    public void PeriodicBoundaryOrientation_SurvivesExportImport(string relativePath)
    {
        var imported = Step242Corpus.Import(NistPath(relativePath));
        Assert.True(imported.IsSuccess, string.Join(" | ", imported.Diagnostics.Select(diagnostic => diagnostic.Message)));

        var exported = Step242Exporter.ExportBody(imported.Value);
        Assert.True(exported.IsSuccess, string.Join(" | ", exported.Diagnostics.Select(diagnostic => diagnostic.Message)));
        var reimported = Step242Importer.ImportBody(exported.Value);
        Assert.True(reimported.IsSuccess, string.Join(" | ", reimported.Diagnostics.Select(diagnostic => diagnostic.Message)));

        var before = imported.Value.FaceOrientationReport!.Faces.OrderBy(face => face.FaceId.Value)
            .Select(face => (face.FaceId, face.Orientation, face.Qualification)).ToArray();
        var after = reimported.Value.FaceOrientationReport!.Faces.OrderBy(face => face.FaceId.Value)
            .Select(face => (face.FaceId, face.Orientation, face.Qualification)).ToArray();
        Assert.Equal(before, after);
    }

    [Fact]
    public void ImportedBoundaryRoles_SelectExplicitJudgmentCandidate()
    {
        var imported = Step242Corpus.Import(NistPath("FTC/nist_ftc_11_asme1_ap242-e2.stp"));
        Assert.True(imported.IsSuccess);

        foreach (var face in imported.Value.Topology.Faces.Where(face => face.LoopIds.Count > 0))
        {
            var roles = face.LoopIds.Select(loopId => imported.Value.Bindings.GetFaceBoundaryRoleBinding(loopId)).ToArray();
            Assert.Equal(face.LoopIds.Count, roles.Length);
            Assert.Single(roles, role => role.Role == FaceBoundaryRole.Outer);
            var decision = StepFaceBoundaryExportPolicy.Decide(imported.Value, face);
            Assert.NotNull(decision);
            Assert.Equal(StepFaceBoundaryExportPolicy.ExplicitRolesCandidate, decision.Candidate);
        }
    }

    [Fact]
    public void BoundaryPolicy_UsesAuthoredOrderOnlyWhenRoleEvidenceIsAbsent()
    {
        var face = new Face(new FaceId(1), [new LoopId(7), new LoopId(3)]);
        var body = new BrepBody(new TopologyModel(), new BrepGeometryStore(), new BrepBindingModel());

        var decision = StepFaceBoundaryExportPolicy.Decide(body, face);

        Assert.NotNull(decision);
        Assert.Equal(StepFaceBoundaryExportPolicy.AuthoredOrderCandidate, decision.Candidate);
        Assert.Equal(
            [(new LoopId(7), FaceBoundaryRole.Outer), (new LoopId(3), FaceBoundaryRole.Inner)],
            decision.Boundaries);
    }

    [Fact]
    public void BoundaryPolicy_RejectsPartialRoleEvidenceInsteadOfGuessing()
    {
        var faceId = new FaceId(1);
        var firstLoop = new LoopId(7);
        var face = new Face(faceId, [firstLoop, new LoopId(3)]);
        var bindings = new BrepBindingModel();
        bindings.AddFaceBoundaryRoleBinding(new FaceBoundaryRoleBinding(faceId, firstLoop, FaceBoundaryRole.Outer, 101));
        var body = new BrepBody(new TopologyModel(), new BrepGeometryStore(), bindings);

        Assert.Null(StepFaceBoundaryExportPolicy.Decide(body, face));
    }

    [Fact]
    public void PeriodicEquality_IsStableAcrossWholePeriodShifts()
    {
        Assert.True(SurfacePeriodicity.EquivalentModuloPeriod(0.125d, 0.125d + double.Tau, double.Tau, 1e-10d));
        Assert.True(SurfacePeriodicity.EquivalentModuloPeriod(-double.Pi, double.Pi, double.Tau, 1e-10d));
        Assert.False(SurfacePeriodicity.EquivalentModuloPeriod(0.125d, 0.25d, double.Tau, 1e-10d));
    }

    [Fact]
    public void Stc06_RemainsExplicitlyAmbiguous()
    {
        var imported = Step242Corpus.Import(NistPath("STC/nist_stc_06_asme1_ap242-e3.stp"));
        Assert.True(imported.IsSuccess);
        Assert.Contains(imported.Value.FaceOrientationReport!.Shells,
            shell => shell.Qualification == FaceOrientationQualification.Ambiguous);
    }

    private static string NistPath(string relativePath) => $"testdata/step242/nist/{relativePath}";
}
