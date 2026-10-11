using System.Collections.Immutable;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aetheris.Cloth3D;
using Aetheris.Humanoid;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Firmament.Garment;

/// <summary>Metre dimensions. This experimental fitted surface is not a flat sewing pattern.</summary>
public sealed record GarmentSkirtSurface(string Id, int Columns = 32, int Rows = 9,
    float WaistAboveHip = .13f, float Length = .48f, float Ease = .02f, float HemFlare = .10f,
    float? HipHeight = null, float? ChestHeight = null);

public sealed record CompiledGarmentSurface(GarmentSkirtSurface Source, HumanoidFigureFit Figure,
    string SourceHash, CompiledCloth3D Cloth, ImmutableArray<int> Waist, ImmutableArray<int> Hem)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { IncludeFields = true, WriteIndented = true };

    public string Artifact(ClothSnapshot3D? settled = null)
    {
        if (settled is not null)
        {
            ClothState3D.ValidateSnapshot(Cloth, settled);
        }
        return JsonSerializer.Serialize(new
        {
            schema = "aetheris.garment.v1", units = "m", upAxis = "Z",
            sourceHash = SourceHash, sourceIdentity = "surface/skirt/" + Source.Id,
            source = Source, figure = Figure, waist = Waist, hem = Hem,
            contentKey = Cloth.ContentKey, definition = Cloth.Definition, settled,
        }, JsonOptions);
    }

    public string Obj(ClothSnapshot3D? snapshot = null)
    {
        if (snapshot is not null)
        {
            ClothState3D.ValidateSnapshot(Cloth, snapshot);
        }
        var positions = snapshot?.Positions ?? Cloth.Definition.Positions;
        var result = new StringBuilder("# Aetheris fitted quad surface; metres; Z up\n");
        foreach (var point in positions)
        {
            result.AppendLine(FormattableString.Invariant($"v {point.X:R} {point.Y:R} {point.Z:R}"));
        }
        foreach (var quad in Cloth.Definition.Surface!.Quads)
        {
            result.AppendLine("f " + string.Join(" ", quad.Vertices.Select(index => index + 1)));
        }
        return result.ToString();
    }
}

/// <summary>Body contours own the fitting geometry; the author declares ease and silhouette.
/// Material cells remain quads. Triangles only lower the surface for rendering/body contact.</summary>
public static class GarmentSurfaceAuthoring
{
    public static CompiledGarmentSurface Skirt(GarmentSkirtSurface source, HumanoidGameplayBody body,
        ClothMaterial3D material)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(material);
        if (string.IsNullOrWhiteSpace(source.Id) || source.Columns is < 16 or > 128
            || source.Rows is < 3 or > 64 || !float.IsFinite(source.WaistAboveHip)
            || !float.IsFinite(source.Length) || !float.IsFinite(source.Ease) || !float.IsFinite(source.HemFlare)
            || source.WaistAboveHip <= 0 || source.WaistAboveHip >= source.Length
            || source.Length > 1.5f || source.Ease <= 0 || source.Ease > .2f
            || source.HemFlare < 0 || source.HemFlare > .5f
            || (source.HipHeight is { } hipHeight && !float.IsFinite(hipHeight))
            || (source.ChestHeight is { } chestHeight && !float.IsFinite(chestHeight)))
        {
            throw new ArgumentException("garment-surface-skirt: Require bounded resolution and positive finite fitting dimensions.");
        }
        var figure = HumanoidFigureFitting.Measure(body, source.ChestHeight * 1000, source.HipHeight * 1000);
        var pelvis = body.Skeleton.Joints.Single(joint => joint.Kind == HumanoidJointKind.Pelvis).GlobalBind;
        var center = new Point3D(pelvis.M41, pelvis.M42, pelvis.M43);
        double waistHeight = figure.Hip.HeightMm + source.WaistAboveHip * 1000;
        var waistContour = HumanoidFigureFitting.Contour(body, waistHeight, center);
        var hipContour = HumanoidFigureFitting.Contour(body, figure.Hip.HeightMm, center);
        float circumference = (float)(waistContour.Measurements.CircumferenceMm / 1000);
        var positions = ImmutableArray.CreateBuilder<Vector3>(source.Columns * source.Rows);
        var coordinates = ImmutableArray.CreateBuilder<Vector2>(positions.Capacity);
        var cells = ImmutableArray.CreateBuilder<ClothQuad3D>(source.Columns * (source.Rows - 1));
        for (int row = 0; row < source.Rows; row++)
        {
            float depth = source.Length * row / (source.Rows - 1);
            float hipFraction = Math.Clamp(depth / source.WaistAboveHip, 0, 1);
            float flareFraction = Math.Max(0, depth - source.WaistAboveHip) / (source.Length - source.WaistAboveHip);
            for (int column = 0; column < source.Columns; column++)
            {
                double angle = -2 * Math.PI * column / source.Columns;
                double waistRadius = Radius(waistContour.Points, center, angle);
                double hipRadius = Radius(hipContour.Points, center, angle);
                double radius = (waistRadius * (1 - hipFraction) + hipRadius * hipFraction) / 1000
                    + source.Ease + source.HemFlare * flareFraction;
                positions.Add(new((float)(center.X / 1000 + radius * Math.Cos(angle)),
                    (float)(center.Y / 1000 + radius * Math.Sin(angle)), (float)(waistHeight / 1000 - depth)));
                coordinates.Add(new(circumference * column / source.Columns, depth));
                if (row == source.Rows - 1)
                {
                    continue;
                }
                int next = (column + 1) % source.Columns;
                int top = row * source.Columns;
                float u0 = circumference * column / source.Columns;
                float u1 = circumference * (column + 1) / source.Columns;
                float v1 = source.Length * (row + 1) / (source.Rows - 1);
                cells.Add(new([top + column, top + next, top + source.Columns + next, top + source.Columns + column],
                    [new(u0, depth), new(u1, depth), new(u1, v1), new(u0, v1)]));
            }
        }
        var surface = new ClothQuadSurface3D(cells.MoveToImmutable(), ClothSurfaceRest3D.FittedSurface);
        var waist = Enumerable.Range(0, source.Columns).ToImmutableArray();
        var hem = Enumerable.Range((source.Rows - 1) * source.Columns, source.Columns).ToImmutableArray();
        var definition = new ClothDefinition3D(source.Id, positions.MoveToImmutable(), coordinates.MoveToImmutable(),
            surface.Triangulate()) { Surface = surface, Material = material, Pins = waist };
        // Includes material and body provenance, not machine paths or private reference geometry.
        string identity = JsonSerializer.Serialize(new { source, material, figure }, new JsonSerializerOptions { IncludeFields = true });
        string hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
        return new(source, figure, hash, definition.Compile(), waist, hem);
    }

    private static double Radius(IReadOnlyList<Point3D> contour, Point3D center, double angle)
    {
        double dx = Math.Cos(angle);
        double dy = Math.Sin(angle);
        double radius = 0;
        for (int index = 0; index < contour.Count; index++)
        {
            var a = contour[index];
            var b = contour[(index + 1) % contour.Count];
            double ex = b.X - a.X;
            double ey = b.Y - a.Y;
            double denominator = dx * ey - dy * ex;
            if (Math.Abs(denominator) < 1e-10)
            {
                continue;
            }
            double ax = a.X - center.X;
            double ay = a.Y - center.Y;
            double distance = (ax * ey - ay * ex) / denominator;
            double fraction = (ax * dy - ay * dx) / denominator;
            if (distance > 0 && fraction >= 0 && fraction <= 1)
            {
                radius = Math.Max(radius, distance);
            }
        }
        if (radius <= 0 || !double.IsFinite(radius))
        {
            throw new InvalidOperationException("garment-surface-contour: No radial intersection; choose an interior fitting axis.");
        }
        return radius;
    }
}
