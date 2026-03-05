using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Step242;
using System.Globalization;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242ExporterTests
{
    private const double EdgeOnLineTolerance = 1e-6;

    [Fact]
    public void ExportBody_BoxBody_ReturnsStepTextWithExpectedSections()
    {
        var boxResult = BrepPrimitives.CreateBox(4d, 6d, 8d);
        Assert.True(boxResult.IsSuccess);

        var export = Step242Exporter.ExportBody(boxResult.Value);

        Assert.True(export.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(export.Value));

        Assert.Contains("ISO-10303-21;", export.Value, StringComparison.Ordinal);
        Assert.Contains("HEADER;", export.Value, StringComparison.Ordinal);
        Assert.Contains("DATA;", export.Value, StringComparison.Ordinal);
        Assert.Contains("ENDSEC;", export.Value, StringComparison.Ordinal);
        Assert.Contains("END-ISO-10303-21;", export.Value, StringComparison.Ordinal);
        Assert.Contains("MANIFOLD_SOLID_BREP", export.Value, StringComparison.Ordinal);
        Assert.Contains("ADVANCED_FACE", export.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportBody_BoxBody_IsDeterministicForSameInput()
    {
        var boxResult = BrepPrimitives.CreateBox(10d, 12d, 14d);
        Assert.True(boxResult.IsSuccess);

        var first = Step242Exporter.ExportBody(boxResult.Value);
        var second = Step242Exporter.ExportBody(boxResult.Value);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value, second.Value);

        var entityLines = first.Value
            .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => line.StartsWith("#", StringComparison.Ordinal))
            .Take(5)
            .ToArray();

        Assert.Equal("#1=CARTESIAN_POINT($,(-5,-6,-7));", entityLines[0]);
        Assert.Equal("#2=VERTEX_POINT($,#1);", entityLines[1]);
    }

    [Fact]
    public void ExportBody_Sphere_ReturnsNotImplementedDiagnostic_InsteadOfThrowing()
    {
        var sphereResult = BrepPrimitives.CreateSphere(3d);
        Assert.True(sphereResult.IsSuccess);

        var export = Step242Exporter.ExportBody(sphereResult.Value);

        Assert.False(export.IsSuccess);
        var diagnostic = Assert.Single(export.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.NotImplemented, diagnostic.Code);
        Assert.Equal(KernelDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("Face:1", diagnostic.Source);
        Assert.Contains("boundary loops", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Debug_ExportBody_BoxBody_EdgeCurveLineBinding_EndpointsStayOnTrimmedLine()
    {
        var boxResult = BrepPrimitives.CreateBox(4d, 6d, 8d);
        Assert.True(boxResult.IsSuccess);

        var export = Step242Exporter.ExportBody(boxResult.Value);
        Assert.True(export.IsSuccess);

        var records = ParseStepEntities(export.Value);

        foreach (var edge in records.EdgeCurves)
        {
            if (!records.Lines.TryGetValue(edge.CurveId, out var line))
            {
                continue;
            }

            var start = ResolveVertexPoint(records, edge.StartVertexId);
            var end = ResolveVertexPoint(records, edge.EndVertexId);
            var direction = ResolveDirection(records, line.VectorId);

            var startCheck = CheckPointOnTrimmedLine(start, line.Origin, direction, line.Length, EdgeOnLineTolerance);
            Assert.True(
                startCheck.IsOnLine,
                $"EDGE_CURVE #{edge.EdgeCurveId} start vertex #{edge.StartVertexId} is not on LINE #{edge.CurveId}. t={startCheck.T}, len={line.Length}, distance={startCheck.DistanceToLine}, point={FormatPoint(start)}, origin={FormatPoint(line.Origin)}, dir={FormatPoint(direction)}");

            var endCheck = CheckPointOnTrimmedLine(end, line.Origin, direction, line.Length, EdgeOnLineTolerance);
            Assert.True(
                endCheck.IsOnLine,
                $"EDGE_CURVE #{edge.EdgeCurveId} end vertex #{edge.EndVertexId} is not on LINE #{edge.CurveId}. t={endCheck.T}, len={line.Length}, distance={endCheck.DistanceToLine}, point={FormatPoint(end)}, origin={FormatPoint(line.Origin)}, dir={FormatPoint(direction)}");
        }
    }

    private static StepEntityRecords ParseStepEntities(string stepText)
    {
        var entities = new Dictionary<int, string>();
        foreach (var rawLine in stepText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!rawLine.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            var equals = rawLine.IndexOf('=');
            if (equals <= 1 || !int.TryParse(rawLine.AsSpan(1, equals - 1), out var entityId))
            {
                continue;
            }

            entities[entityId] = rawLine[(equals + 1)..];
        }

        var points = new Dictionary<int, (double X, double Y, double Z)>();
        var vertexToPoint = new Dictionary<int, int>();
        var vectors = new Dictionary<int, (int DirectionId, double Length)>();
        var directions = new Dictionary<int, (double X, double Y, double Z)>();
        var lines = new Dictionary<int, ParsedLine>();
        var edgeCurves = new List<ParsedEdgeCurve>();

        foreach (var (id, rhs) in entities)
        {
            if (rhs.StartsWith("CARTESIAN_POINT", StringComparison.Ordinal))
            {
                points[id] = ReadTriplet(rhs);
            }
            else if (rhs.StartsWith("VERTEX_POINT", StringComparison.Ordinal))
            {
                vertexToPoint[id] = ReadReference(rhs, occurrence: 1);
            }
            else if (rhs.StartsWith("DIRECTION", StringComparison.Ordinal))
            {
                directions[id] = ReadTriplet(rhs);
            }
            else if (rhs.StartsWith("VECTOR", StringComparison.Ordinal))
            {
                vectors[id] = (ReadReference(rhs, occurrence: 1), ReadLastNumber(rhs));
            }
            else if (rhs.StartsWith("LINE", StringComparison.Ordinal))
            {
                var originPointId = ReadReference(rhs, occurrence: 1);
                var vectorId = ReadReference(rhs, occurrence: 2);
                lines[id] = new ParsedLine(points[originPointId], vectorId, vectors[vectorId].Length);
            }
            else if (rhs.StartsWith("EDGE_CURVE", StringComparison.Ordinal))
            {
                edgeCurves.Add(new ParsedEdgeCurve(id, ReadReference(rhs, occurrence: 1), ReadReference(rhs, occurrence: 2), ReadReference(rhs, occurrence: 3)));
            }
        }

        return new StepEntityRecords(points, vertexToPoint, directions, vectors, lines, edgeCurves);
    }

    private static (double X, double Y, double Z) ResolveVertexPoint(StepEntityRecords records, int vertexPointId)
        => records.Points[records.VertexToPoint[vertexPointId]];

    private static (double X, double Y, double Z) ResolveDirection(StepEntityRecords records, int vectorId)
    {
        var directionId = records.Vectors[vectorId].DirectionId;
        return records.Directions[directionId];
    }

    private static (bool IsOnLine, double T, double DistanceToLine) CheckPointOnTrimmedLine(
        (double X, double Y, double Z) point,
        (double X, double Y, double Z) origin,
        (double X, double Y, double Z) direction,
        double length,
        double tolerance)
    {
        var vx = point.X - origin.X;
        var vy = point.Y - origin.Y;
        var vz = point.Z - origin.Z;

        var t = (vx * direction.X) + (vy * direction.Y) + (vz * direction.Z);
        var projectedX = origin.X + (direction.X * t);
        var projectedY = origin.Y + (direction.Y * t);
        var projectedZ = origin.Z + (direction.Z * t);

        var dx = point.X - projectedX;
        var dy = point.Y - projectedY;
        var dz = point.Z - projectedZ;
        var distanceToLine = Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));

        var inRange = t >= -tolerance && t <= length + tolerance;
        var onLine = distanceToLine <= tolerance;
        return (onLine && inRange, t, distanceToLine);
    }

    private static int ReadReference(string rhs, int occurrence)
    {
        var index = -1;
        for (var i = 0; i < occurrence; i++)
        {
            index = rhs.IndexOf('#', index + 1);
            if (index < 0)
            {
                throw new InvalidOperationException($"Unable to read reference #{occurrence} from '{rhs}'.");
            }
        }

        var end = index + 1;
        while (end < rhs.Length && char.IsDigit(rhs[end]))
        {
            end++;
        }

        return int.Parse(rhs.AsSpan(index + 1, end - index - 1), CultureInfo.InvariantCulture);
    }

    private static (double X, double Y, double Z) ReadTriplet(string rhs)
    {
        var start = rhs.LastIndexOf('(');
        var end = rhs.LastIndexOf(')');
        var parts = rhs[(start + 1)..end].Split(',', StringSplitOptions.TrimEntries);
        return (
            double.Parse(parts[0], CultureInfo.InvariantCulture),
            double.Parse(parts[1], CultureInfo.InvariantCulture),
            double.Parse(parts[2], CultureInfo.InvariantCulture));
    }

    private static double ReadLastNumber(string rhs)
    {
        var start = rhs.LastIndexOf(',') + 1;
        var end = rhs.LastIndexOf(')');
        return double.Parse(rhs[start..end], CultureInfo.InvariantCulture);
    }

    private static string FormatPoint((double X, double Y, double Z) p) => $"({p.X}, {p.Y}, {p.Z})";

    private sealed record StepEntityRecords(
        IReadOnlyDictionary<int, (double X, double Y, double Z)> Points,
        IReadOnlyDictionary<int, int> VertexToPoint,
        IReadOnlyDictionary<int, (double X, double Y, double Z)> Directions,
        IReadOnlyDictionary<int, (int DirectionId, double Length)> Vectors,
        IReadOnlyDictionary<int, ParsedLine> Lines,
        IReadOnlyList<ParsedEdgeCurve> EdgeCurves);

    private sealed record ParsedLine((double X, double Y, double Z) Origin, int VectorId, double Length);

    private sealed record ParsedEdgeCurve(int EdgeCurveId, int StartVertexId, int EndVertexId, int CurveId);
}
