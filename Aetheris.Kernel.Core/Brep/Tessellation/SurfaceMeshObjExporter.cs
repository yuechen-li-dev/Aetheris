using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Brep.Tessellation;

/// <summary>
/// Topology-preserving OBJ lowering for <see cref="SurfaceMeshDocument"/>.
/// Positions retain their SurfaceMeshIR identity; normals and parameter-space
/// coordinates are OBJ corner attributes, so a hard or chart boundary never
/// requires duplicating the geometric vertex buffer.
/// </summary>
public static class SurfaceMeshObjExporter
{
    private const double Epsilon = 1e-12d;

    public static SurfaceMeshObjExport Export(SurfaceMeshDocument document, string objectName = "Aetheris_SurfaceMeshIR")
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!SurfaceMeshIrValidator.TryValidate(document, out var failure))
            throw new ArgumentException(failure, nameof(document));

        var positions = document.Vertices.OrderBy(vertex => vertex.Id).ToArray();
        var positionIndex = positions.Select((vertex, index) => (vertex.Id, Index: index + 1)).ToDictionary(item => item.Id, item => item.Index);
        var vertexById = positions.ToDictionary(vertex => vertex.Id);
        var textureCoordinates = new List<(double U, double V)>();
        var normals = new List<Vector3D>();
        var textureIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        var normalIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        var faces = new List<ObjPolygon>();

        foreach (var patch in document.Patches.OrderBy(patch => patch.FaceId.Value).ThenBy(patch => patch.ChartId, StringComparer.Ordinal))
        {
            foreach (var cell in patch.Cells)
            {
                if (cell.VertexIds.Count < 3) continue;
                var corners = new List<ObjCorner>(cell.VertexIds.Count);
                foreach (var vertexId in cell.VertexIds)
                {
                    var point = vertexById[vertexId].Position;
                    var uv = EvaluateUv(patch, point);
                    var normal = EvaluateNormal(patch, point);
                    var vt = Intern(textureCoordinates, textureIndex, uv.U, uv.V);
                    var vn = Intern(normals, normalIndex, normal.X, normal.Y, normal.Z);
                    corners.Add(new ObjCorner(positionIndex[vertexId], vt, vn));
                }
                faces.Add(new ObjPolygon(patch.FaceId.Value, patch.SemanticOwner, cell.Kind, corners));
            }
        }

        var text = Write(objectName, positions, textureCoordinates, normals, faces);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
        return new SurfaceMeshObjExport(text, hash, positions.Length, textureCoordinates.Count, normals.Count,
            faces.Count, faces.Count(face => face.Kind == SurfaceMeshCellKind.Quad),
            faces.Count(face => face.Kind == SurfaceMeshCellKind.Triangle),
            faces.Count(face => face.Kind == SurfaceMeshCellKind.BoundaryPolygon),
            faces.Count(face => face.Kind == SurfaceMeshCellKind.Singular));
    }

    private static string Write(string objectName, IReadOnlyList<SurfaceMeshVertex> positions, IReadOnlyList<(double U, double V)> textureCoordinates, IReadOnlyList<Vector3D> normals, IReadOnlyList<ObjPolygon> faces)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Aetheris SurfaceMeshIR OBJ; topology-preserving polygon export");
        builder.Append("o ").AppendLine(Sanitize(objectName));
        foreach (var vertex in positions)
            builder.Append("v ").Append(Format(vertex.Position.X)).Append(' ').Append(Format(vertex.Position.Y)).Append(' ').Append(Format(vertex.Position.Z)).AppendLine();
        foreach (var texture in textureCoordinates)
            builder.Append("vt ").Append(Format(texture.U)).Append(' ').Append(Format(texture.V)).AppendLine();
        foreach (var normal in normals)
            builder.Append("vn ").Append(Format(normal.X)).Append(' ').Append(Format(normal.Y)).Append(' ').Append(Format(normal.Z)).AppendLine();

        int? previousFace = null;
        string? previousSemantic = null;
        foreach (var face in faces)
        {
            if (previousFace != face.FaceId || !string.Equals(previousSemantic, face.SemanticOwner, StringComparison.Ordinal))
            {
                builder.Append("g face_").Append(face.FaceId.ToString(CultureInfo.InvariantCulture));
                if (!string.IsNullOrWhiteSpace(face.SemanticOwner)) builder.Append(' ').Append("semantic_").Append(Sanitize(face.SemanticOwner));
                builder.AppendLine();
                previousFace = face.FaceId;
                previousSemantic = face.SemanticOwner;
            }
            builder.Append("f");
            foreach (var corner in face.Corners) builder.Append(' ').Append(corner.Position).Append('/').Append(corner.Texture).Append('/').Append(corner.Normal);
            builder.AppendLine();
        }
        return builder.ToString();
    }

    private static (double U, double V) EvaluateUv(SurfacePatch patch, Point3D point) => patch.Support.Kind switch
    {
        SurfaceMeshSupportKind.Plane when patch.Support.Plane is { } plane => ((point - plane.Origin).Dot(plane.UAxis.ToVector()), (point - plane.Origin).Dot(plane.VAxis.ToVector())),
        SurfaceMeshSupportKind.Cylinder when patch.Support.Cylinder is { } cylinder => PolarUv(point - cylinder.Origin, cylinder.Axis.ToVector(), cylinder.XAxis.ToVector(), cylinder.YAxis.ToVector()),
        SurfaceMeshSupportKind.Cone when patch.Support.Cone is { } cone => ConeUv(cone, point),
        SurfaceMeshSupportKind.Sphere when patch.Support.Sphere is { } sphere => SphereUv(sphere, point),
        SurfaceMeshSupportKind.Torus when patch.Support.Torus is { } torus => TorusUv(torus, point),
        _ => throw new InvalidOperationException($"Patch {patch.FaceId.Value} has no parameterization."),
    };

    private static Vector3D EvaluateNormal(SurfacePatch patch, Point3D point)
    {
        Vector3D normal = patch.Support.Kind switch
        {
            SurfaceMeshSupportKind.Plane when patch.Support.Plane is { } plane => plane.Normal.ToVector(),
            SurfaceMeshSupportKind.Cylinder when patch.Support.Cylinder is { } cylinder => cylinder.Normal(PolarUv(point - cylinder.Origin, cylinder.Axis.ToVector(), cylinder.XAxis.ToVector(), cylinder.YAxis.ToVector()).U).ToVector(),
            SurfaceMeshSupportKind.Cone when patch.Support.Cone is { } cone => cone.Normal(ConeUv(cone, point).U).ToVector(),
            SurfaceMeshSupportKind.Sphere when patch.Support.Sphere is { } sphere => Direction3D.Create(point - sphere.Center).ToVector(),
            SurfaceMeshSupportKind.Torus when patch.Support.Torus is { } torus => torus.Normal(TorusUv(torus, point).U, TorusUv(torus, point).V).ToVector(),
            _ => throw new InvalidOperationException($"Patch {patch.FaceId.Value} has no exact normal evaluator."),
        };
        return patch.SameSense ? normal : -normal;
    }

    private static (double U, double V) PolarUv(Vector3D offset, Vector3D axis, Vector3D xAxis, Vector3D yAxis)
        => (double.Atan2(offset.Dot(yAxis), offset.Dot(xAxis)), offset.Dot(axis));

    private static (double U, double V) ConeUv(Geometry.Surfaces.ConeSurface cone, Point3D point)
    {
        var offset = point - cone.Apex;
        var axis = cone.Axis.ToVector();
        var radial = offset - (axis * offset.Dot(axis));
        var x = cone.ReferenceAxis.ToVector() - (axis * cone.ReferenceAxis.ToVector().Dot(axis));
        var y = axis.Cross(x);
        return (double.Atan2(radial.Dot(y), radial.Dot(x)), cone.AxialParameterFromPoint(point));
    }

    private static (double U, double V) SphereUv(Geometry.Surfaces.SphereSurface sphere, Point3D point)
    {
        var offset = point - sphere.Center;
        var axis = sphere.Axis.ToVector();
        return (double.Atan2(offset.Dot(sphere.YAxis.ToVector()), offset.Dot(sphere.XAxis.ToVector())), double.Asin(double.Clamp(offset.Dot(axis) / sphere.Radius, -1d, 1d)));
    }

    private static (double U, double V) TorusUv(Geometry.Surfaces.TorusSurface torus, Point3D point)
    {
        var offset = point - torus.Center;
        var axisDistance = offset.Dot(torus.Axis.ToVector());
        var planar = offset - (torus.Axis.ToVector() * axisDistance);
        var u = double.Atan2(planar.Dot(torus.YAxis.ToVector()), planar.Dot(torus.XAxis.ToVector()));
        return (u, double.Atan2(axisDistance, planar.Length - torus.MajorRadius));
    }

    private static int Intern(List<(double U, double V)> values, Dictionary<string, int> index, double u, double v)
    {
        var key = $"{Format(u)},{Format(v)}";
        if (index.TryGetValue(key, out var found)) return found;
        values.Add((u, v));
        return index[key] = values.Count;
    }

    private static int Intern(List<Vector3D> values, Dictionary<string, int> index, double x, double y, double z)
    {
        var key = $"{Format(x)},{Format(y)},{Format(z)}";
        if (index.TryGetValue(key, out var found)) return found;
        values.Add(new Vector3D(x, y, z));
        return index[key] = values.Count;
    }

    private static string Format(double value) => double.Abs(value) < Epsilon ? "0" : value.ToString("R", CultureInfo.InvariantCulture);
    private static string Sanitize(string value)
    {
        var token = string.Concat(value.Trim().Select(character => char.IsLetterOrDigit(character) || character is '_' or '-' or '.' ? character : '_'));
        return string.IsNullOrEmpty(token) ? "unnamed" : token;
    }

    private sealed record ObjCorner(int Position, int Texture, int Normal);
    private sealed record ObjPolygon(int FaceId, string? SemanticOwner, SurfaceMeshCellKind Kind, IReadOnlyList<ObjCorner> Corners);
}

public sealed record SurfaceMeshObjExport(
    string Text,
    string DeterministicHash,
    int VertexCount,
    int TextureCoordinateCount,
    int NormalCount,
    int PolygonCount,
    int QuadCount,
    int TriangleCount,
    int BoundaryPolygonCount,
    int SingularPolygonCount);
