using System.Text;
using System.Text.Json;
using Aetheris.Kernel.Core.Geometry;

namespace Aetheris.Kernel.Core.Brep.Tessellation;

internal readonly record struct CircleEdgeAuditContext(
    int? FaceId,
    int? LoopId,
    int? LoopIndex,
    int? OrientedEdgeIndex,
    int EdgeId,
    int CoedgeId,
    int? VertexStartId,
    int? VertexEndId,
    bool? CoedgeIsReversed,
    bool? EdgeCurveSameSense,
    bool? OrientedEdgeOrientation,
    bool? ComputedEffectiveForward);

internal sealed record CircleEdgeTrimAudit(
    int? FaceId,
    int? LoopId,
    int? LoopIndex,
    int? OrientedEdgeIndex,
    int EdgeId,
    int CoedgeId,
    int? VertexStartId,
    int? VertexEndId,
    string CurveKind,
    AuditPoint CircleCenter,
    AuditPoint CircleNormal,
    double CircleRadius,
    AuditPoint RawStartPoint,
    AuditPoint RawEndPoint,
    AuditPoint ProjectedStartPoint,
    AuditPoint ProjectedEndPoint,
    double StartRadiusError,
    double EndRadiusError,
    AuditPoint BasisU,
    AuditPoint BasisV,
    double StartAngle,
    double EndAngle,
    double RawDelta,
    double DeltaForward,
    double DeltaBackward,
    double ChosenDelta,
    string ChosenMode,
    bool DistinctEndpoints,
    bool InputEdgeCurveSameSense,
    bool InputOrientedEdgeOrientation,
    bool ComputedEffectiveForward,
    int SamplePointCount,
    AuditPoint? FirstSamplePoint,
    AuditPoint? LastSamplePoint,
    bool Suspicious,
    string? FailureReason);

internal readonly record struct AuditPoint(double X, double Y, double Z)
{
    public static AuditPoint From(Point3D point) => new(point.X, point.Y, point.Z);

    public static AuditPoint From(Vector3D vector) => new(vector.X, vector.Y, vector.Z);
}

internal sealed class CircleEdgeTrimAuditWriter
{
    private const double SuspiciousArcThreshold = 200d * (double.Pi / 180d);
    private readonly object _gate = new();
    private readonly string _path;
    private int _recordsWritten;
    private int _circleEdgesEncountered;
    private int _suspiciousRecords;

    private CircleEdgeTrimAuditWriter(bool enabled, string? path, bool logAll, int? maxEdges, bool stopOnSuspicious)
    {
        Enabled = enabled;
        _path = path ?? Path.Combine(Path.GetTempPath(), "aetheris-circle-audit.jsonl");
        LogAll = logAll;
        MaxCircleEdges = maxEdges;
        StopOnSuspicious = stopOnSuspicious;
    }

    public bool Enabled { get; }

    public bool LogAll { get; }

    public int? MaxCircleEdges { get; }

    public bool StopOnSuspicious { get; }

    public string Path => _path;

    public int RecordsWritten => _recordsWritten;

    public int CircleEdgesEncountered => _circleEdgesEncountered;

    public int SuspiciousRecords => _suspiciousRecords;

    private static CircleEdgeTrimAuditWriter _instance = CreateFromEnvironment();

    public static CircleEdgeTrimAuditWriter Instance => Volatile.Read(ref _instance);

    internal static CircleEdgeTrimAuditWriter CreateForTesting(bool enabled, string? path = null, bool logAll = false, int? maxEdges = null, bool stopOnSuspicious = false)
        => new(enabled, path, logAll, maxEdges, stopOnSuspicious);

    internal static void ReloadFromEnvironmentForTesting()
        => Volatile.Write(ref _instance, CreateFromEnvironment());

    public static bool ComposeEffectiveForwardSense(bool orientedEdgeOrientation, bool edgeCurveSameSense)
        => orientedEdgeOrientation == edgeCurveSameSense;

    public bool RegisterCircleEdgeEncountered()
    {
        if (!Enabled)
        {
            return false;
        }

        var encountered = Interlocked.Increment(ref _circleEdgesEncountered);
        return MaxCircleEdges.HasValue && encountered > MaxCircleEdges.Value;
    }

    public bool ShouldEmit(CircleEdgeTrimAudit audit)
        => Enabled && (LogAll || audit.Suspicious || !string.IsNullOrWhiteSpace(audit.FailureReason));

    public bool Append(CircleEdgeTrimAudit audit)
    {
        if (!ShouldEmit(audit))
        {
            return false;
        }

        var line = JsonSerializer.Serialize(audit);
        lock (_gate)
        {
            File.AppendAllText(_path, line + "\n", Encoding.UTF8);
        }

        Interlocked.Increment(ref _recordsWritten);
        if (audit.Suspicious)
        {
            Interlocked.Increment(ref _suspiciousRecords);
        }

        return StopOnSuspicious && audit.Suspicious;
    }

    public static bool IsSuspiciousDelta(double chosenDelta)
        => double.Abs(chosenDelta) > SuspiciousArcThreshold;

    private static CircleEdgeTrimAuditWriter CreateFromEnvironment()
    {
        var enabled = IsEnabled(Environment.GetEnvironmentVariable("AETHERIS_CIRCLE_AUDIT"));
        var logAll = IsEnabled(Environment.GetEnvironmentVariable("AETHERIS_CIRCLE_AUDIT_ALL"));
        var stopOnSuspicious = IsEnabled(Environment.GetEnvironmentVariable("AETHERIS_CIRCLE_AUDIT_STOP_ON_SUSPICIOUS"));
        var path = Environment.GetEnvironmentVariable("AETHERIS_CIRCLE_AUDIT_PATH");
        var maxRaw = Environment.GetEnvironmentVariable("AETHERIS_CIRCLE_AUDIT_MAX");
        var maxEdges = int.TryParse(maxRaw, out var parsed) && parsed > 0 ? parsed : null;
        return new CircleEdgeTrimAuditWriter(enabled, path, logAll, maxEdges, stopOnSuspicious);
    }

    private static bool IsEnabled(string? value)
        => string.Equals(value, "1", StringComparison.Ordinal)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
}
