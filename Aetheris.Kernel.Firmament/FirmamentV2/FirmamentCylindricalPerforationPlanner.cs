namespace Aetheris.Kernel.Firmament.FirmamentV2;

public sealed record FirmamentCylindricalPerforationInstance(int Row, int Column, double Z, double AngleRadians)
{
    public string StableId(string featureId) => $"{featureId}.Row{Row}.Col{Column}";
}

public sealed record FirmamentCylindricalPerforationPlan(
    IReadOnlyList<FirmamentCylindricalPerforationInstance> Instances,
    int Rows, int Columns, double AxialPitch, double AngularPitchRadians,
    string? Diagnostic)
{
    public bool Succeeded => Diagnostic is null;
}

/// <summary>
/// Source-space placement only. The full circumference always has an integer
/// column count, so the periodic seam never owns a duplicate instance.
/// </summary>
public static class FirmamentCylindricalPerforationPlanner
{
    private const int MaximumInstances = 1000;
    private const double Epsilon = 1e-9;

    public static FirmamentCylindricalPerforationPlan Plan(
        double outerRadius, double height, double wallThickness, FirmamentV2PerforationDecl feature)
    {
        FirmamentCylindricalPerforationPlan Reject(string code) => new([], 0, 0, 0, 0, code);
        if (feature.SupportFace != "OuterWall") return Reject("perforation-cylindrical-support-unsupported");
        if (feature.Layout is not ("CylindricalGrid" or "CylindricalStaggered"))
            return Reject("perforation-cylindrical-layout-invalid");
        if (System.Math.Abs(feature.OffsetX) > Epsilon || System.Math.Abs(feature.OffsetY) > Epsilon)
            return Reject("perforation-cylindrical-offset-unsupported");
        var axialPitch = feature.Pitch;
        var circumferentialPitch = feature.CircumferentialPitch ?? feature.Pitch;
        var top = feature.MarginTop ?? feature.Margin;
        var bottom = feature.MarginBottom ?? feature.Margin;
        var values = new[] { outerRadius, height, wallThickness, feature.Diameter, axialPitch,
            circumferentialPitch, top, bottom, feature.MinimumLigament, feature.StartAngleDegrees };
        if (values.Any(value => !double.IsFinite(value)) || outerRadius <= wallThickness || wallThickness <= 0 ||
            height <= wallThickness || feature.Diameter <= 0 || axialPitch <= 0 || circumferentialPitch <= 0 ||
            top < 0 || bottom < 0 || feature.MinimumLigament < 0)
            return Reject("perforation-cylindrical-dimension-invalid");
        var radius = feature.Diameter / 2;
        var innerRadius = outerRadius - wallThickness;
        if (radius >= innerRadius) return Reject("perforation-cylindrical-tangent-or-oversize");
        var minZ = Math.Max(wallThickness, bottom) + radius;
        var maxZ = height - top - radius;
        if (maxZ < minZ - Epsilon) return Reject("perforation-no-admissible-holes");
        if (axialPitch + Epsilon < feature.Diameter + feature.MinimumLigament)
            return Reject("perforation-hole-overlap");
        var midRadius = outerRadius - wallThickness / 2;
        var idealColumns = 2 * Math.PI * midRadius / circumferentialPitch;
        if (!double.IsFinite(idealColumns) || idealColumns < 2 || idealColumns > MaximumInstances * 2)
            return Reject("perforation-cylindrical-column-count-invalid");
        var columns = Math.Max(2, (int)Math.Round(idealColumns, MidpointRounding.AwayFromZero));
        var angularPitch = 2 * Math.PI / columns;
        // Clearance is checked at the smaller inner wall, where adjacent
        // radial cutters approach most closely.
        var chord = 2 * innerRadius * Math.Sin(angularPitch / 2);
        if (chord + Epsilon < feature.Diameter + feature.MinimumLigament)
            return Reject("perforation-hole-overlap");
        var rows = (int)Math.Floor((maxZ - minZ + Epsilon) / axialPitch) + 1;
        if (rows < 1) return Reject("perforation-no-admissible-holes");
        if ((long)rows * columns > MaximumInstances) return Reject("perforation-count-exceeded");
        var firstZ = minZ + (maxZ - minZ - (rows - 1) * axialPitch) / 2;
        var start = (feature.StartAngleDegrees % 360d) * Math.PI / 180;
        var instances = new List<FirmamentCylindricalPerforationInstance>(rows * columns);
        for (var row = 0; row < rows; row++)
        {
            var shift = feature.Layout == "CylindricalStaggered" && row % 2 == 1 ? 0.5 : 0;
            for (var column = 0; column < columns; column++)
            {
                var angle = (start + (column + shift) * angularPitch) % (2 * Math.PI);
                if (angle < 0) angle += 2 * Math.PI;
                instances.Add(new(row, column, firstZ + row * axialPitch, angle));
            }
        }
        return new(instances, rows, columns, axialPitch, angularPitch, null);
    }
}
