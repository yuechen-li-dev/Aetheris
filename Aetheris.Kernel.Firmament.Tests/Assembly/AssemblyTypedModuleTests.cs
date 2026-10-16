using Aetheris.Kernel.Firmament.Assembly;

namespace Aetheris.Kernel.Firmament.Tests.Assembly;

public sealed class AssemblyTypedModuleTests
{
    private const string Config = "Record UnitConfig { x: Length } Static standard: UnitConfig = UnitConfig { x: 10mm }";
    private const string Unit = """
        include "config";
        Inventory Record Parts { stock: Part = Firmament("stock").Stock }
        Template<config: UnitConfig>
        Subassembly Unit {
          <Assembly Unit>
            <Part Stock = Parts.stock> Placement { from: Origin; to: World; translateLocal: [config.x,0mm,0mm]; } </Part>
          </Assembly>
          anchor: Unit;
          expose { Semantic Mount = Stock.Mount; }
        }
        """;
    private const string Root = """
        include "config";
        Inventory Record Units { unit: Subassembly = Firmament("unit").Unit }
        Static configs: Set<UnitConfig> {
          C1 => standard
          C2 => standard with { x: 40mm }
        }
        Assembly Engine {
          <Assembly Engine>
            Pattern Cylinders over configs { config => <Assembly Cylinder = Units.unit<config: config>> Placement { from: Origin; to: World; } </Assembly> }
          </Assembly>
          anchor: Engine;
        }
        """;
    private static FirmamentProjectSnapshot Project(string root = Root, string? unit = null, string? config = null) => new("main.firmasm", new Dictionary<string,string> {
        ["main.firmasm"] = root, ["unit.firmament"] = unit ?? Unit, ["config.firmament"] = config ?? Config,
        ["stock.firmament"] = "Model Stock { units: mm Box stock { size: [10mm,20mm,3mm] } expose { Semantic Mount { Axis Axis = [0,0,0] -> [0,0,1]; DatumFrame Frame = [0,0,0] x [1,0,0] y [0,1,0] z [0,0,1]; } } }"
    });
    [Fact]
    public void InventoryPatternUsesCheckedRecordDerivationsWithoutSharingMutableState()
    {
        var result = new AssemblyM1Pipeline().CompileProject(Project());
        Assert.True(result.IsSuccess, string.Join("\n",result.Diagnostics.Select(d => d.Code + ": " + d.Message)));
        Assert.Equal(2,result.Geometry!.InstanceBodies.Count);
        Assert.Single(result.Geometry.DefinitionBodies);
        var step = AssemblyIrAp242Exporter.Export(result);
        Assert.True(step.IsSuccess,string.Join("\n",step.Diagnostics.Select(d => d.Message)));
        var instances = result.Ir!.Instances.Where(i => i.Kind == AssemblyInstanceKind.Part).ToArray();
        Assert.Equal(new[] {10d,40d},instances.Select(i => i.ResolvedTransform!.Matrix[12]).Order().ToArray());
        Assert.All(instances,i => Assert.Contains("Engine.Cylinders.C",i.Path.ToString()));
    }
    [Fact]
    public void PatternBlockOwnsScopedConceptsInterfacesAndMates()
    {
        var root = Root.Replace("config => <Assembly Cylinder = Units.unit<config: config>> Placement { from: Origin; to: World; } </Assembly>", """
            config => {
              <Assembly Cylinder = Units.unit<config: config>></Assembly>
              Concept Struct Layout {
                Axis Base { origin: [config.x,0mm,0mm]; direction: [0,0,1]; reference: [1,0,0]; }
              }
              Interface<Fixed> Installation { datum: Layout.Base; members: [Cylinder.Mount]; }
              Mate Install: Installation { member: Cylinder.Mount; at: 0mm; clocking: 0deg; }
            }
            """);
        var result = new AssemblyM1Pipeline().CompileProject(Project(root));
        Assert.True(result.IsSuccess, string.Join("\n",result.Diagnostics.Select(d => d.Code + ": " + d.Message)));
        Assert.Equal(2,result.Geometry!.InstanceBodies.Count);
        // The exported mount is at the Stock's config.x; seating it at config.x
        // places the whole specialized unit at its original local origin.
        Assert.Equal(new[] {10d,40d},result.Ir!.Instances.Where(i => i.Kind == AssemblyInstanceKind.Part)
            .Select(i => i.ResolvedTransform!.Matrix[12]).Order().ToArray());
    }

    [Fact]
    public void ReorderingKeysPreservesOccurrenceIdentityAndPlacement()
    {
        var first = new AssemblyM1Pipeline().CompileProject(Project());
        var reordered = Root.Replace("C1 => standard\n  C2 => standard with { x: 40mm }", "C2 => standard with { x: 40mm }\n  C1 => standard");
        Assert.NotEqual(Root,reordered);
        var second = new AssemblyM1Pipeline().CompileProject(Project(reordered));
        Assert.True(first.IsSuccess); Assert.True(second.IsSuccess);
        var byPath = first.Ir!.Instances.Where(i => i.Kind == AssemblyInstanceKind.Part).ToDictionary(i => i.Path.ToString());
        foreach (var instance in second.Ir!.Instances.Where(i => i.Kind == AssemblyInstanceKind.Part)) {
            Assert.Equal(byPath[instance.Path.ToString()].StableId,instance.StableId);
            Assert.Equal(byPath[instance.Path.ToString()].ResolvedTransform!.Matrix,instance.ResolvedTransform!.Matrix);
        }
    }
    [Fact]
    public void UnknownPatternIntentCannotDisappear()
    {
        var source = Root.Replace("config => <Assembly", "config => { Fillet Ignored { radius: 2mm } <Assembly")
            .Replace("</Assembly> }", "</Assembly> } }");
        var result = new AssemblyM1Pipeline().CompileProject(Project(source));
        Assert.False(result.IsSuccess); Assert.Null(result.Geometry);
        Assert.Contains(result.Diagnostics,d => d.Message.Contains("assembly-pattern-body-invalid"));
    }

    [Fact]
    public void ModulePrivateValueCannotCaptureTheCallerRecordArgument()
    {
        var unit = Unit.Replace("Inventory Record Parts", "Static config: UnitConfig = UnitConfig { x: 999mm }\nInventory Record Parts");
        var result = new AssemblyM1Pipeline().CompileProject(Project(unit: unit));
        Assert.True(result.IsSuccess,string.Join("\n",result.Diagnostics.Select(d => d.Message)));
        Assert.Equal(new[] {10d,40d},result.Ir!.Instances.Where(i => i.Kind == AssemblyInstanceKind.Part)
            .Select(i => i.ResolvedTransform!.Matrix[12]).Order().ToArray());
    }
    [Fact]
    public void DuplicateDeclarationsInPatternBlockAreDiagnosed()
    {
        var root = Root.Replace("config => <Assembly", "config => { <Assembly")
            .Replace("</Assembly> }", "</Assembly> <Assembly Cylinder></Assembly> } }");
        var result = new AssemblyM1Pipeline().CompileProject(Project(root));
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics,d => d.Message.Contains("assembly-pattern-duplicate-declaration"));
    }

    [Fact]
    public void NestedRecordBundleCrossesInventoryWithoutLosingItsBoundLeaves()
    {
        var config = "Record Rest { x: Length } Record UnitConfig { rest: Rest } Static rest: Rest = Rest { x: 10mm } Static other: Rest = rest with { x: 40mm } Static standard: UnitConfig = UnitConfig { rest: rest }";
        var root = Root.Replace("x: 40mm", "rest: other");
        var unit = Unit.Replace("config.x", "config.rest.x");
        var result = new AssemblyM1Pipeline().CompileProject(Project(root,unit,config));
        Assert.True(result.IsSuccess,string.Join("\n",result.Diagnostics.Select(d => d.Message)));
        Assert.Equal(new[] {10d,40d},result.Ir!.Instances.Where(i => i.Kind == AssemblyInstanceKind.Part)
            .Select(i => i.ResolvedTransform!.Matrix[12]).Order().ToArray());
    }

    [Fact]
    public void DeferredPartMaterializationCannotCaptureAPrivateRecordWithTheCallerName()
    {
        const string root = "include \"config\"; Inventory Record Parts { stock: Part = Firmament(\"unit\").Stock } Assembly Engine { <Assembly Engine><Part Stock = Parts.stock<config: standard>> Placement { from: Origin; to: World; } </Part></Assembly> anchor: Engine; }";
        const string unit = "include \"config\"; Template<config: UnitConfig> Struct Stock { Box stock { size: [config.x,20mm,3mm] } }";
        var result = new AssemblyM1Pipeline().CompileProject(Project(root,unit));
        Assert.False(result.IsSuccess); Assert.Null(result.Geometry);
        Assert.Contains(result.Diagnostics,d => d.Code == "firmament-inventory-part-record-argument-unsupported" && d.SourceSpan?.Source == "main.firmasm");
    }

    [Theory]
    [InlineData("x: 40mm", "x: 40deg")]
    [InlineData("x: 40mm", "typo: 40mm")]
    public void InvalidWithNeverProducesAnAssembly(string before,string after)
    {
        var result = new AssemblyM1Pipeline().CompileProject(Project(Root.Replace(before,after)));
        Assert.False(result.IsSuccess); Assert.Null(result.Geometry);
        Assert.Contains(result.Diagnostics,d => d.Message.Contains("with-") && d.SourceSpan?.Source == "main.firmasm" && d.SourceSpan.Length > 0 && d.LocationNote is not null);
    }
}

