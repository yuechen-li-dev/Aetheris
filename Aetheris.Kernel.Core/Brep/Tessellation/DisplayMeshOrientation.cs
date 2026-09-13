namespace Aetheris.Kernel.Core.Brep.Tessellation;

/// <summary>Projects the exact face sense into render normals and front-face triangle winding.</summary>
public static class DisplayMeshOrientation
{
    public static DisplayFaceMeshPatch Orient(BrepBody body, DisplayFaceMeshPatch patch)
    {
        if (patch.Normals.Count != patch.Positions.Count || patch.TriangleIndices.Count % 3 != 0
            || patch.TriangleIndices.Any(i => i < 0 || i >= patch.Positions.Count))
            throw new InvalidOperationException($"display-mesh-invalid-patch:{patch.FaceId}");
        if (!body.Bindings.TryGetFaceBinding(patch.FaceId, out var binding))
            throw new InvalidOperationException($"display-mesh-missing-face-binding:{patch.FaceId}");
        var normals = patch.Normals.Select(normal => binding.SameSense ? normal : -normal).ToArray();
        var indices = patch.TriangleIndices.ToArray();
        for (var i = 0; i < indices.Length; i += 3)
        {
            var a = indices[i]; var b = indices[i + 1]; var c = indices[i + 2];
            var cross = (patch.Positions[b] - patch.Positions[a]).Cross(patch.Positions[c] - patch.Positions[a]);
            if (cross.Dot(normals[a] + normals[b] + normals[c]) < 0)
                (indices[i + 1], indices[i + 2]) = (indices[i + 2], indices[i + 1]);
        }
        return patch with { Normals = normals, TriangleIndices = indices };
    }
}
