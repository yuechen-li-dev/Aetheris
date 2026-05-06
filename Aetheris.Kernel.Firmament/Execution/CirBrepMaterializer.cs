using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;

namespace Aetheris.Kernel.Firmament.Execution;

internal static class CirBrepMaterializer
{
    internal const string BoxMinusCylinderPattern = "subtract(box,cylinder)";
    internal const string BoxMinusBoxPattern = "subtract(box,box)";

    internal static CirBrepMaterializationResult TryMaterialize(CirNode root)
    {
        ArgumentNullException.ThrowIfNull(root);

        if (root is not CirSubtractNode subtract)
        {
            return Unsupported(BoxMinusCylinderPattern, "Root CIR node must be subtract.", "root-not-subtract");
        }

        if (!TryUnwrapTranslation(subtract.Left, out var leftNode, out var leftTranslation) || leftNode is not CirBoxNode leftBox)
        {
            return Unsupported(BoxMinusCylinderPattern, "Subtract lhs must be a translated/untranslated box node.", "lhs-not-box");
        }

        return subtract.Right switch
        {
            CirCylinderNode cylinder => TryMaterializeBoxMinusCylinder(leftBox, leftTranslation, cylinder, Vector3D.Zero),
            CirTransformNode transformedCylinder when TryUnwrapTranslation(transformedCylinder, out var cylinderNode, out var cylinderTranslation) && cylinderNode is CirCylinderNode cylinder
                => TryMaterializeBoxMinusCylinder(leftBox, leftTranslation, cylinder, cylinderTranslation),
            CirBoxNode rightBox => TryMaterializeBoxMinusBox(leftBox, leftTranslation, rightBox, Vector3D.Zero),
            CirTransformNode transformedBox when TryUnwrapTranslation(transformedBox, out var rightNode, out var rightTranslation) && rightNode is CirBoxNode rightBox
                => TryMaterializeBoxMinusBox(leftBox, leftTranslation, rightBox, rightTranslation),
            _ => Unsupported(BoxMinusCylinderPattern, "Subtract rhs must be a translated/untranslated cylinder or box node.", "rhs-unsupported")
        };
    }

    private static CirBrepMaterializationResult TryMaterializeBoxMinusCylinder(CirBoxNode box, Vector3D boxTranslation, CirCylinderNode cylinder, Vector3D cylinderTranslation)
    {
        var boxResult = BrepPrimitives.CreateBox(box.Width, box.Height, box.Depth);
        if (!boxResult.IsSuccess)
        {
            return Failed(BoxMinusCylinderPattern, "Failed to create BRep box primitive.", boxResult.Diagnostics);
        }

        var cylinderResult = BrepPrimitives.CreateCylinder(cylinder.Radius, cylinder.Height);
        if (!cylinderResult.IsSuccess)
        {
            return Failed(BoxMinusCylinderPattern, "Failed to create BRep cylinder primitive.", cylinderResult.Diagnostics);
        }

        var placedBox = TranslateBody(boxResult.Value, boxTranslation);
        var placedCylinder = TranslateBody(cylinderResult.Value, cylinderTranslation);
        var subtractResult = BrepBoolean.Subtract(placedBox, placedCylinder);
        if (!subtractResult.IsSuccess)
        {
            return Failed(BoxMinusCylinderPattern, "Failed to boolean subtract box/cylinder during CIR rematerialization.", subtractResult.Diagnostics);
        }

        return new CirBrepMaterializationResult(true, subtractResult.Value, BoxMinusCylinderPattern, null, [], "matched-box-minus-cylinder");
    }

    private static CirBrepMaterializationResult TryMaterializeBoxMinusBox(CirBoxNode leftBox, Vector3D leftTranslation, CirBoxNode rightBox, Vector3D rightTranslation)
    {
        var leftBoxResult = BrepPrimitives.CreateBox(leftBox.Width, leftBox.Height, leftBox.Depth);
        if (!leftBoxResult.IsSuccess)
        {
            return Failed(BoxMinusBoxPattern, "Failed to create lhs BRep box primitive.", leftBoxResult.Diagnostics);
        }

        var rightBoxResult = BrepPrimitives.CreateBox(rightBox.Width, rightBox.Height, rightBox.Depth);
        if (!rightBoxResult.IsSuccess)
        {
            return Failed(BoxMinusBoxPattern, "Failed to create rhs BRep box primitive.", rightBoxResult.Diagnostics);
        }

        var placedLeft = TranslateBody(leftBoxResult.Value, leftTranslation);
        var placedRight = TranslateBody(rightBoxResult.Value, rightTranslation);
        var subtractResult = BrepBoolean.Subtract(placedLeft, placedRight);
        if (!subtractResult.IsSuccess)
        {
            return Failed(BoxMinusBoxPattern, "Failed to boolean subtract box/box during CIR rematerialization.", subtractResult.Diagnostics);
        }

        return new CirBrepMaterializationResult(true, subtractResult.Value, BoxMinusBoxPattern, null, [], "matched-box-minus-box");
    }

    private static bool TryUnwrapTranslation(CirNode node, out CirNode unwrapped, out Vector3D translation)
    {
        var total = Vector3D.Zero;
        var current = node;

        while (current is CirTransformNode transformNode)
        {
            if (!TryExtractPureTranslation(transformNode.Transform, out var localTranslation))
            {
                unwrapped = node;
                translation = Vector3D.Zero;
                return false;
            }

            total += localTranslation;
            current = transformNode.Child;
        }

        translation = total;
        unwrapped = current;
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

    private static CirBrepMaterializationResult Unsupported(string pattern, string diagnostic, string reason)
        => new(false, null, pattern, reason, [], diagnostic);

    private static CirBrepMaterializationResult Failed(string pattern, string diagnostic, IReadOnlyList<Core.Diagnostics.KernelDiagnostic> diagnostics)
        => new(false, null, pattern, "materialize-failed", diagnostics, diagnostic);
}

internal sealed record CirBrepMaterializationResult(
    bool IsSuccess,
    BrepBody? Body,
    string PatternName,
    string? UnsupportedReason,
    IReadOnlyList<Core.Diagnostics.KernelDiagnostic> Diagnostics,
    string Message);
