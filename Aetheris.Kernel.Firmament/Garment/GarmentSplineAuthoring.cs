using System.Collections.Immutable;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aetheris.Cloth3D;
using Aetheris.Humanoid;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Surfacing;

namespace Aetheris.Kernel.Firmament.Garment;

/// <summary>Metre dimensions; guides identify body section heights, not garment vertices.
/// This bounded experiment authors two shared-boundary bicubic midsurfaces.</summary>
public sealed record GarmentTubeTopSurface(string Id, float UpperHeight = 1.38f, float LowerHeight = 1.10f,
    float Ease = .025f, int Columns = 48, int Rows = 13, float ChestHeight = 1.28f, float HipHeight = .96f,
    float UnderarmDrop = .08f);

/// <summary>Retained editable source. Controls and kernel supports are millimetres.
/// Shared control IDs express positional continuity; tangent continuity is not assumed.</summary>
public sealed record GarmentSplineSource(GarmentTubeTopSurface Guides, HumanoidFigureFit Figure,
    IReadOnlyList<SplineFitPatch> Patches);

public sealed record GarmentSplineFit(GarmentSplineSource Source, double SeedRmsMm, double FittedRmsMm,
    double MaximumTargetErrorMm, int TargetCount);

public sealed record GarmentSplineClearance(double MinimumMm, double MaximumMm, int SampleCount,
    int WorstPatch, double WorstU, double WorstV);

public sealed record CompiledSplineGarment(GarmentSplineSource Source, string SourceHash, CompiledCloth3D Cloth)
{
    public string Artifact(ClothSnapshot3D? settled = null)
    {
        if (settled is not null)
        {
            ClothState3D.ValidateSnapshot(Cloth, settled);
        }
        return JsonSerializer.Serialize(new
        {
            schema = "aetheris.garment.v1", units = "m", upAxis = "Z", sourceHash = SourceHash,
            sourceIdentity = "spline/tube-top/" + Source.Guides.Id, source = Source,
            contentKey = Cloth.ContentKey, definition = Cloth.Definition, settled,
        }, GarmentSplineAuthoring.JsonOptions);
    }

    public string Obj(ClothSnapshot3D? snapshot = null)
    {
        if (snapshot is not null)
        {
            ClothState3D.ValidateSnapshot(Cloth, snapshot);
        }
        var result = new StringBuilder("# Aetheris spline-lowered quads; metres; Z up\n");
        foreach (var point in snapshot?.Positions ?? Cloth.Definition.Positions)
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

public static class GarmentSplineAuthoring
{
    internal static readonly JsonSerializerOptions JsonOptions = new() { IncludeFields = true, WriteIndented = true };

    public static GarmentSplineFit FitTubeTop(GarmentTubeTopSurface guides, HumanoidGameplayBody body)
    {
        Validate(guides);
        var figure = HumanoidFigureFitting.Measure(body, guides.ChestHeight * 1000, guides.HipHeight * 1000);
        var chest = body.Skeleton.Joints.Single(joint => joint.Kind == HumanoidJointKind.Chest).GlobalBind;
        var center = new Point3D(chest.M41, chest.M42, chest.M43);
        var contours = Enumerable.Range(0, 13).Select(row => HumanoidFigureFitting.Contour(body,
            1000 * (guides.UpperHeight + (guides.LowerHeight - guides.UpperHeight) * row / 12d), center)).ToArray();
        var shared = new Point3D[12, 4];
        for (int column = 0; column < 12; column++)
        {
            for (int row = 0; row < 4; row++)
            {
                shared[column, row] = Target(row / 3d, Math.PI - 2 * Math.PI * column / 12);
            }
        }
        var patches = new List<SplineFitPatch>();
        var samples = new List<SplineFitSample>();
        double squaredSeed = 0;
        for (int patch = 0; patch < 2; patch++)
        {
            var controls = new List<IReadOnlyList<Point3D>>();
            var ids = new List<int>();
            for (int column = 0; column < 7; column++)
            {
                int global = (patch * 6 + column) % 12;
                controls.Add(Enumerable.Range(0, 4).Select(row => shared[global, row]).ToArray());
                ids.AddRange(Enumerable.Range(0, 4).Select(row => global * 4 + row));
            }
            var surface = new BSplineSurfaceWithKnots(3, 3, controls, "UNSPECIFIED", false, false, false,
                [4, 1, 1, 1, 4], [4, 4], [0, .25, .5, .75, 1], [0, 1], "UNSPECIFIED");
            patches.Add(new(patch == 0 ? "front" : "back", surface, ids));
            for (int row = 0; row < contours.Length; row++)
            {
                for (int column = 0; column <= 24; column++)
                {
                    double u = column / 24d;
                    double v = row / 12d;
                    var target = Target(v, Math.PI - Math.PI * (patch + u));
                    samples.Add(new(patch, u, v, target));
                    double error = (surface.Evaluate(u, v) - target).Length;
                    squaredSeed += error * error;
                }
            }
        }
        var fit = BSplineControlNetFitting.Fit(patches, samples);
        return new(new(guides, figure, fit.Patches), Math.Sqrt(squaredSeed / samples.Count),
            fit.RmsError, fit.MaximumError, samples.Count);

        Point3D Target(double v, double angle)
        {
            double side = Math.Cos(angle);
            double top = guides.UpperHeight - guides.UnderarmDrop * side * side;
            double height = top * (1 - v) + guides.LowerHeight * v;
            double section = (guides.UpperHeight - height) / (guides.UpperHeight - guides.LowerHeight) * 12;
            int low = Math.Clamp((int)Math.Floor(section), 0, 11);
            double fraction = Math.Clamp(section - low, 0, 1);
            double radius = GarmentSurfaceAuthoring.Radius(contours[low].Points, center, angle) * (1 - fraction)
                + GarmentSurfaceAuthoring.Radius(contours[low + 1].Points, center, angle) * fraction + guides.Ease * 1000;
            return new(center.X + radius * Math.Cos(angle), center.Y + radius * Math.Sin(angle), height * 1000);
        }
    }

    /// <summary>Lower the retained support itself. There is no vertex projection or renderer deformation.</summary>
    public static CompiledSplineGarment Lower(GarmentSplineSource source, ClothMaterial3D material)
    {
        Validate(source.Guides);
        ValidatePatches(source.Patches);
        var guides = source.Guides;
        int half = guides.Columns / 2;
        float height = guides.UpperHeight - guides.LowerHeight;
        // Metre-valued chart scale; fitted rest derives its material metric from each physical cell.
        float circumference = (float)(source.Figure.Chest.CircumferenceMm / 1000);
        var positions = ImmutableArray.CreateBuilder<Vector3>(guides.Columns * guides.Rows);
        var coordinates = ImmutableArray.CreateBuilder<Vector2>(positions.Capacity);
        var cells = ImmutableArray.CreateBuilder<ClothQuad3D>(guides.Columns * (guides.Rows - 1));
        for (int row = 0; row < guides.Rows; row++)
        {
            float v = (float)row / (guides.Rows - 1);
            for (int column = 0; column < guides.Columns; column++)
            {
                int patch = column / half;
                double u = (column % half) / (double)half;
                positions.Add(Metres(source.Patches[patch].Surface.Evaluate(u, v)));
                coordinates.Add(new(circumference * column / guides.Columns, height * v));
                if (row == guides.Rows - 1)
                {
                    continue;
                }
                int top = row * guides.Columns;
                int next = (column + 1) % guides.Columns;
                float u0 = circumference * column / guides.Columns;
                float u1 = circumference * (column + 1) / guides.Columns;
                float v0 = height * v;
                float v1 = height * (row + 1) / (guides.Rows - 1);
                cells.Add(new([top + column, top + next, top + guides.Columns + next, top + guides.Columns + column],
                    [new(u0, v0), new(u1, v0), new(u1, v1), new(u0, v1)]));
            }
        }
        var surface = new ClothQuadSurface3D(cells.MoveToImmutable(), ClothSurfaceRest3D.FittedSurface);
        var definition = new ClothDefinition3D(guides.Id, positions.MoveToImmutable(), coordinates.MoveToImmutable(),
            surface.Triangulate())
        {
            Surface = surface, Material = material,
            // Explicit authoring support: this does not claim strapless cloth friction.
            Pins = Enumerable.Range((guides.Rows - 1) * guides.Columns, guides.Columns).ToImmutableArray(),
        };
        string identity = JsonSerializer.Serialize(new { source, material }, JsonOptions);
        string hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
        return new(source, hash, definition.Compile());
    }

    public static GarmentSplineClearance Clearance(GarmentSplineSource source, IClothBodySurface3D body,
        int samplesU = 49, int samplesV = 25)
    {
        ValidatePatches(source.Patches);
        if (samplesU is < 3 or > 256 || samplesV is < 3 or > 256)
        {
            throw new ArgumentException("garment-spline-sampling: Require 3..256 samples per parameter axis.");
        }
        double minimum = double.PositiveInfinity;
        double maximum = double.NegativeInfinity;
        int worstPatch = 0;
        double worstU = 0;
        double worstV = 0;
        for (int patch = 0; patch < source.Patches.Count; patch++)
        {
            for (int row = 0; row < samplesV; row++)
            {
                for (int column = 0; column < samplesU; column++)
                {
                    double u = column / (double)(samplesU - 1);
                    double v = row / (double)(samplesV - 1);
                    double distance = body.Query(Metres(source.Patches[patch].Surface.Evaluate(u, v))).SignedDistance * 1000;
                    if (!double.IsFinite(distance))
                    {
                        throw new InvalidOperationException("garment-spline-clearance: Nonfinite body query.");
                    }
                    if (distance < minimum)
                    {
                        minimum = distance;
                        worstPatch = patch;
                        worstU = u;
                        worstV = v;
                    }
                    maximum = Math.Max(maximum, distance);
                }
            }
        }
        return new(minimum, maximum, source.Patches.Count * samplesU * samplesV, worstPatch, worstU, worstV);
    }

    public static string SerializeSource(GarmentSplineSource source) => JsonSerializer.Serialize(source, JsonOptions);

    public static GarmentSplineSource ParseSource(string json)
    {
        var source = JsonSerializer.Deserialize<GarmentSplineSource>(json, JsonOptions)
            ?? throw new InvalidDataException("garment-spline-source: Empty source.");
        Validate(source.Guides);
        ValidatePatches(source.Patches);
        return source;
    }

    internal static Vector3 Metres(Point3D point) => new((float)(point.X / 1000), (float)(point.Y / 1000), (float)(point.Z / 1000));

    private static void Validate(GarmentTubeTopSurface guides)
    {
        ArgumentNullException.ThrowIfNull(guides);
        if (string.IsNullOrWhiteSpace(guides.Id) || guides.Columns is < 16 or > 128 || guides.Columns % 2 != 0
            || guides.Rows is < 3 or > 64 || !float.IsFinite(guides.UpperHeight) || !float.IsFinite(guides.LowerHeight)
            || guides.LowerHeight <= 0 || guides.UpperHeight <= guides.LowerHeight
            || guides.UpperHeight - guides.LowerHeight > .6f || !float.IsFinite(guides.Ease) || guides.Ease is <= 0 or > .1f
            || !float.IsFinite(guides.UnderarmDrop) || guides.UnderarmDrop < 0
            || guides.UnderarmDrop >= guides.UpperHeight - guides.LowerHeight
            || !float.IsFinite(guides.ChestHeight) || !float.IsFinite(guides.HipHeight))
        {
            throw new ArgumentException("garment-spline-guides: Require bounded, finite body guides, ease and even circumferential resolution.");
        }
    }

    private static void ValidatePatches(IReadOnlyList<SplineFitPatch> patches)
    {
        if (patches.Count != 2 || patches.Any(patch => patch.Surface.IsRational
            || patch.Surface.ControlPoints.Count != 7 || patch.Surface.ControlPoints[0].Count != 4
            || patch.Surface.DegreeU != 3 || patch.Surface.DegreeV != 3 || patch.ControlIds.Count != 28
            || patch.Surface.KnotMultiplicitiesU[0] != 4 || patch.Surface.KnotMultiplicitiesU[^1] != 4
            || patch.Surface.KnotMultiplicitiesV[0] != 4 || patch.Surface.KnotMultiplicitiesV[^1] != 4
            || patch.Surface.DomainStartU != 0 || patch.Surface.DomainEndU != 1
            || patch.Surface.DomainStartV != 0 || patch.Surface.DomainEndV != 1)
            || !patches[0].Surface.KnotValuesV.SequenceEqual(patches[1].Surface.KnotValuesV)
            || !patches[0].Surface.KnotMultiplicitiesV.SequenceEqual(patches[1].Surface.KnotMultiplicitiesV))
        {
            throw new ArgumentException("garment-spline-patches: Require two compatible 7x4 bicubic polynomial unit-domain patches.");
        }
        for (int row = 0; row < 4; row++)
        {
            if (patches[0].ControlIds[row] != patches[1].ControlIds[24 + row]
                || patches[0].ControlIds[24 + row] != patches[1].ControlIds[row]
                || patches[0].Surface.ControlPoints[0][row] != patches[1].Surface.ControlPoints[6][row]
                || patches[0].Surface.ControlPoints[6][row] != patches[1].Surface.ControlPoints[0][row])
            {
                throw new ArgumentException("garment-spline-seam: Edited side boundaries must retain shared IDs and identical controls.");
            }
        }
    }
}
