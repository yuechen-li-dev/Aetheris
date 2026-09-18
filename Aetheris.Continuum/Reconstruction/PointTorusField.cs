using Aetheris.Kernel.Core.Math;

namespace Aetheris.Continuum.Reconstruction;

public enum PointTorusSignCapability
{
    SignedClosedOriented,
    UnsignedOnlyOpenSurface,
    UnsignedOnlyUnoriented,
    UnsignedOnlyMultiSheet
}

public enum PointTorusQualification
{
    Qualified,
    UnsignedOnly,
    OutsideValidityBand,
    InsufficientNeighborhood
}

public sealed record PointTorusProvenance(
    string Source,
    string ParameterProducer,
    string? ModelRevision,
    string? ModelHash,
    IReadOnlyDictionary<string, string>? Properties = null);

public sealed record PointTorusFieldSettings(
    int NeighborCount = 32,
    double ValidityBandMm = 5d,
    PointTorusSignCapability SignCapability = PointTorusSignCapability.SignedClosedOriented)
{
    public void Validate(int sampleCount)
    {
        if (NeighborCount < 1 || NeighborCount > sampleCount)
            throw new ArgumentOutOfRangeException(nameof(NeighborCount), "Neighbor count must be within the imported sample count.");
        if (!double.IsFinite(ValidityBandMm) || ValidityBandMm <= 0d)
            throw new ArgumentOutOfRangeException(nameof(ValidityBandMm), "Validity band must be finite and positive.");
    }
}

/// <summary>One precomputed local analytic torus. Learned fitting is deliberately outside this assembly.</summary>
public sealed record PointTorusSample(
    Point3D SourcePoint,
    Vector3D SourceNormal,
    Point3D Center,
    Vector3D Axis,
    double MajorRadius,
    double SignedMinorRadius);

public readonly record struct PointTorusEvaluation(
    double ValueMm,
    PointTorusQualification Qualification,
    bool IsSigned,
    int NeighborsUsed,
    double NearestSourceDistanceMm,
    string Reason);

/// <summary>
/// Source-derived approximate field using precomputed Points-as-Tori local parameters.
/// This is reconstruction/query evidence, never BRep, topology, or manufacturing authority.
/// </summary>
public sealed class PointTorusField
{
    private readonly PointTorusSample[] _samples;
    private readonly KdIndex _index;

    public PointTorusField(IEnumerable<PointTorusSample> samples, PointTorusFieldSettings settings, PointTorusProvenance provenance)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(provenance);
        _samples = samples.ToArray();
        if (_samples.Length == 0) throw new ArgumentException("At least one local torus is required.", nameof(samples));
        settings.Validate(_samples.Length);
        for (var i = 0; i < _samples.Length; i++) Validate(_samples[i], i);
        Settings = settings;
        Provenance = provenance;
        _index = new KdIndex(_samples.Select(static s => s.SourcePoint).ToArray());
    }

    public IReadOnlyList<PointTorusSample> Samples => _samples;
    public PointTorusFieldSettings Settings { get; }
    public PointTorusProvenance Provenance { get; }
    public string Acceleration => "deterministic-kd-tree";

    public PointTorusEvaluation Evaluate(Point3D query)
    {
        var neighbors = _index.Nearest(query, Settings.NeighborCount);
        if (neighbors.Length == 0)
            return new(double.NaN, PointTorusQualification.InsufficientNeighborhood, false, 0, double.PositiveInfinity, "No local torus was available.");

        var maxDistance = Math.Sqrt(neighbors[^1].DistanceSquared);
        var nearest = Math.Sqrt(neighbors[0].DistanceSquared);
        var shift = 0.5d * maxDistance;
        var weighted = 0d;
        var weightSum = 0d;
        var signed = Settings.SignCapability == PointTorusSignCapability.SignedClosedOriented;
        foreach (var neighbor in neighbors)
        {
            var distance = Math.Sqrt(neighbor.DistanceSquared);
            var weight = shift <= 1e-15 ? 1d : Math.Exp((-64d / shift) * (distance - shift));
            var local = EvaluateTorus(_samples[neighbor.Index], query);
            weighted += weight * (signed ? local : Math.Abs(local));
            weightSum += weight;
        }

        var value = weighted / weightSum;
        if (nearest > Settings.ValidityBandMm)
            return new(value, PointTorusQualification.OutsideValidityBand, signed, neighbors.Length, nearest,
                $"Nearest source sample is {nearest:G6} mm away, outside the {Settings.ValidityBandMm:G6} mm validity band." +
                (signed ? string.Empty : $" {SignReason(Settings.SignCapability)}"));
        if (!signed)
            return new(value, PointTorusQualification.UnsignedOnly, false, neighbors.Length, nearest, SignReason(Settings.SignCapability));
        return new(value, PointTorusQualification.Qualified, true, neighbors.Length, nearest, "Closed, globally oriented source within the declared validity band.");
    }

    public PointTorusEvaluation[] EvaluateBatch(IEnumerable<Point3D> queries)
    {
        ArgumentNullException.ThrowIfNull(queries);
        return queries.Select(Evaluate).ToArray();
    }

    public static double EvaluateTorus(PointTorusSample torus, Point3D query)
    {
        var delta = query - torus.Center;
        var axis = torus.Axis;
        var axial = delta.Dot(axis);
        var radial = delta.Cross(axis).Length;
        var tubeDistance = Math.Sqrt(((radial - torus.MajorRadius) * (radial - torus.MajorRadius)) + (axial * axial));
        return Math.Sign(torus.SignedMinorRadius) * (tubeDistance - Math.Abs(torus.SignedMinorRadius));
    }

    private static void Validate(PointTorusSample sample, int index)
    {
        static bool FinitePoint(Point3D p) => double.IsFinite(p.X) && double.IsFinite(p.Y) && double.IsFinite(p.Z);
        static bool FiniteVector(Vector3D v) => double.IsFinite(v.X) && double.IsFinite(v.Y) && double.IsFinite(v.Z);
        if (!FinitePoint(sample.SourcePoint) || !FiniteVector(sample.SourceNormal) || !FinitePoint(sample.Center) || !FiniteVector(sample.Axis) ||
            !double.IsFinite(sample.MajorRadius) || !double.IsFinite(sample.SignedMinorRadius))
            throw new ArgumentException($"Local torus {index} contains a non-finite value.");
        if (Math.Abs(sample.Axis.Length - 1d) > 1e-8)
            throw new ArgumentException($"Local torus {index} axis must be unit length.");
        if (sample.MajorRadius < 0d || Math.Abs(sample.SignedMinorRadius) <= 1e-15)
            throw new ArgumentException($"Local torus {index} radii are invalid.");
    }

    private static string SignReason(PointTorusSignCapability capability) => capability switch
    {
        PointTorusSignCapability.UnsignedOnlyOpenSurface => "Open source surfaces do not define authoritative inside/outside.",
        PointTorusSignCapability.UnsignedOnlyUnoriented => "Globally consistent oriented normals are required for sign.",
        PointTorusSignCapability.UnsignedOnlyMultiSheet => "Thin or locally multi-sheet neighborhoods are restricted to unsigned residuals.",
        _ => "Sign is not qualified."
    };

    private readonly record struct Neighbor(int Index, double DistanceSquared);

    private sealed class KdIndex
    {
        private readonly Point3D[] _points;
        private readonly Node? _root;

        public KdIndex(Point3D[] points)
        {
            _points = points;
            _root = Build(Enumerable.Range(0, points.Length).ToArray(), 0);
        }

        public Neighbor[] Nearest(Point3D query, int count)
        {
            var heap = new PriorityQueue<Neighbor, double>();
            Search(_root, query, count, heap);
            return heap.UnorderedItems.Select(static x => x.Element).OrderBy(static x => x.DistanceSquared).ThenBy(static x => x.Index).ToArray();
        }

        private Node? Build(int[] indices, int depth)
        {
            if (indices.Length == 0) return null;
            var axis = depth % 3;
            Array.Sort(indices, (a, b) => Coordinate(_points[a], axis).CompareTo(Coordinate(_points[b], axis)) is var c && c != 0 ? c : a.CompareTo(b));
            var middle = indices.Length / 2;
            return new(indices[middle], axis,
                Build(indices[..middle], depth + 1),
                Build(indices[(middle + 1)..], depth + 1));
        }

        private void Search(Node? node, Point3D query, int count, PriorityQueue<Neighbor, double> heap)
        {
            if (node is null) return;
            var point = _points[node.Index];
            var d = query - point;
            var distanceSquared = d.LengthSquared;
            if (heap.Count < count) heap.Enqueue(new(node.Index, distanceSquared), -distanceSquared);
            else if (heap.TryPeek(out _, out var worstPriority) && distanceSquared < -worstPriority)
            {
                heap.Dequeue();
                heap.Enqueue(new(node.Index, distanceSquared), -distanceSquared);
            }

            var delta = Coordinate(query, node.Axis) - Coordinate(point, node.Axis);
            var near = delta <= 0d ? node.Left : node.Right;
            var far = delta <= 0d ? node.Right : node.Left;
            Search(near, query, count, heap);
            var worst = heap.Count < count || !heap.TryPeek(out _, out var p) ? double.PositiveInfinity : -p;
            if ((delta * delta) <= worst) Search(far, query, count, heap);
        }

        private static double Coordinate(Point3D p, int axis) => axis == 0 ? p.X : axis == 1 ? p.Y : p.Z;
        private sealed record Node(int Index, int Axis, Node? Left, Node? Right);
    }
}
