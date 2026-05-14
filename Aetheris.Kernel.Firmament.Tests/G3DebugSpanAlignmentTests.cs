using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;
using Aetheris.Kernel.Firmament.Execution;
using Aetheris.Kernel.Firmament.Materializer;
using Aetheris.Kernel.Firmament.Diagnostics;
using Aetheris.Kernel.Firmament.Lowering;
using Aetheris.Kernel.Firmament.Parsing;
using Aetheris.Kernel.Firmament.Validation;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class G3DebugSpanAlignmentTests
{
    [Fact]
    public void BlindHoleMountBlock_ProductionBooleanInputs_Resolve_To_BlindFromTop_With_TopFace_Entry()
    {
        var plan = LowerCase("Aetheris.Firmament.FrictionLab/Cases/blind-hole-mount-block/part.firmament");
        var root = Assert.Single(plan.Primitives);
        var cut = Assert.Single(plan.Booleans);

        var execution = CompileCase("Aetheris.Firmament.FrictionLab/Cases/blind-hole-mount-block/part.firmament");
        var rootBody = execution.PrimitiveExecutionResult!.ExecutedPrimitives.Single(p => p.FeatureId == root.FeatureId).Body;
        Assert.True(BrepBooleanBoxRecognition.TryRecognizeAxisAlignedBox(rootBody, Tolerance, out var rootBox, out _));
        Assert.Equal(-5d, rootBox.MinZ);
        Assert.Equal(15d, rootBox.MaxZ);

        var placementResult = FirmamentPlacementResolver.ResolvePlacementTranslation(
            cut,
            new Dictionary<string, BrepBody>(StringComparer.Ordinal) { [root.FeatureId] = rootBody });
        Assert.True(placementResult.IsSuccess);
        Assert.Equal(5d, placementResult.Value.Z);

        var tool = FirmamentBooleanToolBodyFactory.CreateBody(cut.Tool);
        Assert.True(tool.IsSuccess);
        Assert.True(BrepBooleanCylinderRecognition.TryRecognizeCylinder(tool.Value, Tolerance, out var cylinder, out _));
        Assert.Equal(-5d, cylinder.MinCenter.Z);
        Assert.Equal(5d, cylinder.MaxCenter.Z);

        var topFaceEntryZ = rootBox.MaxZ;
        var placedCylinder = cylinder with
        {
            AxisOrigin = new Point3D(cylinder.AxisOrigin.X, cylinder.AxisOrigin.Y, cylinder.AxisOrigin.Z + 5d + placementResult.Value.Z)
        };
        var toolBottomZ = placedCylinder.MinCenter.Z;
        var toolTopZ = placedCylinder.MaxCenter.Z;
        Assert.Equal(5d, toolBottomZ);
        Assert.Equal(topFaceEntryZ, toolTopZ);

        var boundaryAtMinZ = ResolveAxisParameter(placedCylinder.AxisOrigin.Z, rootBox.MinZ);
        var boundaryAtMaxZ = ResolveAxisParameter(placedCylinder.AxisOrigin.Z, rootBox.MaxZ);
        Assert.False(Covers(placedCylinder, boundaryAtMinZ));
        Assert.True(Covers(placedCylinder, boundaryAtMaxZ));

        Assert.True(BrepBooleanCylinderRecognition.TryValidateCylinderSubtractProfile(
            rootBox,
            placedCylinder,
            Tolerance,
            out var profile,
            out var diagnostic,
            cut.FeatureId));
        Assert.Equal(SupportedBooleanHoleSpanKind.BlindFromTop, profile.SpanKind);
        Assert.Null(diagnostic);
    }

    [Fact]
    public void CounterborePocket_ProductionBooleanInputs_Are_NonSpanning_With_Concrete_AxisValues()
    {
        var plan = LowerCase("Aetheris.Firmament.FrictionLab/Cases/counterbore-hole/part.firmament");
        var root = Assert.Single(plan.Primitives);
        var pocketCut = plan.Booleans.Single(booleanOp => booleanOp.FeatureId == "counterbore_pocket");

        var rootBody = CreateLegacyBox(root);
        Assert.True(BrepBooleanBoxRecognition.TryRecognizeAxisAlignedBox(rootBody, Tolerance, out var rootBox, out _));
        Assert.Equal(-8d, rootBox.MinZ);
        Assert.Equal(8d, rootBox.MaxZ);

        var tool = FirmamentBooleanToolBodyFactory.CreateBody(pocketCut.Tool);
        Assert.True(tool.IsSuccess);
        Assert.True(BrepBooleanCylinderRecognition.TryRecognizeCylinder(tool.Value, Tolerance, out var cylinder, out _));

        Assert.Equal(-3d, cylinder.AxisOrigin.Z);
        Assert.Equal(0d, cylinder.MinAxisParameter);
        Assert.Equal(6d, cylinder.MaxAxisParameter);
        Assert.Equal(-3d, cylinder.MinCenter.Z);
        Assert.Equal(3d, cylinder.MaxCenter.Z);

        var boundaryAtMinZ = ResolveAxisParameter(cylinder.AxisOrigin.Z, rootBox.MinZ);
        var boundaryAtMaxZ = ResolveAxisParameter(cylinder.AxisOrigin.Z, rootBox.MaxZ);
        Assert.Equal(-5d, boundaryAtMinZ);
        Assert.Equal(11d, boundaryAtMaxZ);
        Assert.False(Covers(cylinder, boundaryAtMinZ));
        Assert.False(Covers(cylinder, boundaryAtMaxZ));

        var classification = BrepBooleanCylinderRecognition.TryValidateCylinderSubtractProfile(rootBox, cylinder, Tolerance, out _, out var diagnostic, pocketCut.FeatureId);
        Assert.False(classification);
        Assert.Equal("BrepBoolean.AnalyticHole.NotFullySpanning", diagnostic?.Source);
    }

    private static FirmamentPrimitiveLoweringPlan LowerCase(string relativePath)
    {
        var source = FirmamentCorpusHarness.ReadFixtureText(relativePath);
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

    private static BrepBody CreateLegacyBox(FirmamentLoweredPrimitive primitive)
    {
        var box = Assert.IsType<FirmamentLoweredBoxParameters>(primitive.Parameters);
        var body = BrepPrimitives.CreateBox(box.SizeX, box.SizeY, box.SizeZ);
        Assert.True(body.IsSuccess);
        return body.Value;
    }

    private static double ResolveAxisParameter(double axisOriginZ, double z) => z - axisOriginZ;

    private static bool Covers(in RecognizedCylinder cylinder, double axisParameter)
        => axisParameter >= (cylinder.MinAxisParameter - Tolerance.Linear)
           && axisParameter <= (cylinder.MaxAxisParameter + Tolerance.Linear);

    private static readonly ToleranceContext Tolerance = ToleranceContext.Default;

    private static FirmamentCompilationArtifact CompileCase(string relativePath)
    {
        var source = FirmamentCorpusHarness.ReadFixtureText(relativePath);
        var compile = new FirmamentCompiler().Compile(new FirmamentCompileRequest(new FirmamentSourceDocument(source)));
        Assert.True(compile.Compilation.IsSuccess);
        return compile.Compilation.Value;
    }

}
