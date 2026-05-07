using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Firmament.Execution;

internal enum TrimCapabilityClassification
{
    ExactSupported,
    SpecialCaseOnly,
    Deferred,
    Unsupported
}

internal sealed record TrimCapabilityResult(
    SurfacePatchFamily FamilyA,
    SurfacePatchFamily FamilyB,
    TrimCapabilityClassification Classification,
    IReadOnlyList<TrimCurveFamily> CurveFamilies,
    bool HasOrientationOrPlacementRestrictions,
    string Reason);

internal static class TrimCapabilityMatrix
{
    internal static TrimCapabilityResult Evaluate(SurfacePatchFamily a, SurfacePatchFamily b)
    {
        var key = Normalize(a, b);
        return key switch
        {
            var k when k == Normalize(SurfacePatchFamily.Planar, SurfacePatchFamily.Planar)
                => new(a, b, TrimCapabilityClassification.ExactSupported, [TrimCurveFamily.Line], false, "Planar/planar trims are exact line intersections."),
            var k when k == Normalize(SurfacePatchFamily.Planar, SurfacePatchFamily.Cylindrical)
                => new(a, b, TrimCapabilityClassification.SpecialCaseOnly, [TrimCurveFamily.Line, TrimCurveFamily.Circle, TrimCurveFamily.Ellipse], true, "Planar/cylindrical intersections vary by orientation and offset."),
            var k when k == Normalize(SurfacePatchFamily.Planar, SurfacePatchFamily.Spherical)
                => new(a, b, TrimCapabilityClassification.ExactSupported, [TrimCurveFamily.Circle], true, "Planar/spherical trims are circles when intersecting."),
            var k when k == Normalize(SurfacePatchFamily.Planar, SurfacePatchFamily.Prismatic)
                => new(a, b, TrimCapabilityClassification.ExactSupported, [TrimCurveFamily.Line, TrimCurveFamily.Polyline], false, "Planar/prismatic seams are linear/polyline exact trims."),
            var k when k == Normalize(SurfacePatchFamily.Planar, SurfacePatchFamily.Conical)
                => new(a, b, TrimCapabilityClassification.Deferred, [TrimCurveFamily.Ellipse, TrimCurveFamily.AlgebraicImplicit], true, "General planar/conical intersections require broader conic policy."),
            var k when k == Normalize(SurfacePatchFamily.Planar, SurfacePatchFamily.Toroidal)
                => new(a, b, TrimCapabilityClassification.Deferred, [TrimCurveFamily.AlgebraicImplicit], true, "General planar/toroidal intersections are quartic/algebraic."),
            var k when k == Normalize(SurfacePatchFamily.Cylindrical, SurfacePatchFamily.Cylindrical)
                => new(a, b, TrimCapabilityClassification.Deferred, [TrimCurveFamily.AlgebraicImplicit], true, "Cylindrical/cylindrical exact support is not centralized yet."),
            var k when k == Normalize(SurfacePatchFamily.Spherical, SurfacePatchFamily.Cylindrical)
                => new(a, b, TrimCapabilityClassification.Deferred, [TrimCurveFamily.AlgebraicImplicit], true, "Spherical/cylindrical support is deferred to broader analytic curve policy."),
            var k when k.Item1 == SurfacePatchFamily.Toroidal || k.Item2 == SurfacePatchFamily.Toroidal
                => new(a, b, TrimCapabilityClassification.Deferred, [TrimCurveFamily.AlgebraicImplicit], true, "Toroidal pairings remain deferred except future explicit special-cases."),
            _ => new(a, b, TrimCapabilityClassification.Unsupported, [TrimCurveFamily.Unsupported], true, "No trim capability entry for the requested source surface family pair.")
        };
    }

    private static (SurfacePatchFamily, SurfacePatchFamily) Normalize(SurfacePatchFamily a, SurfacePatchFamily b)
        => a <= b ? (a, b) : (b, a);
}

internal sealed record SourceSurfaceExtractionDiagnostic(string Code, string Message);

internal sealed record SourceSurfaceExtractionResult(
    bool IsSuccess,
    IReadOnlyList<SourceSurfaceDescriptor> Descriptors,
    IReadOnlyList<SourceSurfaceExtractionDiagnostic> Diagnostics,
    IReadOnlyList<string> UnsupportedNodeReasons);

internal static class SourceSurfaceExtractor
{
    internal static SourceSurfaceExtractionResult Extract(CirNode root, NativeGeometryReplayLog? replayLog = null)
    {
        var descriptors = new List<SourceSurfaceDescriptor>();
        var diagnostics = new List<SourceSurfaceExtractionDiagnostic>();
        var unsupported = new List<string>();
        Visit(root, Transform3D.Identity, descriptors, diagnostics, unsupported, replayLog);
        if (root is CirSubtractNode or CirUnionNode or CirIntersectNode)
        {
            diagnostics.Add(new("retention-deferred", "Boolean source-surface extraction inventories primitive surfaces only; retained/discarded classification is deferred."));
        }

        return new(unsupported.Count == 0, descriptors, diagnostics, unsupported);
    }

    private static void Visit(CirNode node, Transform3D accumulated, List<SourceSurfaceDescriptor> descriptors, List<SourceSurfaceExtractionDiagnostic> diagnostics, List<string> unsupported, NativeGeometryReplayLog? replayLog)
    {
        switch (node)
        {
            case CirTransformNode transformNode:
                Visit(transformNode.Child, Transform3D.Compose(accumulated, transformNode.Transform), descriptors, diagnostics, unsupported, replayLog);
                return;
            case CirBoxNode box:
                AddBoxDescriptors(box, accumulated, descriptors, replayLog, nameof(CirBoxNode));
                return;
            case CirCylinderNode:
                AddCylinderDescriptors(accumulated, descriptors, replayLog, nameof(CirCylinderNode));
                return;
            case CirSphereNode:
                descriptors.Add(CreateDescriptor(SurfacePatchFamily.Spherical, "sphere", null, accumulated, node, replayLog, FacePatchOrientationRole.Forward));
                return;
            case CirTorusNode:
                descriptors.Add(CreateDescriptor(SurfacePatchFamily.Toroidal, "torus", null, accumulated, node, replayLog, FacePatchOrientationRole.Forward));
                diagnostics.Add(new("torus-materialization-deferred", "Toroidal source surface extracted; downstream materialization remains deferred."));
                return;
            case CirSubtractNode subtract:
                Visit(subtract.Left, accumulated, descriptors, diagnostics, unsupported, replayLog);
                Visit(subtract.Right, accumulated, descriptors, diagnostics, unsupported, replayLog);
                return;
            case CirUnionNode union:
                Visit(union.Left, accumulated, descriptors, diagnostics, unsupported, replayLog);
                Visit(union.Right, accumulated, descriptors, diagnostics, unsupported, replayLog);
                return;
            case CirIntersectNode intersect:
                Visit(intersect.Left, accumulated, descriptors, diagnostics, unsupported, replayLog);
                Visit(intersect.Right, accumulated, descriptors, diagnostics, unsupported, replayLog);
                return;
            default:
                unsupported.Add($"Unsupported CIR node kind for source-surface extraction: {node.Kind}.");
                return;
        }
    }

    private static void AddBoxDescriptors(CirBoxNode box, Transform3D transform, List<SourceSurfaceDescriptor> descriptors, NativeGeometryReplayLog? replayLog, string owningKind)
    {
        var hx = box.Width * 0.5d;
        var hy = box.Height * 0.5d;
        var hz = box.Depth * 0.5d;
        descriptors.Add(CreateDescriptor(SurfacePatchFamily.Planar, "top", CreateBoundedPatch(transform, new(-hx, -hy, hz), new(hx, -hy, hz), new(hx, hy, hz), new(-hx, hy, hz)), transform, CirNodeKind.Box, replayLog, FacePatchOrientationRole.Forward, owningKind));
        descriptors.Add(CreateDescriptor(SurfacePatchFamily.Planar, "bottom", CreateBoundedPatch(transform, new(-hx, hy, -hz), new(hx, hy, -hz), new(hx, -hy, -hz), new(-hx, -hy, -hz)), transform, CirNodeKind.Box, replayLog, FacePatchOrientationRole.Forward, owningKind));
        descriptors.Add(CreateDescriptor(SurfacePatchFamily.Planar, "left", CreateBoundedPatch(transform, new(-hx, -hy, -hz), new(-hx, hy, -hz), new(-hx, hy, hz), new(-hx, -hy, hz)), transform, CirNodeKind.Box, replayLog, FacePatchOrientationRole.Forward, owningKind));
        descriptors.Add(CreateDescriptor(SurfacePatchFamily.Planar, "right", CreateBoundedPatch(transform, new(hx, -hy, hz), new(hx, hy, hz), new(hx, hy, -hz), new(hx, -hy, -hz)), transform, CirNodeKind.Box, replayLog, FacePatchOrientationRole.Forward, owningKind));
        descriptors.Add(CreateDescriptor(SurfacePatchFamily.Planar, "front", CreateBoundedPatch(transform, new(-hx, hy, -hz), new(hx, hy, -hz), new(hx, hy, hz), new(-hx, hy, hz)), transform, CirNodeKind.Box, replayLog, FacePatchOrientationRole.Forward, owningKind));
        descriptors.Add(CreateDescriptor(SurfacePatchFamily.Planar, "back", CreateBoundedPatch(transform, new(-hx, -hy, hz), new(hx, -hy, hz), new(hx, -hy, -hz), new(-hx, -hy, -hz)), transform, CirNodeKind.Box, replayLog, FacePatchOrientationRole.Forward, owningKind));
    }

    private static void AddCylinderDescriptors(Transform3D transform, List<SourceSurfaceDescriptor> descriptors, NativeGeometryReplayLog? replayLog, string owningKind)
    {
        descriptors.Add(CreateDescriptor(SurfacePatchFamily.Cylindrical, "side", null, transform, CirNodeKind.Cylinder, replayLog, FacePatchOrientationRole.Forward, owningKind));
        descriptors.Add(CreateDescriptor(SurfacePatchFamily.Planar, "cap-top", null, transform, CirNodeKind.Cylinder, replayLog, FacePatchOrientationRole.Forward, owningKind));
        descriptors.Add(CreateDescriptor(SurfacePatchFamily.Planar, "cap-bottom", null, transform, CirNodeKind.Cylinder, replayLog, FacePatchOrientationRole.Reversed, owningKind));
    }

    
    private static BoundedPlanarPatchGeometry CreateBoundedPatch(Transform3D transform, Point3D c00, Point3D c10, Point3D c11, Point3D c01)
    {
        var w00 = transform.Apply(c00);
        var w10 = transform.Apply(c10);
        var w11 = transform.Apply(c11);
        var w01 = transform.Apply(c01);
        return new(w00, w10, w11, w01, (w10 - w00).Cross(w01 - w00));
    }

    private static SourceSurfaceDescriptor CreateDescriptor(SurfacePatchFamily family, string provenanceRole, BoundedPlanarPatchGeometry? boundedPlanarGeometry, Transform3D transform, CirNodeKind nodeKind, NativeGeometryReplayLog? replayLog, FacePatchOrientationRole orientation, string owningKind)
    {
        var op = replayLog?.Operations.LastOrDefault();
        var placementSuffix = op is null ? "" : $"|placement:{op.ResolvedPlacement.Kind}";
        return new SourceSurfaceDescriptor(family, provenanceRole, boundedPlanarGeometry, transform, $"cir:{nodeKind.ToString().ToLowerInvariant()}:{provenanceRole}{placementSuffix}", owningKind, op?.OpIndex, orientation);
    }

    private static SourceSurfaceDescriptor CreateDescriptor(SurfacePatchFamily family, string provenanceRole, BoundedPlanarPatchGeometry? boundedPlanarGeometry, Transform3D transform, CirNode node, NativeGeometryReplayLog? replayLog, FacePatchOrientationRole orientation, string? owningKind = null)
    {
        var op = replayLog?.Operations.LastOrDefault();
        var placementSuffix = op is null ? "" : $"|placement:{op.ResolvedPlacement.Kind}";
        return new SourceSurfaceDescriptor(family, provenanceRole, boundedPlanarGeometry, transform, $"cir:{node.Kind.ToString().ToLowerInvariant()}:{provenanceRole}{placementSuffix}", owningKind ?? node.GetType().Name, op?.OpIndex, orientation);
    }
}
