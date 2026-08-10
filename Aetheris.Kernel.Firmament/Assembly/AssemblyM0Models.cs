using Aetheris.Semantics;

namespace Aetheris.Kernel.Firmament.Assembly;

public enum AssemblyInstanceKind { Assembly, Part }
public enum AssemblyDiagnosticSeverity { Warning, Error }
public enum PlacementConstraintKind { AxisCoincident, AxisAligned, PlaneCoincident, PointCoincident, OffsetAlongAxis }
public enum PlacementStatus { Anchored, Resolved, Underconstrained, Overconstrained, Unresolved }

public sealed record AssemblyDiagnostic(string Code, string Message, AssemblyDiagnosticSeverity Severity = AssemblyDiagnosticSeverity.Error);
public sealed record AssemblyPath(IReadOnlyList<string> Segments)
{
    public override string ToString() => string.Join(".", Segments);
    public static AssemblyPath Parse(string value) => new(value.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    public AssemblyPath Append(string segment) => new([.. Segments, segment]);
}

public sealed record InterfaceRoleDefinition(string Name, IReadOnlyList<string> RequiredCapabilities, int Minimum = 1, int Maximum = 1);
public sealed record InterfaceRequirementDefinition(PlacementConstraintKind Kind, string FirstRole, string FirstMember, string SecondRole, string SecondMember, double OffsetMm = 0);
public sealed record InterfaceFitDefinition(string ShaftRole, string ShaftDimension, string BoreRole, string BoreDimension);
public sealed record InterfaceDefinition(
    string StableId, string Name, IReadOnlyList<InterfaceRoleDefinition> Roles,
    IReadOnlyList<InterfaceRequirementDefinition> Requirements,
    InterfaceFitDefinition? Fit = null,
    IReadOnlyList<string>? AdmittedFreeMotions = null,
    SemanticSourceSpan? SourceSpan = null);

public sealed record AssemblyMemberSource(
    string Name, AssemblyInstanceKind Kind, string DefinitionIdentity,
    IReadOnlyList<AssemblyMemberSource> Children,
    IReadOnlyList<SemanticValue> ExposedSemantics,
    IReadOnlyList<DimensionalRelationSource>? DimensionalRelations = null,
    IReadOnlyList<SemanticProvenance>? Provenance = null);

public sealed record MateRoleAssignment(string Role, AssemblyPath Participant);
public sealed record MateSource(string Name, string InterfaceName, IReadOnlyList<MateRoleAssignment> Roles, SemanticSourceSpan? SourceSpan = null);
public sealed record DimensionalRelationSource(string Name, AssemblyPath From, AssemblyPath To, double Nominal, double LowerTolerance, double UpperTolerance, string Unit, string Provenance);
public sealed record ToleranceStackupAssertSource(string Name, AssemblyPath From, AssemblyPath To, double RequiredMinimum, string Unit, SemanticSourceSpan? SourceSpan = null);
public sealed record AssemblySource(
    string Name, AssemblyMemberSource Root, IReadOnlyList<InterfaceDefinition> Interfaces,
    IReadOnlyList<MateSource> Mates, AssemblyPath Anchor,
    IReadOnlyList<DimensionalRelationSource> DimensionalRelations,
    IReadOnlyList<ToleranceStackupAssertSource> StackupAsserts,
    string SourceIdentity,
    string? DefinitionSource = null);

public sealed record AssemblyTransform(double[] Matrix)
{
    public static AssemblyTransform Identity { get; } = new([1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1]);
}

public sealed record AssemblyInstanceIr(
    string StableId, AssemblyPath Path, AssemblyInstanceKind Kind, string DefinitionIdentity,
    string? ParentStableId, IReadOnlyList<string> ChildrenStableIds,
    SemanticValue SemanticRoot, AssemblyTransform? LocalTransform, AssemblyTransform? ResolvedTransform,
    IReadOnlyList<SemanticProvenance> Provenance);

public sealed record MateEndpointIr(string Role, AssemblyPath ParticipantPath, string ParticipantSemanticValueId, IReadOnlyList<string> RequiredCapabilities);
public sealed record MateIr(string StableId, string Name, string InterfaceStableId, IReadOnlyList<MateEndpointIr> Roles, IReadOnlyList<string> ConstraintIds, string ValidationStatus);
public sealed record PlacementConstraintIr(
    string StableId, PlacementConstraintKind Kind, string MateStableId,
    string FirstSemanticValueId, string SecondSemanticValueId, double OffsetMm,
    double Residual, string Status);
public sealed record PlacementResultIr(string InstanceStableId, PlacementStatus Status, AssemblyTransform? Transform, IReadOnlyList<string> FreeTranslations, IReadOnlyList<string> FreeRotations, IReadOnlyList<string> ConstraintIds);

public sealed record DimensionalRelationIr(
    string StableId, string FromSemanticValueId, string ToSemanticValueId,
    double Nominal, double LowerTolerance, double UpperTolerance, string Unit,
    int Sign, string OriginInstancePath, string Provenance, string? MateStableId = null, string? InterfaceStableId = null,
    IReadOnlyList<SemanticProvenance>? SourceProvenance = null);

public sealed record StackupContributionIr(
    string RelationStableId, int Sign, double Nominal, double LowerTolerance, double UpperTolerance,
    string Unit, string OriginInstancePath, string Provenance, string? MateStableId, string? InterfaceStableId,
    IReadOnlyList<SemanticProvenance>? SourceProvenance = null);
public sealed record ToleranceStackupResultIr(
    string Name, string StartSemanticValueId, string EndSemanticValueId,
    double Nominal, double WorstCaseMinimum, double WorstCaseMaximum,
    double RequiredMinimum, string Unit, bool Passed, string Status,
    IReadOnlyList<StackupContributionIr> Contributions);
public sealed record InterfaceFitResultIr(
    string MateStableId, double NominalClearance, double WorstCaseMinimum, double WorstCaseMaximum, string Unit, bool Compatible);

public sealed record AssemblyPerformanceIr(double ParseMilliseconds, double BindMilliseconds, double MateValidationMilliseconds, double PlacementMilliseconds, double DimensionalGraphMilliseconds, double ToleranceAnalysisMilliseconds, double DefinitionMaterializationMilliseconds = 0, double GeometryExecutionMilliseconds = 0);
public sealed record AssemblyGeometryMetricsIr(int Bodies, int Faces, int Edges, int Vertices, double[] Minimum, double[] Maximum);
public sealed record AssemblyDefinitionArtifactIr(string StableId, string DefinitionIdentity, string SpecializationIdentity, string StepSha256, AssemblyGeometryMetricsIr Metrics, IReadOnlyList<SemanticProvenance> Provenance);
public sealed record AssemblyInstanceGeometryIr(string InstanceStableId, string DefinitionArtifactStableId, AssemblyTransform WorldTransform, AssemblyGeometryMetricsIr Metrics);
public sealed record AssemblyMateResidualIr(string ConstraintStableId, PlacementConstraintKind Kind, double PositionResidualMm, double AngularResidualRadians, bool Passed, string Evidence);
public sealed record AssemblyGeometryArtifactIr(string Schema, IReadOnlyList<AssemblyDefinitionArtifactIr> Definitions, IReadOnlyList<AssemblyInstanceGeometryIr> Instances, IReadOnlyList<AssemblyMateResidualIr> MateResiduals, string DeterministicSha256);
public sealed record AssemblyIr(
    string Schema, string StableId, string Name, string RootInstanceStableId,
    IReadOnlyList<AssemblyInstanceIr> Instances, IReadOnlyList<InterfaceDefinition> Interfaces,
    IReadOnlyList<MateIr> Mates, IReadOnlyList<PlacementConstraintIr> PlacementConstraints,
    IReadOnlyList<PlacementResultIr> Placements, IReadOnlyList<DimensionalRelationIr> DimensionalRelations,
    IReadOnlyList<ToleranceStackupResultIr> ToleranceStackups, IReadOnlyList<InterfaceFitResultIr> FitResults,
    IReadOnlyList<AssemblyDiagnostic> Diagnostics);

public sealed record AssemblyCompilationResult(AssemblyIr? Ir, IReadOnlyList<AssemblyDiagnostic> Diagnostics, AssemblyPerformanceIr? Performance = null)
{
    public bool IsSuccess => Ir is not null && Diagnostics.All(d => d.Severity != AssemblyDiagnosticSeverity.Error);
}
