using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Firmament.ParsedModel;

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

            nodesByFeature[primitive.FeatureId] = ApplyPlacement(node, primitive.Placement, primitive.OpIndex, primitive.FeatureId, diagnostics);
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

            rhs = ApplyPlacement(rhs, boolean.Placement, boolean.OpIndex, boolean.FeatureId, diagnostics);
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

    private static CirNode ApplyPlacement(CirNode node, FirmamentLoweredPlacement? placement, int opIndex, string featureId, List<CirLoweringDiagnostic> diagnostics)
    {
        if (placement is null || placement.Offset.Count == 0)
        {
            return node;
        }

        if (placement.On is not null and not FirmamentLoweredPlacementOriginAnchor)
        {
            diagnostics.Add(new(opIndex, featureId, "Only origin placement anchor is supported in CIR-M1."));
            return node;
        }

        if (placement.Offset.Count != 3)
        {
            diagnostics.Add(new(opIndex, featureId, "Placement offset must have exactly 3 components."));
            return node;
        }

        return new CirTransformNode(node, Transform3D.CreateTranslation(new Vector3D(placement.Offset[0], placement.Offset[1], placement.Offset[2])));
    }
}
