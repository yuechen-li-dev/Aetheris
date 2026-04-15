namespace Aetheris.Kernel.Core.Step242;

internal enum Step242RigidRootClassificationKind
{
    MissingRigidRoot = 0,
    PartLikeSingleRigidRoot = 1,
    AssemblyLikeMultipleRigidRoots = 2
}

internal sealed record Step242RigidRootClassification(
    Step242RigidRootClassificationKind Kind,
    IReadOnlyList<Step242ParsedEntity> RigidRoots)
{
    public Step242ParsedEntity SingleRigidRoot => RigidRoots[0];
}

internal static class Step242RigidRootClassifier
{
    internal static Step242RigidRootClassification Classify(Step242ParsedDocument document)
    {
        var manifoldSolidRoots = document.Entities
            .Where(e => string.Equals(e.Name, "MANIFOLD_SOLID_BREP", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var kind = manifoldSolidRoots.Count switch
        {
            0 => Step242RigidRootClassificationKind.MissingRigidRoot,
            1 => Step242RigidRootClassificationKind.PartLikeSingleRigidRoot,
            _ => Step242RigidRootClassificationKind.AssemblyLikeMultipleRigidRoots
        };

        return new Step242RigidRootClassification(kind, manifoldSolidRoots);
    }
}
