namespace Aetheris.Kernel.Firmament.FirmamentV2;

public sealed record FirmamentV2Document(string ModelName, string Units, IReadOnlyList<FirmamentV2SolidBinding> Solids, IReadOnlyList<FirmamentV2ModifyBlock>? ModifyBlocks = null)
{
    public FirmamentV2SolidBinding Solid => Solids[^1];
    public FirmamentV2SideHoleIntent? SideHoleIntent => ModifyBlocks?.SelectMany(m => m.Regions.Select(r => new FirmamentV2SideHoleIntent(m.TargetSolid, r.Name, r.Attachment.Axis, r.Cut.Tool.Through.Axis, r.Cut.Tool.ToolType, r.Cut.Tool.Radius, r.Cut.Tool.Center?.U ?? 0, r.Cut.Tool.Center?.V ?? 0, r.Cut.Tool.Center is not null, r.Cut.Tool.Center?.Convention ?? FirmamentV2FaceLocalPoint2D.PlusXConvention, Units))).SingleOrDefault();
}

public sealed record FirmamentV2SolidBinding(string Name, string RecordType, FirmamentV2BoxRecord Box, string? DerivedFrom = null, IReadOnlyDictionary<string, IReadOnlyList<double>>? Overrides = null)
{
    public bool IsDerived => !string.IsNullOrWhiteSpace(DerivedFrom);
}

public sealed record FirmamentV2BoxRecord(IReadOnlyList<double> Size, IReadOnlyList<FirmamentV2Exposure> Exposures);
public sealed record FirmamentV2Exposure(string Alias, string SelectorKind, string Selector, string RefType, string Axis, string? Subselector);
public sealed record FirmamentV2FaceSelector(string Axis)
{
    public string Source => $"face({Axis})";
}
public sealed record FirmamentV2ModifyBlock(string TargetSolid, IReadOnlyList<FirmamentV2RegionDecl> Regions);
public sealed record FirmamentV2RegionDecl(string Name, string Kind, FirmamentV2FaceSelector Attachment, FirmamentV2CutOperation Cut);
public sealed record FirmamentV2CutOperation(string OperationKind, FirmamentV2CylinderTool Tool);
public sealed record FirmamentV2CylinderTool(string ToolType, double Radius, FirmamentV2FaceLocalPoint2D? Center, FirmamentV2FaceSelector Through);
public sealed record FirmamentV2FaceLocalPoint2D(double U, double V, string Convention)
{
    public const string PlusXConvention = "face(+X):u=+Y,v=+Z";
}
public sealed record FirmamentV2SideHoleIntent(string TargetSolid, string RegionName, string AttachFace, string ThroughFace, string Tool, double Radius, double CenterU, double CenterV, bool CenterExplicit, string CenterSelectorFrame, string Units);
public sealed record FirmamentV2ParseResult(bool IsSuccess, FirmamentV2Document? Document, IReadOnlyList<string> Diagnostics)
{
    public static FirmamentV2ParseResult Success(FirmamentV2Document document, IReadOnlyList<string> diagnostics) => new(true, document, diagnostics);
    public static FirmamentV2ParseResult Failure(IReadOnlyList<string> diagnostics) => new(false, null, diagnostics);
}
