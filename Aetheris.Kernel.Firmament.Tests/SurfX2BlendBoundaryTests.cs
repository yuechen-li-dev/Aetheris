using Aetheris.Surfacing;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class SurfX2BlendBoundaryTests
{
    [Fact]
    public void JudgedHousingGeneratesRejectsScoresAndRealizesG2Winner()
    {
        var result = SculptingAuthoring.CompileFile(Fixture("Canonical", "Sculpting", "surf-x2-judged-housing.firmament"));

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics.Select(item => $"{item.Code}: {item.Message}")));
        var state = Assert.IsType<BodyState>(result.OutputState);
        var trace = Assert.IsType<BlendJudgmentTrace>(state.BlendJudgment);
        Assert.Equal("StandardBlendJudgment/v1", trace.JudgmentPolicyId);
        Assert.Equal("PowerM3Degree6", trace.SelectedCandidateId);
        Assert.Equal(4, trace.Candidates.Count);
        var rejected = Assert.Single(trace.Candidates, candidate => candidate.CandidateId == "PowerM2Degree4");
        Assert.Equal(BlendCandidateDisposition.Rejected, rejected.Disposition);
        Assert.Contains("g2-unsatisfied", rejected.RejectionReason, StringComparison.Ordinal);
        Assert.Equal(0.1024d, rejected.BoundaryEvidence.MaximumNormalCurvatureError, 8);
        var selected = Assert.Single(trace.Candidates, candidate => candidate.Disposition == BlendCandidateDisposition.Selected);
        Assert.Equal(BlendContinuity.G2, selected.ContinuityCapability);
        Assert.Equal(0d, selected.BoundaryEvidence.MaximumNormalCurvatureError);
        Assert.Equal(3, trace.Candidates.Count(candidate => candidate.Metrics is not null));
        Assert.All(state.SurfacePatches.Single().ContinuityContracts, contract => Assert.Equal(PatchBoundaryContinuity.G2, contract.Continuity));
        Assert.Contains(state.ValidationEvidence, item => item.Check.EndsWith(":G2", StringComparison.Ordinal) && item.Satisfied && item.Level == LocalityEvidenceLevel.ExactAnalytic);
        Assert.Equal("PowerM3Degree6", state.Delta!.BlendJudgment!.SelectedCandidateId);
        var export = SculptStepExporter.Export(state, "JudgedBlendHousing");
        Assert.True(export.IsSuccess); Assert.Equal(1, export.Inventory.NonRationalBSpline); Assert.Equal(0, export.Inventory.RationalNurbs);
    }

    [Fact]
    public void CandidateSetWinnerDeltaAndStepAreDeterministic()
    {
        var source = File.ReadAllText(Fixture("Canonical", "Sculpting", "surf-x2-judged-housing.firmament"));
        var first = SculptingAuthoring.Compile(source); var second = SculptingAuthoring.Compile(source);

        Assert.True(first.IsSuccess); Assert.True(second.IsSuccess);
        Assert.Equal(first.OutputState!.StateId, second.OutputState!.StateId);
        Assert.Equal(first.OutputState.BlendJudgment!.CandidateSetId, second.OutputState.BlendJudgment!.CandidateSetId);
        Assert.Equal(first.OutputState.BlendJudgment.SelectedCandidateId, second.OutputState.BlendJudgment.SelectedCandidateId);
        Assert.Equal(SculptStepExporter.Export(first.OutputState, first.ModelName).Step, SculptStepExporter.Export(second.OutputState, second.ModelName).Step);
    }

    [Fact]
    public void BlendPersistsAsBlendIntentAndReplaysWithoutCollapsingToReplaceRegion()
    {
        var compiled = SculptingAuthoring.CompileFile(Fixture("Canonical", "Sculpting", "surf-x2-judged-housing.firmament"));
        Assert.True(compiled.IsSuccess);
        var authority = Assert.IsType<ConstructionState>(compiled.OutputState!.ConstructionAuthority);
        var operation = Assert.Single(authority.Operations);
        Assert.Equal("BlendBoundary", operation.OperationKind);
        Assert.IsType<BlendBoundaryOperation>(operation.Payload);

        var replay = ConstructionStateReplayer.Replay(authority);
        Assert.True(replay.IsSuccess, string.Join(" | ", replay.Diagnostics.Select(item => item.Message)));
        Assert.Equal(compiled.OutputState.StateId, replay.OutputState!.StateId);
        Assert.Equal(compiled.OutputState.BlendJudgment!.SelectedCandidateId, replay.OutputState.BlendJudgment!.SelectedCandidateId);
    }

    [Fact]
    public void ExplicitFallbackAndEligibleManualOverrideAreVisible()
    {
        var source = File.ReadAllText(Fixture("Canonical", "Sculpting", "surf-x2-judged-housing.firmament"));
        var fallback = SculptingAuthoring.Compile(source.Replace("Minimum: G2", "Minimum: G1", StringComparison.Ordinal).Replace("MaximumDegree: 10", "MaximumDegree: 4", StringComparison.Ordinal));
        Assert.True(fallback.IsSuccess, string.Join(" | ", fallback.Diagnostics.Select(item => item.Message)));
        Assert.Equal("PowerM2Degree4", fallback.OutputState!.BlendJudgment!.SelectedCandidateId);
        Assert.Contains("active G1", fallback.OutputState.BlendJudgment.Request, StringComparison.Ordinal);

        var overridden = SculptingAuthoring.Compile(source.Replace("Policy: StandardBlendJudgment", "Policy: StandardBlendJudgment\n      UseCandidate: PowerM4Degree8", StringComparison.Ordinal));
        Assert.True(overridden.IsSuccess, string.Join(" | ", overridden.Diagnostics.Select(item => item.Message)));
        Assert.True(overridden.OutputState!.BlendJudgment!.ManualOverride);
        Assert.Equal("PowerM4Degree8", overridden.OutputState.BlendJudgment.SelectedCandidateId);
    }

    [Fact]
    public void RequiredG2FailsWhenDegreeGateLeavesOnlyG1()
    {
        var result = SculptingAuthoring.CompileFile(Fixture("Invalid", "Sculpting", "surf-x2-g2-unsatisfied.firmament"));
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, item => item.Code == "surf-blend-no-eligible-candidates");
    }

    private static string Fixture(params string[] parts) => Path.Combine([Root(), "fixtures", .. parts]);
    private static string Root() { var directory = new DirectoryInfo(AppContext.BaseDirectory); while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent; return directory!.FullName; }
}
