using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;
using Aetheris.Kernel.Core.Results;

namespace Aetheris.Kernel.Core.Brep.Recipes;

/// <summary>
/// Recognized intent for the canonical box/cylinder through-hole recipe.
/// Recognition and composition policy have already proved that the root is a
/// box and that the single cylindrical void spans two distinct planar supports.
/// </summary>
internal sealed record ThroughHoleRecipeRequest(
    SafeBooleanRootDescriptor Root,
    SupportedBooleanHole Hole,
    SafeBooleanComposition ConstructionHistory,
    ToleranceContext Tolerance);

/// <summary>
/// Converts caller-owned, recognized box/cylinder semantics into the narrow
/// through-hole recipe contract. This performs bounded composition policy; it
/// does not construct or recognize temporary operand bodies.
/// </summary>
internal static class ThroughHoleRecipeRequestBuilder
{
    public static KernelResult<ThroughHoleRecipeRequest> FromBoxAndZCylinder(
        double hostSizeX,
        double hostSizeY,
        double hostSizeZ,
        Vector3D hostTranslation,
        double toolRadius,
        double toolHeight,
        Vector3D toolTranslation,
        string? featureId = null,
        ToleranceContext? tolerance = null)
    {
        var resolvedTolerance = tolerance ?? ToleranceContext.Default;
        if (!AllPositiveFinite(hostSizeX, hostSizeY, hostSizeZ, toolRadius, toolHeight)
            || !AllFinite(hostTranslation.X, hostTranslation.Y, hostTranslation.Z,
                toolTranslation.X, toolTranslation.Y, toolTranslation.Z))
        {
            return Failure("Through-hole semantic dimensions and translations must be finite, with positive dimensions.");
        }

        var rootBox = new AxisAlignedBoxExtents(
            hostTranslation.X - (hostSizeX * 0.5d),
            hostTranslation.X + (hostSizeX * 0.5d),
            hostTranslation.Y - (hostSizeY * 0.5d),
            hostTranslation.Y + (hostSizeY * 0.5d),
            hostTranslation.Z - (hostSizeZ * 0.5d),
            hostTranslation.Z + (hostSizeZ * 0.5d));
        var root = SafeBooleanRootDescriptor.FromBox(rootBox);
        var history = new SafeBooleanComposition(rootBox, [], root);
        var cylinder = new RecognizedCylinder(
            new Point3D(toolTranslation.X, toolTranslation.Y, toolTranslation.Z),
            Direction3D.Create(new Vector3D(0d, 0d, 1d)),
            toolRadius,
            -toolHeight * 0.5d,
            toolHeight * 0.5d);
        var surface = new AnalyticSurface(AnalyticSurfaceKind.Cylinder, Cylinder: cylinder);

        if (!BrepBooleanSafeCompositionGraphValidator.TryValidateNextSubtract(
                history,
                surface,
                resolvedTolerance,
                out var updatedHistory,
                out var diagnostic,
                featureId))
        {
            return KernelResult<ThroughHoleRecipeRequest>.Failure([
                diagnostic?.ToKernelDiagnostic()
                ?? new KernelDiagnostic(
                    KernelDiagnosticCode.ValidationFailed,
                    KernelDiagnosticSeverity.Error,
                    "Through-hole semantic request was rejected by bounded composition policy.",
                    "Brep.Recipes.ThroughHole.RequestBuilder"),
            ]);
        }

        var hole = updatedHistory.Holes.Single();
        if (hole.SpanKind != SupportedBooleanHoleSpanKind.Through)
        {
            return Failure("Through-hole semantic request must span both host boundary planes.");
        }

        return KernelResult<ThroughHoleRecipeRequest>.Success(
            new ThroughHoleRecipeRequest(root, hole, updatedHistory, resolvedTolerance));
    }

    private static bool AllPositiveFinite(params double[] values)
        => values.All(value => double.IsFinite(value) && value > 0d);

    private static bool AllFinite(params double[] values)
        => values.All(double.IsFinite);

    private static KernelResult<ThroughHoleRecipeRequest> Failure(string message)
        => KernelResult<ThroughHoleRecipeRequest>.Failure([
            new KernelDiagnostic(
                KernelDiagnosticCode.ValidationFailed,
                KernelDiagnosticSeverity.Error,
                message,
                "Brep.Recipes.ThroughHole.RequestBuilder"),
        ]);
}

/// <summary>
/// The "Hello World" of explicit BRep construction: six surviving box faces,
/// one circular inner loop on each entry/exit face, and one cylindrical wall
/// whose periodic seam and rings have known senses. Surgery realizes the known
/// loops/faces/shell; this class does not inspect arbitrary operand topology.
///
/// Rotated tools, blind termination, tangent/coincident entry, intersecting
/// prior voids, and non-planar supports require different recognized contracts.
/// Generalizing this request to accept two bodies would erase the facts that
/// make this recipe tractable.
/// </summary>
internal static class ThroughHoleConstructionRecipe
{
    public static KernelResult<BrepBody> Execute(ThroughHoleRecipeRequest? request)
    {
        if (request is null)
        {
            return Failure("Through-hole recipe requires recognized intent.");
        }

        if (request.Root.Kind != SafeBooleanRootKind.Box
            || request.Root != request.ConstructionHistory.RootDescriptor
            || request.Hole.SpanKind != SupportedBooleanHoleSpanKind.Through
            || request.Hole.Surface.Kind != AnalyticSurfaceKind.Cylinder
            || request.ConstructionHistory.Holes.Count != 1
            || request.ConstructionHistory.Holes[0] != request.Hole)
        {
            return Failure(
                "Through-hole recipe requires one recognized cylindrical void spanning two distinct planar supports on a box root.");
        }

        // The historical builder remains the rollback/parity seam. Its narrow
        // realization entry now uses Surgery primitives; recognition, Judgment,
        // and construction history stay outside this recipe.
        return BrepBooleanBoxCylinderHoleBuilder.BuildRecognizedThroughHoleTopology(
            request.ConstructionHistory,
            request.Tolerance);
    }

    private static KernelResult<BrepBody> Failure(string message)
        => KernelResult<BrepBody>.Failure([
            new KernelDiagnostic(
                KernelDiagnosticCode.ValidationFailed,
                KernelDiagnosticSeverity.Error,
                message,
                "Brep.Recipes.ThroughHole"),
        ]);
}
