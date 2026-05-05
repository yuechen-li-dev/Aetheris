using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Firmament.ParsedModel;
using Aetheris.Kernel.Firmament.Execution;

namespace Aetheris.Kernel.Firmament.Lowering;

public sealed record CirLoweringDiagnostic(int OpIndex, string FeatureId, string Message);
public sealed record CirLoweringResult(CirNode Root, IReadOnlyList<CirLoweringDiagnostic> Diagnostics);

internal static class FirmamentCirLowerer
{
    public static KernelResult<CirLoweringResult> Lower(FirmamentPrimitiveLoweringPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var nodesByFeature = new Dictionary<string, CirNode>(StringComparer.Ordinal);
        var diagnostics = new List<CirLoweringDiagnostic>();

        foreach (var primitive in plan.Primitives.OrderBy(p => p.OpIndex))
        {
            var node = LowerPrimitive(primitive, diagnostics);
            if (node is null)
            {
                continue;
            }

            nodesByFeature[primitive.FeatureId] = ApplyPlacement(node, primitive.Placement, primitive.OpIndex, primitive.FeatureId, nodesByFeature, diagnostics);
        }

        foreach (var boolean in plan.Booleans.OrderBy(b => b.OpIndex))
        {
            if (boolean.Kind is not (FirmamentLoweredBooleanKind.Add or FirmamentLoweredBooleanKind.Subtract or FirmamentLoweredBooleanKind.Intersect))
            {
                diagnostics.Add(new(boolean.OpIndex, boolean.FeatureId, $"Unsupported boolean op for CIR-M1: {boolean.Kind}."));
                continue;
            }

            if (!nodesByFeature.TryGetValue(boolean.PrimaryReferenceFeatureId, out var lhs))
            {
                diagnostics.Add(new(boolean.OpIndex, boolean.FeatureId, $"Missing primary reference '{boolean.PrimaryReferenceFeatureId}'."));
                continue;
            }

            var rhs = LowerTool(boolean, diagnostics);
            if (rhs is null)
            {
                continue;
            }

            rhs = ApplyDefaultToolLocalFrame(boolean.Tool, rhs);
            rhs = ApplyPlacement(rhs, boolean.Placement, boolean.OpIndex, boolean.FeatureId, nodesByFeature, diagnostics);
            var composed = boolean.Kind switch
            {
                FirmamentLoweredBooleanKind.Add => new CirUnionNode(lhs, rhs),
                FirmamentLoweredBooleanKind.Subtract => new CirSubtractNode(lhs, rhs),
                FirmamentLoweredBooleanKind.Intersect => new CirIntersectNode(lhs, rhs),
                _ => lhs
            };

            nodesByFeature[boolean.FeatureId] = composed;
        }

        if (diagnostics.Count > 0)
        {
            return KernelResult<CirLoweringResult>.Failure(diagnostics.Select(d => new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, d.Message, d.FeatureId)).ToList());
        }

        if (plan.Booleans.Count > 0)
        {
            var lastBoolean = plan.Booleans.OrderBy(b => b.OpIndex).Last();
            if (!nodesByFeature.TryGetValue(lastBoolean.FeatureId, out var rootFromBoolean))
            {
                return KernelResult<CirLoweringResult>.Failure([new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, "Unable to resolve final boolean root.", "Firmament.CirLowering")]);
            }

            return KernelResult<CirLoweringResult>.Success(new CirLoweringResult(rootFromBoolean, diagnostics));
        }

        var finalPrimitive = plan.Primitives.OrderBy(p => p.OpIndex).LastOrDefault();
        if (finalPrimitive is null || !nodesByFeature.TryGetValue(finalPrimitive.FeatureId, out var root))
        {
            return KernelResult<CirLoweringResult>.Failure([new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, "No CIR-lowerable root found.", "Firmament.CirLowering")]);
        }

        return KernelResult<CirLoweringResult>.Success(new CirLoweringResult(root, diagnostics));
    }

    private static CirNode? LowerTool(FirmamentLoweredBoolean boolean, List<CirLoweringDiagnostic> diagnostics)
    {
        if (!boolean.Tool.RawFields.TryGetValue("op", out var op))
        {
            diagnostics.Add(new(boolean.OpIndex, boolean.FeatureId, "Boolean tool missing op."));
            return null;
        }

        return op switch
        {
            "box" when boolean.Tool.RawFields.TryGetValue("size", out var size) => BuildBox(size),
            "cylinder" when boolean.Tool.RawFields.TryGetValue("radius", out var radius) && boolean.Tool.RawFields.TryGetValue("height", out var height)
                => new CirCylinderNode(double.Parse(radius), double.Parse(height)),
            "sphere" when boolean.Tool.RawFields.TryGetValue("radius", out var sr) => new CirSphereNode(double.Parse(sr)),
            _ => UnsupportedTool(boolean, diagnostics)
        };
    }

    private static CirNode? UnsupportedTool(FirmamentLoweredBoolean boolean, List<CirLoweringDiagnostic> diagnostics)
    {
        diagnostics.Add(new(boolean.OpIndex, boolean.FeatureId, $"Unsupported boolean tool op for CIR-M1: {boolean.Tool.OpName}."));
        return null;
    }

    private static CirNode? LowerPrimitive(FirmamentLoweredPrimitive primitive, List<CirLoweringDiagnostic> diagnostics) => primitive switch
    {
        { Kind: FirmamentLoweredPrimitiveKind.Box, Parameters: FirmamentLoweredBoxParameters box } => new CirBoxNode(box.SizeX, box.SizeY, box.SizeZ),
        { Kind: FirmamentLoweredPrimitiveKind.Cylinder, Parameters: FirmamentLoweredCylinderParameters cylinder } => new CirCylinderNode(cylinder.Radius, cylinder.Height),
        { Kind: FirmamentLoweredPrimitiveKind.Sphere, Parameters: FirmamentLoweredSphereParameters sphere } => new CirSphereNode(sphere.Radius),
        _ => UnsupportedPrimitive(primitive, diagnostics)
    };

    private static CirNode? UnsupportedPrimitive(FirmamentLoweredPrimitive primitive, List<CirLoweringDiagnostic> diagnostics)
    {
        diagnostics.Add(new(primitive.OpIndex, primitive.FeatureId, $"Unsupported primitive for CIR-M1: {primitive.Kind}."));
        return null;
    }

    private static CirNode BuildBox(string sizeRaw)
    {
        var parts = sizeRaw.Trim('[', ']').Split(',').Select(p => double.Parse(p.Trim())).ToArray();
        return new CirBoxNode(parts[0], parts[1], parts[2]);
    }

    private static CirNode ApplyDefaultToolLocalFrame(FirmamentLoweredToolOp tool, CirNode node)
    {
        var zShift = ResolveDefaultToolFrameZShift(tool);
        if (!zShift.HasValue || Math.Abs(zShift.Value) <= 1e-12d)
        {
            return node;
        }

        return new CirTransformNode(node, Transform3D.CreateTranslation(new Vector3D(0d, 0d, zShift.Value)));
    }

    private static double? ResolveDefaultToolFrameZShift(FirmamentLoweredToolOp tool)
    {
        if (string.Equals(tool.OpName, "box", StringComparison.Ordinal)
            && tool.RawFields.TryGetValue("size", out var boxSizeRaw)
            && !string.IsNullOrWhiteSpace(boxSizeRaw))
        {
            var box = FirmamentPrimitiveToolParsing.ParseBox(boxSizeRaw);
            return box.SizeZ * 0.5d;
        }

        if (string.Equals(tool.OpName, "cylinder", StringComparison.Ordinal)
            && tool.RawFields.TryGetValue("height", out var cylinderHeightRaw)
            && !string.IsNullOrWhiteSpace(cylinderHeightRaw))
        {
            return FirmamentPrimitiveToolParsing.ParseScalar(cylinderHeightRaw) * 0.5d;
        }

        if (string.Equals(tool.OpName, "cone", StringComparison.Ordinal)
            && tool.RawFields.TryGetValue("height", out var coneHeightRaw)
            && !string.IsNullOrWhiteSpace(coneHeightRaw))
        {
            return FirmamentPrimitiveToolParsing.ParseScalar(coneHeightRaw) * 0.5d;
        }

        return null;
    }

    private static CirNode ApplyPlacement(CirNode node, FirmamentLoweredPlacement? placement, int opIndex, string featureId, IReadOnlyDictionary<string, CirNode> loweredFeatures, List<CirLoweringDiagnostic> diagnostics)
    {
        if (placement is null)
        {
            return node;
        }

        if (placement.Offset.Count != 0 && placement.Offset.Count != 3)
        {
            diagnostics.Add(new(opIndex, featureId, "Placement offset must have exactly 3 components."));
            return node;
        }

        var anchor = ResolvePlacementAnchor(placement, loweredFeatures, opIndex, featureId, diagnostics);
        if (anchor is null)
        {
            return node;
        }

        var offset = placement.Offset.Count == 3 ? new Vector3D(placement.Offset[0], placement.Offset[1], placement.Offset[2]) : Vector3D.Zero;
        var translation = (anchor.Value - Point3D.Origin) + offset;
        if (translation.LengthSquared <= 1e-24d)
        {
            return node;
        }

        return new CirTransformNode(node, Transform3D.CreateTranslation(translation));
    }


    private static Point3D? ResolvePlacementAnchor(FirmamentLoweredPlacement placement, IReadOnlyDictionary<string, CirNode> loweredFeatures, int opIndex, string featureId, List<CirLoweringDiagnostic> diagnostics)
    {
        if (placement.On is null || placement.On is FirmamentLoweredPlacementOriginAnchor)
        {
            return Point3D.Origin;
        }

        if (placement.AroundAxis is not null || placement.RadialOffset is not null || placement.AngleDegrees is not null)
        {
            diagnostics.Add(new(opIndex, featureId, "CIR-M2 does not support around-axis placement semantics."));
            return null;
        }

        if (placement.On is not FirmamentLoweredPlacementSelectorAnchor selectorAnchor)
        {
            diagnostics.Add(new(opIndex, featureId, "Unsupported placement anchor shape for CIR-M2."));
            return null;
        }

        var selector = selectorAnchor.Selector;
        var split = selector.IndexOf('.', StringComparison.Ordinal);
        if (split <= 0 || split >= selector.Length - 1)
        {
            diagnostics.Add(new(opIndex, featureId, $"Placement selector '{selector}' is not selector-shaped."));
            return null;
        }

        var referencedFeature = selector[..split];
        var port = selector[(split + 1)..];
        if (!loweredFeatures.TryGetValue(referencedFeature, out var referencedNode))
        {
            diagnostics.Add(new(opIndex, featureId, $"Placement selector '{selector}' references unknown feature '{referencedFeature}' for CIR lowering."));
            return null;
        }

        if (!FirmamentPlacementAnchorSemantics.TryResolveAuthoredFaceAnchorFromBounds(port, referencedNode.Bounds.Min, referencedNode.Bounds.Max, out var anchor))
        {
            diagnostics.Add(new(opIndex, featureId, $"CIR-M3 supports only authored '.top_face' and '.bottom_face' placement selectors; got '{selector}'."));
            return null;
        }

        return anchor;
    }

}
