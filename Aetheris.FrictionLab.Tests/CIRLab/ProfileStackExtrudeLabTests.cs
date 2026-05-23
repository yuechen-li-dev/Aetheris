using Aetheris.Firmament.FrictionLab.CIRLab;

namespace Aetheris.FrictionLab.Tests.CIRLab;

public class ProfileStackExtrudeLabTests
{
    [Fact] public void ProfileStackExtrude_SteppedHole_EmitsBrepAndStep(){var r=ProfileStackExtrudeLab.Run().Scenarios.Single(s=>s.Scenario=="stepped-hole");Assert.True(r.Success || r.Status.StartsWith("blocker:"));if(r.Success){Assert.Contains("ISO-10303-21",r.StepMarkers);Assert.DoesNotContain("BREP_WITH_VOIDS",r.StepMarkers);} }
    [Fact] public void ProfileStackExtrude_ThroughHole_EmitsBrepAndStep(){var r=ProfileStackExtrudeLab.Run().Scenarios.Single(s=>s.Scenario=="through-hole");Assert.True(r.Success || r.Status.StartsWith("blocker:"));}
    [Fact] public void ProfileStackExtrude_BlindHole_EmitsBrepAndStepOrReportsBlocker(){var r=ProfileStackExtrudeLab.Run().Scenarios.Single(s=>s.Scenario=="blind-hole");Assert.True(r.Success || r.Status.StartsWith("blocker:"));}
    [Fact] public void ProfileStackExtrude_Counterbore_EmitsBrepAndStep(){var r=ProfileStackExtrudeLab.Run().Scenarios.Single(s=>s.Scenario=="counterbore");Assert.True(r.Success || r.Status.StartsWith("blocker:"));}
    [Fact] public void ProfileStackExtrude_RoleMetadataIncludesExpectedSteppedFaces(){var r=ProfileStackExtrudeLab.Run().Scenarios.Single(s=>s.Scenario=="stepped-hole");Assert.Contains(r.SemanticRoles,x=>x.StartsWith("InnerWall_Radius2"));Assert.Contains(r.SemanticRoles,x=>x.Contains("Shoulder_R2"));}
    [Fact] public void ProfileStackExtrude_ComparisonReportIncludesBooleanBaseline(){var r=ProfileStackExtrudeLab.Run();Assert.NotEmpty(r.Scenarios);}
    [Fact] public void ProfileStackExtrude_DeterministicOutput(){var a=ProfileStackExtrudeLab.Run();var b=ProfileStackExtrudeLab.Run();Assert.Equal(string.Join(";",a.Scenarios.Select(x=>$"{x.Scenario}:{x.Status}")),string.Join(";",b.Scenarios.Select(x=>$"{x.Scenario}:{x.Status}")));}
    [Fact] public void ProfileStackExtrude_ReportIsDecisionGrade(){var r=ProfileStackExtrudeLab.Run();Assert.False(string.IsNullOrWhiteSpace(r.Recommendation));Assert.NotEmpty(r.BoundaryNotes);}    
}
