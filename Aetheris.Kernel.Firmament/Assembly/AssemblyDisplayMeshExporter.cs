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
    /// <summary>Apply a new pure pose to an existing display document without tessellating
    /// or rebuilding any shared part definition.</summary>
    public static AssemblyDisplayMeshDocument WithPose(AssemblyDisplayMeshDocument document, AssemblyPoseResult pose)
    {
        if (!pose.IsSuccess) throw new InvalidOperationException("assembly-mesh-invalid-pose");
        var transforms = pose.Instances.ToDictionary(instance => instance.StableId, instance => instance.ResolvedTransform, StringComparer.Ordinal);
        if (transforms.Count != document.Occurrences.Count || document.Occurrences.Any(occurrence =>
                !transforms.TryGetValue(occurrence.Id, out var transform) || transform is null))
            throw new InvalidOperationException("assembly-mesh-pose-occurrence-mismatch");
        return document with
        {
            Occurrences = document.Occurrences.Select(occurrence => occurrence with
            {
                Transform = transforms[occurrence.Id]!.Matrix.ToArray()
            }).ToArray()
        };
    }

    public static AssemblyDisplayMeshDocument Export(AssemblyM1CompilationResult compilation, DisplayTessellationOptions? options = null,
        AssemblyPoseResult? pose = null)
    {
        if (!compilation.IsSuccess || compilation.Ir is null || compilation.Geometry is null)
            throw new InvalidOperationException("assembly-mesh-invalid-compilation");
        if (pose is { IsSuccess: false }) throw new InvalidOperationException("assembly-mesh-invalid-pose");
        if (pose is not null && (pose.Instances.Count != compilation.Ir.Instances.Count
            || pose.Instances.Any(instance => !compilation.Ir.Instances.Any(original => original.StableId == instance.StableId
                && original.DefinitionIdentity == instance.DefinitionIdentity))))
            throw new InvalidOperationException("assembly-mesh-pose-occurrence-mismatch");
        var geometry = compilation.Geometry;
        var ids = geometry.Artifact.Definitions.ToDictionary(d => d.DefinitionIdentity, d => d.StableId, StringComparer.Ordinal);
        var definitions = new List<AssemblyDisplayMeshDefinition>();
        foreach (var (identity, body) in geometry.DefinitionBodies.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            definitions.Add(PrepareDefinition(ids[identity], identity, body, options));
        }
        var occurrences = (pose?.Instances ?? compilation.Ir.Instances).OrderBy(i => i.Path.ToString(), StringComparer.Ordinal).Select(instance =>
        {
            var id = instance.Kind == AssemblyInstanceKind.Part
                ? ids.GetValueOrDefault(instance.DefinitionIdentity) ?? throw new InvalidOperationException($"assembly-mesh-missing-definition:{instance.Path}") : null;
            if (instance.ResolvedTransform is null || instance.ResolvedTransform.Matrix.Length != 16 || instance.ResolvedTransform.Matrix.Any(v => !double.IsFinite(v)))
                throw new InvalidOperationException($"assembly-mesh-unresolved-transform:{instance.Path}");
            return new AssemblyDisplayMeshOccurrence(instance.StableId, instance.Path.ToString(), instance.ParentStableId, id, instance.ResolvedTransform.Matrix.ToArray(), instance.StableId);
        }).ToArray();
        return new("aetheris/assembly-display-mesh/1", compilation.Ir.Name, "mm", definitions, occurrences);
    }

    internal static AssemblyDisplayMeshDefinition PrepareDefinition(string id, string identity, Aetheris.Kernel.Core.Brep.BrepBody body, DisplayTessellationOptions? options = null)
    {
            var effectiveOptions = options ?? new DisplayTessellationOptions(double.Pi / 16, .3, 6, 64);
            // Reuse the OBJ export's shared-boundary mesher for planar/cylindrical
            // and formed-wire torus parts. A failure on this admitted family must remain visible; do not
            // silently replace a failed cap or bore with the legacy mesh.
            var useSurfaceMeshIr = body.Topology.Faces.All(face =>
                body.TryGetFaceSurfaceGeometry(face.Id, out var surface)
                && surface?.Kind is SurfaceGeometryKind.Plane or SurfaceGeometryKind.Cylinder or SurfaceGeometryKind.Torus);
            var structuredSpline = !useSurfaceMeshIr && RectangularSplineDisplayTessellator.TryTessellate(body, effectiveOptions, out var splineMesh)
                ? splineMesh : null;
            var tessellation = structuredSpline is not null
                ? Aetheris.Kernel.Core.Results.KernelResult<DisplayTessellationResult>.Success(structuredSpline)
                : useSurfaceMeshIr
                ? BrepDisplayTessellator.TessellateSurfaceMeshIr(body, effectiveOptions)
                : BrepDisplayTessellator.TessellateBounded(body, effectiveOptions);
            if (!tessellation.IsSuccess || tessellation.Value.FacePatches.Count != body.Topology.Faces.Count())
                throw new InvalidOperationException($"assembly-mesh-definition-failed:{identity}:" + string.Join(";", tessellation.Diagnostics.Select(d => d.Message)));
            var positions = new List<double>(); var normals = new List<double>(); var indices = new List<int>();
            var ranges = new List<AssemblyDisplayMeshRange>();
            foreach (var rawFace in tessellation.Value.FacePatches.OrderBy(p => p.FaceId.Value))
            {
                // Both tessellation paths emit canonically oriented patches. Assembly placement transforms
                // positions and normals but never reinterprets interchange orientation evidence.
                var face = rawFace;
                if (face.Positions.Count == 0 || face.Normals.Count != face.Positions.Count || face.TriangleIndices.Count == 0
                    || face.TriangleIndices.Count % 3 != 0 || face.TriangleIndices.Any(i => i < 0 || i >= face.Positions.Count))
                    throw new InvalidOperationException($"assembly-mesh-face-invalid:{identity}:{face.FaceId}:positions={face.Positions.Count};normals={face.Normals.Count};indices={face.TriangleIndices.Count};" + string.Join(";", tessellation.Diagnostics.Select(d => d.Message)));
                var offset = positions.Count / 3;
                var startTriangle = indices.Count / 3;
                positions.AddRange(face.Positions.SelectMany(p => new[] { p.X, p.Y, p.Z }));
                normals.AddRange(face.Normals.SelectMany(p => new[] { p.X, p.Y, p.Z }));
                indices.AddRange(face.TriangleIndices.Select(i => i + offset));
                ranges.Add(new(startTriangle, face.TriangleIndices.Count / 3, $"face:{face.FaceId.Value}", id));
            }
            if (positions.Concat(normals).Any(v => !double.IsFinite(v)))
                throw new InvalidOperationException($"assembly-mesh-nonfinite:{identity}");
            return new(id, identity, positions.ToArray(), normals.ToArray(), indices.ToArray(),
                tessellation.Value.MeshPipeline.ToString(), ranges);
    }

    public static string Serialize(AssemblyDisplayMeshDocument document)
        => JsonSerializer.Serialize(document, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
}
