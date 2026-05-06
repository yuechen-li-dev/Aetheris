using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;

namespace Aetheris.Kernel.Firmament.Execution;

internal static class CirBrepMaterializer
{
    internal const string BoxMinusCylinderPattern = "subtract(box,cylinder)";

    internal static CirBrepMaterializationResult TryMaterialize(CirNode root)
    {
        ArgumentNullException.ThrowIfNull(root);

        if (root is not CirSubtractNode subtract)
        {
            return Unsupported("Root CIR node must be subtract.", "root-not-subtract");
        }

        if (!TryUnwrapTranslation(subtract.Left, out var boxNode, out var boxTranslation) || boxNode is not CirBoxNode box)
        {
            return Unsupported("Subtract lhs must be a translated/untranslated box node.", "lhs-not-box");
        }

        if (!TryUnwrapTranslation(subtract.Right, out var cylinderNode, out var cylinderTranslation) || cylinderNode is not CirCylinderNode cylinder)
        {
            return Unsupported("Subtract rhs must be a translated/untranslated cylinder node.", "rhs-not-cylinder");
        }

        var boxResult = BrepPrimitives.CreateBox(box.Width, box.Height, box.Depth);
        if (!boxResult.IsSuccess)
        {
            return Failed("Failed to create BRep box primitive.", boxResult.Diagnostics);
        }

        var cylinderResult = BrepPrimitives.CreateCylinder(cylinder.Radius, cylinder.Height);
        if (!cylinderResult.IsSuccess)
        {
            return Failed("Failed to create BRep cylinder primitive.", cylinderResult.Diagnostics);
        }

        var placedBox = TranslateBody(boxResult.Value, boxTranslation);
        var placedCylinder = TranslateBody(cylinderResult.Value, cylinderTranslation);
        var subtractResult = BrepBoolean.Subtract(placedBox, placedCylinder);
        if (!subtractResult.IsSuccess)
        {
            return Failed("Failed to boolean subtract box/cylinder during CIR rematerialization.", subtractResult.Diagnostics);
        }

        return new CirBrepMaterializationResult(true, subtractResult.Value, BoxMinusCylinderPattern, null, [], "matched-box-minus-cylinder");
    }

    private static bool TryUnwrapTranslation(CirNode node, out CirNode unwrapped, out Vector3D translation)
    {
        if (node is CirTransformNode transformNode)
        {
            if (!TryExtractPureTranslation(transformNode.Transform, out translation))
            {
                unwrapped = node;
                return false;
            }

            unwrapped = transformNode.Child;
            return true;
        }

        translation = Vector3D.Zero;
        unwrapped = node;
        return true;
    }

    private static bool TryExtractPureTranslation(Transform3D transform, out Vector3D translation)
    {
        var origin = transform.Apply(Point3D.Origin);
        var x = transform.Apply(new Point3D(1d, 0d, 0d));
        var y = transform.Apply(new Point3D(0d, 1d, 0d));
        var z = transform.Apply(new Point3D(0d, 0d, 1d));

        var eps = 1e-9d;
        var xDelta = x - origin;
        var yDelta = y - origin;
        var zDelta = z - origin;
        var isIdentityBasis = NearlyEqual(xDelta, new Vector3D(1d, 0d, 0d), eps)
            && NearlyEqual(yDelta, new Vector3D(0d, 1d, 0d), eps)
            && NearlyEqual(zDelta, new Vector3D(0d, 0d, 1d), eps);

        if (!isIdentityBasis)
        {
            translation = Vector3D.Zero;
            return false;
        }

        translation = origin - Point3D.Origin;
        return true;
    }

    private static bool NearlyEqual(Vector3D left, Vector3D right, double eps)
        => double.Abs(left.X - right.X) <= eps
           && double.Abs(left.Y - right.Y) <= eps
           && double.Abs(left.Z - right.Z) <= eps;

    private static BrepBody TranslateBody(BrepBody body, Vector3D translation)
    {
        if (translation == Vector3D.Zero)
        {
            return body;
        }

        return FirmamentPrimitiveExecutionTranslation.TranslateBody(body, translation);
    }

    private static CirBrepMaterializationResult Unsupported(string diagnostic, string reason)
        => new(false, null, BoxMinusCylinderPattern, reason, [], diagnostic);

    private static CirBrepMaterializationResult Failed(string diagnostic, IReadOnlyList<Core.Diagnostics.KernelDiagnostic> diagnostics)
        => new(false, null, BoxMinusCylinderPattern, "materialize-failed", diagnostics, diagnostic);
}

internal sealed record CirBrepMaterializationResult(
    bool IsSuccess,
    BrepBody? Body,
    string PatternName,
    string? UnsupportedReason,
    IReadOnlyList<Core.Diagnostics.KernelDiagnostic> Diagnostics,
    string Message);
