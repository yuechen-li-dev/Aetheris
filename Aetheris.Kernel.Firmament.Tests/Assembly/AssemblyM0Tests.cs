using System.Text.Json;
using Aetheris.Kernel.Firmament.Assembly;
using Aetheris.Semantics;

namespace Aetheris.Kernel.Firmament.Tests.Assembly;

public sealed class AssemblyM0Tests
{
    [Fact]
    public void TemplateAssembly_ReusesDefinitionAndExposesOnlyIntentionalSurface()
    {
        const string source = """
            Template < Spec: ModuleSpec >
            Assembly BearingModule {
              <Assembly BearingModule>
                <Part Housing = HousingPart>
                  Semantic Mount { Axis Axis = [0,0,0] -> [0,0,1]; Dimension Offset = 45mm tol +0.10mm -0.08mm; }
                </Part>
              </Assembly>
              Anchor: BearingModule.Housing.Mount;
              Expose { Semantic Mount = Housing.Mount; Dimension MountToDriveOffset = Housing.Mount.Offset; }
            }
            Interface MountPair { Role Module requires AxisCapable; Role Frame requires AxisCapable; }
            Assembly Machine {
              <Assembly Machine>
                <Part Frame = FramePart> Semantic Mount { Axis Axis = [0,0,0] -> [0,0,1]; } </Part>
                <Assembly Left = BearingModule<Spec: StandardSpec>></Assembly>
                <Assembly Right = BearingModule<Spec: StandardSpec>></Assembly>
              </Assembly>
              Anchor: Machine.Frame.Mount;
              Mate LeftMount: MountPair { Module: Machine.Left.Mount; Frame: Machine.Frame.Mount; }
            }
            """;
        var parsed = new AssemblyM0Parser().Parse(source);
        Assert.True(parsed.IsSuccess, string.Join(Environment.NewLine, parsed.Diagnostics.Select(x => x.Message)));
        var result = new AssemblyM0Compiler().Compile(parsed.Source!);
        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics.Select(x => x.Message)));
        var left = result.Ir!.Instances.Single(instance => instance.Path.ToString() == "Machine.Left");
        var right = result.Ir.Instances.Single(instance => instance.Path.ToString() == "Machine.Right");
        Assert.True(left.IsEncapsulatedDefinition);
        Assert.Equal(left.DefinitionIdentity, right.DefinitionIdentity);
        Assert.NotEqual(left.SemanticRoot.ExposedMembers["Mount"].StableIdentity, right.SemanticRoot.ExposedMembers["Mount"].StableIdentity);
        Assert.Equal("valid", Assert.Single(result.Ir.Mates).ValidationStatus);

        var hidden = source.Replace("Machine.Left.Mount", "Machine.Left.Housing.Mount", StringComparison.Ordinal);
        var hiddenResult = new AssemblyM0Compiler().Compile(new AssemblyM0Parser().Parse(hidden).Source!);
        Assert.Contains(hiddenResult.Diagnostics, diagnostic => diagnostic.Code == "assembly-internal-member-hidden");
    }

    [Fact]
    public void BearingModule_CompilesTreeMatesPlacementFitAndAutomaticStackup()
    {
        var path = FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/AssemblyM0/bearing-module.firmament");
        var result = new AssemblyM0Pipeline().CompileFile(path);

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics.Select(x => $"{x.Code}: {x.Message}")));
        var ir = Assert.IsType<AssemblyIr>(result.Ir);
        Assert.Equal("aetheris/assembly-ir/m0", ir.Schema);
        Assert.Equal(7, ir.Instances.Count);
        Assert.Equal(2, ir.Interfaces.Count);
        Assert.Equal(2, ir.Mates.Count);
        Assert.Equal(3, ir.PlacementConstraints.Count);
        Assert.Contains(ir.Placements, x => x.Status == PlacementStatus.Anchored);
        Assert.Contains(ir.Placements, x => x.Status == PlacementStatus.Resolved && x.Transform is not null);
        var shaftPlacement = ir.Placements.Single(x => x.InstanceStableId.EndsWith(":BearingModule.Rotor.Shaft", StringComparison.Ordinal));
        var bearingPlacement = ir.Placements.Single(x => x.InstanceStableId.EndsWith(":BearingModule.FixedSupport.Bearing", StringComparison.Ordinal));
        Assert.Equal(-15, shaftPlacement.Transform!.Matrix[14], 8);
        Assert.Equal(-15, bearingPlacement.Transform!.Matrix[14], 8);

        var fit = Assert.Single(ir.FitResults);
        Assert.Equal(0.02, fit.NominalClearance, 8);
        Assert.Equal(0.002, fit.WorstCaseMinimum, 8);
        Assert.Equal(0.03, fit.WorstCaseMaximum, 8);

        var stack = Assert.Single(ir.ToleranceStackups);
        Assert.True(stack.Passed);
        Assert.Equal(6, stack.Contributions.Count);
        Assert.Equal(45, stack.Nominal, 8);
        Assert.Equal(44.92, stack.WorstCaseMinimum, 8);
        Assert.Equal(45.10, stack.WorstCaseMaximum, 8);
        Assert.Contains(stack.Contributions, x => x.Provenance == "BearingTable.6204.Width");
        Assert.Contains(stack.Contributions, x => x.Provenance == "SpacerTemplate.Width");
    }

    [Fact]
    public void RepeatedCompilation_IsDeterministicExceptPerformanceTelemetry()
    {
        var path = FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/AssemblyM0/bearing-module.firmament");
        var pipeline = new AssemblyM0Pipeline();
        var first = pipeline.CompileFile(path).Ir!;
        var second = pipeline.CompileFile(path).Ir!;
        var options = new JsonSerializerOptions { WriteIndented = true };
        Assert.Equal(JsonSerializer.Serialize(first, options), JsonSerializer.Serialize(second, options));
        Assert.Equal(first.Instances.Select(x => x.StableId), second.Instances.Select(x => x.StableId));
        Assert.Equal(first.Mates.Select(x => x.StableId), second.Mates.Select(x => x.StableId));
    }

    [Fact]
    public void SameDefinitionInstances_HaveDistinctInstanceScopedSemanticIdentities()
    {
        const string source = """
            Assembly Pair {
              <Assembly Pair>
                <Part Left = Bolt> Semantic Shank { Axis Axis = [0,0,0] -> [0,0,1]; } </Part>
                <Part Right = Bolt> Semantic Shank { Axis Axis = [0,0,0] -> [0,0,1]; } </Part>
              </Assembly>
              Anchor: Pair.Left.Shank;
            }
            """;
        var result = new AssemblyM0Compiler().Compile(new AssemblyM0Parser().Parse(source).Source!);
        var left = result.Ir!.Instances.Single(x => x.Path.ToString() == "Pair.Left");
        var right = result.Ir.Instances.Single(x => x.Path.ToString() == "Pair.Right");
        Assert.Equal(left.DefinitionIdentity, right.DefinitionIdentity);
        Assert.NotEqual(left.SemanticRoot.ExposedMembers["Shank"].StableIdentity, right.SemanticRoot.ExposedMembers["Shank"].StableIdentity);
    }

    [Fact]
    public void InterfaceParser_AdmitsNRoleRelationalIr()
    {
        const string source = """
            Interface ThreePlate {
              Role PlateA requires PlaneCapable;
              Role PlateB requires PlaneCapable;
              Role Fastener requires AxisCapable;
              Lower PlaneCoincident PlateA.Seat PlateB.Seat;
            }
            Assembly Three { <Assembly Three><Part P = Plate> Semantic S { Point P = [0,0,0]; } </Part></Assembly> Anchor: Three.P.S; }
            """;
        var parsed = new AssemblyM0Parser().Parse(source);
        Assert.True(parsed.IsSuccess, string.Join(Environment.NewLine, parsed.Diagnostics.Select(x => x.Message)));
        Assert.Equal(3, Assert.Single(parsed.Source!.Interfaces).Roles.Count);
    }

    [Fact]
    public void MissingRoleAndCapabilityMismatch_AreTypedDiagnostics()
    {
        const string source = """
            Interface PinHole { Role Pin requires AxisCapable; Role Hole requires AxisCapable; }
            Assembly Bad {
              <Assembly Bad><Part Thing = Block> Semantic Datum { Point Point = [0,0,0]; } </Part></Assembly>
              Anchor: Bad.Thing.Datum;
              Mate Broken: PinHole { Pin: Bad.Thing.Datum; }
            }
            """;
        var parsed = new AssemblyM0Parser().Parse(source);
        var result = new AssemblyM0Compiler().Compile(parsed.Source!);
        Assert.Contains(result.Diagnostics, x => x.Code == AssemblyM0Compiler.MissingRole);
        Assert.Contains(result.Diagnostics, x => x.Code == AssemblyM0Compiler.CapabilityMismatch);
    }

    [Fact]
    public void FailingStackup_ReportsTypedFailureAndPreservesFullChain()
    {
        var path = FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/AssemblyM0/bearing-module-failing.firmament");
        var result = new AssemblyM0Pipeline().CompileFile(path);
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, x => x.Code == AssemblyM0Compiler.ToleranceAssertionFailure);
        var stack = Assert.Single(result.Ir!.ToleranceStackups);
        Assert.False(stack.Passed);
        Assert.Equal(5, stack.Contributions.Count);
        Assert.Equal(44.92, stack.WorstCaseMinimum, 8);
    }

    [Fact]
    public void AxisOnlyInterfaceWithoutAdmittedFreedom_IsUnderconstrained()
    {
        const string source = """
            Interface AxisOnly {
              Role A requires AxisCapable;
              Role B requires AxisCapable;
              Lower AxisCoincident A.Axis B.Axis;
            }
            Assembly Free {
              <Assembly Free>
                <Part A = Pin> Semantic Joint { Axis Axis = [0,0,0] -> [0,0,1]; } </Part>
                <Part B = Hole> Semantic Joint { Axis Axis = [1,0,0] -> [0,0,1]; } </Part>
              </Assembly>
              Anchor: Free.A.Joint;
              Mate M: AxisOnly { A: Free.A.Joint; B: Free.B.Joint; }
            }
            """;
        var result = new AssemblyM0Compiler().Compile(new AssemblyM0Parser().Parse(source).Source!);
        Assert.Contains(result.Diagnostics, x => x.Code == AssemblyM0Compiler.Underconstrained);
        var placement = result.Ir!.Placements.Single(x => x.InstanceStableId.EndsWith(":Free.B", StringComparison.Ordinal));
        Assert.Equal(["along-axis"], placement.FreeTranslations);
        Assert.Equal(["about-axis"], placement.FreeRotations);
    }

    [Fact]
    public void ConflictingMates_AreOverconstrainedWithTypedDiagnostic()
    {
        const string source = """
            Interface AxisJoin {
              Role Moving requires AxisCapable;
              Role Fixed requires AxisCapable;
              Lower AxisCoincident Moving.AxisA Fixed.AxisA;
              Lower AxisCoincident Moving.AxisB Fixed.AxisB;
              Allow translation:along-axis;
              Allow rotation:about-axis;
            }
            Assembly Conflict {
              <Assembly Conflict>
                <Part Moving = Pin> Semantic Joint { Axis AxisA = [0,0,0] -> [0,0,1]; Axis AxisB = [0,0,0] -> [0,0,1]; } </Part>
                <Part Fixed = Block> Semantic Joint { Axis AxisA = [0,0,0] -> [0,0,1]; Axis AxisB = [10,0,0] -> [0,0,1]; } </Part>
              </Assembly>
              Anchor: Conflict.Fixed.Joint;
              Mate Broken: AxisJoin { Moving: Conflict.Moving.Joint; Fixed: Conflict.Fixed.Joint; }
            }
            """;
        var result = new AssemblyM0Compiler().Compile(new AssemblyM0Parser().Parse(source).Source!);
        Assert.Contains(result.Diagnostics, x => x.Code == AssemblyM0Compiler.Overconstrained);
        Assert.Contains(result.Ir!.Placements, x => x.Status == PlacementStatus.Overconstrained);
    }

    [Fact]
    public void ForgeProducedSemanticValue_ParticipatesWithoutOriginSpecificMateLogic()
    {
        static SemanticValue Axis(string id, string origin) => new(id, new("Concept"), [new AxisCapability()],
            [new ExactAxisBinding(0, 0, 0, 0, 0, 1, id + ":axis")],
            [new SemanticValue(id + ":Axis", new("Axis"), [new AxisCapability()], [new ExactAxisBinding(0, 0, 0, 0, 0, 1, id + ":axis")], exposedName: "Axis")],
            [new(origin, id, "producer-provenance")], exposedName: "Joint");
        var root = new AssemblyMemberSource("ForgeAssembly", AssemblyInstanceKind.Assembly, "ForgeAssembly",
            [new("Native", AssemblyInstanceKind.Part, "NativePart", [], [Axis("native:joint", "Firmament")]),
             new("Extension", AssemblyInstanceKind.Part, "ForgePart", [], [Axis("forge:joint", "Forge")])], []);
        var iface = new InterfaceDefinition("interface:AxisPair", "AxisPair",
            [new("A", ["AxisCapable"]), new("B", ["AxisCapable"])],
            [new(PlacementConstraintKind.AxisCoincident, "A", "Axis", "B", "Axis")],
            AdmittedFreeMotions: ["translation:along-axis", "rotation:about-axis"]);
        var source = new AssemblySource("ForgeAssembly", root, [iface],
            [new("ForgeMate", "AxisPair", [new("A", AssemblyPath.Parse("ForgeAssembly.Native.Joint")), new("B", AssemblyPath.Parse("ForgeAssembly.Extension.Joint"))])],
            AssemblyPath.Parse("ForgeAssembly.Native.Joint"), [], [], "forge-test");
        var result = new AssemblyM0Compiler().Compile(source);
        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics.Select(x => x.Message)));
        Assert.Equal("valid", Assert.Single(result.Ir!.Mates).ValidationStatus);
        Assert.Contains(result.Ir.Instances.Single(x => x.Path.ToString() == "ForgeAssembly.Extension").SemanticRoot.ExposedMembers["Joint"].Provenance,
            x => x.Stage == "Forge");
    }

    [Fact]
    public void LegacyFirmasm_LoadsWithDeprecationDiagnostic()
    {
        var path = FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Assembly/LegacyImports/examples/occt-nut-bolt/nut-bolt-assembly.firmasm");
        var result = new FirmasmManifestLoader().LoadFromFile(path);
        Assert.True(result.IsSuccess);
        Assert.Contains(result.Diagnostics, x => x.Message.StartsWith("legacy-firmasm-syntax:", StringComparison.Ordinal));
    }
}
