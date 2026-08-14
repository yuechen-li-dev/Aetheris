using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.SheetMetal;

public sealed record FabricationBendLine(
    string SemanticId,
    SheetPoint2 Start,
    SheetPoint2 End,
    SheetBendDirection Direction,
    double AngleRadians);

public sealed record SheetMetalFabricationIr(
    string PartId,
    string Units,
    PlanarContourLoop2 OuterCutContour,
    IReadOnlyList<PlanarContourLoop2> InnerCutContours,
    IReadOnlyList<FabricationBendLine> BendLines,
    IReadOnlyList<string> ReliefSemanticIds,
    string DeterministicHash);

public static class SheetMetalFabricationArtifacts
{
    public static SheetMetalFabricationIr Create(SheetMetalPartIr part,SheetMetalFlatPatternIr flat)
    {
        ArgumentNullException.ThrowIfNull(part);ArgumentNullException.ThrowIfNull(flat);
        var contour=flat.ExactBlankContour??throw new InvalidOperationException("Fabrication IR requires an accepted exact blank contour.");
        return new(part.StableId,"mm",contour.OuterLoop,contour.InnerLoops,
            flat.BendLines.OrderBy(x=>x.BendId,StringComparer.Ordinal).Select(x=>new FabricationBendLine(x.BendId,x.Start,x.End,x.Direction,x.BendAngleRadians)).ToArray(),
            (flat.ReliefLoops??[]).OrderBy(x=>x.ReliefId,StringComparer.Ordinal).Select(x=>x.ReliefId).ToArray(),flat.DeterministicHash);
    }
}
