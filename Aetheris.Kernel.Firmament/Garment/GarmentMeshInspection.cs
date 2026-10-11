using System.Numerics;

namespace Aetheris.Kernel.Firmament.Garment;

/// <summary>Rest-pattern diagnostics. Lengths are metres, angles are degrees.
/// Lumped area includes pinned vertices; it does not infer mass from their zero inverse mass.</summary>
public sealed record GarmentMeshQuality(int UniqueEdges, double MinimumEdgeLength,
    double MedianEdgeLength, double MaximumEdgeLength, double MinimumTriangleAngleDegrees,
    int TrianglesBelowTenDegrees, double MaximumLumpedAreaRatio, int UnreferencedVertices);

public static class GarmentMeshInspection
{
    public static GarmentMeshQuality Measure(CompiledGarment garment)
    {
        ArgumentNullException.ThrowIfNull(garment);
        var definition = garment.Cloth.Definition;
        var edges = new HashSet<(int A, int B)>();
        var lumpedAreas = new double[definition.Positions.Length];
        double minimumAngle = 180;
        int thinTriangles = 0;
        for (int face = 0; face < definition.Indices.Length; face += 3)
        {
            int[] triangle = [definition.Indices[face], definition.Indices[face + 1], definition.Indices[face + 2]];
            var a = definition.Positions[triangle[0]];
            var b = definition.Positions[triangle[1]];
            var c = definition.Positions[triangle[2]];
            double area = Vector3.Cross(b - a, c - a).Length() * .5;
            double triangleAngle = 180;
            for (int corner = 0; corner < 3; corner++)
            {
                int vertex = triangle[corner];
                int next = triangle[(corner + 1) % 3];
                int previous = triangle[(corner + 2) % 3];
                edges.Add((Math.Min(vertex, next), Math.Max(vertex, next)));
                lumpedAreas[vertex] += area / 3;
                Vector3 first = definition.Positions[next] - definition.Positions[vertex];
                Vector3 second = definition.Positions[previous] - definition.Positions[vertex];
                double cosine = Vector3.Dot(first, second) / ((double)first.Length() * second.Length());
                double angle = Math.Acos(Math.Clamp(cosine, -1, 1)) * 180 / Math.PI;
                triangleAngle = Math.Min(triangleAngle, angle);
            }
            minimumAngle = Math.Min(minimumAngle, triangleAngle);
            if (triangleAngle < 10)
            {
                thinTriangles++;
            }
        }
        double[] lengths = edges.Select(edge => (double)Vector3.Distance(
            definition.Positions[edge.A], definition.Positions[edge.B])).Order().ToArray();
        double median = lengths.Length % 2 == 0
            ? (lengths[lengths.Length / 2 - 1] + lengths[lengths.Length / 2]) * .5
            : lengths[lengths.Length / 2];
        double[] positiveAreas = lumpedAreas.Where(area => area > 0).ToArray();
        return new(lengths.Length, lengths[0], median, lengths[^1], minimumAngle, thinTriangles,
            positiveAreas.Max() / positiveAreas.Min(), lumpedAreas.Count(area => area == 0));
    }
}
