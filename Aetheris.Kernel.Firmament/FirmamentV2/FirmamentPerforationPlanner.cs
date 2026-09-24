namespace Aetheris.Kernel.Firmament.FirmamentV2;

public sealed record FirmamentPerforationInstance(int Row, int Column, double X, double Y)
{
    public string StableId(string featureId) => $"{featureId}.Row{Row}.Col{Column}";
}

public sealed record FirmamentPerforationPlan(
    IReadOnlyList<FirmamentPerforationInstance> Instances, string? Diagnostic)
{
    public bool Succeeded => Diagnostic is null;
}

/// <summary>Deterministic box face placement and DFM admission, independent of BRep ordering.</summary>
public static class FirmamentPerforationPlanner
{
    private const int MaximumInstances = 1000;
    private const double Tol = 1e-9;

    public static FirmamentPerforationPlan Plan(double width, double height, FirmamentV2PerforationDecl feature)
    {
        if (feature.SupportFace != "+Z") return new([], "perforation-support-unsupported: only the planar +Z Box face is currently materialized");
        if (feature.Layout is not ("Grid" or "Hex")) return new([], "perforation-layout-invalid: expected Grid or Hex");
        if (!double.IsFinite(feature.Diameter) || feature.Diameter <= 0 || !double.IsFinite(feature.Pitch) || feature.Pitch <= 0)
            return new([], "perforation-dimension-invalid: Diameter and Pitch must be positive finite lengths");
        if (!double.IsFinite(feature.Margin) || feature.Margin < 0 || !double.IsFinite(feature.MinimumLigament) || feature.MinimumLigament < 0
            || !double.IsFinite(feature.OffsetX) || !double.IsFinite(feature.OffsetY))
            return new([], "perforation-policy-invalid: margin and ligament must be nonnegative; offsets must be finite");
        if (feature.Pitch <= feature.Diameter + Tol || feature.Pitch - feature.Diameter + Tol < feature.MinimumLigament)
            return new([], "perforation-hole-overlap: pitch must exceed diameter and satisfy MinimumLigament");
        var radius = feature.Diameter / 2;
        var xmin = -width / 2 + feature.Margin + radius;
        var xmax = width / 2 - feature.Margin - radius;
        var ymin = -height / 2 + feature.Margin + radius;
        var ymax = height / 2 - feature.Margin - radius;
        if (!double.IsFinite(xmin) || !double.IsFinite(xmax) || !double.IsFinite(ymin) || !double.IsFinite(ymax) || xmax < xmin - Tol || ymax < ymin - Tol)
            return new([], "perforation-support-too-small: the support cannot fit a full opening and margin");
        var rowPitch = feature.Layout == "Hex" ? feature.Pitch * Math.Sqrt(3) / 2 : feature.Pitch;
        if (Math.Abs(feature.OffsetX) >= feature.Pitch || Math.Abs(feature.OffsetY) >= rowPitch)
            return new([], "perforation-offset-invalid: offsets must remain within one lattice pitch");
        if ((xmax - xmin) / feature.Pitch > MaximumInstances || (ymax - ymin) / rowPitch > MaximumInstances)
            return new([], "perforation-count-exceeded: pattern dimensions exceed the bounded 1000-opening planner");
        var xOrigin = xmin + ((xmax - xmin) % feature.Pitch) / 2;
        var yOrigin = ymin + ((ymax - ymin) % rowPitch) / 2;
        var result = new List<FirmamentPerforationInstance>();
        // The default field is centered; indices remain anchored to this lattice when an offset clips holes.
        var rows = Math.Min(MaximumInstances + 1, (int)Math.Ceiling((ymax - ymin) / rowPitch) + 2);
        var columns = Math.Min(MaximumInstances + 1, (int)Math.Ceiling((xmax - xmin) / feature.Pitch) + 2);
        for (var row = 0; row < rows; row++)
        {
            var y = yOrigin + feature.OffsetY + row * rowPitch;
            if (y > ymax + Tol) break;
            if (y < ymin - Tol) continue;
            var stagger = feature.Layout == "Hex" && row % 2 == 1 ? feature.Pitch / 2 : 0;
            for (var column = 0; column < columns; column++)
            {
                var x = xOrigin + feature.OffsetX + stagger + column * feature.Pitch;
                if (x > xmax + Tol) break;
                if (x < xmin - Tol) continue;
                result.Add(new(row, column, x, y));
                if (result.Count > MaximumInstances) return new([], "perforation-count-exceeded: at most 1000 openings are admitted");
            }
        }
        return result.Count == 0 ? new([], "perforation-no-admissible-holes: offset and margin leave no full opening") : new(result, null);
    }
}
