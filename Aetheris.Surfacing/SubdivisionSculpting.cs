using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Surfacing;

public sealed record CageVertex(string Id, Point3D Position, IReadOnlyList<string> Parents);
public sealed record CageFace(string Id, IReadOnlyList<string> Vertices, IReadOnlyList<int> SourceFaceIds);
public sealed record CageEdge(string Id, string A, string B, IReadOnlyList<int> Faces);
public sealed record MeshExportPreparation(TriangleMesh NativeMesh, TriangleMesh EncodedMesh, TriangleMeshTopologyReport Topology, double MaximumFloatQuantizationMm);
public sealed record SubdivisionCage(IReadOnlyList<CageVertex> Vertices, IReadOnlyList<CageFace> Faces, IReadOnlyList<string> Creases);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$operation")]
[JsonDerivedType(typeof(SubdivideCage), "Subdivide")]
[JsonDerivedType(typeof(MoveCageVertices), "Move")]
[JsonDerivedType(typeof(ScaleCageVertices), "Scale")]
[JsonDerivedType(typeof(CreaseCageEdges), "Crease")]
public abstract record MeshSculptOperation(string Id)
{
    public IReadOnlyList<string> Preserve { get; init; } = [];
}
public sealed record SubdivideCage(string Id, int Levels) : MeshSculptOperation(Id);
public sealed record MoveCageVertices(string Id, IReadOnlyList<string> Vertices, Vector3D Offset) : MeshSculptOperation(Id);
public sealed record ScaleCageVertices(string Id, IReadOnlyList<string> Vertices, Vector3D Factor, Point3D Origin) : MeshSculptOperation(Id);
public sealed record CreaseCageEdges(string Id, IReadOnlyList<string> Edges, bool Sharp) : MeshSculptOperation(Id);
public sealed record MeshConstruction(string Schema, ConstructionState Source, string Cage, int Segments, double MaximumDeviationMm, IReadOnlyList<MeshSculptOperation> Operations);
public sealed record MeshSculptState(string StateId, string? PredecessorStateId, string Name, string SourceBodyStateId,
    SubdivisionCage Cage, MeshConstruction Construction, double ConversionDeviationMm, string ConversionEvidence)
{
    public string ArtifactKind => "SubdivisionMesh";
    public bool StepExportAvailable => false;
    public string SelfIntersectionStatus => "NotCertified";
    public IReadOnlyList<CageEdge> Edges => SubdivisionSculptor.Edges(Cage);
}

/// <summary>Finite Catmull-Clark refinement over a real closed quad cage. No BRep fitting/fallback.</summary>
public static class SubdivisionSculptor
{
    public const string Schema = "aetheris.mesh-sculpt.v1";
    public const int MaximumFaces = 24576;
    public static string Hash(string value) => System.Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    public static string EdgeId(string a, string b) => "e" + Hash(string.CompareOrdinal(a, b) < 0 ? a + "|" + b : b + "|" + a)[..20];

    public static IReadOnlyList<CageEdge> Edges(SubdivisionCage cage)
    {
        var edges = new Dictionary<string, (string A, string B, List<int> Faces)>(StringComparer.Ordinal);
        for (var i = 0; i < cage.Faces.Count; i++)
        {
            var v = cage.Faces[i].Vertices;
            for (var j = 0; j < v.Count; j++)
            {
                var a = v[j]; var b = v[(j + 1) % v.Count]; var id = EdgeId(a, b);
                if (!edges.TryGetValue(id, out var edge)) edges.Add(id, edge = (a, b, []));
                edge.Faces.Add(i);
            }
        }
        return edges.OrderBy(e => e.Key, StringComparer.Ordinal).Select(e => new CageEdge(e.Key, e.Value.A, e.Value.B, e.Value.Faces.ToArray())).ToArray();
    }

    public static MeshSculptState Convert(BodyState source, string name, string kind, int segments = 4, double maximumDeviationMm = 1)
    {
        if (source.ConstructionAuthority is null) throw Error("mesh-source-not-replayable", "Conversion requires a source construction recipe.");
        if (kind is not ("Box" or "Sphere")) throw Error("mesh-cage-unsupported", "Explicit cage must be Box or Sphere; arbitrary BRep retopology is not implemented.");
        if (segments is < 1 or > 16 || !double.IsFinite(maximumDeviationMm) || maximumDeviationMm <= 0)
            throw Error("mesh-conversion-budget-invalid", "Segments must be 1..16 and maximumDeviation must be positive and finite.");
        var body = source.Body;
        var vertices = body.Topology.Vertices.Select(v => body.TryGetVertexPoint(v.Id, out var p) ? p : throw Error("mesh-source-unbound", "Unbound source vertex.")).ToArray();
        var surfaces = body.Topology.Faces.Select(f => body.TryGetFaceSurfaceGeometry(f.Id, out var s) ? s! : throw Error("mesh-source-unbound", "Unbound source face.")).ToArray();
        Func<int, int, int, Point3D> Map = (x, y, z) => new(x, y, z);
        double radius = 0; Point3D center = Point3D.Origin;
        if (kind == "Box")
        {
            if (vertices.Length != 8 || body.Topology.Faces.Count() != 6 || surfaces.Any(s => s.Plane is null) || body.Topology.Faces.Any(f => f.LoopIds.Count != 1))
                throw Error("mesh-box-cage-source-unsupported", "Box cage conversion requires a pure axis-aligned box with eight vertices and six planar faces.");
            var lo = new Point3D(vertices.Min(p => p.X), vertices.Min(p => p.Y), vertices.Min(p => p.Z));
            var hi = new Point3D(vertices.Max(p => p.X), vertices.Max(p => p.Y), vertices.Max(p => p.Z));
            if (vertices.Any(p => (p.X != lo.X && p.X != hi.X) || (p.Y != lo.Y && p.Y != hi.Y) || (p.Z != lo.Z && p.Z != hi.Z)) || vertices.Distinct().Count() != 8)
                throw Error("mesh-box-cage-source-unsupported", "A bounding box is not a box conversion; every source corner must match.");
            Map = (x, y, z) => new(lo.X + (hi.X - lo.X) * (x + segments) / (2d * segments), lo.Y + (hi.Y - lo.Y) * (y + segments) / (2d * segments), lo.Z + (hi.Z - lo.Z) * (z + segments) / (2d * segments));
        }
        else
        {
            var spheres = surfaces.Select(s => s.Sphere).ToArray();
            if (spheres.Length == 0 || spheres.Any(s => s is null) || spheres.Distinct().Count() != 1)
                throw Error("mesh-sphere-cage-source-unsupported", "Sphere cage conversion requires one common exact spherical support and no other surface family.");
            radius = spheres[0]!.Value.Radius; center = spheres[0]!.Value.Center;
            Map = (x, y, z) => center + new Vector3D(x, y, z) * (radius / Math.Sqrt(x * x + y * y + z * z));
        }
        var vs = new List<CageVertex>(); var fs = new List<CageFace>(); var ids = new Dictionary<(int, int, int), string>();
        string Vertex(int x, int y, int z)
        {
            if (ids.TryGetValue((x, y, z), out var id)) return id;
            id = "v" + vs.Count; ids.Add((x, y, z), id); vs.Add(new(id, Map(x, y, z), [source.StateId.Value])); return id;
        }
        // Six consistently outward cube parameterizations, welded by lattice key.
        (int X, int Y, int Z) Grid(int side, int u, int v) => side switch
        {
            0 => (segments, u, v),
            1 => (-segments, v, u),
            2 => (v, segments, u),
            3 => (u, -segments, v),
            4 => (u, v, segments),
            _ => (v, u, -segments)
        };
        var sourceFaces = body.Topology.Faces.Select(f => f.Id.Value).ToArray();
        for (var side = 0; side < 6; side++) for (var i = 0; i < segments; i++) for (var j = 0; j < segments; j++)
        {
            var u = -segments + 2 * i; var v = -segments + 2 * j;
            var keys = new[] { Grid(side, u, v), Grid(side, u + 2, v), Grid(side, u + 2, v + 2), Grid(side, u, v + 2) };
            var quad = keys.Select(p => Vertex(p.X, p.Y, p.Z)).ToArray();
            var owners = kind == "Sphere" ? sourceFaces : body.Topology.Faces.Where(f =>
                body.TryGetFaceSurfaceGeometry(f.Id, out var s) && s?.Plane is { } plane &&
                quad.All(id => Math.Abs((vs.Single(vertex => vertex.Id == id).Position - plane.Origin).Dot(plane.Normal.ToVector())) <= 1e-8)).Select(f => f.Id.Value).ToArray();
            if (kind == "Box" && owners.Length != 1) throw Error("mesh-box-face-correspondence-unsupported", "A cage face must have one proven source plane; no nearest-face projection.");
            fs.Add(new("q" + fs.Count, quad, owners));
        }
        var cage = new SubdivisionCage(vs.ToArray(), fs.ToArray(), []);
        if (kind == "Box")
        {
            var byId = vs.ToDictionary(v => v.Id, v => v.Position, StringComparer.Ordinal);
            var crease = Edges(cage).Where(e =>
            {
                var a = byId[e.A]; var b = byId[e.B]; var lo = vertices.Aggregate((p, q) => new(Math.Min(p.X, q.X), Math.Min(p.Y, q.Y), Math.Min(p.Z, q.Z))); var hi = vertices.Aggregate((p, q) => new(Math.Max(p.X, q.X), Math.Max(p.Y, q.Y), Math.Max(p.Z, q.Z)));
                return (a.X == b.X && (a.X == lo.X || a.X == hi.X) ? 1 : 0) + (a.Y == b.Y && (a.Y == lo.Y || a.Y == hi.Y) ? 1 : 0) + (a.Z == b.Z && (a.Z == lo.Z || a.Z == hi.Z) ? 1 : 0) >= 2;
            }).Select(e => e.Id).ToArray();
            cage = cage with { Creases = crease };
        }
        var prepared = PrepareExport(cage, maximumDeviationMm);
        var deviation = kind == "Sphere"
            ? Math.Max(RadialBound(prepared.NativeMesh, center, radius), RadialBound(prepared.EncodedMesh, center, radius))
            : prepared.MaximumFloatQuantizationMm;
        if (deviation > maximumDeviationMm) throw Error("mesh-conversion-deviation-exceeded", $"Finite cage radial deviation {deviation:R}mm exceeds maximumDeviation {maximumDeviationMm:R}mm; increase segments or authorize a larger conversion budget.");
        var recipe = new MeshConstruction(Schema, source.ConstructionAuthority, kind, segments, maximumDeviationMm, []);
        return new(Hash(source.StateId.Value + "|" + Schema + "|" + kind + "|" + segments + "|" + maximumDeviationMm.ToString("R", CultureInfo.InvariantCulture) + "|" + Fingerprint(cage)), source.StateId.Value, name, source.StateId.Value, cage, recipe, deviation,
            kind == "Box" ? "Exact native box cage; sharp edges retained, with output quantization inside the declared budget." : "Finite cube-sphere cage; conservative triangle-plane radial bound, including output quantization, relative to the exact source sphere. Not a limit-surface certificate.");
    }

    public static MeshSculptState Apply(MeshSculptState input, string name, MeshSculptOperation operation)
    {
        if (input.Construction.Operations.Count >= 64) throw Error("mesh-operation-budget-exceeded", "At most 64 operations are admitted per mesh recipe.");
        var cage = input.Cage;
        var preserved = operation.Preserve.Count == 0 ? new HashSet<string>() : Selection(cage, operation.Preserve);
        switch (operation)
        {
            case SubdivideCage subdivide:
                if (subdivide.Levels is < 1 or > 4) throw Error("mesh-subdivision-budget-invalid", "Subdivide levels must be 1..4, within the cumulative face budget.");
                for (var i = 0; i < subdivide.Levels; i++) cage = Refine(cage);
                break;
            case MoveCageVertices move:
                if (!Finite(move.Offset)) throw Error("mesh-edit-nonfinite", "Move offset must be finite.");
                var moved = Selection(cage, move.Vertices);
                cage = cage with { Vertices = cage.Vertices.Select(v => moved.Contains(v.Id) ? v with { Position = v.Position + move.Offset } : v).ToArray() };
                break;
            case ScaleCageVertices scale:
                if (!Finite(scale.Factor) || scale.Factor.X <= 0 || scale.Factor.Y <= 0 || scale.Factor.Z <= 0 || !Finite(new(scale.Origin.X, scale.Origin.Y, scale.Origin.Z))) throw Error("mesh-scale-invalid", "Scale factors must be positive and finite, with a finite explicit origin.");
                var scaled = Selection(cage, scale.Vertices);
                cage = cage with { Vertices = cage.Vertices.Select(v => scaled.Contains(v.Id) ? v with { Position = scale.Origin + new Vector3D((v.Position.X - scale.Origin.X) * scale.Factor.X, (v.Position.Y - scale.Origin.Y) * scale.Factor.Y, (v.Position.Z - scale.Origin.Z) * scale.Factor.Z) } : v).ToArray() };
                break;
            case CreaseCageEdges crease:
                var edges = Edges(cage).Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
                var selected = crease.Edges.SequenceEqual(new[] { "All" }) ? edges : crease.Edges.ToHashSet(StringComparer.Ordinal);
                if (selected.Count == 0 || selected.Count != crease.Edges.Count && !crease.Edges.SequenceEqual(new[] { "All" }) || selected.Any(e => !edges.Contains(e))) throw Error("mesh-edge-unresolved", "Crease requires unique current edge handles, or [All].");
                var creases = cage.Creases.ToHashSet(StringComparer.Ordinal);
                if (crease.Sharp) creases.UnionWith(selected); else creases.ExceptWith(selected);
                cage = cage with { Creases = creases.Order(StringComparer.Ordinal).ToArray() };
                break;
            default: throw Error("mesh-operation-unsupported", "Unknown mesh operation.");
        }
        Evaluate(cage); // Validate the successor before publishing it.
        var current = cage.Vertices.ToDictionary(v => v.Id, v => v.Position, StringComparer.Ordinal);
        if (input.Cage.Vertices.Any(v => preserved.Contains(v.Id) && (!current.TryGetValue(v.Id, out var p) || p != v.Position)))
            throw Error("mesh-preservation-failed", "An explicitly preserved cage vertex moved or disappeared; no successor was published.");
        if (operation is MoveCageVertices or ScaleCageVertices)
        {
            var old = input.Cage.Vertices.ToDictionary(v => v.Id, v => v.Position, StringComparer.Ordinal);
            foreach (var face in cage.Faces) for (var i = 1; i < 3; i++)
            {
                var a = face.Vertices[0]; var b = face.Vertices[i]; var c = face.Vertices[i + 1];
                if ((old[b] - old[a]).Cross(old[c] - old[a]).Dot((current[b] - current[a]).Cross(current[c] - current[a])) <= 0)
                    throw Error("mesh-local-foldover", "The edit reverses a face triangle relative to its predecessor. Global self-intersection remains separately unqualified.");
            }
        }
        var recipe = input.Construction with { Operations = [.. input.Construction.Operations, operation] };
        return input with { StateId = Hash(input.StateId + "|" + System.Text.Json.JsonSerializer.Serialize<MeshSculptOperation>(operation) + "|" + Fingerprint(cage)), PredecessorStateId = input.StateId, Name = name, Cage = cage, Construction = recipe };
    }

    private static HashSet<string> Selection(SubdivisionCage cage, IReadOnlyList<string> requested)
    {
        var all = cage.Vertices.Select(v => v.Id).ToHashSet(StringComparer.Ordinal);
        if (requested.SequenceEqual(new[] { "All" })) return all;
        var result = requested.ToHashSet(StringComparer.Ordinal);
        if (result.Count == 0 || result.Count != requested.Count || result.Any(v => !all.Contains(v))) throw Error("mesh-vertex-unresolved", "Use unique current cage vertex handles or [All]. Stale handles are not rebound by proximity.");
        return result;
    }

    private static SubdivisionCage Refine(SubdivisionCage cage)
    {
        if (cage.Faces.Count > MaximumFaces / 4) throw Error("mesh-subdivision-budget-exceeded", $"Refinement exceeds {MaximumFaces} faces.");
        var edges = Edges(cage); var points = cage.Vertices.ToDictionary(v => v.Id, v => v.Position, StringComparer.Ordinal);
        var incident = cage.Vertices.ToDictionary(v => v.Id, _ => new List<CageEdge>(), StringComparer.Ordinal);
        foreach (var e in edges) { incident[e.A].Add(e); incident[e.B].Add(e); }
        var facePoints = cage.Faces.Select(f => Average(f.Vertices.Select(v => points[v]))).ToArray();
        var sharp = cage.Creases.ToHashSet(StringComparer.Ordinal); var output = new List<CageVertex>();
        foreach (var vertex in cage.Vertices)
        {
            var adjacent = incident[vertex.Id].ToArray();
            var creased = adjacent.Where(e => sharp.Contains(e.Id)).ToArray(); var p = vertex.Position;
            Point3D position;
            // Infinite sharpness: corners stay fixed; a two-edge crease uses
            // the cubic B-spline 6P/8 + (P0+P1)/8 rule. A dart stays smooth.
            if (creased.Length >= 3) position = p;
            else if (creased.Length == 2) position = Average([p, p, p, p, p, p, points[Other(creased[0], vertex.Id)], points[Other(creased[1], vertex.Id)]]);
            else
            {
                // Standard Catmull-Clark vertex rule: (F + 2R + (n-3)P)/n.
                var faces = adjacent.SelectMany(e => e.Faces).Distinct().ToArray(); var n = faces.Length;
                var f = Average(faces.Select(i => facePoints[i])); var r = Average(adjacent.Select(e => Average([points[e.A], points[e.B]])));
                position = new((f.X + 2 * r.X + (n - 3) * p.X) / n, (f.Y + 2 * r.Y + (n - 3) * p.Y) / n, (f.Z + 2 * r.Z + (n - 3) * p.Z) / n);
            }
            output.Add(vertex with { Position = position });
        }
        // A sharp edge splits at its midpoint; a smooth edge also averages
        // both adjacent face centers. The old edge ID names its new vertex.
        foreach (var edge in edges) output.Add(new(edge.Id, sharp.Contains(edge.Id) ? Average([points[edge.A], points[edge.B]]) : Average([points[edge.A], points[edge.B], facePoints[edge.Faces[0]], facePoints[edge.Faces[1]]]), [edge.A, edge.B]));
        for (var i = 0; i < cage.Faces.Count; i++) output.Add(new("f" + Hash(cage.Faces[i].Id)[..20], facePoints[i], cage.Faces[i].Vertices.ToArray()));
        var facesOut = new List<CageFace>();
        for (var i = 0; i < cage.Faces.Count; i++)
        {
            var face = cage.Faces[i]; var v = face.Vertices; var center = "f" + Hash(face.Id)[..20];
            for (var j = 0; j < 4; j++) facesOut.Add(new(face.Id + "_" + j, [v[j], EdgeId(v[j], v[(j + 1) % 4]), center, EdgeId(v[(j + 3) % 4], v[j])], face.SourceFaceIds));
        }
        var childCreases = edges.Where(e => sharp.Contains(e.Id)).SelectMany(e => new[] { EdgeId(e.A, e.Id), EdgeId(e.Id, e.B) }).Order(StringComparer.Ordinal).ToArray();
        return new(output.ToArray(), facesOut.ToArray(), childCreases);
    }

    public static TriangleMesh Evaluate(SubdivisionCage cage)
    {
        if (cage.Faces.Count == 0 || cage.Faces.Count > MaximumFaces || cage.Vertices.Select(v => v.Id).Distinct().Count() != cage.Vertices.Count || cage.Vertices.Any(v => !Finite(new(v.Position.X, v.Position.Y, v.Position.Z)))) throw Error("mesh-cage-invalid", "Invalid cage size, identities or coordinates.");
        var indices = cage.Vertices.Select((v, i) => (v.Id, i)).ToDictionary(v => v.Id, v => v.i, StringComparer.Ordinal);
        if (cage.Faces.Any(f => f.Vertices.Count != 4 || f.Vertices.Distinct().Count() != 4 || f.Vertices.Any(v => !indices.ContainsKey(v)))) throw Error("mesh-cage-invalid", "Every cage face must be a quad with four distinct current vertices.");
        var edges = Edges(cage);
        var edgeIds = edges.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        if (edges.Any(e => e.Faces.Count != 2) || cage.Creases.Any(c => !edgeIds.Contains(c))) throw Error("mesh-cage-nonmanifold", "Cage must be closed with current crease handles.");
        var links = cage.Vertices.ToDictionary(v => v.Id, _ => new List<CageEdge>(), StringComparer.Ordinal);
        foreach (var e in edges) { links[e.A].Add(e); links[e.B].Add(e); }
        foreach (var vertex in cage.Vertices)
        {
            var incident = links[vertex.Id]; var faces = incident.SelectMany(e => e.Faces).ToHashSet();
            if (faces.Count < 3) throw Error("mesh-cage-vertex-link-invalid", "Every cage vertex needs a closed connected face fan.");
            var seen = new HashSet<int>(); var pending = new Stack<int>(); pending.Push(faces.First());
            while (pending.Count > 0) { var f = pending.Pop(); if (!seen.Add(f)) continue; foreach (var e in incident.Where(e => e.Faces.Contains(f))) foreach (var next in e.Faces) if (!seen.Contains(next)) pending.Push(next); }
            if (seen.Count != faces.Count) throw Error("mesh-cage-vertex-link-invalid", "Disconnected face fans cannot share one vertex identity.");
        }
        var triangles = cage.Faces.SelectMany(f => new[] { indices[f.Vertices[0]], indices[f.Vertices[1]], indices[f.Vertices[2]], indices[f.Vertices[0]], indices[f.Vertices[2]], indices[f.Vertices[3]] }).ToArray();
        var positions = cage.Vertices.Select(v => v.Position).ToArray(); var normals = new Vector3D[positions.Length];
        for (var i = 0; i < triangles.Length; i += 3)
        {
            var a = triangles[i]; var b = triangles[i + 1]; var c = triangles[i + 2]; var n = (positions[b] - positions[a]).Cross(positions[c] - positions[a]);
            normals[a] += n; normals[b] += n; normals[c] += n;
        }
        for (var i = 0; i < normals.Length; i++) if (normals[i].Length > 0) normals[i] /= normals[i].Length;
        var creaseIds = cage.Creases.ToHashSet(StringComparer.Ordinal);
        var hard = edges.Where(e => creaseIds.Contains(e.Id)).Select(e => (indices[e.A], indices[e.B])).ToHashSet();
        var mesh = new TriangleMesh(positions, normals, triangles, hard, Fingerprint(cage));
        if (!TriangleMeshValidator.TryValidateClosed(mesh, out var topology, out var failure) || !double.IsFinite(topology.SignedVolume) || normals.Any(n => !Finite(n))) throw Error("mesh-successor-invalid", failure ?? "Mesh numeric range exhausted.");
        // Existing validator checks incidence/volume, not directed edge winding.
        var uses = new Dictionary<(int, int), int>();
        for (var i = 0; i < triangles.Length; i += 3) for (var j = 0; j < 3; j++) { var a = triangles[i + j]; var b = triangles[i + (j + 1) % 3]; var key = a < b ? (a, b) : (b, a); uses[key] = uses.GetValueOrDefault(key) + (a < b ? 1 : -1); }
        if (uses.Any(e => e.Value != 0)) throw Error("mesh-winding-invalid", "Adjacent faces must traverse shared edges in opposite directions.");
        return mesh;
    }

    /// <summary>Shared compiler/export admission for the current 32-bit GLB/STL boundary.</summary>
    public static MeshExportPreparation PrepareExport(SubdivisionCage cage, double maximumQuantizationMm)
    {
        var mesh = Evaluate(cage);
        var encoded = mesh with { Positions = mesh.Positions.Select(p => new Point3D((float)p.X, (float)p.Y, (float)p.Z)).ToArray() };
        if (encoded.Positions.Any(p => !Finite(new(p.X, p.Y, p.Z)))
            || !TriangleMeshValidator.TryValidateClosed(encoded, out var topology, out _) || !double.IsFinite(topology.SignedVolume))
            throw Error("mesh-export-precision-exhausted", "The 32-bit output coordinates cannot retain a finite closed mesh. No artifact is admitted.");
        var deviation = mesh.Positions.Zip(encoded.Positions, (a, b) => (a - b).Length).Max();
        if (!double.IsFinite(maximumQuantizationMm) || maximumQuantizationMm <= 0 || deviation > maximumQuantizationMm)
            throw Error("mesh-export-precision-budget-exceeded", $"Output quantization {deviation:R}mm exceeds the inherited maximumDeviation budget {maximumQuantizationMm:R}mm. Reduce the coordinate scale or explicitly authorize a larger budget.");
        return new(mesh, encoded, topology, deviation);
    }

    public static string Fingerprint(SubdivisionCage cage) => Hash(string.Join(";", cage.Vertices.Select(v => v.Id + ":" + v.Position.X.ToString("R", CultureInfo.InvariantCulture) + "," + v.Position.Y.ToString("R", CultureInfo.InvariantCulture) + "," + v.Position.Z.ToString("R", CultureInfo.InvariantCulture))) + "|" + string.Join(";", cage.Faces.Select(f => f.Id + ":" + string.Join(",", f.Vertices))) + "|" + string.Join(",", cage.Creases.Order(StringComparer.Ordinal)));
    private static Point3D Average(IEnumerable<Point3D> values) { var p = values.ToArray(); return new(p.Average(x => x.X), p.Average(x => x.Y), p.Average(x => x.Z)); }
    private static string Other(CageEdge e, string v) => e.A == v ? e.B : e.A;
    private static bool Finite(Vector3D v) => double.IsFinite(v.X) && double.IsFinite(v.Y) && double.IsFinite(v.Z);
    private static double RadialBound(TriangleMesh mesh, Point3D center, double radius)
    {
        var lower = radius; var upper = 0d;
        for (var i = 0; i < mesh.TriangleIndices.Count; i += 3)
        {
            var a = mesh.Positions[mesh.TriangleIndices[i]] - center; var b = mesh.Positions[mesh.TriangleIndices[i + 1]] - center; var c = mesh.Positions[mesh.TriangleIndices[i + 2]] - center;
            var n = (b - a).Cross(c - a); var d = a.Dot(n) / n.Length;
            if (d <= 0) throw Error("mesh-sphere-cage-invalid", "Sphere cage triangle is not outward relative to its source center.");
            lower = Math.Min(lower, d); upper = Math.Max(upper, Math.Max(a.Length, Math.Max(b.Length, c.Length)));
        }
        return Math.Max(radius - lower, Math.Abs(upper - radius));
    }
    private static InvalidOperationException Error(string code, string message) => new(code + ": " + message);
}
