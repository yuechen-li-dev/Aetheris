using System.Globalization;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242ExporterSolidWorksValidityTests
{
    private const double Tolerance = 1e-6;

    [Fact]
    public void ExportBody_Box_ProducesLineEdgeCurvesWithEndpointsOnLineAndCoherentVertexReuse()
    {
        var box = BrepPrimitives.CreateBox(4d, 6d, 8d);
        Assert.True(box.IsSuccess);

        var step = Step242Exporter.ExportBody(box.Value);
        Assert.True(step.IsSuccess);

        var graph = Parse(step.Value);

        foreach (var edge in graph.EdgeCurves)
        {
            if (!graph.Lines.TryGetValue(edge.CurveId, out var line))
            {
                continue;
            }

            var startPoint = graph.ResolveVertex(edge.StartVertexId);
            var endPoint = graph.ResolveVertex(edge.EndVertexId);

            Assert.True(IsPointOnLine(startPoint, line.Origin, line.Direction, Tolerance),
                $"EDGE_CURVE #{edge.EdgeCurveId} start vertex is off line #{edge.CurveId}.");
            Assert.True(IsPointOnLine(endPoint, line.Origin, line.Direction, Tolerance),
                $"EDGE_CURVE #{edge.EdgeCurveId} end vertex is off line #{edge.CurveId}.");
        }

        var uniqueVertices = DeduplicatePoints(graph.VertexToPoint.Values.Select(id => graph.Points[id]).ToArray(), Tolerance);
        Assert.Equal(8, uniqueVertices.Count);

        var uniqueEdges = new HashSet<(int A, int B)>();
        foreach (var edge in graph.EdgeCurves)
        {
            var a = edge.StartVertexId;
            var b = edge.EndVertexId;
            uniqueEdges.Add(a <= b ? (a, b) : (b, a));
        }

        Assert.Equal(12, uniqueEdges.Count);
    }

    private static bool IsPointOnLine((double X, double Y, double Z) p, (double X, double Y, double Z) origin, (double X, double Y, double Z) direction, double tolerance)
    {
        var vx = p.X - origin.X;
        var vy = p.Y - origin.Y;
        var vz = p.Z - origin.Z;

        var t = (vx * direction.X) + (vy * direction.Y) + (vz * direction.Z);
        var projected = (
            origin.X + (direction.X * t),
            origin.Y + (direction.Y * t),
            origin.Z + (direction.Z * t));

        var dx = p.X - projected.Item1;
        var dy = p.Y - projected.Item2;
        var dz = p.Z - projected.Item3;
        var distance = double.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
        return distance <= tolerance;
    }

    private static List<(double X, double Y, double Z)> DeduplicatePoints((double X, double Y, double Z)[] points, double tolerance)
    {
        var unique = new List<(double X, double Y, double Z)>();
        var toleranceSq = tolerance * tolerance;
        foreach (var point in points)
        {
            var exists = unique.Any(existing =>
            {
                var dx = existing.X - point.X;
                var dy = existing.Y - point.Y;
                var dz = existing.Z - point.Z;
                return ((dx * dx) + (dy * dy) + (dz * dz)) <= toleranceSq;
            });

            if (!exists)
            {
                unique.Add(point);
            }
        }

        return unique;
    }

    private static ParsedGraph Parse(string stepText)
    {
        var rhsById = new Dictionary<int, string>();
        foreach (var rawLine in stepText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!rawLine.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            var equalsIndex = rawLine.IndexOf('=');
            if (equalsIndex <= 1)
            {
                continue;
            }

            var id = int.Parse(rawLine.AsSpan(1, equalsIndex - 1), CultureInfo.InvariantCulture);
            rhsById[id] = rawLine[(equalsIndex + 1)..];
        }

        var points = new Dictionary<int, (double X, double Y, double Z)>();
        var vertexToPoint = new Dictionary<int, int>();
        var directions = new Dictionary<int, (double X, double Y, double Z)>();
        var vectors = new Dictionary<int, int>();
        var lines = new Dictionary<int, ParsedLine>();
        var edgeCurves = new List<ParsedEdgeCurve>();

        foreach (var (id, rhs) in rhsById)
        {
            if (rhs.StartsWith("CARTESIAN_POINT", StringComparison.Ordinal))
            {
                points[id] = ReadTriplet(rhs);
            }
            else if (rhs.StartsWith("VERTEX_POINT", StringComparison.Ordinal))
            {
                vertexToPoint[id] = ReadReference(rhs, 1);
            }
            else if (rhs.StartsWith("DIRECTION", StringComparison.Ordinal))
            {
                directions[id] = ReadTriplet(rhs);
            }
            else if (rhs.StartsWith("VECTOR", StringComparison.Ordinal))
            {
                vectors[id] = ReadReference(rhs, 1);
            }
            else if (rhs.StartsWith("LINE", StringComparison.Ordinal))
            {
                lines[id] = new ParsedLine(points[ReadReference(rhs, 1)], directions[vectors[ReadReference(rhs, 2)]]);
            }
            else if (rhs.StartsWith("EDGE_CURVE", StringComparison.Ordinal))
            {
                edgeCurves.Add(new ParsedEdgeCurve(id, ReadReference(rhs, 1), ReadReference(rhs, 2), ReadReference(rhs, 3)));
            }
        }

        return new ParsedGraph(points, vertexToPoint, lines, edgeCurves);
    }

    private static int ReadReference(string rhs, int occurrence)
    {
        var index = -1;
        for (var i = 0; i < occurrence; i++)
        {
            index = rhs.IndexOf('#', index + 1);
            if (index < 0)
            {
                throw new InvalidOperationException($"Missing reference #{occurrence} in {rhs}");
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
        var start = rhs.IndexOf("((", StringComparison.Ordinal);
        if (start >= 0)
        {
            start += 2;
        }
        else
        {
            start = rhs.LastIndexOf('(') + 1;
        }

        var end = rhs.IndexOf(')', start);
        var parts = rhs[start..end].Split(',', StringSplitOptions.TrimEntries);
        return (
            double.Parse(parts[0], CultureInfo.InvariantCulture),
            double.Parse(parts[1], CultureInfo.InvariantCulture),
            double.Parse(parts[2], CultureInfo.InvariantCulture));
    }

    private sealed record ParsedGraph(
        IReadOnlyDictionary<int, (double X, double Y, double Z)> Points,
        IReadOnlyDictionary<int, int> VertexToPoint,
        IReadOnlyDictionary<int, ParsedLine> Lines,
        IReadOnlyList<ParsedEdgeCurve> EdgeCurves)
    {
        public (double X, double Y, double Z) ResolveVertex(int vertexPointId) => Points[VertexToPoint[vertexPointId]];
    }

    private sealed record ParsedLine((double X, double Y, double Z) Origin, (double X, double Y, double Z) Direction);

    private sealed record ParsedEdgeCurve(int EdgeCurveId, int StartVertexId, int EndVertexId, int CurveId);
}
