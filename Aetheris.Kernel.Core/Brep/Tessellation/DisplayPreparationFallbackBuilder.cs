using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Tessellation;

public static class DisplayPreparationFallbackBuilder
{
    public static KernelResult<DisplayTessellationResult> Build(
        BrepBody body,
        DisplayTessellationOptions? tessellationOptions = null)
        => Build(body, tessellationOptions, null);

    internal static KernelResult<DisplayTessellationResult> Build(
        BrepBody body,
        DisplayTessellationOptions? tessellationOptions,
        DisplayPreparationBsplineScaffoldOptions? scaffoldOptions)
    {
        var tessellation = BrepDisplayTessellator.Tessellate(body, tessellationOptions);
        if (!tessellation.IsSuccess)
        {
            return tessellation;
        }

        var effectiveScaffoldOptions = scaffoldOptions ?? DisplayPreparationBsplineScaffoldOptions.ConservativeDefaults;
        var fallbackByFace = tessellation.Value.FacePatches.ToDictionary(patch => patch.FaceId);
        var scaffoldBuilder = new BsplineUvGridScaffoldBuilder();

        foreach (var face in body.Topology.Faces.OrderBy(f => f.Id.Value))
        {
            if (!fallbackByFace.TryGetValue(face.Id, out var existingPatch))
            {
                continue;
            }

            if (!IsWithinBoundedSubset(body, face.Id))
            {
                continue;
            }

            if (!body.TryGetFaceSurfaceGeometry(face.Id, out var surface) || surface?.BSplineSurfaceWithKnots is not BSplineSurfaceWithKnots bspline)
            {
                continue;
            }

            var request = new BsplineUvGridScaffoldBuildRequest(
                effectiveScaffoldOptions.USegments,
                effectiveScaffoldOptions.VSegments,
                TrimMask: null,
                ReferencePositions: existingPatch.Positions,
                ReferenceTriangleCount: existingPatch.TriangleIndices.Count / 3,
                Acceptance: effectiveScaffoldOptions.Acceptance);

            var scaffold = scaffoldBuilder.Build(bspline, request);
            if (scaffold.Acceptance != BsplineUvGridScaffoldAcceptance.Accepted || scaffold.Mesh is null)
            {
                fallbackByFace[face.Id] = existingPatch with
                {
                    Source = DisplayFaceMeshSource.Tessellator,
                    ScaffoldRejectionReason = scaffold.RejectionReason.ToString(),
                };
                continue;
            }

            fallbackByFace[face.Id] = new DisplayFaceMeshPatch(
                face.Id,
                scaffold.Mesh.Positions.ToArray(),
                ComputeVertexNormals(scaffold.Mesh.Positions, scaffold.Mesh.TriangleIndices),
                scaffold.Mesh.TriangleIndices.ToArray(),
                DisplayFaceMeshSource.BsplineUvScaffold,
                null);
        }

        var orderedFaces = body.Topology.Faces
            .OrderBy(f => f.Id.Value)
            .Select(face => fallbackByFace[face.Id])
            .ToArray();

        return KernelResult<DisplayTessellationResult>.Success(
            new DisplayTessellationResult(orderedFaces, tessellation.Value.EdgePolylines),
            tessellation.Diagnostics);
    }

    private static bool IsWithinBoundedSubset(BrepBody body, FaceId faceId)
    {
        if (body.Topology.Faces.Count() != 1)
        {
            return false;
        }

        var loopIds = body.GetLoopIds(faceId);
        return loopIds.Count == 1;
    }

    private static IReadOnlyList<Vector3D> ComputeVertexNormals(IReadOnlyList<Point3D> positions, IReadOnlyList<int> triangleIndices)
    {
        var accumulators = new Vector3D[positions.Count];

        for (var i = 0; i + 2 < triangleIndices.Count; i += 3)
        {
            var i0 = triangleIndices[i];
            var i1 = triangleIndices[i + 1];
            var i2 = triangleIndices[i + 2];

            var p0 = positions[i0];
            var p1 = positions[i1];
            var p2 = positions[i2];
            var faceNormal = (p1 - p0).Cross(p2 - p0);
            if (faceNormal.Length <= 1e-12d)
            {
                continue;
            }

            accumulators[i0] += faceNormal;
            accumulators[i1] += faceNormal;
            accumulators[i2] += faceNormal;
        }

        var normals = new Vector3D[positions.Count];
        for (var i = 0; i < accumulators.Length; i++)
        {
            var candidate = accumulators[i];
            if (!candidate.TryNormalize(out var normalized))
            {
                normalized = new Vector3D(0d, 0d, 1d);
            }

            normals[i] = normalized;
        }

        return normals;
    }
}

internal sealed record DisplayPreparationBsplineScaffoldOptions(
    int USegments,
    int VSegments,
    BsplineUvGridScaffoldAcceptanceThresholds Acceptance)
{
    public static DisplayPreparationBsplineScaffoldOptions ConservativeDefaults { get; } = new(
        12,
        12,
        BsplineUvGridScaffoldAcceptanceThresholds.ConservativeDefaults);
}
