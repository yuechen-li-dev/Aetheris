namespace Aetheris.Kernel.Firmament.FirmamentV2;

/// <summary>Normalized, immutable source model for the Concept Path frontend.</summary>
public sealed record ConceptPathDeclaration(string Name, string SourceSpan, string LocalFrame, ConceptPathStart Start, double InitialHeadingDegrees, IReadOnlyList<ConceptPathStep> Steps, string StableId, string Provenance);
public sealed record ConceptPathStart(double X, double Y, string SourceSpan);
public abstract record ConceptPathStep(string Name, string SourceSpan, string StableGuideId, string StableEndpointId);
public sealed record ConceptPathLine(string Name, string SourceSpan, ConceptPathDirection Direction, double? Length, string? TargetPoint, string StableGuideId, string StableEndpointId) : ConceptPathStep(Name, SourceSpan, StableGuideId, StableEndpointId);
public sealed record ConceptPathArc(string Name, string SourceSpan, double Radius, double TurnDegrees, string StableGuideId, string StableEndpointId) : ConceptPathStep(Name, SourceSpan, StableGuideId, StableEndpointId);
public sealed record ConceptPathClose(string Name, string SourceSpan, string StableGuideId, string StableEndpointId) : ConceptPathStep(Name, SourceSpan, StableGuideId, StableEndpointId);
public abstract record ConceptPathDirection;
public sealed record ContinueHeading : ConceptPathDirection;
public sealed record RelativeTurn(double Degrees) : ConceptPathDirection;
public sealed record AbsoluteHeading(double Degrees) : ConceptPathDirection;
public sealed record ToPoint(string Reference) : ConceptPathDirection;

/// <summary>Compact resolved view used by the existing profile inspection JSON.</summary>
public sealed record ConceptPathInspection(
    string Name,
    double StartX,
    double StartY,
    double InitialHeadingDegrees,
    IReadOnlyList<ConceptPathEntryInspection> Entries,
    IReadOnlyList<string>? Capabilities = null,
    IReadOnlyList<ConceptPathExposedMemberInspection>? ExposedMembers = null,
    IReadOnlyList<ConceptPathConsumerInspection>? Consumers = null,
    string? Provenance = null);
public sealed record ConceptPathEntryInspection(string Name, string Kind, double StartX, double StartY, double EndX, double EndY, double HeadingDegrees, double? Radius, double? SweepDegrees, string GuideId, string EndpointId);
public sealed record ConceptPathExposedMemberInspection(string Name, string Kind, string Capability, string StableId);
public sealed record ConceptPathConsumerInspection(string Kind, string Name, string RequiredCapability, string Provenance);
