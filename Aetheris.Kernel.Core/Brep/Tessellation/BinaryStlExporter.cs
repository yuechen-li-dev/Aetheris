using System.Text;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Brep.Tessellation;

public static class BinaryStlExporter
{
    public static void Export(string path, TriangleMesh mesh)
    {
        if (!TriangleMeshValidator.TryValidateClosed(mesh, out _, out var failure)) throw new InvalidOperationException(failure);
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
