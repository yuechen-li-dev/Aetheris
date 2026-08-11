using Aetheris.Collaboration;

namespace Aetheris.Collaboration.Tests;

public sealed class ReviewCompilerTests
{
    private const string Source = """
Review DFM-004 {
  Target: Housing.Bore.Diameter;
  Status: Resolved;
  Issue DFM-004-I1 { Author: "Alice Chen"; Organization: "Northstar Machining"; Date: 2026-08-10; Text: "Current tolerance requires finish grinding."; }
  Proposal DFM-004-P1 { Author: "Alice Chen"; Date: 2026-08-10; Property: tolerance; Current: PlusMinus(0.005mm); Proposed: PlusMinus(0.010mm); Reason: "Avoid a secondary grinding operation."; }
  Resolution DFM-004-R1 { Author: "Daniel Ruiz"; Organization: "Aster Works"; Date: 2026-08-10; Text: "Accepted for the next authoritative revision."; }
}
""";

    [Fact]
    public void CompilesTypedThreadAndProposalWithoutMutatingCurrentValue()
    {
        var targets = new Dictionary<string, (string?, IReadOnlyList<string>)> { ["Housing.Bore.Diameter"] = ("PlusMinus(0.005mm)", ["Dimensional", "PMI"]) };
        var result = FirmamentReviewCompiler.Compile(Source, "part.firmament", targets);
        Assert.True(result.IsSuccess, string.Join("; ", result.Diagnostics));
        var thread = Assert.Single(result.Review!.Threads); Assert.Equal(ReviewStatus.Resolved, thread.Status); Assert.Equal(3, thread.Entries.Count);
        var proposal = thread.Entries.Single(item => item.Kind == ReviewEntryKind.Proposal).Proposal!;
        Assert.Equal("PlusMinus(0.005mm)", proposal.CurrentValue); Assert.Equal("PlusMinus(0.010mm)", proposal.ProposedValue);
        Assert.Equal("PlusMinus(0.005mm)", targets["Housing.Bore.Diameter"].Item1);
    }

    [Theory]
    [InlineData("Author: \"Alice Chen\";", "", FirmamentReviewCompiler.AuthorRequired)]
    [InlineData("Date: 2026-08-10;", "", FirmamentReviewCompiler.DateRequired)]
    [InlineData("Date: 2026-08-10;", "Date: 2026-02-30;", FirmamentReviewCompiler.DateInvalid)]
    public void RejectsMissingOrInvalidAuthoredData(string before, string after, string diagnostic)
    {
        var result = FirmamentReviewCompiler.Compile(Source.Replace(before, after, StringComparison.Ordinal), "part.firmament");
        Assert.False(result.IsSuccess); Assert.Contains(result.Diagnostics, item => item.StartsWith(diagnostic, StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsUnknownTargetAndUnitMismatch()
    {
        var mismatched = Source.Replace("PlusMinus(0.010mm)", "PlusMinus(0.010in)", StringComparison.Ordinal);
        var result = FirmamentReviewCompiler.Compile(mismatched, "part.firmament", new Dictionary<string, (string?, IReadOnlyList<string>)> { ["Other"] = (null, []) });
        Assert.Contains(result.Diagnostics, item => item.StartsWith(FirmamentReviewCompiler.TargetUnknown, StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, item => item.StartsWith(FirmamentReviewCompiler.UnitMismatch, StringComparison.Ordinal));
    }
}
