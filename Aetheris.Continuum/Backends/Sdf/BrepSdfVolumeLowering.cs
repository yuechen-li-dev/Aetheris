using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Queries;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Continuum.Backends.Sdf;

/// <summary>A complete final-boundary recognition, not a construction-history mirror.</summary>
public sealed record BrepSdfVolumeField(SdfNode Root, string Qualification, RectangularPlateDefinition Stock)
{
    public IReadOnlyList<FaceId> CoveredFaces => Stock.CoveredFaces;
}

public static class BrepSdfVolumeLowering
{
    /// <summary>Every admitted boundary belongs to the recognized stock or its
    /// disjoint round through holes. Additional material, pockets, blends or
    /// unsupported trims reject the entire lowering; no partial field escapes.</summary>
    public static bool TryLower(BrepBody body, out BrepSdfVolumeField? field, out string reason)
    {
        ArgumentNullException.ThrowIfNull(body);
        field = null;
        if (body.FaceOrientationReport is not { } report || report.Shells.Count != 1
            || report.Shells.Any(s => s.IsInnerVoid || !s.IsClosedManifold || s.Qualification != FaceOrientationQualification.DerivedQualified))
        {
            reason = "Final-body SDF requires one qualified closed material shell with resolved orientation.";
            return false;
        }
        if (!BrepRectangularPlateQuery.TryDefine(body, out var plate, out reason)) return false;
        var stock = plate!;
        var center = new Vector3D(stock.Origin.X + stock.Width / 2,
            stock.Origin.Y + stock.Height / 2, stock.Origin.Z + stock.Thickness / 2);
        if (!double.IsFinite(stock.Thickness * 3))
        {
            reason = "Through-hole extension exceeds the finite field domain.";
            return false;
        }
        SdfNode root = new SdfTransformNode(new SdfBoxNode(stock.Width, stock.Height, stock.Thickness),
            Transform3D.CreateTranslation(center));
        foreach (var hole in stock.Holes)
        {
            // Extend past both stock caps: a finite tool ending on a cap would
            // leave a spurious zero-valued disk in the hole mouth.
            var tool = new SdfTransformNode(new SdfCylinderNode(hole.Diameter / 2, stock.Thickness * 3),
                Transform3D.CreateTranslation(new(hole.X, hole.Y, center.Z)));
            root = new SdfSubtractNode(root, tool);
        }
        field = new(root, "CompleteFinalBrepRectangularStockThroughHoles", stock);
        reason = "Every final face, loop and edge is covered by exact stock and through-hole recognition.";
        return true;
    }
}
