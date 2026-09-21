using System.Text.Json;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Geometry;

namespace Aetheris.Kernel.Firmament.Assembly;

public sealed record AssemblyDisplayMeshRange(int StartTriangle, int TriangleCount, string FaceId, string SemanticEntityId);
public sealed record AssemblyDisplayMeshDefinition(string Id, string Identity, double[] Positions, double[] Normals, int[] Indices,
    string MeshPipeline = "LegacyTessellator", IReadOnlyList<AssemblyDisplayMeshRange>? Ranges = null);
public sealed record AssemblyDisplayMeshOccurrence(string Id, string Path, string? ParentId, string? DefinitionId, double[] Transform,
    string? SemanticEntityId = null);
public sealed record AssemblyDisplayMeshDocument(string Schema, string Name, string Units,
    IReadOnlyList<AssemblyDisplayMeshDefinition> Definitions, IReadOnlyList<AssemblyDisplayMeshOccurrence> Occurrences);

/// <summary>Deterministic local-definition meshes plus world occurrence frames. Never drops failed geometry.</summary>
public static class AssemblyDisplayMeshExporter
{
    public static AssemblyDisplayMeshDocument Export(AssemblyM1CompilationResult compilation, DisplayTessellationOptions? options = null)
    {
        if (!compilation.IsSuccess || compilation.Ir is null || compilation.Geometry is null)
            throw new InvalidOperationException("assembly-mesh-invalid-compilation");
        var geometry = compilation.Geometry;
        var ids = geometry.Artifact.Definitions.ToDictionary(d => d.DefinitionIdentity, d => d.StableId, StringComparer.Ordinal);
        var definitions = new List<AssemblyDisplayMeshDefinition>();
        foreach (var (identity, body) in geometry.DefinitionBodies.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            var effectiveOptions = options ?? new DisplayTessellationOptions(double.Pi / 16, .3, 6, 64);
            // Reuse the OBJ export's shared-boundary mesher for planar/cylindrical
            // parts. A failure on this admitted family must remain visible; do not
            // silently replace a failed cap or bore with the legacy mesh.
            var useSurfaceMeshIr = body.Topology.Faces.All(face =>
                body.TryGetFaceSurfaceGeometry(face.Id, out var surface)
                && surface?.Kind is SurfaceGeometryKind.Plane or SurfaceGeometryKind.Cylinder);
            if (body.Geometry.Surfaces.Any(s => s.Value.Kind == SurfaceGeometryKind.BSplineSurfaceWithKnots))
            {
                var trims = Aetheris.Surfacing.BoundedPcurveBuilder.Populate(body.Topology, body.Geometry, body.Bindings);
                if (!trims.IsSuccess)
                    throw new InvalidOperationException($"assembly-mesh-pcurve-failed:{identity}:" + string.Join(";", trims.Diagnostics));
            }
            var tessellation = useSurfaceMeshIr
                ? BrepDisplayTessellator.TessellateSurfaceMeshIr(body, effectiveOptions)
                : BrepDisplayTessellator.TessellateBounded(body, effectiveOptions);
            if (!tessellation.IsSuccess || tessellation.Value.FacePatches.Count != body.Topology.Faces.Count())
                throw new InvalidOperationException($"assembly-mesh-definition-failed:{identity}:" + string.Join(";", tessellation.Diagnostics.Select(d => d.Message)));
            var positions = new List<double>(); var normals = new List<double>(); var indices = new List<int>();
            var ranges = new List<AssemblyDisplayMeshRange>();
            foreach (var rawFace in tessellation.Value.FacePatches.OrderBy(p => p.FaceId.Value))
            {
                // Both tessellation paths now emit face-oriented patches: SurfaceMeshIR always did,
                // and BrepDisplayTessellator projects the face sense once, for every surface kind, at
                // the single place face patches are produced. Re-orienting here flipped every face whose
                // binding says same_sense=.F. a second time - one bore cylinder was enough to put this
                // gear's enclosed volume 6.25% over its cap-area-times-height value.
                var face = rawFace;
                if (face.Positions.Count == 0 || face.Normals.Count != face.Positions.Count || face.TriangleIndices.Count == 0
                    || face.TriangleIndices.Count % 3 != 0 || face.TriangleIndices.Any(i => i < 0 || i >= face.Positions.Count))
                    throw new InvalidOperationException($"assembly-mesh-face-invalid:{identity}:{face.FaceId}:positions={face.Positions.Count};normals={face.Normals.Count};indices={face.TriangleIndices.Count};" + string.Join(";", tessellation.Diagnostics.Select(d => d.Message)));
                var offset = positions.Count / 3;
                var startTriangle = indices.Count / 3;
                positions.AddRange(face.Positions.SelectMany(p => new[] { p.X, p.Y, p.Z }));
                normals.AddRange(face.Normals.SelectMany(p => new[] { p.X, p.Y, p.Z }));
                indices.AddRange(face.TriangleIndices.Select(i => i + offset));
                ranges.Add(new(startTriangle, face.TriangleIndices.Count / 3, $"face:{face.FaceId.Value}", ids[identity]));
            }
            if (positions.Concat(normals).Any(v => !double.IsFinite(v)))
                throw new InvalidOperationException($"assembly-mesh-nonfinite:{identity}");
            definitions.Add(new(ids[identity], identity, positions.ToArray(), normals.ToArray(), indices.ToArray(),
                tessellation.Value.MeshPipeline.ToString(), ranges));
        }
        var occurrences = compilation.Ir.Instances.OrderBy(i => i.Path.ToString(), StringComparer.Ordinal).Select(instance =>
        {
            var id = instance.Kind == AssemblyInstanceKind.Part
                ? ids.GetValueOrDefault(instance.DefinitionIdentity) ?? throw new InvalidOperationException($"assembly-mesh-missing-definition:{instance.Path}") : null;
            if (instance.ResolvedTransform is null || instance.ResolvedTransform.Matrix.Length != 16 || instance.ResolvedTransform.Matrix.Any(v => !double.IsFinite(v)))
                throw new InvalidOperationException($"assembly-mesh-unresolved-transform:{instance.Path}");
            return new AssemblyDisplayMeshOccurrence(instance.StableId, instance.Path.ToString(), instance.ParentStableId, id, instance.ResolvedTransform.Matrix.ToArray(), instance.StableId);
        }).ToArray();
        return new("aetheris/assembly-display-mesh/1", compilation.Ir.Name, "mm", definitions, occurrences);
    }

    public static string Serialize(AssemblyDisplayMeshDocument document)
        => JsonSerializer.Serialize(document, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
}
