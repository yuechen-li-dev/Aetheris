using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;

namespace Aetheris.FrictionLab;

public static class CirBoxCylinderRecognitionLab
{
    public static CirBoxCylinderRecognitionLabResult Recognize(CirNode root, bool allowTranslationWrappers = true, double? linearTolerance = null)
    {
        var tol = linearTolerance ?? ToleranceContext.Default.Linear;
        if (!TryUnwrap(root, allowTranslationWrappers, out var subtractNode, out _, out var reason))
        {
            return Fail(reason, "Expected root subtract of box and cylinder.");
        }
        if (subtractNode is not CirSubtractNode subtract)
        {
            return Fail(CirLabRecognitionReason.RootNotSubtract, "Expected root subtract of box and cylinder.");
        }

        if (!TryUnwrap(subtract.Left, allowTranslationWrappers, out var leftNode, out var leftTranslation, out reason))
        {
            return Fail(reason, "Subtract lhs transform normalization failed.");
        }

        if (leftNode is not CirBoxNode box)
        {
            return Fail(CirLabRecognitionReason.BaseNotBox, "Subtract lhs must normalize to CirBoxNode.");
        }

        if (!TryUnwrap(subtract.Right, allowTranslationWrappers, out var rightNode, out var rightTranslation, out reason))
        {
            return Fail(reason, "Subtract rhs transform normalization failed.");
        }

        if (rightNode is not CirCylinderNode cylinder)
        {
            return Fail(CirLabRecognitionReason.ToolNotCylinder, "Subtract rhs must normalize to CirCylinderNode.");
        }

        if (box.Width <= 0d || box.Height <= 0d || box.Depth <= 0d || cylinder.Radius <= 0d || cylinder.Height <= 0d)
        {
            return Fail(CirLabRecognitionReason.InvalidDimensions, "All box/cylinder dimensions must be positive.");
        }

        var axis = "Z"; // native CirCylinderNode axis convention
        var dz = rightTranslation.Z - leftTranslation.Z;
        var through = cylinder.Height + (2d * tol) >= box.Depth &&
                      (rightTranslation.Z - (cylinder.Height * 0.5d)) <= (-box.Depth * 0.5d + leftTranslation.Z + tol) &&
                      (rightTranslation.Z + (cylinder.Height * 0.5d)) >= (box.Depth * 0.5d + leftTranslation.Z - tol);
        if (!through)
        {
            return Fail(CirLabRecognitionReason.NotThrough, "Cylinder does not span full box depth.");
        }

        var dx = Math.Abs(rightTranslation.X - leftTranslation.X);
        var dy = Math.Abs(rightTranslation.Y - leftTranslation.Y);
        var maxX = (box.Width * 0.5d) - cylinder.Radius;
        var maxY = (box.Height * 0.5d) - cylinder.Radius;
        if (dx >= maxX - tol || dy >= maxY - tol)
        {
            return Fail(CirLabRecognitionReason.TangentOrOutside, "Cylinder is tangent/grazing/outside XY clearance envelope.");
        }

        return new(true, CirLabRecognitionReason.None, "Recognized canonical box-cylinder through-hole subtract.", box, leftTranslation, cylinder, rightTranslation, axis, dz + cylinder.Height, new CirSubtractNode(new CirTransformNode(box, Transform3D.CreateTranslation(leftTranslation)), new CirTransformNode(cylinder, Transform3D.CreateTranslation(rightTranslation))));
    }

    private static bool TryUnwrap(CirNode node, bool allowTranslationWrappers, out CirNode unwrapped, out Vector3D translation, out CirLabRecognitionReason reason)
    {
        unwrapped = node;
        translation = Vector3D.Zero;
        reason = CirLabRecognitionReason.None;

        while (unwrapped is CirTransformNode t)
        {
            if (!allowTranslationWrappers || !TryExtractPureTranslation(t.Transform, out var step))
            {
                reason = CirLabRecognitionReason.UnsupportedTransform;
                return false;
            }

            translation += step;
            unwrapped = t.Child;
        }

        if (unwrapped is CirSubtractNode s)
        {
            reason = CirLabRecognitionReason.None;
            unwrapped = s;
            return true;
        }

        return true;
    }

    public static bool TryExtractPureTranslation(Transform3D transform, out Vector3D translation)
    {
        var o = transform.Apply(Point3D.Origin);
        var x = transform.Apply(new Point3D(1d, 0d, 0d));
        var y = transform.Apply(new Point3D(0d, 1d, 0d));
        var z = transform.Apply(new Point3D(0d, 0d, 1d));
        var eps = 1e-9d;
        if (!NearlyEqual(x - o, new Vector3D(1d, 0d, 0d), eps)
            || !NearlyEqual(y - o, new Vector3D(0d, 1d, 0d), eps)
            || !NearlyEqual(z - o, new Vector3D(0d, 0d, 1d), eps))
        {
            translation = Vector3D.Zero;
            return false;
        }

        translation = o - Point3D.Origin;
        return true;
    }

    private static bool NearlyEqual(Vector3D a, Vector3D b, double eps)
        => Math.Abs(a.X - b.X) <= eps && Math.Abs(a.Y - b.Y) <= eps && Math.Abs(a.Z - b.Z) <= eps;

    private static CirBoxCylinderRecognitionLabResult Fail(CirLabRecognitionReason reason, string diagnostic)
        => new(false, reason, diagnostic, null, Vector3D.Zero, null, Vector3D.Zero, null, 0d, null);
}
