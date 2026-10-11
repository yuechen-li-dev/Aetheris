using System.Collections.Immutable;

namespace Aetheris.Cloth3D;

internal static class ClothMaterialRegions3D
{
    public static int[] Validate(ClothDefinition3D definition)
    {
        if (definition.MaterialRegions.IsDefault || definition.MaterialRegions.Length > 256)
        {
            throw new ArgumentException("cloth-material-regions: Supply at most 256 explicit regions.");
        }
        int[] owners = Enumerable.Repeat(-1, definition.Positions.Length).ToArray();
        var identities = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < definition.MaterialRegions.Length; index++)
        {
            var region = definition.MaterialRegions[index];
            if (region is null || string.IsNullOrWhiteSpace(region.Id) || !identities.Add(region.Id)
                || region.Vertices.IsDefaultOrEmpty
                || !ValidScale(region.StretchComplianceScale) || !ValidScale(region.BendComplianceScale))
            {
                throw new ArgumentException("cloth-material-region: Require unique names, vertices and finite scales in (0, 1000].");
            }
            foreach (int vertex in region.Vertices)
            {
                if (vertex < 0 || vertex >= owners.Length || owners[vertex] != -1)
                {
                    throw new ArgumentException("cloth-material-region-overlap: Vertices must be valid, unique and disjoint across regions.");
                }
                owners[vertex] = index;
            }
        }
        return owners;
    }

    public static void Apply(ImmutableArray<ClothMaterialRegion3D> regions, int[] owners,
        List<ClothConstraint3D> constraints)
    {
        for (int index = 0; index < constraints.Count; index++)
        {
            var constraint = constraints[index];
            if (constraint.Kind == ClothConstraintKind3D.Stitch)
            {
                continue;
            }
            int owner = owners[constraint.Vertices[0]];
            if (owner < 0 || constraint.Vertices.Any(vertex => owners[vertex] != owner))
            {
                continue;
            }
            var region = regions[owner];
            bool bend = constraint.Kind is ClothConstraintKind3D.FlatBend
                or ClothConstraintKind3D.VertexBend or ClothConstraintKind3D.SurfaceBend;
            float scale = bend ? region.BendComplianceScale : region.StretchComplianceScale;
            float compliance = constraint.Compliance * scale;
            if (!float.IsFinite(compliance))
            {
                throw new ArgumentException("cloth-material-region-overflow: Scaled compliance must remain finite.");
            }
            constraints[index] = constraint with { Compliance = compliance };
        }
    }

    private static bool ValidScale(float value)
    {
        return float.IsFinite(value) && value > 0 && value <= 1000;
    }
}
