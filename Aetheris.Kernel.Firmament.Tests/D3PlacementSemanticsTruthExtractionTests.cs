using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;
using Aetheris.Kernel.Firmament.Execution;
using Aetheris.Kernel.Firmament.Lowering;
using Aetheris.Kernel.Firmament.Parsing;
using Aetheris.Kernel.Firmament.Validation;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class D3PlacementSemanticsTruthExtractionTests
{
    [Fact]
    public void ProbeA_Box_With_Explicit_Origin_Offset_Zero_Has_CenteredXY_BottomZ_Frame()
    {
        const string source = """
firmament:
  version: 1

model:
  name: d3_probe_a_box_zero
  units: mm

ops[1]:
  -
    op: box
    id: b
    size[3]:
      10
      10
      10
    place:
      on: origin
      offset[3]:
        0
        0
        0
""";

        var compilation = Compile(source);
        Assert.True(compilation.Compilation.IsSuccess);

        var body = Assert.Single(compilation.Compilation.Value.PrimitiveExecutionResult!.ExecutedPrimitives).Body;
        var bounds = GetBounds(body);
        Assert.Equal(new Point3D(-5d, -5d, 0d), bounds.Min);
        Assert.Equal(new Point3D(5d, 5d, 10d), bounds.Max);
    }

    [Fact]
    public void ProbeB_Box_With_Explicit_Origin_Offset_Positive_Translates_From_CenteredXY_BottomZ_Frame()
    {
        const string source = """
firmament:
  version: 1

model:
  name: d3_probe_b_box_positive
  units: mm

ops[1]:
  -
    op: box
    id: b
    size[3]:
      10
      10
      10
    place:
      on: origin
      offset[3]:
        10
        0
        0
""";

        var compilation = Compile(source);
        Assert.True(compilation.Compilation.IsSuccess);

        var body = Assert.Single(compilation.Compilation.Value.PrimitiveExecutionResult!.ExecutedPrimitives).Body;
        var bounds = GetBounds(body);
        Assert.Equal(new Point3D(5d, -5d, 0d), bounds.Min);
        Assert.Equal(new Point3D(15d, 5d, 10d), bounds.Max);
    }

    [Fact]
    public void ProbeC_TwoBox_Additive_Union_Uses_PublishedWorld_Primitive_Frames()
    {
        const string source = """
firmament:
  version: 1

model:
  name: d3_probe_c_two_box_add
  units: mm

ops[2]:
  -
    op: box
    id: base
    size[3]:
      60
      20
      10
    place:
      on: origin
      offset[3]:
        30
        10
        0

  -
    op: add
    id: bracket
    to: base
    with:
      op: box
      size[3]:
        10
        20
        60
    place:
      on: origin
      offset[3]:
        5
        10
        0
""";

        var plan = Lower(source);
        var basePrimitive = Assert.Single(plan.Primitives);
        var addBoolean = Assert.Single(plan.Booleans);

        var compilation = Compile(source);
        Assert.True(compilation.Compilation.IsSuccess);

        var publishedBase = compilation.Compilation.Value.PrimitiveExecutionResult!.ExecutedPrimitives.Single(p => p.FeatureId == "base").Body;
        var publishedBaseBounds = GetBounds(publishedBase);
        Assert.Equal(new Point3D(0d, 0d, 0d), publishedBaseBounds.Min);
        Assert.Equal(new Point3D(60d, 20d, 10d), publishedBaseBounds.Max);

        var toolBody = FirmamentBooleanToolBodyFactory.CreateBody(addBoolean.Tool);
        Assert.True(toolBody.IsSuccess);
        var toolBounds = GetBounds(toolBody.Value);
        Assert.Equal(new Point3D(-5d, -10d, -30d), toolBounds.Min);
        Assert.Equal(new Point3D(5d, 10d, 30d), toolBounds.Max);

        var translation = FirmamentPlacementResolver.ResolvePlacementTranslation(
            addBoolean,
            new Dictionary<string, BrepBody>(StringComparer.Ordinal) { [basePrimitive.FeatureId] = publishedBase });
        Assert.True(translation.IsSuccess);
        Assert.Equal(new Vector3D(5d, 10d, 0d), translation.Value);

        var publishedToolBounds = (
            Min: new Point3D(toolBounds.Min.X + translation.Value.X, toolBounds.Min.Y + translation.Value.Y, toolBounds.Min.Z + 30d + translation.Value.Z),
            Max: new Point3D(toolBounds.Max.X + translation.Value.X, toolBounds.Max.Y + translation.Value.Y, toolBounds.Max.Z + 30d + translation.Value.Z));
        Assert.Equal(new Point3D(0d, 0d, 0d), publishedToolBounds.Min);
        Assert.Equal(new Point3D(10d, 20d, 60d), publishedToolBounds.Max);

        var publishedBoolean = compilation.Compilation.Value.PrimitiveExecutionResult.ExecutedBooleans.Single(b => b.FeatureId == "bracket").Body;
        var publishedBooleanBounds = GetBounds(publishedBoolean);
        Assert.Equal(new Point3D(0d, 0d, 0d), publishedBooleanBounds.Min);
        Assert.Equal(new Point3D(60d, 20d, 60d), publishedBooleanBounds.Max);
    }

    [Fact]
    public void ProbeD_Placement_Block_Presence_Changes_Sphere_Behavior_But_Not_Box_At_Origin()
    {
        const string source = """
firmament:
  version: 1

model:
  name: d3_probe_d_place_presence
  units: mm

ops[4]:
  -
    op: box
    id: box_no_place
    size[3]:
      10
      10
      10

  -
    op: box
    id: box_place_zero
    size[3]:
      10
      10
      10
    place:
      on: origin
      offset[3]:
        0
        0
        0

  -
    op: sphere
    id: sphere_no_place
    radius: 5

  -
    op: sphere
    id: sphere_place_origin_zero
    radius: 5
    place:
      on: origin
      offset[3]:
        0
        0
        0
""";

        var compilation = Compile(source);
        Assert.True(compilation.Compilation.IsSuccess);

        var executed = compilation.Compilation.Value.PrimitiveExecutionResult!.ExecutedPrimitives;

        var boxNoPlaceBounds = GetBounds(executed.Single(p => p.FeatureId == "box_no_place").Body);
        var boxPlaceZeroBounds = GetBounds(executed.Single(p => p.FeatureId == "box_place_zero").Body);
        Assert.Equal(boxNoPlaceBounds, boxPlaceZeroBounds);
        Assert.Equal(new Point3D(-5d, -5d, 0d), boxNoPlaceBounds.Min);
        Assert.Equal(new Point3D(5d, 5d, 10d), boxNoPlaceBounds.Max);

        var sphereNoPlaceBounds = GetBounds(executed.Single(p => p.FeatureId == "sphere_no_place").Body);
        var spherePlaceOriginZeroBounds = GetBounds(executed.Single(p => p.FeatureId == "sphere_place_origin_zero").Body);

        Assert.Equal(new Point3D(-5d, -5d, -5d), sphereNoPlaceBounds.Min);
        Assert.Equal(new Point3D(5d, 5d, 5d), sphereNoPlaceBounds.Max);

        Assert.Equal(new Point3D(-5d, -5d, 0d), spherePlaceOriginZeroBounds.Min);
        Assert.Equal(new Point3D(5d, 5d, 10d), spherePlaceOriginZeroBounds.Max);
    }

    private static FirmamentCompileResult Compile(string source)
    {
        var compiler = new FirmamentCompiler();
        return compiler.Compile(new FirmamentCompileRequest(new FirmamentSourceDocument(source)));
    }

    private static FirmamentPrimitiveLoweringPlan Lower(string source)
    {
        var parse = FirmamentTopLevelParser.Parse(source);
        Assert.True(parse.IsSuccess);
        var schema = FirmamentSchemaValidator.Validate(parse.Value);
        Assert.True(schema.IsSuccess);
        var primitiveFields = FirmamentPrimitiveRequiredFieldValidator.Validate(parse.Value);
        Assert.True(primitiveFields.IsSuccess);
        var booleanFields = FirmamentBooleanRequiredFieldValidator.Validate(parse.Value);
        Assert.True(booleanFields.IsSuccess);
        var patternFields = FirmamentPatternRequiredFieldValidator.Validate(parse.Value);
        Assert.True(patternFields.IsSuccess);
        var validationFields = FirmamentValidationRequiredFieldValidator.Validate(parse.Value);
        Assert.True(validationFields.IsSuccess);
        var targetShapes = FirmamentValidationTargetShapeValidator.Validate(parse.Value);
        Assert.True(targetShapes.IsSuccess);
        var coherent = FirmamentDocumentCoherenceValidator.Validate(targetShapes.Value);
        Assert.True(coherent.IsSuccess);
        var lowered = FirmamentPrimitiveLowerer.Lower(coherent.Value);
        Assert.True(lowered.IsSuccess);
        return lowered.Value;
    }

    private static (Point3D Min, Point3D Max) GetBounds(BrepBody body)
    {
        if (body.SafeBooleanComposition is { } composition)
        {
            var extents = composition.OuterBox;
            return (
                new Point3D(extents.MinX, extents.MinY, extents.MinZ),
                new Point3D(extents.MaxX, extents.MaxY, extents.MaxZ));
        }

        if (BrepBooleanBoxRecognition.TryRecognizeAxisAlignedBox(body, ToleranceContext.Default, out var box, out _))
        {
            return (
                new Point3D(box.MinX, box.MinY, box.MinZ),
                new Point3D(box.MaxX, box.MaxY, box.MaxZ));
        }

        if (BrepBooleanAnalyticSurfaceRecognition.TryRecognizeSphere(body, ToleranceContext.Default, out var sphereSurface, out _))
        {
            var sphere = sphereSurface.Sphere!.Value;
            return (
                new Point3D(sphere.Center.X - sphere.Radius, sphere.Center.Y - sphere.Radius, sphere.Center.Z - sphere.Radius),
                new Point3D(sphere.Center.X + sphere.Radius, sphere.Center.Y + sphere.Radius, sphere.Center.Z + sphere.Radius));
        }

        var points = body.Topology.Vertices
            .Select(v => body.TryGetVertexPoint(v.Id, out var p) ? p : (Point3D?)null)
            .Where(p => p is not null)
            .Select(p => p!.Value)
            .ToArray();
        Assert.NotEmpty(points);

        return (
            new Point3D(points.Min(p => p.X), points.Min(p => p.Y), points.Min(p => p.Z)),
            new Point3D(points.Max(p => p.X), points.Max(p => p.Y), points.Max(p => p.Z)));
    }
}
