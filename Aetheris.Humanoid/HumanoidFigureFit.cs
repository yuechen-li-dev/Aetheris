using Aetheris.Kernel.Core.Math;
using System.Security.Cryptography;

namespace Aetheris.Humanoid;

public sealed record HumanoidFitSection(double HeightMm, double CircumferenceMm,
    double MinimumX, double MaximumX, double MinimumY, double MaximumY, int Segments);
public sealed record HumanoidFigureFit(string BodyId, string TopologyHash, string SkeletonId, string RestPoseId,
    Point3D LeftShoulder, Point3D RightShoulder, Point3D Neck, HumanoidFitSection Chest, HumanoidFitSection Hip,
    string RestSurfaceHash)
{
    public string MeasurementPoseId { get; init; } = "Rest";
}

/// <summary>Measurements of the supplied gameplay surface, not canonical-template landmarks.
/// Sections must form a closed contour containing the torso joint. Shoulder guides are explicit joint proxies.</summary>
public static class HumanoidFigureFitting
{
    private const double WeldToleranceMm = .0001;

    public static HumanoidFigureFit Measure(HumanoidGameplayBody body, double? chestHeightMm = null,
        double? hipHeightMm = null, SolvedHumanoidPose? pose = null)
    {
        ArgumentNullException.ThrowIfNull(body);
        var chest = Joint(HumanoidJointKind.Chest);
        var pelvis = Joint(HumanoidJointKind.Pelvis);
        var positions = pose is null ? body.Surface.Vertices.Select(vertex => vertex.Position).ToArray() : body.EvaluatePositions(pose);
        return new HumanoidFigureFit(body.Id, body.Surface.ConnectivityHash, body.Skeleton.SkeletonId, body.Skeleton.RestPoseId,
            Joint(HumanoidJointKind.LeftShoulder), Joint(HumanoidJointKind.RightShoulder), Joint(HumanoidJointKind.Neck),
            Section(body.Surface, positions, chestHeightMm ?? chest.Z, chest),
            Section(body.Surface, positions, hipHeightMm ?? pelvis.Z, pelvis), SurfaceHash(body))
        {
            MeasurementPoseId = pose?.PoseState.PoseId ?? "Rest",
        };

        Point3D Joint(HumanoidJointKind kind)
        {
            var joint = body.Skeleton.Joints.SingleOrDefault(joint => joint.Kind == kind)
                ?? throw new InvalidOperationException("garment-fit-joint-missing: " + kind);
            int index = body.Skeleton.Joints.ToList().FindIndex(candidate => candidate.Kind == kind);
            var transform = pose?.GlobalTransforms[index] ?? joint.GlobalBind;
            return new(transform.M41, transform.M42, transform.M43);
        }
    }

    public static string SurfaceHash(HumanoidGameplayBody body)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> coordinates = stackalloc byte[24];
        foreach (var vertex in body.Surface.Vertices)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteDoubleLittleEndian(coordinates, vertex.Position.X);
            System.Buffers.Binary.BinaryPrimitives.WriteDoubleLittleEndian(coordinates[8..], vertex.Position.Y);
            System.Buffers.Binary.BinaryPrimitives.WriteDoubleLittleEndian(coordinates[16..], vertex.Position.Z);
            hash.AppendData(coordinates);
        }
        Span<byte> indices = stackalloc byte[12];
        foreach (var face in body.Surface.Faces)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(indices, face.A);
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(indices[4..], face.B);
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(indices[8..], face.C);
            hash.AppendData(indices);
        }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    public static HumanoidFitSection Section(HumanoidGameplayBody body, double heightMm, Point3D torsoProbe)
    {
        return Section(body.Surface, body.Surface.Vertices.Select(vertex => vertex.Position).ToArray(), heightMm, torsoProbe);
    }

    private static HumanoidFitSection Section(HumanoidSurface surface, IReadOnlyList<Point3D> positions,
        double heightMm, Point3D torsoProbe)
    {
        if (!double.IsFinite(heightMm))
        {
            throw new ArgumentException("garment-fit-section-invalid: Height must be finite.");
        }
        var points = new List<Point3D>();
        var pointIds = new Dictionary<(long X, long Y), int>();
        var adjacency = new Dictionary<int, HashSet<int>>();
        var faces = surface.Components.Where(component => component.Kind == HumanoidComponentKind.OuterSkin)
            .SelectMany(component => component.FaceIndices).Distinct().Order();
        foreach (int faceIndex in faces)
        {
            var face = surface.Faces[faceIndex];
            var triangle = new[] { positions[face.A], positions[face.B], positions[face.C] };
            var crossings = new List<Point3D>();
            for (int edge = 0; edge < 3; edge++)
            {
                var a = triangle[edge];
                var b = triangle[(edge + 1) % 3];
                if (Math.Abs(a.Z - heightMm) < 1e-8)
                {
                    throw new InvalidOperationException("garment-fit-section-coincident: Move the section height away from a mesh vertex.");
                }
                if ((a.Z > heightMm) == (b.Z > heightMm))
                {
                    continue;
                }
                double fraction = (heightMm - a.Z) / (b.Z - a.Z);
                crossings.Add(new(a.X + (b.X - a.X) * fraction, a.Y + (b.Y - a.Y) * fraction, heightMm));
            }
            if (crossings.Count != 2)
            {
                continue;
            }
            int first = PointId(crossings[0]);
            int second = PointId(crossings[1]);
            if (first == second)
            {
                continue;
            }
            adjacency[first].Add(second);
            adjacency[second].Add(first);
        }
        var visited = new HashSet<int>();
        var candidates = new List<IReadOnlyList<Point3D>>();
        foreach (int seed in adjacency.Keys.Order())
        {
            if (visited.Contains(seed))
            {
                continue;
            }
            var component = new List<int>();
            var pending = new Stack<int>();
            pending.Push(seed);
            while (pending.TryPop(out int vertex))
            {
                if (!visited.Add(vertex))
                {
                    continue;
                }
                component.Add(vertex);
                foreach (int neighbor in adjacency[vertex])
                {
                    pending.Push(neighbor);
                }
            }
            // Never repair an open/branched body section into a guessed circumference.
            if (component.Any(vertex => adjacency[vertex].Count != 2))
            {
                if (component.Any(vertex => Math.Abs(points[vertex].X - torsoProbe.X) < 250))
                {
                    throw new InvalidOperationException("garment-fit-section-open: Torso section is open or branched at " + heightMm + "mm.");
                }
                continue;
            }
            var loop = new List<Point3D>();
            int previous = -1;
            int current = seed;
            do
            {
                loop.Add(points[current]);
                int next = adjacency[current].Order().First(neighbor => neighbor != previous);
                previous = current;
                current = next;
            } while (current != seed && loop.Count <= component.Count);
            if (Contains(loop, torsoProbe))
            {
                candidates.Add(loop);
            }
        }
        if (candidates.Count != 1)
        {
            throw new InvalidOperationException("garment-fit-section-ambiguous: Expected one closed torso contour at " + heightMm + "mm, found " + candidates.Count + ".");
        }
        var contour = candidates[0];
        double circumference = 0;
        for (int index = 0; index < contour.Count; index++)
        {
            circumference += (contour[(index + 1) % contour.Count] - contour[index]).Length;
        }
        return new(heightMm, circumference, contour.Min(point => point.X), contour.Max(point => point.X),
            contour.Min(point => point.Y), contour.Max(point => point.Y), contour.Count);

        int PointId(Point3D point)
        {
            var key = ((long)Math.Round(point.X / WeldToleranceMm), (long)Math.Round(point.Y / WeldToleranceMm));
            if (!pointIds.TryGetValue(key, out int index))
            {
                index = points.Count;
                points.Add(point);
                pointIds.Add(key, index);
                adjacency.Add(index, []);
            }
            return index;
        }
    }

    private static bool Contains(IReadOnlyList<Point3D> polygon, Point3D point)
    {
        bool inside = false;
        for (int index = 0; index < polygon.Count; index++)
        {
            var a = polygon[index];
            var b = polygon[(index + 1) % polygon.Count];
            if ((a.Y > point.Y) != (b.Y > point.Y)
                && point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X)
            {
                inside = !inside;
            }
        }
        return inside;
    }
}
