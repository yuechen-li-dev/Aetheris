using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;
using Aetheris.Kernel.Firmament.Lowering;
using Aetheris.Kernel.Firmament.Validation;

namespace Aetheris.Kernel.Firmament.Execution;

internal static class FirmamentPrimitiveExecutor
{
    public static KernelResult<FirmamentPrimitiveExecutionResult> Execute(FirmamentPrimitiveLoweringPlan loweringPlan)
    {
        ArgumentNullException.ThrowIfNull(loweringPlan);

        var executedPrimitives = new List<FirmamentExecutedPrimitive>(loweringPlan.Primitives.Count);
        var executedBooleans = new List<FirmamentExecutedBoolean>(loweringPlan.Booleans.Count);
        var publishedBodiesByFeatureId = new Dictionary<string, BrepBody>(StringComparer.Ordinal);
        var booleanExecutionBodiesByFeatureId = new Dictionary<string, BrepBody>(StringComparer.Ordinal);
        var featureGraphStates = new Dictionary<string, FirmamentSafeSubtractFeatureGraphState>(StringComparer.Ordinal);

        foreach (var primitive in loweringPlan.Primitives.OrderBy(p => p.OpIndex))
        {
            var bodyResult = ExecutePrimitive(primitive, publishedBodiesByFeatureId);
            if (!bodyResult.IsSuccess)
            {
                return KernelResult<FirmamentPrimitiveExecutionResult>.Failure(bodyResult.Diagnostics);
            }

            executedPrimitives.Add(new FirmamentExecutedPrimitive(primitive.OpIndex, primitive.FeatureId, primitive.Kind, bodyResult.Value.Published));
            publishedBodiesByFeatureId[primitive.FeatureId] = bodyResult.Value.Published;
            booleanExecutionBodiesByFeatureId[primitive.FeatureId] = bodyResult.Value.LegacyForBoolean;
            featureGraphStates[primitive.FeatureId] = primitive.Kind == FirmamentLoweredPrimitiveKind.Box
                ? FirmamentSafeSubtractFeatureGraphState.BoxRoot
                : FirmamentSafeSubtractFeatureGraphState.Other;
        }

        foreach (var boolean in loweringPlan.Booleans.OrderBy(b => b.OpIndex))
        {
            if (!booleanExecutionBodiesByFeatureId.TryGetValue(boolean.PrimaryReferenceFeatureId, out var baseBody))
            {
                continue;
            }

            var featureGraphValidation = FirmamentSafeSubtractFeatureGraphValidator.ValidateNextBoolean(
                boolean,
                featureGraphStates,
                booleanExecutionBodiesByFeatureId);
            if (!featureGraphValidation.IsSuccess)
            {
                return KernelResult<FirmamentPrimitiveExecutionResult>.Failure(
                [
                    CreateBooleanExecutionFailureDiagnostic(boolean),
                    .. featureGraphValidation.Diagnostics
                ]);
            }

            var toolResult = FirmamentBooleanToolBodyFactory.CreateBody(boolean.Tool);
            if (!toolResult.IsSuccess)
            {
                return KernelResult<FirmamentPrimitiveExecutionResult>.Failure(toolResult.Diagnostics);
            }

            var booleanResult = ExecuteBoolean(boolean.Kind, baseBody, toolResult.Value);
            if (!booleanResult.IsSuccess)
            {
                return KernelResult<FirmamentPrimitiveExecutionResult>.Failure(
                [
                    CreateBooleanExecutionFailureDiagnostic(boolean),
                    .. booleanResult.Diagnostics
                ]);
            }

            var placedBooleanBody = booleanResult.Value;
            var placementResult = FirmamentPlacementResolver.ResolvePlacementTranslation(boolean, publishedBodiesByFeatureId);
            if (!placementResult.IsSuccess)
            {
                return KernelResult<FirmamentPrimitiveExecutionResult>.Failure(placementResult.Diagnostics);
            }

            placedBooleanBody = TranslateBody(placedBooleanBody, placementResult.Value);

            executedBooleans.Add(new FirmamentExecutedBoolean(boolean.OpIndex, boolean.FeatureId, boolean.Kind, placedBooleanBody));
            publishedBodiesByFeatureId[boolean.FeatureId] = placedBooleanBody;
            booleanExecutionBodiesByFeatureId[boolean.FeatureId] = placedBooleanBody;
            featureGraphStates[boolean.FeatureId] = featureGraphValidation.Value.ResultState;
        }

        return KernelResult<FirmamentPrimitiveExecutionResult>.Success(new FirmamentPrimitiveExecutionResult(executedPrimitives, executedBooleans));
    }

    private static KernelDiagnostic CreateBooleanExecutionFailureDiagnostic(FirmamentLoweredBoolean boolean)
        => new(
            KernelDiagnosticCode.NotImplemented,
            KernelDiagnosticSeverity.Error,
            $"Requested boolean feature '{boolean.FeatureId}' ({boolean.Kind.ToString().ToLowerInvariant()}) could not be executed.",
            Source: "firmament");

    private static KernelResult<FirmamentExecutedPrimitiveBodies> ExecutePrimitive(FirmamentLoweredPrimitive primitive, IReadOnlyDictionary<string, BrepBody> publishedBodies)
    {
        var legacyResult = ExecuteLegacyPrimitive(primitive);
        if (!legacyResult.IsSuccess)
        {
            return KernelResult<FirmamentExecutedPrimitiveBodies>.Failure(legacyResult.Diagnostics);
        }

        var defaultFrameBody = ApplyDefaultLocalFrame(primitive, legacyResult.Value);
        var placementResult = FirmamentPlacementResolver.ResolvePlacementTranslation(primitive, publishedBodies);
        if (!placementResult.IsSuccess)
        {
            return KernelResult<FirmamentExecutedPrimitiveBodies>.Failure(placementResult.Diagnostics);
        }

        var publishedBody = TranslateBody(defaultFrameBody, placementResult.Value);
        return KernelResult<FirmamentExecutedPrimitiveBodies>.Success(new FirmamentExecutedPrimitiveBodies(publishedBody, legacyResult.Value));
    }

    private static BrepBody ApplyDefaultLocalFrame(FirmamentLoweredPrimitive primitive, BrepBody body)
    {
        return primitive.Kind switch
        {
            FirmamentLoweredPrimitiveKind.Box => TranslateBody(body, new Vector3D(0d, 0d, ((FirmamentLoweredBoxParameters)primitive.Parameters).SizeZ * 0.5d)),
            FirmamentLoweredPrimitiveKind.Cylinder => TranslateBody(body, new Vector3D(0d, 0d, ((FirmamentLoweredCylinderParameters)primitive.Parameters).Height * 0.5d)),
            FirmamentLoweredPrimitiveKind.Cone => TranslateBody(body, new Vector3D(0d, 0d, ((FirmamentLoweredConeParameters)primitive.Parameters).Height * 0.5d)),
            _ => body
        };
    }

    private static KernelResult<BrepBody> ExecuteLegacyPrimitive(FirmamentLoweredPrimitive primitive)
    {
        return primitive.Kind switch
        {
            FirmamentLoweredPrimitiveKind.Box => BrepPrimitives.CreateBox(((FirmamentLoweredBoxParameters)primitive.Parameters).SizeX, ((FirmamentLoweredBoxParameters)primitive.Parameters).SizeY, ((FirmamentLoweredBoxParameters)primitive.Parameters).SizeZ),
            FirmamentLoweredPrimitiveKind.Cylinder => BrepPrimitives.CreateCylinder(((FirmamentLoweredCylinderParameters)primitive.Parameters).Radius, ((FirmamentLoweredCylinderParameters)primitive.Parameters).Height),
            FirmamentLoweredPrimitiveKind.Cone => ExecuteCone((FirmamentLoweredConeParameters)primitive.Parameters),
            FirmamentLoweredPrimitiveKind.Torus => BrepPrimitives.CreateTorus(((FirmamentLoweredTorusParameters)primitive.Parameters).MajorRadius, ((FirmamentLoweredTorusParameters)primitive.Parameters).MinorRadius),
            FirmamentLoweredPrimitiveKind.Sphere => BrepPrimitives.CreateSphere(((FirmamentLoweredSphereParameters)primitive.Parameters).Radius),
            _ => KernelResult<BrepBody>.Failure([new KernelDiagnostic(KernelDiagnosticCode.NotImplemented, KernelDiagnosticSeverity.Error, $"Primitive execution for kind '{primitive.Kind}' is not implemented.")])
        };
    }

    internal static KernelResult<BrepBody> ExecuteCone(FirmamentLoweredConeParameters parameters)
    {
        var frame = new ExtrudeFrame3D(
            origin: Point3D.Origin,
            normal: Direction3D.Create(new Vector3D(0d, 0d, 1d)),
            uAxis: Direction3D.Create(new Vector3D(1d, 0d, 0d)));
        var axis = new RevolveAxis3D(Point3D.Origin, new Vector3D(0d, 0d, 1d));

        var coneResult = BrepRevolve.Create(
            [
                new ProfilePoint2D(parameters.BottomRadius, 0d),
                new ProfilePoint2D(parameters.TopRadius, parameters.Height)
            ],
            frame,
            axis);

        if (!coneResult.IsSuccess)
        {
            return coneResult;
        }

        return KernelResult<BrepBody>.Success(TranslateBody(coneResult.Value, new Vector3D(0d, 0d, -parameters.Height * 0.5d)));
    }

    private static KernelResult<BrepBody> ExecuteBoolean(FirmamentLoweredBooleanKind kind, BrepBody left, BrepBody right) =>
        kind switch
        {
            FirmamentLoweredBooleanKind.Add => BrepBoolean.Union(left, right),
            FirmamentLoweredBooleanKind.Subtract => BrepBoolean.Subtract(left, right),
            FirmamentLoweredBooleanKind.Intersect => BrepBoolean.Intersect(left, right),
            _ => KernelResult<BrepBody>.Failure([new KernelDiagnostic(KernelDiagnosticCode.NotImplemented, KernelDiagnosticSeverity.Error, $"Boolean execution for kind '{kind}' is not implemented.")])
        };

    private static BrepBody TranslateBody(BrepBody body, Vector3D translation)
    {
        if (translation == Vector3D.Zero) return body;

        var translatedGeometry = new BrepGeometryStore();
        foreach (var curveEntry in body.Geometry.Curves)
        {
            translatedGeometry.AddCurve(curveEntry.Key, curveEntry.Value.Kind switch
            {
                CurveGeometryKind.Line3 => CurveGeometry.FromLine(new Line3Curve(curveEntry.Value.Line3!.Value.Origin + translation, curveEntry.Value.Line3.Value.Direction)),
                CurveGeometryKind.Circle3 => CurveGeometry.FromCircle(new Circle3Curve(curveEntry.Value.Circle3!.Value.Center + translation, curveEntry.Value.Circle3.Value.Normal, curveEntry.Value.Circle3.Value.Radius, curveEntry.Value.Circle3.Value.XAxis)),
                _ => curveEntry.Value
            });
        }

        foreach (var surfaceEntry in body.Geometry.Surfaces)
        {
            translatedGeometry.AddSurface(surfaceEntry.Key, surfaceEntry.Value.Kind switch
            {
                SurfaceGeometryKind.Plane => SurfaceGeometry.FromPlane(new PlaneSurface(surfaceEntry.Value.Plane!.Value.Origin + translation, surfaceEntry.Value.Plane.Value.Normal, surfaceEntry.Value.Plane.Value.UAxis)),
                SurfaceGeometryKind.Cylinder => SurfaceGeometry.FromCylinder(new CylinderSurface(surfaceEntry.Value.Cylinder!.Value.Origin + translation, surfaceEntry.Value.Cylinder.Value.Axis, surfaceEntry.Value.Cylinder.Value.Radius, surfaceEntry.Value.Cylinder.Value.XAxis)),
                SurfaceGeometryKind.Cone => SurfaceGeometry.FromCone(new ConeSurface(surfaceEntry.Value.Cone!.Value.PlacementOrigin + translation, surfaceEntry.Value.Cone.Value.Axis, surfaceEntry.Value.Cone.Value.PlacementRadius, surfaceEntry.Value.Cone.Value.SemiAngleRadians, surfaceEntry.Value.Cone.Value.ReferenceAxis)),
                SurfaceGeometryKind.Torus => SurfaceGeometry.FromTorus(new TorusSurface(surfaceEntry.Value.Torus!.Value.Center + translation, surfaceEntry.Value.Torus.Value.Axis, surfaceEntry.Value.Torus.Value.MajorRadius, surfaceEntry.Value.Torus.Value.MinorRadius, surfaceEntry.Value.Torus.Value.XAxis)),
                SurfaceGeometryKind.Sphere => SurfaceGeometry.FromSphere(new SphereSurface(surfaceEntry.Value.Sphere!.Value.Center + translation, surfaceEntry.Value.Sphere.Value.Axis, surfaceEntry.Value.Sphere.Value.Radius, surfaceEntry.Value.Sphere.Value.XAxis)),
                _ => surfaceEntry.Value
            });
        }

        var vertexPoints = new Dictionary<VertexId, Point3D>();
        foreach (var vertex in body.Topology.Vertices)
        {
            if (FirmamentPlacementResolver.TryGetVertexPoint(body, vertex.Id, out var point))
            {
                vertexPoints[vertex.Id] = point + translation;
            }
        }

        return new BrepBody(body.Topology, translatedGeometry, body.Bindings, vertexPoints, body.SafeBooleanComposition?.Translate(translation));
    }
}

internal sealed record FirmamentExecutedPrimitiveBodies(BrepBody Published, BrepBody LegacyForBoolean);

internal static class FirmamentPrimitiveToolParsing
{
    public static FirmamentLoweredBoxParameters ParseBox(string sizeRaw)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(sizeRaw);
        var elements = doc.RootElement.EnumerateArray().ToArray();
        return new FirmamentLoweredBoxParameters(ParseScalar(elements[0].ToString()), ParseScalar(elements[1].ToString()), ParseScalar(elements[2].ToString()));
    }

    public static double ParseScalar(string raw)
    {
        var trimmed = raw.Trim();
        if (trimmed.StartsWith("\"", StringComparison.Ordinal) && trimmed.EndsWith("\"", StringComparison.Ordinal) && trimmed.Length >= 2)
        {
            trimmed = trimmed[1..^1];
        }

        return double.Parse(trimmed, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
    }
}
