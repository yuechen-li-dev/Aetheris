using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Results;

namespace Aetheris.Kernel.Core.Brep.EdgeFinishing;

/// <summary>
/// Bounded concave chamfer preflight for explicit internal concave planar-planar vertical edges on
/// full-height occupied-cell additive-root sources.
/// This stage resolves selector + distance bounds and intentionally does not perform geometry rewrite.
/// </summary>
public static class BrepBoundedConcaveChamferPreflight
{
    public static KernelResult<BoundedConcaveChamferSelection> ResolveInternalConcaveVerticalEdge(
        SafeBooleanComposition? composition,
        BrepBoundedChamferEdge edge,
        double distance)
    {
        if (!edge.IsInternalConcaveToken())
        {
            return KernelResult<BoundedConcaveChamferSelection>.Failure([Failure(
                "Bounded concave chamfer preflight requires an explicit internal concave edge token.",
                "firmament.chamfer-bounded")]);
        }

        if (!double.IsFinite(distance) || distance <= 0d)
        {
            return KernelResult<BoundedConcaveChamferSelection>.Failure([Failure(
                "Bounded chamfer distance must be finite and greater than 0.",
                "firmament.chamfer-bounded")]);
        }

        var cells = composition?.OccupiedCells;
        if (cells is null || cells.Count == 0)
        {
            return KernelResult<BoundedConcaveChamferSelection>.Failure([Failure(
                "Bounded concave chamfer requires occupied-cell additive-root metadata for internal-edge resolution.",
                "firmament.chamfer-bounded")]);
        }

        var minZ = cells.Min(cell => cell.MinZ);
        var maxZ = cells.Max(cell => cell.MaxZ);
        for (var i = 0; i < cells.Count; i++)
        {
            if (!NearlyEqual(cells[i].MinZ, minZ) || !NearlyEqual(cells[i].MaxZ, maxZ))
            {
                return KernelResult<BoundedConcaveChamferSelection>.Failure([Failure(
                    "Bounded concave chamfer currently supports full-height prismatic occupied-cell roots only; mixed-Z occupied cells are outside this milestone.",
                    "firmament.chamfer-bounded")]);
            }
        }

        var xCoords = cells.SelectMany(cell => new[] { cell.MinX, cell.MaxX }).Distinct().OrderBy(v => v).ToArray();
        var yCoords = cells.SelectMany(cell => new[] { cell.MinY, cell.MaxY }).Distinct().OrderBy(v => v).ToArray();
        if (xCoords.Length < 2 || yCoords.Length < 2)
        {
            return KernelResult<BoundedConcaveChamferSelection>.Failure([Failure(
                "Bounded concave chamfer preflight could not recover a usable XY cell grid.",
                "firmament.chamfer-bounded")]);
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

        var corners = new List<BoundedConcaveChamferCornerCandidate>();
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

                var maxDistance = System.Math.Min(
                    System.Math.Min(xCoords[xi] - xCoords[xi - 1], xCoords[xi + 1] - xCoords[xi]),
                    System.Math.Min(yCoords[yi] - yCoords[yi - 1], yCoords[yi + 1] - yCoords[yi]));
                if (maxDistance <= 0d || !double.IsFinite(maxDistance))
                {
                    continue;
                }

                corners.Add(new BoundedConcaveChamferCornerCandidate(xCoords[xi], yCoords[yi], maxDistance));
            }
        }

        if (corners.Count == 0)
        {
            return KernelResult<BoundedConcaveChamferSelection>.Failure([Failure(
                "Bounded concave chamfer supports explicit internal concave planar-planar vertical edges only; no qualifying edge was found.",
                "firmament.chamfer-bounded")]);
        }

        var selected = SelectCorner(edge, corners);
        if (selected is null)
        {
            return KernelResult<BoundedConcaveChamferSelection>.Failure([Failure(
                "Bounded concave chamfer edge token did not resolve to a unique internal concave edge candidate on this source body.",
                "firmament.chamfer-bounded")]);
        }

        if (distance >= selected.Value.MaxAllowedDistance)
        {
            return KernelResult<BoundedConcaveChamferSelection>.Failure([Failure(
                "Bounded chamfer distance is too large for the selected internal concave edge; distance must be strictly less than the local bounded neighborhood extent.",
                "firmament.chamfer-bounded")]);
        }

        var interactionInference = InferInteractingCorner(selected.Value, corners, distance);
        if (interactionInference.Status == ConcaveInteractionInferenceStatus.Ambiguous)
        {
            return KernelResult<BoundedConcaveChamferSelection>.Failure([Failure(
                "Bounded concave chamfer 2-edge interaction inference found multiple nearby concave-edge interactions; this milestone supports exactly one inferred interacting edge.",
                "firmament.chamfer-bounded")]);
        }

        if (interactionInference.Corner.HasValue && distance >= interactionInference.Corner.Value.MaxAllowedDistance)
        {
            return KernelResult<BoundedConcaveChamferSelection>.Failure([Failure(
                "Bounded chamfer distance is too large for the inferred interacting internal concave edge; distance must be strictly less than both local bounded neighborhood extents.",
                "firmament.chamfer-bounded")]);
        }

        return KernelResult<BoundedConcaveChamferSelection>.Success(new BoundedConcaveChamferSelection(
            selected.Value.X,
            selected.Value.Y,
            minZ,
            maxZ,
            selected.Value.MaxAllowedDistance,
            interactionInference.Corner.HasValue,
            interactionInference.Corner?.X,
            interactionInference.Corner?.Y));
    }

    private static BoundedConcaveChamferCornerCandidate? SelectCorner(
        BrepBoundedChamferEdge edge,
        IReadOnlyList<BoundedConcaveChamferCornerCandidate> corners)
    {
        var orderedByXThenY = corners.OrderBy(c => c.X).ThenBy(c => c.Y).ToArray();
        var orderedByXThenYDesc = corners.OrderByDescending(c => c.X).ThenBy(c => c.Y).ToArray();

        return edge switch
        {
            BrepBoundedChamferEdge.InnerXMinYMin => orderedByXThenY.First(),
            BrepBoundedChamferEdge.InnerXMinYMax => orderedByXThenY.OrderByDescending(c => c.Y).First(),
            BrepBoundedChamferEdge.InnerXMaxYMin => orderedByXThenYDesc.First(),
            BrepBoundedChamferEdge.InnerXMaxYMax => orderedByXThenYDesc.OrderByDescending(c => c.Y).First(),
            _ => null
        };
    }

    private static bool NearlyEqual(double a, double b)
        => System.Math.Abs(a - b) <= 1e-9;

    private static InferredConcaveInteraction InferInteractingCorner(
        BoundedConcaveChamferCornerCandidate selectedCorner,
        IReadOnlyList<BoundedConcaveChamferCornerCandidate> corners,
        double distance)
    {
        var nearby = corners
            .Where(candidate => !NearlyEqual(candidate.X, selectedCorner.X) || !NearlyEqual(candidate.Y, selectedCorner.Y))
            .Where(candidate =>
            {
                var sharesX = NearlyEqual(candidate.X, selectedCorner.X);
                var sharesY = NearlyEqual(candidate.Y, selectedCorner.Y);
                if (sharesX == sharesY)
                {
                    return false;
                }

                var span = sharesX
                    ? System.Math.Abs(candidate.Y - selectedCorner.Y)
                    : System.Math.Abs(candidate.X - selectedCorner.X);
                return span <= (2d * distance) + 1e-9d;
            })
            .OrderBy(candidate => System.Math.Abs(candidate.X - selectedCorner.X) + System.Math.Abs(candidate.Y - selectedCorner.Y))
            .ToArray();

        if (nearby.Length == 0)
        {
            return new InferredConcaveInteraction(ConcaveInteractionInferenceStatus.None, null);
        }

        if (nearby.Length > 1
            && NearlyEqual(
                System.Math.Abs(nearby[0].X - selectedCorner.X) + System.Math.Abs(nearby[0].Y - selectedCorner.Y),
                System.Math.Abs(nearby[1].X - selectedCorner.X) + System.Math.Abs(nearby[1].Y - selectedCorner.Y)))
        {
            return new InferredConcaveInteraction(ConcaveInteractionInferenceStatus.Ambiguous, null);
        }

        return new InferredConcaveInteraction(ConcaveInteractionInferenceStatus.Unique, nearby[0]);
    }

    private static KernelDiagnostic Failure(string message, string source)
        => new(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, message, Source: source);

    private readonly record struct BoundedConcaveChamferCornerCandidate(double X, double Y, double MaxAllowedDistance);
    private readonly record struct InferredConcaveInteraction(ConcaveInteractionInferenceStatus Status, BoundedConcaveChamferCornerCandidate? Corner);
    private enum ConcaveInteractionInferenceStatus
    {
        None,
        Unique,
        Ambiguous
    }
}

public readonly record struct BoundedConcaveChamferSelection(
    double EdgeX,
    double EdgeY,
    double MinZ,
    double MaxZ,
    double MaxAllowedDistance,
    bool HasInteractingEdge,
    double? InteractingEdgeX,
    double? InteractingEdgeY);
