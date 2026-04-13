using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Results;

namespace Aetheris.Kernel.Core.Brep.EdgeFinishing;

/// <summary>
/// M5b bounded manufacturing-fillet preflight for explicit internal concave planar vertical edges on
/// orthogonal full-height additive-root footprints represented by occupied cells.
/// This is intentionally a bounded selector/radius gate and not a general blend/corner-resolution system.
/// </summary>
public static class BrepBoundedManufacturingFilletPreflight
{
    public static KernelResult<BoundedManufacturingFilletSelection> ResolveInternalConcaveVerticalEdge(
        SafeBooleanComposition composition,
        BrepBoundedManufacturingFilletEdge edge,
        double radius)
    {
        if (!double.IsFinite(radius) || radius <= 0d)
        {
            return KernelResult<BoundedManufacturingFilletSelection>.Failure([Failure("Bounded fillet radius must be finite and greater than 0.", "firmament.fillet-bounded")]);
        }

        var cells = composition.OccupiedCells;
        if (cells is null || cells.Count == 0)
        {
            return KernelResult<BoundedManufacturingFilletSelection>.Failure([Failure(
                "Bounded M5b fillet requires an occupied-cell orthogonal additive-root source so internal concave edge candidates can be identified explicitly.",
                "firmament.fillet-bounded")]);
        }

        var minZ = cells[0].MinZ;
        var maxZ = cells[0].MaxZ;
        for (var i = 1; i < cells.Count; i++)
        {
            if (!NearlyEqual(cells[i].MinZ, minZ) || !NearlyEqual(cells[i].MaxZ, maxZ))
            {
                return KernelResult<BoundedManufacturingFilletSelection>.Failure([Failure(
                    "Bounded M5b fillet currently supports full-height prismatic occupied-cell roots only; mixed-Z occupied cells are outside this milestone.",
                    "firmament.fillet-bounded")]);
            }
        }

        var xCoords = cells.SelectMany(cell => new[] { cell.MinX, cell.MaxX }).Distinct().OrderBy(v => v).ToArray();
        var yCoords = cells.SelectMany(cell => new[] { cell.MinY, cell.MaxY }).Distinct().OrderBy(v => v).ToArray();
        if (xCoords.Length < 2 || yCoords.Length < 2)
        {
            return KernelResult<BoundedManufacturingFilletSelection>.Failure([Failure("Bounded fillet preflight could not recover a usable XY cell grid.", "firmament.fillet-bounded")]);
        }

        var occupied = new bool[xCoords.Length - 1, yCoords.Length - 1];
        for (var xi = 0; xi < xCoords.Length - 1; xi++)
        {
            for (var yi = 0; yi < yCoords.Length - 1; yi++)
            {
                var cx = 0.5d * (xCoords[xi] + xCoords[xi + 1]);
                var cy = 0.5d * (yCoords[yi] + yCoords[yi + 1]);
                occupied[xi, yi] = cells.Any(cell =>
                    cx > cell.MinX && cx < cell.MaxX &&
                    cy > cell.MinY && cy < cell.MaxY);
            }
        }

        var corners = new List<BoundedManufacturingFilletCornerCandidate>();
        for (var xi = 1; xi < xCoords.Length - 1; xi++)
        {
            for (var yi = 1; yi < yCoords.Length - 1; yi++)
            {
                var sw = occupied[xi - 1, yi - 1];
                var se = occupied[xi, yi - 1];
                var nw = occupied[xi - 1, yi];
                var ne = occupied[xi, yi];
                var occupiedCount = (sw ? 1 : 0) + (se ? 1 : 0) + (nw ? 1 : 0) + (ne ? 1 : 0);
                if (occupiedCount != 3)
                {
                    continue;
                }

                var dx = System.Math.Min(xCoords[xi] - xCoords[xi - 1], xCoords[xi + 1] - xCoords[xi]);
                var dy = System.Math.Min(yCoords[yi] - yCoords[yi - 1], yCoords[yi + 1] - yCoords[yi]);
                var maxRadius = 0.5d * System.Math.Min(dx, dy);
                if (maxRadius <= 0d || !double.IsFinite(maxRadius))
                {
                    continue;
                }

                corners.Add(new BoundedManufacturingFilletCornerCandidate(xCoords[xi], yCoords[yi], maxRadius));
            }
        }

        if (corners.Count == 0)
        {
            return KernelResult<BoundedManufacturingFilletSelection>.Failure([Failure(
                "Bounded M5b fillet only supports explicit internal concave planar-planar vertical edges; no qualifying internal concave edge was found on the source body.",
                "firmament.fillet-bounded")]);
        }

        var selected = SelectCorner(edge, corners);
        if (selected is null)
        {
            return KernelResult<BoundedManufacturingFilletSelection>.Failure([Failure(
                "Bounded fillet edge token did not resolve to a unique internal concave edge candidate on this source body.",
                "firmament.fillet-bounded")]);
        }

        if (radius >= selected.Value.MaxAllowedRadius)
        {
            return KernelResult<BoundedManufacturingFilletSelection>.Failure([Failure(
                "Bounded fillet radius is too large for the selected internal concave edge; radius must be strictly less than the local bounded neighborhood extent.",
                "firmament.fillet-bounded")]);
        }

        return KernelResult<BoundedManufacturingFilletSelection>.Success(
            new BoundedManufacturingFilletSelection(selected.Value.X, selected.Value.Y, minZ, maxZ, selected.Value.MaxAllowedRadius));
    }

    private static BoundedManufacturingFilletCornerCandidate? SelectCorner(
        BrepBoundedManufacturingFilletEdge edge,
        IReadOnlyList<BoundedManufacturingFilletCornerCandidate> corners)
    {
        var orderedByXThenY = corners.OrderBy(c => c.X).ThenBy(c => c.Y).ToArray();
        var orderedByXThenYDesc = corners.OrderByDescending(c => c.X).ThenBy(c => c.Y).ToArray();

        return edge switch
        {
            BrepBoundedManufacturingFilletEdge.InnerXMinYMin => orderedByXThenY.First(),
            BrepBoundedManufacturingFilletEdge.InnerXMinYMax => orderedByXThenY.OrderByDescending(c => c.Y).First(),
            BrepBoundedManufacturingFilletEdge.InnerXMaxYMin => orderedByXThenYDesc.First(),
            BrepBoundedManufacturingFilletEdge.InnerXMaxYMax => orderedByXThenYDesc.OrderByDescending(c => c.Y).First(),
            _ => null
        };
    }

    private static bool NearlyEqual(double a, double b)
        => System.Math.Abs(a - b) <= 1e-9;

    private static KernelDiagnostic Failure(string message, string source)
        => new(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, message, Source: source);

    private readonly record struct BoundedManufacturingFilletCornerCandidate(double X, double Y, double MaxAllowedRadius);
}

public readonly record struct BoundedManufacturingFilletSelection(
    double EdgeX,
    double EdgeY,
    double MinZ,
    double MaxZ,
    double MaxAllowedRadius);

public enum BrepBoundedManufacturingFilletEdge
{
    InnerXMinYMin,
    InnerXMinYMax,
    InnerXMaxYMin,
    InnerXMaxYMax
}
