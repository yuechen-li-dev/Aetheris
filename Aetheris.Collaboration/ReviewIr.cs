namespace Aetheris.Collaboration;

public enum ReviewEntryKind { Comment, Issue, Proposal, Resolution }
public enum ReviewStatus { Open, Accepted, Rejected, Resolved, Superseded }

public sealed record ReviewIdentity
{
    public ReviewIdentity(string name, string? organization = null, string? email = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Review author name is required.", nameof(name));
        Name = name; Organization = organization; Email = email;
    }
    public string Name { get; }
    public string? Organization { get; }
    public string? Email { get; }
}

public sealed record StructuredProposalIr(
    string Property,
    string? CurrentValue,
    string ProposedValue,
    string? Units,
    string? Rationale);

public sealed record ReviewTargetIr(
    string SemanticReference,
    string SourcePath,
    string? CurrentEngineeringValue,
    IReadOnlyList<string> Capabilities,
    string? PresentationReference = null);

public sealed record ReviewEntryIr(
    string Id,
    ReviewEntryKind Kind,
    ReviewIdentity Author,
    DateOnly AuthoredDate,
    string Text,
    StructuredProposalIr? Proposal,
    int AuthoredOrder);

public sealed record ReviewThreadIr(
    string Id,
    ReviewTargetIr Target,
    ReviewStatus Status,
    IReadOnlyList<ReviewEntryIr> Entries,
    string Provenance);

public sealed record ReviewIr(
    IReadOnlyList<ReviewThreadIr> Threads,
    string OrderingPolicy = "Authored source order; stable thread and entry identities are preserved.",
    string SchemaVersion = "aetheris-review-m0");

public sealed record ReviewCompilationResult(
    bool IsSuccess,
    ReviewIr? Review,
    IReadOnlyList<string> Diagnostics,
    IReadOnlyList<(int Start, int Length)> SourceRanges);
