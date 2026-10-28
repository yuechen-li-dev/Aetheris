using System.Text;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Brep.Tessellation;

public static class BinaryStlExporter
{
    public static void Export(string path, TriangleMesh mesh)
        => Export(path, mesh, out _);

    public static void Export(string path, TriangleMesh mesh, out TriangleMeshTopologyReport topology)
    {
        if (!TriangleMeshValidator.TryValidateClosed(mesh, out _, out var failure)) throw new InvalidOperationException(failure);
        // STL stores float32 coordinates and no vertex identities. Qualify the
        // actual representation, including vertices which collapse on conversion,
        // before creating or replacing the destination file.
        var positions = new List<Point3D>(); var index = new Dictionary<Point3D, int>();
        var remap = new int[mesh.Positions.Count];
        for (var i = 0; i < mesh.Positions.Count; i++)
        {
            var p = mesh.Positions[i]; var stored = new Point3D((float)p.X, (float)p.Y, (float)p.Z);
            if (!index.TryGetValue(stored, out var id)) { id = positions.Count; index.Add(stored, id); positions.Add(stored); }
            remap[i] = id;
        }
        mesh = mesh with { Positions = positions, TriangleIndices = mesh.TriangleIndices.Select(i => remap[i]).ToArray() };
        if (!TriangleMeshValidator.TryValidateClosed(mesh, out topology, out failure))
            throw new InvalidOperationException("Binary STL float32 coordinates do not preserve a closed oriented mesh: " + failure);
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: false);
        var header = new byte[80]; Encoding.ASCII.GetBytes("Aetheris SurfaceMeshIR validated binary STL").CopyTo(header, 0); writer.Write(header);
        writer.Write((uint)(mesh.TriangleIndices.Count / 3));
        for (var i = 0; i < mesh.TriangleIndices.Count; i += 3)
        {
            var a = mesh.Positions[mesh.TriangleIndices[i]]; var b = mesh.Positions[mesh.TriangleIndices[i + 1]]; var c = mesh.Positions[mesh.TriangleIndices[i + 2]];
            var normal = (b - a).Cross(c - a); normal.TryNormalize(out normal);
            WriteVector(normal); WritePoint(a); WritePoint(b); WritePoint(c); writer.Write((ushort)0);
        }
        void WriteVector(Vector3D v) { writer.Write((float)v.X); writer.Write((float)v.Y); writer.Write((float)v.Z); }
        void WritePoint(Point3D p) { writer.Write((float)p.X); writer.Write((float)p.Y); writer.Write((float)p.Z); }
    }
}
