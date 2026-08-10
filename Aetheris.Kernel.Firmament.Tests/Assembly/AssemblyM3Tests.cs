using Aetheris.Kernel.Firmament.Assembly;
using Aetheris.Semantics;

namespace Aetheris.Kernel.Firmament.Tests.Assembly;

public sealed class AssemblyM3Tests
{
    private static string Fixture => FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/AssemblyM3/bearing-module.firmament");

    [Fact]
    public void BearingModule_SolvesDefinitionOnceAndComposesOccurrenceWorldTransforms()
    {
        var first = new AssemblyM1Pipeline().CompileFile(Fixture);
        var second = new AssemblyM1Pipeline().CompileFile(Fixture);
        Assert.True(first.IsSuccess, Evidence(first));

        var definition = Assert.Single(first.Ir!.AssemblyDefinitions!);
        Assert.Equal("BearingModule<Spec:StandardModuleSpec>", definition.SpecializationIdentity);
        Assert.Equal(5, definition.LocalInstances.Count);
        Assert.Equal("valid", Assert.Single(definition.LocalMates).ValidationStatus);
        Assert.True(Assert.Single(definition.LocalToleranceStackups).Passed);
        Assert.Contains(definition.Provenance, item => item.Stage == "static-table" && item.Identity == "BlockStandards");
        Assert.Contains(definition.Provenance, item => item.Stage == "static-with" && item.Identity == "StandardModuleSpec");

        var left = first.Ir.Instances.Single(item => item.Path.ToString() == "Machine.LeftModule");
        var right = first.Ir.Instances.Single(item => item.Path.ToString() == "Machine.RightModule");
        Assert.Equal(left.DefinitionIdentity, right.DefinitionIdentity);
        Assert.Equal(-30, left.ResolvedTransform!.Matrix[12], 8);
        Assert.Equal(30, right.ResolvedTransform!.Matrix[12], 8);
        var leftShaft = first.Ir.Instances.Single(item => item.Path.ToString() == "Machine.LeftModule.Shaft");
        var rightShaft = first.Ir.Instances.Single(item => item.Path.ToString() == "Machine.RightModule.Shaft");
        Assert.Equal(-30, leftShaft.ResolvedTransform!.Matrix[12], 8);
        Assert.Equal(30, rightShaft.ResolvedTransform!.Matrix[12], 8);
        Assert.Equal(10, leftShaft.ResolvedTransform.Matrix[14], 8);

        Assert.Equal(first.Geometry!.Artifact.DeterministicSha256, second.Geometry!.Artifact.DeterministicSha256);
        Assert.Equal(definition.StableId, Assert.Single(second.Ir!.AssemblyDefinitions!).StableId);
    }

    [Fact]
    public void BearingModule_PublicWorldDatumsAndParentToleranceRetainExpandedPrivateChain()
    {
        var result = new AssemblyM1Pipeline().CompileFile(Fixture);
        Assert.True(result.IsSuccess, Evidence(result));
        var left = result.Ir!.Instances.Single(item => item.Path.ToString() == "Machine.LeftModule");

        var axis = Assert.IsType<ExactAxisBinding>(AssemblyWorldQuery.Resolve(result.Ir, left.SemanticRoot.ExposedMembers["DriveAxis"].StableIdentity));
        var plane = Assert.IsType<ExactPlaneBinding>(AssemblyWorldQuery.Resolve(result.Ir, left.SemanticRoot.ExposedMembers["MountFace"].StableIdentity));
        var point = Assert.IsType<ExactPointBinding>(AssemblyWorldQuery.Resolve(result.Ir, left.SemanticRoot.ExposedMembers["MountPoint"].StableIdentity));
        Assert.Equal(-30, axis.OriginX, 8); Assert.Equal(10, axis.OriginZ, 8);
        Assert.Equal(-30, plane.OriginX, 8); Assert.Equal(-30, point.X, 8);

        var summary = result.Ir.DimensionalRelations.Single(item => item.OriginInstancePath == "Machine.LeftModule" && item.ExpandedContributors is not null);
        Assert.Equal(45, summary.Nominal, 8); Assert.Equal(-0.08, summary.LowerTolerance, 8); Assert.Equal(0.10, summary.UpperTolerance, 8);
        Assert.Equal(3, summary.ExpandedContributors!.Count);
        Assert.Contains(summary.SourceProvenance!, item => item.Stage == "static-with");

        var stack = Assert.Single(result.Ir.ToleranceStackups);
        Assert.True(stack.Passed); Assert.Equal(45, stack.Nominal, 8);
        Assert.Equal(44.92, stack.WorstCaseMinimum, 8); Assert.Equal(45.10, stack.WorstCaseMaximum, 8);
        var publicStep = Assert.Single(stack.Contributions, item => item.ExpandedContributors is not null);
        Assert.Equal(["Housing seat", "Bearing width / BlockStandards", "Spacer width / StandardModuleSpec with"],
            publicStep.ExpandedContributors!.Select(item => item.Provenance));

        Assert.Equal("valid", result.Ir.Mates.Single(item => item.Name == "PlaceLeft").ValidationStatus);
        var hiddenSource = File.ReadAllText(Fixture).Replace("Machine.LeftModule.Mount", "Machine.LeftModule.Housing.Mount", StringComparison.Ordinal);
        var hidden = new AssemblyM0Parser().Parse(hiddenSource, Fixture);
        var hiddenCompilation = new AssemblyM0Compiler().Compile(hidden.Source!);
        Assert.Contains(hiddenCompilation.Diagnostics, item => item.Code == "assembly-internal-member-hidden");
    }

    [Fact]
    public void InternallyUnderconstrainedTemplateSpecializationIsRejectedBeforeParentExecution()
    {
        var source = File.ReadAllText(Fixture).Replace("Mate ShaftSeat: RigidSeat", "// Mate ShaftSeat: RigidSeat", StringComparison.Ordinal);
        var parsed = new AssemblyM0Parser().Parse(source, Fixture);
        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.Diagnostics, item => item.Code == "assembly-template-internal-constraint-failure" && item.Message.Contains("Shaft", StringComparison.Ordinal));
    }

    [Fact]
    public void DifferentAssemblyTemplateArgumentsProduceDistinctLocallySolvedDefinitions()
    {
        var source = File.ReadAllText(Fixture).Replace(
            "<Assembly RightModule = BearingModule<Spec: StandardModuleSpec>>",
            "<Assembly RightModule = BearingModule<Spec: BaseModuleSpec>>", StringComparison.Ordinal);
        var parsed = new AssemblyM0Parser().Parse(source, Fixture);
        Assert.True(parsed.IsSuccess, string.Join(Environment.NewLine, parsed.Diagnostics.Select(item => item.Message)));
        var compiled = new AssemblyM0Compiler().Compile(parsed.Source!);
        Assert.Equal(2, compiled.Ir!.AssemblyDefinitions!.Count);
        Assert.NotEqual(
            compiled.Ir.Instances.Single(item => item.Path.ToString() == "Machine.LeftModule").DefinitionIdentity,
            compiled.Ir.Instances.Single(item => item.Path.ToString() == "Machine.RightModule").DefinitionIdentity);
        Assert.All(compiled.Ir.AssemblyDefinitions, item => Assert.True(item.SolveMilliseconds > 0));
    }

    private static string Evidence(AssemblyM1CompilationResult result) => string.Join(Environment.NewLine, result.Diagnostics.Select(item => item.Code + ": " + item.Message));
}
