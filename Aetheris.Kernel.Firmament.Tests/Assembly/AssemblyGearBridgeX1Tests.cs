using Aetheris.Kernel.Firmament.Assembly;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Firmament.Tests.Assembly;

public sealed class AssemblyGearBridgeX1Tests
{
    [Theory]
    [InlineData("exposed-gear-port")]
    [InlineData("gear-between-subassemblies")]
    [InlineData("nested-register-gear")]
    [InlineData("three-gear-train-hierarchical")]
    [InlineData("bevel-subassembly-pair")]
    public void CanonicalHierarchicalGearFixturesQualify(string name)
    {
        var path = FirmamentCorpusHarness.ResolveFixtureFullPath($"fixtures/Canonical/AssemblyInterfaces/{name}.firmament");
        var result = new AssemblyM0Pipeline().CompileFile(path);
        Assert.True(result.IsSuccess, Evidence(result));
        Assert.All(result.Ir!.Mates, mate => Assert.Equal("valid", mate.ValidationStatus));
    }

    [Theory]
    [InlineData("exposed-wrong-endpoint-type", "assembly-role-capability-mismatch")]
    [InlineData("private-gear-access", "assembly-internal-member-hidden")]
    [InlineData("gear-module-mismatch", "firmament-gear-interface-incompatible")]
    [InlineData("gear-pressure-angle-mismatch", "firmament-gear-interface-incompatible")]
    [InlineData("gear-center-distance-mismatch", "firmament-gear-interface-incompatible")]
    [InlineData("gear-axis-mismatch", "firmament-gear-interface-incompatible")]
    [InlineData("invalid-nested-gear-path", "assembly-expose-unresolved-path")]
    public void InvalidHierarchicalGearFixturesFailTyped(string name, string code)
    {
        var path = FirmamentCorpusHarness.ResolveFixtureFullPath($"fixtures/Invalid/AssemblyInterfaces/{name}.firmament");
        var result = new AssemblyM0Pipeline().CompileFile(path);
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, item => item.Code == code);
    }

    [Fact]
    public void ExposedGearPortsReuseGearAuthorityAcrossSharedSubassemblies()
    {
        var path = FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/AssemblyInterfaces/exposed-gear-port.firmament");
        var result = new AssemblyM0Pipeline().CompileFile(path);

        Assert.True(result.IsSuccess, Evidence(result));
        var transfer = Assert.Single(result.Ir!.Mates, mate => mate.Name == "Transfer");
        Assert.Equal("valid", transfer.ValidationStatus);
        Assert.NotNull(transfer.GearResult);
        Assert.Equal("compatible", transfer.GearResult!.CompatibilityStatus);
        Assert.Equal(60d, transfer.GearResult.ExpectedCenterDistanceMm);
        Assert.Equal(60d, transfer.GearResult.ActualCenterDistanceMm, 5);
        Assert.Equal(2d, transfer.GearResult.Ratio);
        Assert.Equal(-1, transfer.GearResult.RotationSign);
        Assert.Equal("Register/Digit0/Output", transfer.GearResult.A.ExposedPortPath);
        Assert.Equal("Register/Digit0/OutputGear", transfer.GearResult.A.HierarchicalPath);
        Assert.Equal("Register/Digit1/Input", transfer.GearResult.B.ExposedPortPath);
        Assert.Equal(4.5d, transfer.GearResult.A.PhaseDegrees);
        Assert.Equal(2, result.Ir.Instances.Count(instance => instance.DefinitionIdentity == "DigitModule"));
        Assert.Single(result.Ir.AssemblyDefinitions!, definition => definition.DefinitionIdentity == "DigitModule");
    }

    [Fact]
    public void ExistingGearCompatibilityRejectsHierarchicalModulePressureCenterAxisAndTypeMismatches()
    {
        var module = CompilePair("Module: 1.5mm Teeth: 20 PressureAngle: 20deg", 35);
        Assert.Contains(module.Diagnostics, item => item.Code == "firmament-gear-interface-incompatible" && item.Message.Contains("module-mismatch", StringComparison.Ordinal));

        var pressure = CompilePair("Module: 2mm Teeth: 20 PressureAngle: 25deg", 40);
        Assert.Contains(pressure.Diagnostics, item => item.Code == "firmament-gear-interface-incompatible" && item.Message.Contains("pressure-angle-mismatch", StringComparison.Ordinal));

        var center = CompilePair("Module: 2mm Teeth: 20 PressureAngle: 20deg", 41);
        Assert.Contains(center.Diagnostics, item => item.Code == "firmament-gear-interface-incompatible" && item.Message.Contains("center-distance-mismatch", StringComparison.Ordinal));

        var axis = CompilePair("Module: 2mm Teeth: 20 PressureAngle: 20deg", 40,
            "1,0,0,0, 0,0,1,0, 0,-1,0,0, 40,0,0,1");
        Assert.Contains(axis.Diagnostics, item => item.Code == "firmament-gear-interface-incompatible" && item.Message.Contains("axis-relation-mismatch", StringComparison.Ordinal));

        const string wrongType = """
            Units: mm
            SpurGear G { Module: 2mm Teeth: 20 PressureAngle: 20deg FaceWidth: 6mm BoreDiameter: 5mm }
            Assembly Wrong {
              <Assembly Wrong><Part Gear = G></Part><Part Shaft = Block> Semantic Port { Axis Axis = [0,0,0] -> [0,0,1]; } </Part></Assembly>
              Anchor: Wrong.Gear;
              Interface<Gear> Invalid { A: Wrong.Gear; B: Wrong.Shaft.Port; }
            }
            """;
        var typed = new AssemblyM0Compiler().Compile(new AssemblyM0Parser().Parse(wrongType).Source!);
        Assert.Contains(typed.Diagnostics, item => item.Code == AssemblyM0Compiler.CapabilityMismatch && item.Message.Contains("GearCapable", StringComparison.Ordinal));

        var axialSource = wrongType.Replace("Interface<Gear> Invalid", "Interface<Axial> Invalid", StringComparison.Ordinal);
        var coerced = new AssemblyM0Compiler().Compile(new AssemblyM0Parser().Parse(axialSource).Source!);
        Assert.Contains(coerced.Diagnostics, item => item.Code == AssemblyM0Compiler.CapabilityMismatch && item.Message.Contains("not implicitly coerced", StringComparison.Ordinal));
    }

    [Fact]
    public void GearAxisAndPhaseSurviveTwoHierarchyLevels()
    {
        const string source = """
            Units: mm
            SpurGear G { Module: 2mm Teeth: 20 PressureAngle: 20deg FaceWidth: 6mm BoreDiameter: 5mm Phase: 9deg }
            Subassembly Digit {
              <Assembly Digit><Part Wheel = G></Part></Assembly>
              Anchor: Digit.Wheel;
              Expose { Gear Port = Wheel; }
            }
            Subassembly Register {
              <Assembly Register><Assembly Digit = Digit></Assembly></Assembly>
              Anchor: Register.Digit.Port;
              Expose { Gear Port = Digit.Port; }
            }
            Assembly Machine {
              <Assembly Machine>
                <Assembly A = Register></Assembly>
                <Assembly B = Register>Placement ImportedOccurrence = [1,0,0,0, 0,1,0,0, 0,0,1,0, 40,0,0,1];</Assembly>
              </Assembly>
              Anchor: Machine.A.Port;
              Interface<Gear> Transfer { A: Machine.A.Port; B: Machine.B.Port; }
            }
            """;
        var result = new AssemblyM0Compiler().Compile(new AssemblyM0Parser().Parse(source).Source!);
        Assert.True(result.IsSuccess, Evidence(result));
        var gear = Assert.Single(result.Ir!.Mates).GearResult!;
        Assert.Equal("Machine/A/Port", gear.A.ExposedPortPath);
        Assert.Equal("Machine/A/Digit/Wheel", gear.A.HierarchicalPath);
        Assert.Equal(9d, gear.A.PhaseDegrees);
        Assert.Equal(new[] { 0d, 0d, 1d }, gear.A.Axis);
        Assert.Equal(2, result.Ir.AssemblyDefinitions!.Count);
    }

    [Fact]
    public void ExposedGearAxisUsesNestedOccurrenceRotation()
    {
        const string source = """
            Units: mm
            SpurGear G { Module: 2mm Teeth: 20 PressureAngle: 20deg FaceWidth: 6mm BoreDiameter: 5mm }
            Subassembly Cell { <Assembly Cell><Part Gear = G></Part></Assembly> Anchor: Cell.Gear; Expose { Gear Port = Gear; } }
            Assembly Rotated {
              <Assembly Rotated>
                <Assembly A = Cell>Placement ImportedOccurrence = [1,0,0,0, 0,0,1,0, 0,-1,0,0, 0,0,0,1];</Assembly>
                <Assembly B = Cell>Placement ImportedOccurrence = [1,0,0,0, 0,0,1,0, 0,-1,0,0, 40,0,0,1];</Assembly>
              </Assembly>
              Interface<Gear> Mesh { A: Rotated.A.Port; B: Rotated.B.Port; }
            }
            """;
        var result = new AssemblyM0Compiler().Compile(new AssemblyM0Parser().Parse(source).Source!);
        Assert.True(result.IsSuccess, Evidence(result));
        var gear = Assert.Single(result.Ir!.Mates).GearResult!;
        Assert.Equal(new[] { 0d, -1d, 0d }, gear.A.Axis.Select(value => Math.Round(value, 6)).ToArray());
        Assert.Equal(40d, gear.ActualCenterDistanceMm, 5);
    }

    [Fact]
    public void OneGearDefinitionBacksOneHundredOccurrenceSpecificPorts()
    {
        var occurrences = string.Join(Environment.NewLine, Enumerable.Range(0, 100).Select(index =>
            $"<Assembly D{index} = Digit>Placement ImportedOccurrence = [1,0,0,0, 0,1,0,0, 0,0,1,0, {index * 50},0,0,1];</Assembly>"));
        var source = $$"""
            Units: mm
            SpurGear G { Module: 2mm Teeth: 20 PressureAngle: 20deg FaceWidth: 6mm BoreDiameter: 5mm }
            Subassembly Digit { <Assembly Digit><Part Wheel = G></Part></Assembly> Anchor: Digit.Wheel; Expose { Gear Port = Wheel; } }
            Assembly Stress { <Assembly Stress>{{occurrences}}</Assembly> Anchor: Stress.D0.Port; }
            """;
        var first = new AssemblyM0Compiler().Compile(new AssemblyM0Parser().Parse(source).Source!);
        var second = new AssemblyM0Compiler().Compile(new AssemblyM0Parser().Parse(source).Source!);
        Assert.True(first.IsSuccess, Evidence(first));
        Assert.Single(first.Ir!.AssemblyDefinitions!);
        Assert.Equal(100, first.Ir.Instances.Count(item => item.DefinitionIdentity == "Digit"));
        Assert.Equal(first.Ir.Instances.Select(item => item.StableId), second.Ir!.Instances.Select(item => item.StableId));
        Assert.All(first.Ir.Instances.Where(item => item.DefinitionIdentity == "Digit"), item =>
            Assert.True(item.SemanticRoot.ExposedMembers["Port"].Capabilities.Supports<Aetheris.Semantics.GearCapability>()));
    }

    [Fact]
    public void GearGeometryUsesOrdinaryWholeTreeAp242LoweringDeterministically()
    {
        var path = FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/AssemblyInterfaces/exposed-gear-port.firmament");
        var first = new AssemblyM1Pipeline().CompileFile(path);
        var second = new AssemblyM1Pipeline().CompileFile(path);
        Assert.True(first.IsSuccess, Evidence(first));
        Assert.True(second.IsSuccess, Evidence(second));
        Assert.Equal(2, first.Geometry!.Artifact.Definitions.Count);
        Assert.Equal(4, first.Geometry.Artifact.Instances.Count);
        Assert.Equal(first.Geometry.Artifact.DeterministicSha256, second.Geometry!.Artifact.DeterministicSha256);

        var step = AssemblyIrAp242Exporter.Export(first);
        Assert.True(step.IsSuccess, string.Join(Environment.NewLine, step.Diagnostics.Select(item => item.Message)));
        var imported = Step242AssemblyImporter.Import(step.Value);
        Assert.True(imported.IsSuccess, string.Join(Environment.NewLine, imported.Diagnostics.Select(item => item.Message)));
        Assert.True(imported.Value.Occurrences.Count >= 4);
        Assert.DoesNotContain(".step", File.ReadAllText(path), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PrivateGearCannotBeReachedAndAnExposedAliasIsTheSameAuthority()
    {
        const string source = """
            Units: mm
            SpurGear G { Module: 2mm Teeth: 20 PressureAngle: 20deg FaceWidth: 6mm BoreDiameter: 5mm }
            Subassembly Cell { <Assembly Cell><Part Hidden = G></Part></Assembly> Anchor: Cell.Hidden; }
            Assembly Machine {
              <Assembly Machine><Assembly A = Cell></Assembly><Assembly B = Cell>Placement ImportedOccurrence = [1,0,0,0, 0,1,0,0, 0,0,1,0, 40,0,0,1];</Assembly></Assembly>
              Anchor: Machine.A;
              Interface<Gear> Illegal { A: Machine.A.Hidden; B: Machine.B.Hidden; }
            }
            """;
        var result = new AssemblyM0Compiler().Compile(new AssemblyM0Parser().Parse(source).Source!);
        Assert.Contains(result.Diagnostics, item => item.Code == "assembly-internal-member-hidden");

        var exposed = new AssemblyM0Pipeline().CompileFile(FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/AssemblyInterfaces/exposed-gear-port.firmament"));
        var transfer = Assert.Single(exposed.Ir!.Mates, mate => mate.Name == "Transfer").GearResult!;
        var local = Assert.Single(exposed.Ir.AssemblyDefinitions!).LocalMates.Single(mate => mate.Name == "InternalMesh").GearResult!;
        Assert.Equal(local.B.DefinitionIdentity, transfer.A.DefinitionIdentity);
        Assert.Equal(local.B.PhaseDegrees, transfer.A.PhaseDegrees);
    }

    [Fact]
    public void InternalSpurBevelMiterAndRatchetRemainOwnedByTheExistingEvaluator()
    {
        var internalPair = CompileFamilies(
            "SpurGear A { Module: 1mm Teeth: 20 PressureAngle: 20deg FaceWidth: 6mm BoreDiameter: 5mm }",
            "InternalSpurGear B { Module: 1mm Teeth: 60 PressureAngle: 20deg FaceWidth: 6mm OutsideDiameter: 65mm Phase: 3deg }",
            "1,0,0,0, 0,1,0,0, 0,0,1,0, 20,0,0,1", "");
        Assert.Equal("ExternalInternal", Assert.Single(internalPair.Ir!.Mates).GearResult!.Kind);
        Assert.Equal(1, Assert.Single(internalPair.Ir.Mates).GearResult!.RotationSign);

        var bevel = CompileFamilies(
            "BevelGear A { Module: 1mm Teeth: 20 PressureAngle: 20deg FaceWidth: 5mm BoreDiameter: 5mm PitchConeAngle: 26.565051177deg }",
            "BevelGear B { Module: 1mm Teeth: 40 PressureAngle: 20deg FaceWidth: 5mm BoreDiameter: 8mm PitchConeAngle: 63.434948823deg }",
            "1,0,0,0, 0,0,1,0, 0,-1,0,0, 0,0,0,1", "ShaftAngle: 90deg");
        Assert.True(bevel.IsSuccess, Evidence(bevel));
        Assert.Equal("Bevel", Assert.Single(bevel.Ir!.Mates).GearResult!.Kind);

        var miter = CompileFamilies(
            "MiterGear A { Module: 1mm Teeth: 24 PressureAngle: 20deg FaceWidth: 5mm BoreDiameter: 5mm }",
            "MiterGear B { Module: 1mm Teeth: 24 PressureAngle: 20deg FaceWidth: 5mm BoreDiameter: 5mm }",
            "1,0,0,0, 0,0,1,0, 0,-1,0,0, 0,0,0,1", "ShaftAngle: 90deg");
        Assert.True(miter.IsSuccess, Evidence(miter));

        var ratchet = CompileFamilies(
            "RatchetGear A { Teeth: 60 OutsideDiameter: 40mm FaceWidth: 6mm BoreDiameter: 10mm DriveFaceAngle: 0deg }",
            "Pawl B { Width: 16mm Length: 38mm Thickness: 6mm PivotDiameter: 5mm NoseLength: 6mm EngagementAngle: 15deg }",
            "1,0,0,0, 0,1,0,0, 0,0,1,0, 0,0,0,1", "EngagementPhase: 2deg AllowedDirection: Clockwise");
        Assert.True(ratchet.IsSuccess, Evidence(ratchet));
        Assert.Equal("RatchetPawl", Assert.Single(ratchet.Ir!.Mates).GearResult!.Kind);
    }

    private static AssemblyCompilationResult CompilePair(string secondParameters, double x, string? matrix = null)
    {
        matrix ??= $"1,0,0,0, 0,1,0,0, 0,0,1,0, {x},0,0,1";
        var source = $$"""
            Units: mm
            SpurGear A { Module: 2mm Teeth: 20 PressureAngle: 20deg FaceWidth: 6mm BoreDiameter: 5mm }
            SpurGear B { {{secondParameters}} FaceWidth: 6mm BoreDiameter: 5mm }
            Assembly Pair {
              <Assembly Pair><Part A = A></Part><Part B = B>Placement ImportedOccurrence = [{{matrix}}];</Part></Assembly>
              Anchor: Pair.A;
              Interface<Gear> Mesh { A: Pair.A; B: Pair.B; }
            }
            """;
        return new AssemblyM0Compiler().Compile(new AssemblyM0Parser().Parse(source).Source!);
    }

    private static AssemblyCompilationResult CompileFamilies(string first, string second, string matrix, string options)
    {
        var source = $$"""
            Units: mm
            {{first}}
            {{second}}
            Assembly Pair {
              <Assembly Pair><Part A = A></Part><Part B = B>Placement ImportedOccurrence = [{{matrix}}];</Part></Assembly>
              Anchor: Pair.A;
              Interface<Gear> Mesh { A: Pair.A; B: Pair.B; {{options}} }
            }
            """;
        return new AssemblyM0Compiler().Compile(new AssemblyM0Parser().Parse(source).Source!);
    }

    private static string Evidence(AssemblyCompilationResult result) =>
        string.Join(Environment.NewLine, result.Diagnostics.Select(item => item.Code + ": " + item.Message));

    private static string Evidence(AssemblyM1CompilationResult result) =>
        string.Join(Environment.NewLine, result.Diagnostics.Select(item => item.Code + ": " + item.Message));
}
