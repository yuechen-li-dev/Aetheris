using System.Security.Cryptography;
using System.Text;
using System.Diagnostics;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Brep.Recipes;
using Aetheris.Kernel.Core.Brep.Surgery;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Topology;
using Aetheris.Kernel.StandardLibrary;
using Xunit.Abstractions;

namespace Aetheris.Kernel.Core.Tests.Brep.Recipes;

public sealed class RecognizedConstructionRecipeTests
{
    private readonly ITestOutputHelper _output;

    public RecognizedConstructionRecipeTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void ThroughHole_DirectRecipe_LegacyAndFacadeAreTopologyAndStepIdentical()
    {
        var root = BrepBooleanBoxRecognition.CreateBoxFromExtents(
            new AxisAlignedBoxExtents(-20d, 20d, -15d, 15d, 0d, 12d)).Value;
        var tool = TransformBody(
            BrepPrimitives.CreateCylinder(4d, 20d).Value,
            Transform3D.CreateTranslation(new Vector3D(3d, -2d, 6d)));
        var facade = BrepBoolean.Subtract(root, tool);
        Assert.True(facade.IsSuccess, Join(facade.Diagnostics));

        var history = Assert.IsType<SafeBooleanComposition>(facade.Value.SafeBooleanComposition);
        var hole = Assert.Single(history.Holes);
        var request = new ThroughHoleRecipeRequest(history.RootDescriptor, hole, history, ToleranceContext.Default);

        var recipe = ThroughHoleConstructionRecipe.Execute(request);
        var legacy = BrepBooleanBoxCylinderHoleBuilder.BuildRecognizedThroughHoleLegacy(history, ToleranceContext.Default);

        Assert.True(recipe.IsSuccess, Join(recipe.Diagnostics));
        Assert.True(legacy.IsSuccess, Join(legacy.Diagnostics));
        AssertEquivalent(legacy.Value, recipe.Value);
        AssertEquivalent(recipe.Value, facade.Value);
        Assert.Same(history, recipe.Value.SafeBooleanComposition);
        Assert.Equal(SupportedBooleanHoleSpanKind.Through, hole.SpanKind);
        Assert.Equal(7, recipe.Value.Topology.Faces.Count());
        Assert.Equal(9, recipe.Value.Topology.Loops.Count());
        Assert.Equal(30, recipe.Value.Topology.Coedges.Count());
        Assert.Single(recipe.Value.Geometry.Surfaces, entry => entry.Value.Kind == SurfaceGeometryKind.Cylinder);
        AssertRoundTrips(recipe.Value);
    }

    [Fact]
    public void ThroughHole_DirectRecipeRejectsNonThroughHistoryWithTypedDiagnostic()
    {
        var result = ThroughHoleConstructionRecipe.Execute(null);

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.ValidationFailed, diagnostic.Code);
        Assert.Equal("Brep.Recipes.ThroughHole", diagnostic.Source);
        Assert.Contains("recognized intent", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ThroughHole_SemanticRequestBuilder_MatchesFacadeWithoutTemporaryOperands()
    {
        var request = ThroughHoleRecipeRequestBuilder.FromBoxAndZCylinder(
            40d,
            30d,
            12d,
            new Vector3D(2d, 3d, 6d),
            4d,
            20d,
            new Vector3D(5d, 1d, 6d),
            "semantic-hole");
        Assert.True(request.IsSuccess, Join(request.Diagnostics));

        var direct = ThroughHoleConstructionRecipe.Execute(request.Value);
        var root = TransformBody(
            BrepPrimitives.CreateBox(40d, 30d, 12d).Value,
            Transform3D.CreateTranslation(new Vector3D(2d, 3d, 6d)));
        var tool = TransformBody(
            BrepPrimitives.CreateCylinder(4d, 20d).Value,
            Transform3D.CreateTranslation(new Vector3D(5d, 1d, 6d)));
        var facade = BrepBoolean.Subtract(root, tool);

        Assert.True(direct.IsSuccess, Join(direct.Diagnostics));
        Assert.True(facade.IsSuccess, Join(facade.Diagnostics));
        AssertEquivalent(facade.Value, direct.Value);
        Assert.Equal("semantic-hole", Assert.Single(direct.Value.SafeBooleanComposition!.Holes).FeatureId);
    }

    [Fact]
    public void ThroughHole_RecipeLayerHasNoMeaningfulRuntimeRegression()
    {
        var root = BrepPrimitives.CreateBox(40d, 30d, 12d).Value;
        var tool = BrepPrimitives.CreateCylinder(4d, 20d).Value;
        var facade = BrepBoolean.Subtract(root, tool).Value;
        var history = facade.SafeBooleanComposition!;
        var request = new ThroughHoleRecipeRequest(
            history.RootDescriptor,
            Assert.Single(history.Holes),
            history,
            ToleranceContext.Default);

        const int iterations = 100;
        _ = ThroughHoleConstructionRecipe.Execute(request);
        _ = BrepBooleanBoxCylinderHoleBuilder.BuildRecognizedThroughHoleLegacy(history, ToleranceContext.Default);

        var legacy = Stopwatch.StartNew();
        for (var index = 0; index < iterations; index++)
        {
            Assert.True(BrepBooleanBoxCylinderHoleBuilder.BuildRecognizedThroughHoleLegacy(history, ToleranceContext.Default).IsSuccess);
        }
        legacy.Stop();

        var recipe = Stopwatch.StartNew();
        for (var index = 0; index < iterations; index++)
        {
            Assert.True(ThroughHoleConstructionRecipe.Execute(request).IsSuccess);
        }
        recipe.Stop();

        _output.WriteLine($"legacy={legacy.Elapsed.TotalMilliseconds:F3}ms recipe={recipe.Elapsed.TotalMilliseconds:F3}ms iterations={iterations}");
        Assert.True(recipe.Elapsed <= legacy.Elapsed * 3 + TimeSpan.FromMilliseconds(10));
    }

    [Fact]
    public void StandardLibrary_ThroughHole_DirectRecipeMatchesCompatibilityFacade()
    {
        var direct = StandardLibraryReusableParts.CreateCubeWithCylindricalHole();
        var facade = BrepBoolean.Subtract(
            BrepPrimitives.CreateBox(20d, 20d, 20d).Value,
            BrepPrimitives.CreateCylinder(3d, 24d).Value);

        Assert.True(direct.IsSuccess, Join(direct.Diagnostics));
        Assert.True(facade.IsSuccess, Join(facade.Diagnostics));
        AssertEquivalent(facade.Value, direct.Value);
        Assert.Equal(StandardLibraryReusableParts.CubeWithCylindricalHolePartName,
            Assert.Single(direct.Value.SafeBooleanComposition!.Holes).FeatureId);
        AssertRoundTrips(direct.Value);
    }

    [Theory]
    [InlineData("firmament", 80d, 50d, 25d, 4d, 25d)]
    [InlineData("cir", 20d, 20d, 10d, 3d, 20d)]
    [InlineData("standard-library", 20d, 20d, 20d, 3d, 24d)]
    public void KnownCaller_DirectRecipeAvoidsFacadeRecognitionOverhead(
        string caller,
        double width,
        double depth,
        double height,
        double radius,
        double toolHeight)
    {
        const int iterations = 30;
        var request = ThroughHoleRecipeRequestBuilder.FromBoxAndZCylinder(
            width, depth, height, Vector3D.Zero, radius, toolHeight, Vector3D.Zero, caller).Value;

        _ = ThroughHoleConstructionRecipe.Execute(request);
        _ = BrepBoolean.Subtract(BrepPrimitives.CreateBox(width, depth, height).Value, BrepPrimitives.CreateCylinder(radius, toolHeight).Value);

        var facade = Stopwatch.StartNew();
        for (var index = 0; index < iterations; index++)
        {
            Assert.True(BrepBoolean.Subtract(
                BrepPrimitives.CreateBox(width, depth, height).Value,
                BrepPrimitives.CreateCylinder(radius, toolHeight).Value).IsSuccess);
        }
        facade.Stop();

        var direct = Stopwatch.StartNew();
        for (var index = 0; index < iterations; index++)
        {
            Assert.True(ThroughHoleConstructionRecipe.Execute(request).IsSuccess);
        }
        direct.Stop();

        _output.WriteLine($"caller={caller} facade={facade.Elapsed.TotalMilliseconds:F3}ms direct-recipe={direct.Elapsed.TotalMilliseconds:F3}ms iterations={iterations}");
        Assert.True(direct.Elapsed <= facade.Elapsed * 3 + TimeSpan.FromMilliseconds(10));
    }

    [Fact]
    public void PolygonalThroughCut_DirectRecipe_LegacyAndFacadeAreTopologyAndStepIdentical()
    {
        var root = StandardLibraryPrimitives.CreateRoundedCornerBox(24d, 18d, 20d, 4d).Value;
        var tool = StandardLibraryPrimitives.CreateSlotCut(10d, 4d, 24d, 2d).Value;
        Assert.True(BrepBooleanSafeComposition.TryRecognize(root, ToleranceContext.Default, out var rootHistory, out _));
        Assert.True(BrepBooleanPrismaticToolRecognition.TryRecognize(tool, ToleranceContext.Default, out var recognizedTool, out _));
        var outer = Assert.IsAssignableFrom<IReadOnlyList<(double X, double Y)>>(rootHistory.RootDescriptor.PolygonFootprint);

        var request = new PolygonalThroughCutRecipeRequest(
            outer,
            rootHistory.RootDescriptor.Box,
            recognizedTool.Footprint,
            ToleranceContext.Default,
            rootHistory);
        var recipe = PolygonalThroughCutRecipe.Execute(request);
        var legacy = BrepBooleanPolygonalPrismThroughCutBuilder.BuildLegacy(
            outer,
            rootHistory.RootDescriptor.Box,
            recognizedTool.Footprint);
        var facade = BrepBoolean.Subtract(root, tool);

        Assert.True(recipe.IsSuccess, Join(recipe.Diagnostics));
        Assert.True(legacy.IsSuccess, Join(legacy.Diagnostics));
        Assert.True(facade.IsSuccess, Join(facade.Diagnostics));
        AssertEquivalent(legacy.Value, recipe.Value);
        AssertEquivalent(recipe.Value, facade.Value);
        Assert.Equal(66, recipe.Value.Topology.Faces.Count());
        Assert.Equal(66, recipe.Value.Geometry.Surfaces.Count());
        AssertRoundTrips(recipe.Value);
    }

    [Fact]
    public void PolygonalThroughCut_DirectRecipeRejectsIncompleteFootprintWithTypedDiagnostic()
    {
        var request = new PolygonalThroughCutRecipeRequest(
            [(0d, 0d), (1d, 0d)],
            new AxisAlignedBoxExtents(0d, 2d, 0d, 2d, 0d, 2d),
            [(0.2d, 0.2d), (0.8d, 0.2d), (0.5d, 0.8d)],
            ToleranceContext.Default);

        var result = PolygonalThroughCutRecipe.Execute(request);

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.ValidationFailed, diagnostic.Code);
        Assert.Equal("Brep.Recipes.PolygonalThroughCut", diagnostic.Source);
    }

    private static void AssertEquivalent(BrepBody expected, BrepBody actual)
    {
        Assert.Equal(expected.Topology.Vertices.Count(), actual.Topology.Vertices.Count());
        Assert.Equal(expected.Topology.Edges.Count(), actual.Topology.Edges.Count());
        Assert.Equal(expected.Topology.Faces.Count(), actual.Topology.Faces.Count());
        Assert.Equal(expected.Topology.Loops.Count(), actual.Topology.Loops.Count());
        Assert.Equal(expected.Topology.Coedges.Count(), actual.Topology.Coedges.Count());
        Assert.Equal(expected.Geometry.Curves.Count(), actual.Geometry.Curves.Count());
        Assert.Equal(expected.Geometry.Surfaces.Count(), actual.Geometry.Surfaces.Count());
        Assert.Equal(expected.Bindings.EdgeBindings.Count(), actual.Bindings.EdgeBindings.Count());
        Assert.Equal(expected.Bindings.FaceBindings.Count(), actual.Bindings.FaceBindings.Count());
        Assert.Equal(StepHash(expected), StepHash(actual));
        Assert.True(BrepSurgeryValidation.ValidateBody(actual, requireAllEdgeAndFaceBindings: true).IsSuccess);
    }

    private static void AssertRoundTrips(BrepBody body)
    {
        var export = Step242Exporter.ExportBody(body);
        Assert.True(export.IsSuccess, Join(export.Diagnostics));
        var import = Step242Importer.ImportBody(export.Value);
        Assert.True(import.IsSuccess, Join(import.Diagnostics));
        Assert.True(BrepBindingValidator.Validate(import.Value, requireAllEdgeAndFaceBindings: true).IsSuccess);
    }

    private static string StepHash(BrepBody body)
    {
        var export = Step242Exporter.ExportBody(body);
        Assert.True(export.IsSuccess, Join(export.Diagnostics));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(export.Value))).ToLowerInvariant();
    }

    private static string Join(IEnumerable<KernelDiagnostic> diagnostics)
        => string.Join(Environment.NewLine, diagnostics.Select(diagnostic => diagnostic.Message));

    private static BrepBody TransformBody(BrepBody body, Transform3D transform)
    {
        var geometry = new BrepGeometryStore();
        foreach (var curveEntry in body.Geometry.Curves)
        {
            geometry.AddCurve(curveEntry.Key, curveEntry.Value.Kind switch
            {
                CurveGeometryKind.Line3 => CurveGeometry.FromLine(new Line3Curve(
                    transform.Apply(curveEntry.Value.Line3!.Value.Origin),
                    transform.Apply(curveEntry.Value.Line3.Value.Direction))),
                CurveGeometryKind.Circle3 => CurveGeometry.FromCircle(new Circle3Curve(
                    transform.Apply(curveEntry.Value.Circle3!.Value.Center),
                    transform.Apply(curveEntry.Value.Circle3.Value.Normal),
                    curveEntry.Value.Circle3.Value.Radius,
                    transform.Apply(curveEntry.Value.Circle3.Value.XAxis))),
                _ => curveEntry.Value,
            });
        }

        foreach (var surfaceEntry in body.Geometry.Surfaces)
        {
            geometry.AddSurface(surfaceEntry.Key, surfaceEntry.Value.Kind switch
            {
                SurfaceGeometryKind.Plane => SurfaceGeometry.FromPlane(new PlaneSurface(
                    transform.Apply(surfaceEntry.Value.Plane!.Value.Origin),
                    transform.Apply(surfaceEntry.Value.Plane.Value.Normal),
                    transform.Apply(surfaceEntry.Value.Plane.Value.UAxis))),
                SurfaceGeometryKind.Cylinder => SurfaceGeometry.FromCylinder(new CylinderSurface(
                    transform.Apply(surfaceEntry.Value.Cylinder!.Value.Origin),
                    transform.Apply(surfaceEntry.Value.Cylinder.Value.Axis),
                    surfaceEntry.Value.Cylinder.Value.Radius,
                    transform.Apply(surfaceEntry.Value.Cylinder.Value.XAxis))),
                _ => surfaceEntry.Value,
            });
        }

        var points = new Dictionary<VertexId, Point3D>();
        foreach (var vertex in body.Topology.Vertices)
        {
            if (body.TryGetVertexPoint(vertex.Id, out var point))
            {
                points[vertex.Id] = transform.Apply(point);
            }
        }
        return new BrepBody(body.Topology, geometry, body.Bindings, points, body.SafeBooleanComposition, body.ShellRepresentation);
    }
}
