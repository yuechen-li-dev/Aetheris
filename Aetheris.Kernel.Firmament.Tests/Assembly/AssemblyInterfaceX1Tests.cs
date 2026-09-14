using Aetheris.Kernel.Firmament.Assembly;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Firmament.Tests.Assembly;

public sealed class AssemblyInterfaceX1Tests
{
    [Fact]
    public void MultiFileSubassembliesReuseDefinitionsExposePortsAndRetainHierarchy()
    {
        var path = FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/AssemblyInterfaces/machine.firmament");
        var first = new AssemblyM0Pipeline().CompileFile(path);
        var second = new AssemblyM0Pipeline().CompileFile(path);

        Assert.True(first.IsSuccess, Evidence(first));
        Assert.Equal(3, first.Ir!.SourceDependencies!.Count);
        Assert.Equal(2, first.Ir.AssemblyDefinitions!.Count);
        Assert.Equal(15, first.Ir.Instances.Count);
        Assert.Equal(first.Ir.Instances.Select(item => item.StableId), second.Ir!.Instances.Select(item => item.StableId));
        Assert.Equal(2, first.Ir.Instances.Count(item => item.DefinitionIdentity == "Register"));
        Assert.Equal(4, first.Ir.Instances.Count(item => item.DefinitionIdentity == "DigitModule"));
        Assert.Contains(first.Ir.Instances, item => item.Path.ToString() == "DifferenceEngineReadiness.RegisterB.Digit1.Shaft");
        Assert.All(first.Ir.Instances.Where(item => item.DefinitionIdentity is "Register" or "DigitModule"), item => Assert.True(item.IsEncapsulatedDefinition));

        var carry = Assert.Single(first.Ir.Mates);
        Assert.Equal("RegisterCarry", carry.Name);
        Assert.All(carry.Roles, role => Assert.DoesNotContain("Digit", role.ParticipantPath.ToString(), StringComparison.Ordinal));
        Assert.Equal(MechanicalInterfaceFamily.Axial, first.Ir.Interfaces.Single(item => item.Name == "RegisterCarry").Family);

        var register = first.Ir.AssemblyDefinitions.Single(item => item.DefinitionIdentity == "Register");
        var custom = Assert.Single(register.LocalMates);
        Assert.Equal(3, custom.ConstraintIds.Count);
        Assert.Equal("passed", Assert.Single(custom.RequirementResults!).Status);
        var digit = first.Ir.AssemblyDefinitions.Single(item => item.DefinitionIdentity == "DigitModule");
        Assert.Equal(2, Assert.Single(digit.LocalMates).ConstraintIds.Count);
    }

    [Fact]
    public void TypedFixedAxialAndRevoluteExpandThroughAtomicAuthority()
    {
        const string source = """
            Assembly Typed {
              <Assembly Typed>
                <Part Fixed = Block> Semantic Mount { DatumFrame Frame = [0,0,0] x [1,0,0] y [0,1,0] z [0,0,1]; } </Part>
                <Part Moving = Block> Semantic Mount { DatumFrame Frame = [0,0,0] x [1,0,0] y [0,1,0] z [0,0,1]; } </Part>
              </Assembly>
              Anchor: Typed.Fixed.Mount;
              Interface<Fixed> Mount { A: Typed.Moving.Mount; B: Typed.Fixed.Mount; }
            }
            """;
        var result = new AssemblyM0Compiler().Compile(new AssemblyM0Parser().Parse(source).Source!);
        Assert.True(result.IsSuccess, Evidence(result));
        var definition = Assert.Single(result.Ir!.Interfaces);
        Assert.True(definition.CompilerOwnedExpansion);
        Assert.Equal(MechanicalInterfaceFamily.Fixed, definition.Family);
        Assert.Equal(PlacementConstraintKind.FrameCoincident, Assert.Single(result.Ir.PlacementConstraints).Kind);
    }

    [Fact]
    public void FailedCustomRequirementNamesInterfaceAndRequirement()
    {
        var path = FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Invalid/AssemblyInterfaces/failed-custom-require.firmament");
        var result = new AssemblyM0Pipeline().CompileFile(path);
        var diagnostic = Assert.Single(result.Diagnostics, item => item.Code == "assembly-interface-requirement-failed");
        Assert.Contains("ImpossibleClearance", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("ClearancePositive", diagnostic.Message, StringComparison.Ordinal);
        Assert.Equal("failed", Assert.Single(Assert.Single(result.Ir!.Mates).RequirementResults!).Status);
    }

    [Fact]
    public void IncludedSubassemblyLowersWholeTreeOnceToSharedAp242Definition()
    {
        var path = FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/AssemblyInterfaces/executable-machine.firmament");
        var compiled = new AssemblyM1Pipeline().CompileFile(path);
        Assert.True(compiled.IsSuccess, string.Join(Environment.NewLine, compiled.Diagnostics.Select(item => item.Code + ": " + item.Message)));
        Assert.Equal(2, compiled.Ir!.Instances.Count(item => item.DefinitionIdentity == "ExecutableCell"));
        Assert.Single(compiled.Ir.AssemblyDefinitions!, item => item.DefinitionIdentity == "ExecutableCell");
        Assert.Single(compiled.Geometry!.Artifact.Definitions);

        var step = AssemblyIrAp242Exporter.Export(compiled);
        Assert.True(step.IsSuccess, string.Join(Environment.NewLine, step.Diagnostics.Select(item => item.Message)));
        var imported = Step242AssemblyImporter.Import(step.Value);
        Assert.True(imported.IsSuccess, string.Join(Environment.NewLine, imported.Diagnostics.Select(item => item.Message)));
        var cellDefinition = Assert.Single(imported.Value.Definitions, item => item.Geometry is null && item.Name == "ExecutableCell");
        Assert.Equal(2, imported.Value.Occurrences.Count(item => item.DefinitionStableId == cellDefinition.StableId));
        Assert.DoesNotContain(".step", File.ReadAllText(path), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".step", File.ReadAllText(Path.Combine(Path.GetDirectoryName(path)!, "executable-cell.firmament")), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParentCannotCrossSubassemblyPrivateBoundary()
    {
        var path = FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Invalid/AssemblyInterfaces/private-internal-access.firmament");
        var result = new AssemblyM0Pipeline().CompileFile(path);
        Assert.Contains(result.Diagnostics, item => item.Code == "assembly-internal-member-hidden");
    }

    [Fact]
    public void IncludeFailuresAreTypedAndCyclesShowChain()
    {
        var directory = Path.Combine(Path.GetTempPath(), "aetheris-assembly-x1-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var missing = Path.Combine(directory, "missing-root.firmament");
            File.WriteAllText(missing, "Include \"absent.firmament\";\nAssembly Root { <Assembly Root></Assembly> }");
            Assert.Contains(new AssemblyM0Parser().ParseFile(missing).Diagnostics, item => item.Code == "assembly-include-file-not-found");

            var a = Path.Combine(directory, "a.firmament"); var b = Path.Combine(directory, "b.firmament");
            File.WriteAllText(a, "Include \"b.firmament\";\nAssembly Root { <Assembly Root></Assembly> }");
            File.WriteAllText(b, "Include \"a.firmament\";");
            var cycle = Assert.Single(new AssemblyM0Parser().ParseFile(a).Diagnostics, item => item.Code == "assembly-include-cycle");
            Assert.Contains("a.firmament -> b.firmament -> a.firmament", cycle.Message, StringComparison.OrdinalIgnoreCase);

            var nested = Path.Combine(directory, "nested"); Directory.CreateDirectory(nested);
            var outside = Path.Combine(directory, "outside.firmament"); File.WriteAllText(outside, "Subassembly Outside { <Assembly Outside></Assembly> }");
            var escaping = Path.Combine(nested, "root.firmament"); File.WriteAllText(escaping, "Include \"../outside.firmament\";\nAssembly Root { <Assembly Root></Assembly> }");
            Assert.Contains(new AssemblyM0Parser().ParseFile(escaping).Diagnostics, item => item.Code == "assembly-include-outside-root");

            const string duplicate = """
                Subassembly Same { <Assembly Same></Assembly> }
                Subassembly Same { <Assembly Same></Assembly> }
                Interface<Axial> Pair { }
                Interface<Axial> Pair { }
                Assembly Root { <Assembly Root></Assembly> }
                """;
            var duplicateDiagnostics = new AssemblyM0Parser().Parse(duplicate).Diagnostics;
            Assert.Contains(duplicateDiagnostics, item => item.Code == "assembly-template-duplicate-definition");
            Assert.Contains(duplicateDiagnostics, item => item.Code == "assembly-interface-duplicate-name");
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void OneDefinitionCanBackOneHundredDeterministicOccurrences()
    {
        var occurrences = string.Join(Environment.NewLine, Enumerable.Range(0, 100).Select(index => $"<Assembly Cell{index} = Cell></Assembly>"));
        var mates = string.Join(Environment.NewLine, Enumerable.Range(1, 99).Select(index => $"Mate Link{index}: Chain {{ A: Stress.Cell{index}.Port; B: Stress.Cell{index - 1}.Port; }}"));
        var source = $$"""
            Subassembly Cell {
              <Assembly Cell><Part Body = CellBody> Semantic Port { Axis Axis = [0,0,0] -> [0,0,1]; } </Part></Assembly>
              Anchor: Cell.Body.Port;
              Expose { Semantic Port = Body.Port; }
            }
            Interface<Axial> Chain { Role A requires AxisCapable; Role B requires AxisCapable; }
            Assembly Stress {
              <Assembly Stress>{{occurrences}}</Assembly>
              Anchor: Stress.Cell0.Port;
              {{mates}}
            }
            """;
        var first = new AssemblyM0Compiler().Compile(new AssemblyM0Parser().Parse(source).Source!);
        var second = new AssemblyM0Compiler().Compile(new AssemblyM0Parser().Parse(source).Source!);
        Assert.True(first.IsSuccess, Evidence(first));
        Assert.Single(first.Ir!.AssemblyDefinitions!);
        Assert.Equal(100, first.Ir.Instances.Count(item => item.DefinitionIdentity == "Cell"));
        Assert.Equal(first.Ir.Instances.Select(item => item.StableId), second.Ir!.Instances.Select(item => item.StableId));
    }

    private static string Evidence(AssemblyCompilationResult result) => string.Join(Environment.NewLine, result.Diagnostics.Select(item => item.Code + ": " + item.Message));
}
