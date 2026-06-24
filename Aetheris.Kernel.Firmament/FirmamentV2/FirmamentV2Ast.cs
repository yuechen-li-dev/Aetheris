namespace Aetheris.Kernel.Firmament.FirmamentV2;

public sealed record FirmamentV2Document(string ModelName, string Units, IReadOnlyList<FirmamentV2SolidBinding> Solids, IReadOnlyList<FirmamentV2ModifyBlock>? ModifyBlocks = null, IReadOnlyList<FirmamentV2TemplateDecl>? Templates = null, IReadOnlyList<FirmamentV2PmiDecl>? Pmi = null, IReadOnlyList<FirmamentV2RecognizedRegion>? RecognizedRegions = null, IReadOnlyList<FirmamentV2ReplacementDecl>? Replacements = null, IReadOnlyList<FirmamentV2LetDeclaration>? Lets = null, IReadOnlyList<FirmamentV2BoundLet>? BoundLets = null, IReadOnlyList<FirmamentV2LetRecordDeclaration>? LetRecords = null, IReadOnlyList<FirmamentV2BoundLetRecord>? BoundLetRecords = null)
{
    public FirmamentV2SolidBinding Solid => Solids[^1];
    public FirmamentV2SideHoleIntent? SideHoleIntent => ModifyBlocks?.SelectMany(m => m.Regions.Select(r =>
    {
        var targetSolid = Solids.Single(s => string.Equals(s.Name, m.TargetSolid, StringComparison.Ordinal));
        var route = FirmamentV2SideHoleRoutePolicy.Resolve(r.Attachment.Axis, r.Cut.Tool.Through.Axis, targetSolid.Box!.Size, r.Cut.Tool.Radius, r.Cut.Tool.Center?.U ?? 0, r.Cut.Tool.Center?.V ?? 0).Route!;
        return new FirmamentV2SideHoleIntent(m.TargetSolid, r.Name, r.Attachment.Source, r.Attachment.Kind, r.Attachment.Axis, r.Cut.Tool.Through.Source, r.Cut.Tool.Through.Kind, r.Cut.Tool.Through.Axis, r.Cut.Tool.ToolType, r.Cut.Tool.Radius, r.Cut.Tool.Center?.U ?? 0, r.Cut.Tool.Center?.V ?? 0, r.Cut.Tool.Center is not null, route.CenterFrame, Units, route);
    })).SingleOrDefault();
}

public sealed record FirmamentV2SourceSpan(int Start, int Length);
public enum FirmamentV2PrimitiveType { Int, Float, Length, Angle, String, Bool }
public sealed record FirmamentV2LiteralValue(FirmamentV2PrimitiveType Type, object Value, double? NumericValue = null, string? Unit = null, string? Raw = null);
public abstract record FirmamentV2ValueExpression;
public sealed record FirmamentV2LiteralExpression(FirmamentV2LiteralValue Value) : FirmamentV2ValueExpression;
public sealed record FirmamentV2DottedReferenceExpression(string RecordName, string FieldName, string Source) : FirmamentV2ValueExpression;
public sealed record FirmamentV2IdentifierReferenceExpression(string Name, string Source) : FirmamentV2ValueExpression;
public sealed record FirmamentV2BinaryExpression(FirmamentV2ValueExpression Left, string Operator, FirmamentV2ValueExpression Right, string Source) : FirmamentV2ValueExpression;
public sealed record FirmamentV2LetDeclaration(string Name, FirmamentV2PrimitiveType DeclaredType, FirmamentV2ValueExpression ValueExpression, FirmamentV2SourceSpan SourceSpan)
{
    public FirmamentV2LiteralValue LiteralValue => ValueExpression is FirmamentV2LiteralExpression literal ? literal.Value : throw new InvalidOperationException("Let value is not a literal.");
}
public sealed record FirmamentV2LetRecordDeclaration(string Name, IReadOnlyList<FirmamentV2LetRecordField> Fields, FirmamentV2SourceSpan SourceSpan);
public sealed record FirmamentV2LetRecordField(string Name, FirmamentV2PrimitiveType DeclaredType, FirmamentV2ValueExpression ValueExpression, FirmamentV2SourceSpan SourceSpan)
{
    public FirmamentV2LiteralValue LiteralValue => ValueExpression is FirmamentV2LiteralExpression literal ? literal.Value : throw new InvalidOperationException("Record field value is not a literal.");
}
public sealed record FirmamentV2BoundExpression(FirmamentV2PrimitiveType InferredType, FirmamentV2LiteralValue Value, IReadOnlySet<string> Dependencies, FirmamentV2SourceSpan SourceSpan);
public sealed record FirmamentV2BoundLet(string Name, FirmamentV2PrimitiveType Type, FirmamentV2LiteralValue Value, FirmamentV2SourceSpan SourceSpan, FirmamentV2BoundExpression? Expression = null, IReadOnlySet<string>? Dependencies = null);
public sealed record FirmamentV2BoundLetRecord(string Name, IReadOnlyDictionary<string, FirmamentV2BoundLet> Fields, FirmamentV2SourceSpan SourceSpan);

public sealed record FirmamentV2SolidBinding(string Name, string RecordType, FirmamentV2PrimitiveRecord Primitive, string? DerivedFrom = null, IReadOnlyDictionary<string, IReadOnlyList<double>>? Overrides = null)
{
    public bool IsDerived => !string.IsNullOrWhiteSpace(DerivedFrom);
    public FirmamentV2BoxRecord? Box => Primitive as FirmamentV2BoxRecord;
    public FirmamentV2CylinderRecord? Cylinder => Primitive as FirmamentV2CylinderRecord;
    public FirmamentV2ConeRecord? Cone => Primitive as FirmamentV2ConeRecord;
    public FirmamentV2SphereRecord? Sphere => Primitive as FirmamentV2SphereRecord;
    public FirmamentV2TorusRecord? Torus => Primitive as FirmamentV2TorusRecord;
    public FirmamentV2InlineStepRecord? InlineStep => Primitive as FirmamentV2InlineStepRecord;
}

public abstract record FirmamentV2PrimitiveRecord;
public sealed record FirmamentV2BoxRecord(IReadOnlyList<double> Size, IReadOnlyList<FirmamentV2Exposure> Exposures) : FirmamentV2PrimitiveRecord;
public sealed record FirmamentV2CylinderRecord(double Radius, double Height) : FirmamentV2PrimitiveRecord;
public sealed record FirmamentV2ConeRecord(double BottomRadius, double TopRadius, double Height) : FirmamentV2PrimitiveRecord;
public sealed record FirmamentV2SphereRecord(double Radius) : FirmamentV2PrimitiveRecord;
public sealed record FirmamentV2TorusRecord(double MajorRadius, double MinorRadius) : FirmamentV2PrimitiveRecord;
public sealed record FirmamentV2InlineStepRecord(string SourcePath, string NormalizedPath, string ContentHash, bool CanonicalInput, string CanonicalEvidence, ImportedStepTopologyMap TopologyMap) : FirmamentV2PrimitiveRecord;
public sealed record ImportedStepTopologyMap(IReadOnlyDictionary<string, string> FaceEntityToFaceId, IReadOnlyDictionary<string, string> FaceIdToFaceEntity)
{
    public bool TryResolveFaceEntity(string entityRef, out string faceId) => FaceEntityToFaceId.TryGetValue(entityRef, out faceId!);
}
public sealed record FirmamentV2RecognizedRegion(string BodyName, string RegionName, string Kind, IReadOnlyList<string> FaceRefs, string Confidence, FirmamentV2RecognitionEvidence? Evidence = null, FirmamentV2SemanticProposal? Proposal = null)
{
    public string TargetSource => $"{BodyName}.region(\"{RegionName}\")";
}

public sealed record FirmamentV2RecognitionEvidence(IReadOnlyList<string> SurfaceFamilies, double? Radius = null, string? Axis = null, FirmamentV2FaceLocalPoint2D? Center = null, bool? Through = null, IReadOnlyList<string>? Notes = null);
public sealed record FirmamentV2SemanticProposal(string ProposalKind, string FeatureName, string? PlacementTarget = null, FirmamentV2FaceLocalPoint2D? Center = null, double? Radius = null, string? EndCondition = null);

public sealed record FirmamentV2ReplacementDecl(string ImportedBodyName, string RecognizedRegionName, string ReplacementKind, string ReplacementFeatureName, string PlacementTarget, FirmamentV2FaceLocalPoint2D Center, double Radius, string EndCondition, IReadOnlyList<double> HostSize, string Source)
{
    public string TargetSource => $"{ImportedBodyName}.region(\"{RecognizedRegionName}\")";
}

public sealed record FirmamentV2ImportedStepFaceTarget(string BodySymbol, string EntityRef)
{
    public string Source => $"{BodySymbol}.face(\"{EntityRef}\")";
}
public sealed record FirmamentV2Exposure(string Alias, string SelectorKind, string Selector, string RefType, string Axis, string? Subselector);
public sealed record FirmamentV2FaceTarget(string Source, string Kind, string Axis, string ResolvedSelector, string RefType)
{
    public static FirmamentV2FaceTarget Direct(string axis) => new($"face({axis})", "DirectSelector", axis, $"face({axis})", "FaceRef");
    public static FirmamentV2FaceTarget Alias(string alias, string axis) => new(alias, "Alias", axis, $"face({axis})", "FaceRef");
}

public sealed record FirmamentV2FaceSelector(string Axis)
{
    public string Source => $"face({Axis})";
}
public sealed record FirmamentV2ModifyBlock(string TargetSolid, IReadOnlyList<FirmamentV2RegionDecl> Regions, IReadOnlyList<FirmamentV2SemanticHoleDecl> SemanticHoles)
{
    public FirmamentV2ModifyBlock(string TargetSolid, IReadOnlyList<FirmamentV2RegionDecl> Regions) : this(TargetSolid, Regions, []) { }
}
public sealed record FirmamentV2RegionDecl(string Name, string Kind, FirmamentV2FaceTarget Attachment, FirmamentV2CutOperation Cut);
public sealed record FirmamentV2CutOperation(string OperationKind, FirmamentV2CylinderTool Tool);
public sealed record FirmamentV2CylinderTool(string ToolType, double Radius, FirmamentV2FaceLocalPoint2D? Center, FirmamentV2FaceTarget Through);
public sealed record FirmamentV2FaceLocalPoint2D(double U, double V, string Convention)
{
    public const string PlusXConvention = "face(+X):u=+Y,v=+Z";
    public const string MinusXConvention = "face(-X):u=+Y,v=+Z";
    public const string PlusYConvention = "face(+Y):u=+X,v=+Z";
    public const string MinusYConvention = "face(-Y):u=+X,v=+Z";
    public const string PlusZConvention = "face(+Z):u=+X,v=+Y";
    public const string MinusZConvention = "face(-Z):u=+X,v=+Y";
    public static string ConventionFor(string attachFace) => attachFace switch
    {
        "-X" => MinusXConvention,
        "+Y" => PlusYConvention,
        "-Y" => MinusYConvention,
        "+Z" => PlusZConvention,
        "-Z" => MinusZConvention,
        _ => PlusXConvention
    };
}
public sealed record FirmamentV2SideHoleIntent(string TargetSolid, string RegionName, string AttachTargetSource, string AttachTargetKind, string AttachFace, string ThroughTargetSource, string ThroughTargetKind, string ThroughFace, string Tool, double Radius, double CenterU, double CenterV, bool CenterExplicit, string CenterSelectorFrame, string Units, FirmamentV2SideHoleRoutePolicyEvidence PolicyRoute)
{
    public string Route => PolicyRoute.Direction;
    public FirmamentV2SideHoleRoutePolicyEvidence RouteEvidence => PolicyRoute;
}
public enum FirmamentV2SemanticHoleVariant { Shaft, Counterbore, Countersink }
public enum FirmamentV2SemanticHoleEndKind { ThroughAll, Depth }
public sealed record FirmamentV2SemanticHoleEnd(FirmamentV2SemanticHoleEndKind Kind, double? Depth = null);
public sealed record FirmamentV2SemanticHoleDecl(
    string Name,
    FirmamentV2SemanticHoleVariant Variant,
    FirmamentV2FaceTarget EntryFace,
    FirmamentV2FaceLocalPoint2D Center,
    double ShaftDiameter,
    FirmamentV2SemanticHoleEnd EndCondition,
    double? CounterboreDiameter = null,
    double? CounterboreDepth = null,
    double? CountersinkDiameter = null,
    double? CountersinkAngleDegrees = null);
public enum FirmamentV2PmiKind { HoleDiameter, DatumPlane }
public sealed record FirmamentV2PmiDecl(string Name, FirmamentV2PmiKind Kind, string Target, double? Value = null);
public sealed record FirmamentV2ConceptDecl(string Name, string RawValue, double NumericValue, string? Unit);
public sealed record FirmamentV2TemplateDecl(string Process, string Name, IReadOnlyList<FirmamentV2ConceptDecl> Concepts);

public sealed record FirmamentV2ParseResult(bool IsSuccess, FirmamentV2Document? Document, IReadOnlyList<string> Diagnostics)
{
    public static FirmamentV2ParseResult Success(FirmamentV2Document document, IReadOnlyList<string> diagnostics) => new(true, document, diagnostics);
    public static FirmamentV2ParseResult Failure(IReadOnlyList<string> diagnostics) => new(false, null, diagnostics);
}
