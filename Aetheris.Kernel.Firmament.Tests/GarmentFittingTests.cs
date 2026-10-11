using System.Collections.Immutable;
using System.Numerics;
using Aetheris.Cloth3D;
using Aetheris.Kernel.Firmament.Garment;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Humanoid;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class GarmentFittingTests
{
    [Fact]
    public void KnownPatternComposesGuidesAndPoseMotionWithoutChangingTheCutOrBind()
    {
        string path = FirmamentCorpusHarness.ResolveFixtureFullPath(
            "fixtures/Canonical/Garment/garmentcode-dress-guided.firmament");
        string source = File.ReadAllText(path);
        var body = HumanoidPoseAuthoringTests.Body();
        var binds = body.Skeleton.Joints.Select(joint => joint.GlobalBind).ToArray();
        var binding = new GarmentFigureBinding("Anatolia", body);
        var authored = GarmentCompiler.Compile(source, path, binding);
        Assert.True(authored.IsSuccess, string.Join("; ", authored.Diagnostics));

        int fittingDeclaration = source.IndexOf("  Fitting collar", StringComparison.Ordinal);
        Assert.True(fittingDeclaration >= 0);
        string controlSource = source[..fittingDeclaration] + "Drape { figure: Anatolia; pose: Rest; } }";
        var control = GarmentCompiler.Compile(controlSource, path, binding);
        Assert.True(control.IsSuccess, string.Join("; ", control.Diagnostics));
        var garment = authored.Garment!;
        Assert.Equal(control.Garment!.Cloth.ContentKey, garment.Cloth.ContentKey);
        Assert.Empty(garment.Cloth.Definition.Pins);
        Assert.Equal(20, Assert.Single(garment.FittingGuides).ReleaseAfter);

        var posing = garment.Source.Posing!;
        var transition = posing.Transitions[garment.Source.Transition!];
        var fitting = posing.Resolve(transition.From, "Anatolia");
        var wearing = posing.Resolve(transition.To, "Anatolia");
        var fittingPose = HumanoidPoseAuthoring.Solve(fitting, body);
        var wearingPose = HumanoidPoseAuthoring.Solve(wearing, body);
        Assert.NotEqual(body.EvaluatePositions(fittingPose).ToArray(), body.EvaluatePositions(wearingPose).ToArray());
        Assert.Equal(binds, body.Skeleton.Joints.Select(joint => joint.GlobalBind));
    }

    [Fact]
    public void FittingToolingOffersFieldsAndFormattingPreservesTheCompiledSchedule()
    {
        const string draft = "schema Garment\nGarment draft {\n Fitting collar {\n ";
        var completion = FirmamentLanguageService.Complete(draft, "draft.firmament", "1", draft.Length);
        Assert.Equal("Fitting", completion.Context);
        Assert.Contains(completion.MissingRequiredFields, field => field == "ReleaseAfter");
        var format = FirmamentLanguageAnalysisService.Format(Fixture(), "fitting.firmament", "1");
        var original = GarmentCompiler.Compile(Fixture()).Garment!;
        var formatted = GarmentCompiler.Compile(format.Text).Garment!;
        Assert.Equal(original.Cloth.ContentKey, formatted.Cloth.ContentKey);
        Assert.Equal(original.FittingGuides[0].ReleaseAfter, formatted.FittingGuides[0].ReleaseAfter);
        Assert.True(original.FittingGuides[0].Constraint.Vertices.SequenceEqual(formatted.FittingGuides[0].Constraint.Vertices));
    }

    [Theory]
    [InlineData("1030mm - 30mm")]
    [InlineData("1e3mm")]
    [InlineData("Pow(10, 3) * 1mm")]
    public void ConceptPlaneCoordinatesUseTheExistingCompileTimeScalarEvaluator(string height)
    {
        string source = Fixture();
        int coordinate = source.IndexOf("1000mm]", StringComparison.Ordinal);
        Assert.True(coordinate >= 0);
        source = source.Remove(coordinate, "1000mm".Length).Insert(coordinate, height);
        var result = GarmentCompiler.Compile(source);
        Assert.True(result.IsSuccess, string.Join("; ", result.Diagnostics));
        Assert.Equal(1, Assert.Single(result.Garment!.FittingGuides).Constraint.Offset);
    }

    [Fact]
    public void ConceptPlaneBindsEdgesWithoutChangingMaterialTopologyOrPermanentPins()
    {
        string source = Fixture();
        var compiled = GarmentCompiler.Compile(source);
        Assert.True(compiled.IsSuccess, string.Join("; ", compiled.Diagnostics));
        var garment = compiled.Garment!;
        var control = GarmentCompiler.Compile(source[..source.IndexOf("  Fitting collar", StringComparison.Ordinal)] + "}").Garment!;
        Assert.Equal(control.Cloth.ContentKey, garment.Cloth.ContentKey);
        Assert.Empty(garment.Cloth.Definition.Pins);
        Assert.Single(garment.Panels);
        var guide = Assert.Single(garment.FittingGuides);
        Assert.Equal("guides.collarFloor", guide.ConceptPlane);
        Assert.Equal(Vector3.UnitZ, guide.Constraint.Normal);
        Assert.Equal(1, guide.Constraint.Offset);
        Assert.Equal(5, guide.ReleaseAfter);
        Assert.Equal(guide.Constraint.Vertices.Length, guide.Constraint.Vertices.Distinct().Count());
    }

    [Theory]
    [InlineData("guides.collarFloor;", "guides.missing;", "garment-fitting-plane")]
    [InlineData("sheet.edges.Left", "sheet.edges.missing", "garment-fitting-edge-unknown")]
    [InlineData("releaseAfter: 5", "releaseAfter: 5.5", "garment-fitting-properties")]
    [InlineData("normal: [0, 0, 1]", "normal: [0, 0, 2]", "garment-fitting-properties")]
    [InlineData("normal: [0, 0, 1]", "normal: [0, 0, 1mm]", "garment-fitting-plane")]
    [InlineData("collarFloor: Plane", "collarFloor: true\n  collarFloor: Plane", "garment-fitting-plane")]
    public void InvalidBindingsHaveOwnedDiagnostics(string before, string after, string code)
    {
        var result = GarmentCompiler.Compile(Fixture().Replace(before, after, StringComparison.Ordinal));
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == code);
    }

    [Fact]
    public void RealDrapeReleasesGuidesAndMeasuresUnsupportedMotion()
    {
        var garment = GarmentCompiler.Compile(Fixture()).Garment!;
        var result = GarmentDraping.SettleOnSurface(garment, new GroundPlane(), "test", "ground", 20,
            settings: new() { Substeps = 2, Iterations = 4, StopWhenSettled = true });
        Assert.True(result.GuidesReleased);
        Assert.True(result.ReplayExact);
        Assert.Equal(20, result.CompletedSettlingTicks);
        var supported = Assert.Single(result.FittingCheckpoints).Snapshot;
        Assert.Equal(35, supported.Tick);
        Assert.All(garment.FittingGuides[0].Constraint.Vertices,
            vertex => Assert.InRange(supported.Positions[vertex].Z, .99999f, 1.00001f));
        Assert.True(result.Snapshot.Positions.Max(position => position.Z) < .99f);
        Assert.All(result.Samples.Where(sample => sample.Phase == "sewing" || sample.Frame <= 5), sample =>
        {
            Assert.True(Assert.Single(sample.Guides).Active);
            Assert.False(sample.Settled);
        });
        Assert.All(result.Samples.Where(sample => sample.Phase == "settling" && sample.Frame > 5),
            sample => Assert.False(Assert.Single(sample.Guides).Active));
        Assert.Throws<InvalidOperationException>(() => GarmentDraping.SettleOnSurface(
            garment, new GroundPlane(), "test", "ground", 19));
    }

    [Fact]
    public void CompliantHalfSpaceHasXpbdResidualAndExactReplayWithoutIterationStiffening()
    {
        var plan = Triangle().Compile();
        var guide = new ClothHalfSpaceGuide3D("test", [0, 1, 2], Vector3.UnitZ, 1, .00001f);
        var contacts = new ClothContacts3D { Guides = [guide] };
        var options = new ClothStepOptions3D { Gravity = Vector3.Zero, Substeps = 1, Iterations = 1, ContactIterations = 1 };
        using var solver = new ClothSolver3D(plan);
        var before = solver.Capture();
        solver.Step(options, contacts);
        var expected = solver.Capture();
        float alpha = guide.Compliance / (options.DeltaSeconds * options.DeltaSeconds);
        float height = plan.InverseMasses[0] / (plan.InverseMasses[0] + alpha);
        Assert.InRange(MathF.Abs(expected.Positions[0].Z - height), 0, .000001f);
        solver.Restore(before);
        solver.Step(options with { Iterations = 12 }, contacts);
        Assert.InRange(Vector3.Distance(expected.Positions[0], solver.Capture().Positions[0]), 0, .000001f);
        solver.Restore(before);
        solver.Step(options, contacts);
        Assert.True(expected.Positions.SequenceEqual(solver.Capture().Positions));
        Assert.True(expected.Velocities.SequenceEqual(solver.Capture().Velocities));
    }

    [Fact]
    public void PhysicalContactWinsWhenAConstructionGuideConflictsWithTheBody()
    {
        var plan = Triangle().Compile();
        using var solver = new ClothSolver3D(plan);
        var contacts = new ClothContacts3D
        {
            Guides = [new("floor", [0, 1, 2], Vector3.UnitZ, 1, 0)],
            PlaneNormal = -Vector3.UnitZ,
            PlaneOffset = -.5f,
        };
        solver.Step(new() { Gravity = Vector3.Zero, Substeps = 1, Iterations = 4, ContactIterations = 1 }, contacts);
        Assert.All(solver.Capture().Positions, position => Assert.True(position.Z < .51f));
        Assert.InRange(ClothState3D.Measure(plan, solver.Capture(), contacts).MaximumSurfacePenetration, 0, .000001f);
    }

    [Fact]
    public void InvalidGuideStopsBeforeStateMutationAndGpuCannotSilentlyIgnoreIt()
    {
        var plan = Triangle().Compile();
        using var solver = new ClothSolver3D(plan);
        var before = solver.Capture();
        var bad = new ClothContacts3D { Guides = [new("bad", [3], Vector3.UnitZ, 1, 0)] };
        Assert.Throws<ArgumentException>(() => solver.Step(new(), bad));
        Assert.Equal(0, solver.Tick);
        Assert.True(before.Positions.SequenceEqual(solver.Capture().Positions));
        var contacts = new ClothContacts3D { Guides = [new("good", [0], Vector3.UnitZ, 1, 0)] };
        var error = Assert.Throws<NotSupportedException>(() => new ClothGpuData3D(plan).Header(
            new(), contacts, new(ClothPhase3D.Predict, 0, 1), 1));
        Assert.Contains("AUR-CLOTH-005", error.Message);
    }

    private static ClothDefinition3D Triangle() => new("guide", [new(0, 0, 0), new(1, 0, 0), new(0, 1, 0)],
        [new(0, 0), new(1, 0), new(0, 1)], [0, 1, 2]);

    private static string Fixture()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Aetheris.slnx")))
        {
            root = root.Parent;
        }
        return File.ReadAllText(Path.Combine(root!.FullName, "fixtures", "Canonical", "Garment", "temporary-fitting.firmament"));
    }

    private sealed class GroundPlane : IClothBodySurface3D
    {
        public ClothBodySample3D Query(Vector3 point) => new(new(point.X, point.Y, 0), Vector3.UnitZ, point.Z);
    }
}
