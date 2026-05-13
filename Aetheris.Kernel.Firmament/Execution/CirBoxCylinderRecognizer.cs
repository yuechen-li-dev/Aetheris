using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;

namespace Aetheris.Kernel.Firmament.Execution;

internal enum CirBoxCylinderRecognitionReason
{
    Recognized,
    UnsupportedRootNotSubtract,
    UnsupportedLeftNotBox,
    UnsupportedRightNotCylinder,
    UnsupportedTransform,
    InvalidBoxDimensions,
    InvalidCylinderDimensions,
    UnsupportedNotThroughHole,
    UnsupportedTangentOrGrazing,
    UnsupportedCylinderOutsideBox,
    ReplayMismatch,
    UnsupportedNestedOrComposite,
    Unknown
}

internal enum CirBoxCylinderAxisKind
{
    Z
}

internal sealed record CirBoxCylinderRecognizerInput(
    CirNode Root,
    NativeGeometryReplayLog? ReplayLog = null,
    string? SourceLabel = null);

internal sealed record RecognizedBoxCylinder(
    double BoxWidth,
    double BoxHeight,
    double BoxDepth,
    double CylinderRadius,
    double CylinderHeight,
    Vector3D BoxTranslation,
    Vector3D CylinderTranslation,
    CirBoxCylinderAxisKind Axis,
    double ThroughLength,
    double ClearanceXMinus,
    double ClearanceXPlus,
    double ClearanceYMinus,
    double ClearanceYPlus);

internal sealed record CirBoxCylinderRecognitionResult(
    bool Success,
    CirBoxCylinderRecognitionReason Reason,
    string Diagnostic,
    RecognizedBoxCylinder? Value,
    IReadOnlyList<string> Diagnostics);

internal static class CirBoxCylinderRecognizer
{
    internal static CirBoxCylinderRecognitionResult Recognize(CirBoxCylinderRecognizerInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var diagnostics = new List<string>();
        var tol = ToleranceContext.Default.Linear;

        if (input.Root is not CirSubtractNode subtract)
        {
            return Fail(CirBoxCylinderRecognitionReason.UnsupportedRootNotSubtract, "Root must be CirSubtractNode.", diagnostics);
        }

        if (subtract.Left is CirSubtractNode or CirUnionNode or CirIntersectNode || subtract.Right is CirSubtractNode or CirUnionNode or CirIntersectNode)
        {
            return Fail(CirBoxCylinderRecognitionReason.UnsupportedNestedOrComposite, "Nested/composite booleans are unsupported in CIR-STEP-V1.", diagnostics);
        }

        if (!TryUnwrapTranslation(subtract.Left, out var leftNode, out var boxTranslation))
        {
            return Fail(CirBoxCylinderRecognitionReason.UnsupportedTransform, "Subtract left operand has unsupported transform wrapper.", diagnostics);
        }

        if (!TryUnwrapTranslation(subtract.Right, out var rightNode, out var cylinderTranslation))
        {
            return Fail(CirBoxCylinderRecognitionReason.UnsupportedTransform, "Subtract right operand has unsupported transform wrapper.", diagnostics);
        }

        if (leftNode is not CirBoxNode box)
        {
            return Fail(CirBoxCylinderRecognitionReason.UnsupportedLeftNotBox, "Subtract left operand must normalize to CirBoxNode.", diagnostics);
        }

        if (rightNode is not CirCylinderNode cylinder)
        {
            return Fail(CirBoxCylinderRecognitionReason.UnsupportedRightNotCylinder, "Subtract right operand must normalize to CirCylinderNode.", diagnostics);
        }

        if (box.Width <= 0d || box.Height <= 0d || box.Depth <= 0d || !double.IsFinite(box.Width) || !double.IsFinite(box.Height) || !double.IsFinite(box.Depth))
        {
            return Fail(CirBoxCylinderRecognitionReason.InvalidBoxDimensions, "Box dimensions must be finite positive values.", diagnostics);
        }

        if (cylinder.Radius <= 0d || cylinder.Height <= 0d || !double.IsFinite(cylinder.Radius) || !double.IsFinite(cylinder.Height))
        {
            return Fail(CirBoxCylinderRecognitionReason.InvalidCylinderDimensions, "Cylinder dimensions must be finite positive values.", diagnostics);
        }

        var boxMinZ = boxTranslation.Z - (box.Depth * 0.5d);
        var boxMaxZ = boxTranslation.Z + (box.Depth * 0.5d);
        var cylinderMinZ = cylinderTranslation.Z - (cylinder.Height * 0.5d);
        var cylinderMaxZ = cylinderTranslation.Z + (cylinder.Height * 0.5d);
        if (cylinderMinZ > boxMinZ + tol || cylinderMaxZ < boxMaxZ - tol)
        {
            return Fail(CirBoxCylinderRecognitionReason.UnsupportedNotThroughHole, "Cylinder does not span full box depth.", diagnostics);
        }

        var halfW = box.Width * 0.5d;
        var halfH = box.Height * 0.5d;
        var dx = cylinderTranslation.X - boxTranslation.X;
        var dy = cylinderTranslation.Y - boxTranslation.Y;

        var clearanceXMinus = dx + halfW - cylinder.Radius;
        var clearanceXPlus = halfW - dx - cylinder.Radius;
        var clearanceYMinus = dy + halfH - cylinder.Radius;
        var clearanceYPlus = halfH - dy - cylinder.Radius;

        if (clearanceXMinus < -tol || clearanceXPlus < -tol || clearanceYMinus < -tol || clearanceYPlus < -tol)
        {
            return Fail(CirBoxCylinderRecognitionReason.UnsupportedCylinderOutsideBox, "Cylinder footprint extends outside box XY extents.", diagnostics);
        }

        if (clearanceXMinus <= tol || clearanceXPlus <= tol || clearanceYMinus <= tol || clearanceYPlus <= tol)
        {
            return Fail(CirBoxCylinderRecognitionReason.UnsupportedTangentOrGrazing, "Cylinder is tangent/grazing within tolerance and unsupported.", diagnostics);
        }

        EnrichReplayDiagnostics(input.ReplayLog, diagnostics, out var mismatch);

        var value = new RecognizedBoxCylinder(
            box.Width,
            box.Height,
            box.Depth,
            cylinder.Radius,
            cylinder.Height,
            boxTranslation,
            cylinderTranslation,
            CirBoxCylinderAxisKind.Z,
            cylinder.Height + (cylinderTranslation.Z - boxTranslation.Z),
            clearanceXMinus,
            clearanceXPlus,
            clearanceYMinus,
            clearanceYPlus);

        return new(true, mismatch ? CirBoxCylinderRecognitionReason.ReplayMismatch : CirBoxCylinderRecognitionReason.Recognized, mismatch ? "Recognized from CIR geometry with replay mismatch diagnostics." : "Recognized canonical subtract(box,cylinder) through-hole.", value, diagnostics);
    }

    private static void EnrichReplayDiagnostics(NativeGeometryReplayLog? replayLog, List<string> diagnostics, out bool mismatch)
    {
        mismatch = false;
        if (replayLog is null)
        {
            diagnostics.Add("Replay log unavailable; geometry-only recognition executed.");
            return;
        }

        var subtractOps = replayLog.Operations.Where(op => string.Equals(op.OperationKind, "boolean:subtract", StringComparison.Ordinal)).ToArray();
        if (subtractOps.Length == 0)
        {
            diagnostics.Add("Replay log contains no boolean:subtract operation.");
            mismatch = true;
            return;
        }

        var cylinderSubtract = subtractOps.FirstOrDefault(op => string.Equals(op.ToolKind, "cylinder", StringComparison.Ordinal));
        if (cylinderSubtract is null)
        {
            diagnostics.Add("Replay log subtract operations did not identify a cylinder tool kind.");
            mismatch = true;
            return;
        }

        diagnostics.Add($"Replay context: opIndex={cylinderSubtract.OpIndex}, feature='{cylinderSubtract.FeatureId}', source='{cylinderSubtract.SourceFeatureId}', toolKind='{cylinderSubtract.ToolKind}'.");
    }

    private static bool TryUnwrapTranslation(CirNode node, out CirNode unwrapped, out Vector3D translation)
    {
        unwrapped = node;
        translation = Vector3D.Zero;
        while (unwrapped is CirTransformNode transformNode)
        {
            if (!TryExtractPureTranslation(transformNode.Transform, out var step))
            {
                unwrapped = node;
                translation = Vector3D.Zero;
                return false;
            }

            translation += step;
            unwrapped = transformNode.Child;
        }

        return true;
    }

    private static bool TryExtractPureTranslation(Transform3D transform, out Vector3D translation)
    {
        var origin = transform.Apply(Point3D.Origin);
        var x = transform.Apply(new Point3D(1d, 0d, 0d));
        var y = transform.Apply(new Point3D(0d, 1d, 0d));
        var z = transform.Apply(new Point3D(0d, 0d, 1d));
        const double eps = 1e-9d;
        if (!NearlyEqual(x - origin, new Vector3D(1d, 0d, 0d), eps)
            || !NearlyEqual(y - origin, new Vector3D(0d, 1d, 0d), eps)
            || !NearlyEqual(z - origin, new Vector3D(0d, 0d, 1d), eps))
        {
            translation = Vector3D.Zero;
            return false;
        }

        translation = origin - Point3D.Origin;
        return true;
    }

    private static bool NearlyEqual(Vector3D a, Vector3D b, double eps)
        => Math.Abs(a.X - b.X) <= eps && Math.Abs(a.Y - b.Y) <= eps && Math.Abs(a.Z - b.Z) <= eps;

    private static CirBoxCylinderRecognitionResult Fail(CirBoxCylinderRecognitionReason reason, string diagnostic, List<string> diagnostics)
        => new(false, reason, diagnostic, null, diagnostics);
}
