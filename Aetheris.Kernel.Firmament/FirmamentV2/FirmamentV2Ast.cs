namespace Aetheris.Kernel.Firmament.FirmamentV2;

public sealed record FirmamentV2Document(string ModelName, string Units, IReadOnlyList<FirmamentV2SolidBinding> Solids, IReadOnlyList<FirmamentV2ModifyBlock>? ModifyBlocks = null, IReadOnlyList<FirmamentV2TemplateDecl>? Templates = null, IReadOnlyList<FirmamentV2PmiDecl>? Pmi = null)
{
    public FirmamentV2SolidBinding Solid => Solids[^1];
    public FirmamentV2SideHoleIntent? SideHoleIntent => ModifyBlocks?.SelectMany(m => m.Regions.Select(r =>
    {
        var targetSolid = Solids.Single(s => string.Equals(s.Name, m.TargetSolid, StringComparison.Ordinal));
        var route = FirmamentV2SideHoleRoutePolicy.Resolve(r.Attachment.Axis, r.Cut.Tool.Through.Axis, targetSolid.Box!.Size, r.Cut.Tool.Radius, r.Cut.Tool.Center?.U ?? 0, r.Cut.Tool.Center?.V ?? 0).Route!;
        return new FirmamentV2SideHoleIntent(m.TargetSolid, r.Name, r.Attachment.Source, r.Attachment.Kind, r.Attachment.Axis, r.Cut.Tool.Through.Source, r.Cut.Tool.Through.Kind, r.Cut.Tool.Through.Axis, r.Cut.Tool.ToolType, r.Cut.Tool.Radius, r.Cut.Tool.Center?.U ?? 0, r.Cut.Tool.Center?.V ?? 0, r.Cut.Tool.Center is not null, route.CenterFrame, Units, route);
    })).SingleOrDefault();
}

public sealed record FirmamentV2SolidBinding(string Name, string RecordType, FirmamentV2PrimitiveRecord Primitive, string? DerivedFrom = null, IReadOnlyDictionary<string, IReadOnlyList<double>>? Overrides = null)
{
    public bool IsDerived => !string.IsNullOrWhiteSpace(DerivedFrom);
    public FirmamentV2BoxRecord? Box => Primitive as FirmamentV2BoxRecord;
    public FirmamentV2CylinderRecord? Cylinder => Primitive as FirmamentV2CylinderRecord;
    public FirmamentV2ConeRecord? Cone => Primitive as FirmamentV2ConeRecord;
    public FirmamentV2SphereRecord? Sphere => Primitive as FirmamentV2SphereRecord;
    public FirmamentV2TorusRecord? Torus => Primitive as FirmamentV2TorusRecord;
}

public abstract record FirmamentV2PrimitiveRecord;
public sealed record FirmamentV2BoxRecord(IReadOnlyList<double> Size, IReadOnlyList<FirmamentV2Exposure> Exposures) : FirmamentV2PrimitiveRecord;
public sealed record FirmamentV2CylinderRecord(double Radius, double Height) : FirmamentV2PrimitiveRecord;
public sealed record FirmamentV2ConeRecord(double BottomRadius, double TopRadius, double Height) : FirmamentV2PrimitiveRecord;
public sealed record FirmamentV2SphereRecord(double Radius) : FirmamentV2PrimitiveRecord;
public sealed record FirmamentV2TorusRecord(double MajorRadius, double MinorRadius) : FirmamentV2PrimitiveRecord;
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
