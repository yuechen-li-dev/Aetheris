using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Firmament.Assembly;

namespace Aetheris.Surfacing;

public static class MeshSculptExport
{
    public static byte[] Glb(MeshSculptState state)
    {
        var mesh = SubdivisionSculptor.PrepareExport(state.Cage, state.Construction.MaximumDeviationMm).NativeMesh;
        // Flat per-triangle render normals retain every crease. Render vertex
        // duplication is a projection; the authoritative cage stays welded.
        var positions = new List<double>(); var normals = new List<double>();
        for (var i = 0; i < mesh.TriangleIndices.Count; i += 3)
        {
            var a = mesh.Positions[mesh.TriangleIndices[i]]; var b = mesh.Positions[mesh.TriangleIndices[i + 1]]; var c = mesh.Positions[mesh.TriangleIndices[i + 2]];
            var n = (b - a).Cross(c - a); n /= n.Length;
            foreach (var p in new[] { a, b, c }) { positions.AddRange([p.X, p.Y, p.Z]); normals.AddRange([n.X, n.Y, n.Z]); }
        }
        var definition = new AssemblyDisplayMeshDefinition("mesh", state.StateId, positions.ToArray(), normals.ToArray(), Enumerable.Range(0, positions.Count / 3).ToArray(), "SubdivisionCage");
        var occurrence = new AssemblyDisplayMeshOccurrence(state.Name, state.Name, null, "mesh", [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1]);
        return AssemblyGlbExporter.Serialize(new("aetheris.assembly.display-mesh.v1", state.Name, "mm", [definition], [occurrence]));
    }
}
