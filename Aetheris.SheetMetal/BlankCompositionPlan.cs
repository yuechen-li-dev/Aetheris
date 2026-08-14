using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.SheetMetal;

public enum BlankCompositionOperationKind
{
    AddMaterialRegion,
    ResolveCornerRelief,
    InsertThroughCut
}

public sealed record BlankCompositionOperation(
    string StableId,
    BlankCompositionOperationKind Kind,
    string SemanticOwner,
    PlanarContour2 Contour,
    string ExpectedTopology,
    int Order);

/// <summary>
/// Exact material-composition authority for an authored flat blank. Operations are
/// ordered by known Sheet Metal topology; this is deliberately not an arbitrary
/// polygon Boolean request.
/// </summary>
public sealed record BlankCompositionPlan(
    string PartId,
    PlanarContour2 BaseContour,
    IReadOnlyList<BlankCompositionOperation> OrderedOperations)
{
    public IReadOnlyList<BlankCompositionOperation> MaterialAdditions=>OrderedOperations.Where(x=>x.Kind==BlankCompositionOperationKind.AddMaterialRegion).ToArray();
    public IReadOnlyList<BlankCompositionOperation> CornerResolutions=>OrderedOperations.Where(x=>x.Kind==BlankCompositionOperationKind.ResolveCornerRelief).ToArray();
    public IReadOnlyList<BlankCompositionOperation> Cuts=>OrderedOperations.Where(x=>x.Kind==BlankCompositionOperationKind.InsertThroughCut).ToArray();
}
