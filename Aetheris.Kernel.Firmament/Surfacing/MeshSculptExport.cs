using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.Assembly;

namespace Aetheris.Surfacing;

public static class MeshSculptExport
{
    public static byte[] Glb(MeshSculptState state) => Glb(state, SubdivisionSculptor.PrepareExport(state).NativeMesh);

    public static byte[] Glb(MeshSculptState state, TriangleMesh mesh)
    {
        // Render vertices are a projection. Each smooth fan shares its normal
        // and the two sides of a sharp edge keep separate shading.
        var smoothNormals = state.Construction.PreviewLevels > 0 ? SmoothCornerNormals(mesh) : null;
        var positions = new List<double>(); var normals = new List<double>();
        for (var i = 0; i < mesh.TriangleIndices.Count; i += 3)
        {
            var a = mesh.Positions[mesh.TriangleIndices[i]]; var b = mesh.Positions[mesh.TriangleIndices[i + 1]]; var c = mesh.Positions[mesh.TriangleIndices[i + 2]];
            var n = (b - a).Cross(c - a); n /= n.Length;
            for (var corner = i; corner < i + 3; corner++)
            {
                var p = mesh.Positions[mesh.TriangleIndices[corner]];
                var normal = smoothNormals?[corner] ?? n;
                positions.AddRange([p.X, p.Y, p.Z]);
                normals.AddRange([normal.X, normal.Y, normal.Z]);
            }
        }
        var definition = new AssemblyDisplayMeshDefinition("mesh", state.StateId, positions.ToArray(), normals.ToArray(), Enumerable.Range(0, positions.Count / 3).ToArray(), "SubdivisionCage");
        var occurrence = new AssemblyDisplayMeshOccurrence(state.Name, state.Name, null, "mesh", [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1]);
        return AssemblyGlbExporter.Serialize(new("aetheris.assembly.display-mesh.v1", state.Name, "mm", [definition], [occurrence])
            { GeometryAuthority = "Firmament subdivision cage; GLB is a sampled surface" });
    }

    private static Vector3D[] SmoothCornerNormals(TriangleMesh mesh)
    {
        var indices = mesh.TriangleIndices;
        var parents = Enumerable.Range(0, indices.Count).ToArray();
        var weights = new Vector3D[indices.Count];
        var faceNormals = new Vector3D[indices.Count / 3];
        var previous = new Dictionary<(int, int), (int, int)>();
        var sharp = mesh.HardEdges.Select(e => e.Item1 < e.Item2 ? e : (e.Item2, e.Item1)).ToHashSet();

        for (var i = 0; i < indices.Count; i += 3)
        {
            var a = mesh.Positions[indices[i]];
            var b = mesh.Positions[indices[i + 1]];
            var c = mesh.Positions[indices[i + 2]];
            faceNormals[i / 3] = (b - a).Cross(c - a);
            for (var j = 0; j < 3; j++)
            {
                var first = i + j;
                var second = i + (j + 1) % 3;
                if (indices[first] > indices[second]) (first, second) = (second, first);
                var edge = (indices[first], indices[second]);
                if (sharp.Contains(edge)) continue;
                if (!previous.TryAdd(edge, (first, second)))
                {
                    var other = previous[edge];
                    parents[Root(first)] = Root(other.Item1);
                    parents[Root(second)] = Root(other.Item2);
                }
            }
        }

        for (var i = 0; i < indices.Count; i++) weights[Root(i)] += faceNormals[i / 3];
        var normals = new Vector3D[indices.Count];
        for (var i = 0; i < normals.Length; i++)
        {
            var n = weights[Root(i)];
            if (n.Length <= 0) n = faceNormals[i / 3];
            normals[i] = n / n.Length;
        }
        return normals;

        int Root(int corner)
        {
            while (parents[corner] != corner)
            {
                parents[corner] = parents[parents[corner]];
                corner = parents[corner];
            }
            return corner;
        }
    }
}
