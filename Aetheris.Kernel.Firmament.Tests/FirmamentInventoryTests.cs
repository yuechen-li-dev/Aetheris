using Aetheris.Kernel.Firmament.Assembly;
using Aetheris.Kernel.Firmament.Scene;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentInventoryTests
{
    private const string Bracket = "Record Spec { width: Length } Static spec: Spec = Spec { width: 30mm } Model Bracket { units: mm Box stock { size: [spec.width,20mm,6mm] } }";
    private const string Root = """
        Inventory Record Components { bracket: Part = Firmament("parts/bracket").Bracket }
        Assembly Machine {
          <Assembly Machine>
            <Part Left = Components.bracket> Placement { from: Origin; to: World; } </Part>
            <Part Right = Components.bracket> Placement { from: Origin; to: World; translateLocal: [50mm,0mm,0mm]; } </Part>
          </Assembly>
          Anchor: Machine;
        }
        """;
    private static FirmamentProjectSnapshot Project(string root = Root, params KeyValuePair<string,string>[] extra) =>
        new("main.firmasm", new[] { KeyValuePair.Create("main.firmasm",root),KeyValuePair.Create("parts/bracket.firmament",Bracket) }.Concat(extra));
    private static void Accepted(AssemblyM1CompilationResult result) => Assert.True(result.IsSuccess,string.Join("\n",result.Diagnostics.Select(d => d.Code+": "+d.Message)));

    [Fact]
    public void ExtensionlessPartUsesOneDefinitionAndPreservesConcreteGeometry()
    {
        var result=new AssemblyM1Pipeline().CompileProject(Project()); Accepted(result);
        var bodies=result.Geometry!.InstanceBodies.Values.ToArray(); Assert.Equal(2,bodies.Length);
        Assert.Single(result.Geometry.DefinitionBodies);
        var metric=Assert.Single(result.Geometry.Artifact.Definitions).Metrics;
        Assert.Equal(new[] { -15d,-10,0 },metric.Minimum);
        Assert.Equal(new[] { 15d,10,6 },metric.Maximum);
        Assert.Contains(result.Ir!.SourceDependencies!,d => d.Path=="parts/bracket.firmament");
    }

    [Fact]
    public void IdenticallyNamedExportsKeepTheirModuleSchemasAndPrivateValues()
    {
        var root=Root.Replace("bracket: Part = Firmament(\"parts/bracket\").Bracket", "bracket: Part = Firmament(\"parts/bracket\").Bracket other: Part = Firmament(\"parts/other\").Bracket")
            .Replace("Right = Components.bracket","Right = Components.other");
        var result=new AssemblyM1Pipeline().CompileProject(Project(root,
            KeyValuePair.Create("parts/other.firmament","Record Spec { width: Length thickness: Length } Static spec: Spec = Spec { width: 40mm thickness: 9mm } Model Bracket { units: mm Box stock { size: [spec.width,20mm,spec.thickness] } }")));
        Accepted(result); Assert.Equal(2,result.Geometry!.DefinitionBodies.Count);
        Assert.Contains(result.Geometry.Artifact.Definitions,d => d.Metrics.Maximum[0]==20 && d.Metrics.Maximum[2]==9);
    }

    [Theory]
    [InlineData("bracket: Scene = Firmament(\"parts/bracket\").Bracket","firmament-inventory-kind-mismatch")]
    [InlineData("bracket: Part = Firmament(\"parts/bracket\").Missing","firmament-inventory-export-unresolved")]
    [InlineData("bracket: Part = Firmament(\"parts/missing\").Bracket","assembly-include-file-not-found")]
    [InlineData("bracket: Part = Unknown(\"parts/bracket\")","firmament-inventory-resource-invalid")]
    [InlineData("bracket: Part = Step(\"bearing\")","assembly-profile-external-resource-unsupported")]
    [InlineData("bracket: Part = Firmament(\"parts/bracket\").Bracket bracket: Part = Firmament(\"parts/bracket\").Bracket","firmament-inventory-duplicate-field")]
    public void InvalidEntriesRejectAtTheirAuthoredField(string field,string code)
    {
        var source="Inventory Record Components { "+field+" } Assembly Machine { <Assembly Machine></Assembly> Anchor: Machine; }";
        var result=new AssemblyM1Pipeline().CompileProject(Project(source));
        Assert.False(result.IsSuccess); Assert.Null(result.Geometry);
        Assert.Contains(result.Diagnostics,d => d.Code==code && d.SourceSpan?.Source=="main.firmasm" && d.SourceSpan.Length>0);
    }

    [Fact]
    public void AmbiguousExtensionlessLocatorRejectsRatherThanChoosingAFile()
    {
        var result=new AssemblyM1Pipeline().CompileProject(Project(Root,KeyValuePair.Create("parts/bracket.firmasm",Bracket)));
        Assert.False(result.IsSuccess); Assert.Contains(result.Diagnostics,d => d.Code=="firmament-module-locator-ambiguous");
    }

    [Fact]
    public void SubassemblyAndAssemblyImportsRetainTheirLocallySolvedProductTrees()
    {
        foreach(var kind in new[] {"Subassembly","Assembly"})
        {
            var child=Root.Replace("parts/bracket","bracket").Replace("Machine","Drive");
            if(kind=="Subassembly") child=child.Replace("Assembly Drive {", "Subassembly Drive {");
            var root=$$"""
                Inventory Record Components { drive: {{kind}} = Firmament("parts/drive").Drive }
                Assembly Machine { <Assembly Machine><Assembly Unit = Components.drive></Assembly></Assembly> Anchor: Machine.Unit; }
                """;
            var result=new AssemblyM1Pipeline().CompileProject(Project(root,KeyValuePair.Create("parts/drive.firmament",child)));
            Accepted(result); Assert.Equal(2,result.Geometry!.InstanceBodies.Count);
            Assert.Contains(result.Ir!.Instances,i => i.Path.ToString()=="Machine.Unit.Left");
        }
    }

    [Fact]
    public void SceneImportsPartAssemblyAndNestedSceneWithWorldPlacement()
    {
        const string child="Scene Workcell { units: mm <Part plate = Components.bracket> Placement { from: Origin; to: World; translateLocal: [20mm,0mm,0mm]; } </Part> }";
        const string root="""
            Inventory Record Library {
              part: Part = Firmament("parts/bracket").Bracket
              machine: Assembly = Firmament("machine").Machine
              cell: Scene = Firmament("cell").Workcell
            }
            Scene Factory { units: mm
              <Part stock = Library.part> Placement { from: Origin; to: World; } </Part>
              <Assembly machine = Library.machine> Placement { from: Origin; to: World; translateLocal: [100mm,0mm,0mm]; } </Assembly>
              <Scene cell = Library.cell> Placement { from: Origin; to: World; translateLocal: [200mm,0mm,0mm]; } </Scene>
            }
            """;
        var project=new FirmamentProjectSnapshot("scene.firmament",[
            KeyValuePair.Create("scene.firmament",root),KeyValuePair.Create("parts/bracket.firmament",Bracket),
            KeyValuePair.Create("machine.firmament",Root),KeyValuePair.Create("cell.firmament","Inventory Record Components { bracket: Part = Firmament(\"parts/bracket\").Bracket } "+child)]);
        using var session=new FirmamentSceneSession(); var result=session.CompileProject(project);
        Assert.True(result.IsSuccess,string.Join("\n",result.Diagnostics));
        Assert.Equal(-15,result.Scene!.MinimumMm[0]); Assert.Equal(235,result.Scene.MaximumMm[0]);
        Assert.Contains(result.Scene.Nodes,n => n.Path=="Factory.cell.plate" && n.Span.Source=="cell.firmament");
    }
    [Fact]
    public void RetainedPartCacheReusesAndInvalidatesTheActualModuleSource()
    {
        using var session=new FirmamentCompilationSession();
        var first=session.CompileProject(Project()); Accepted(first);
        var second=session.CompileProject(Project()); Accepted(second);
        Assert.True(Assert.Single(second.Reuse!.Definitions).Reused);
        var changed=new FirmamentProjectSnapshot("main.firmasm",[
            KeyValuePair.Create("main.firmasm",Root),KeyValuePair.Create("parts/bracket.firmament",Bracket.Replace("30mm","40mm"))]);
        var third=session.CompileProject(changed); Accepted(third);
        Assert.False(Assert.Single(third.Reuse!.Definitions).Reused);
        Assert.Equal(20,Assert.Single(third.Geometry!.Artifact.Definitions).Metrics.Maximum[0]);
    }

    [Fact]
    public void PublishedModulePortsParticipateInTheExistingMateSolver()
    {
        var part=Bracket.Replace(" } }", " } expose { Semantic Mount { DatumFrame Frame = [0,0,0] x [1,0,0] y [0,1,0] z [0,0,1]; } } }");
        var root=Root[..Root.IndexOf("Assembly Machine",StringComparison.Ordinal)]+"""
            Assembly Machine {
              <Assembly Machine><Part Left = Components.bracket></Part><Part Right = Components.bracket></Part></Assembly>
              Anchor: Machine.Left.Mount;
              Interface<Fixed> Installation { A: Machine.Left.Mount.Frame; B: Machine.Right.Mount.Frame; gap: 10mm; }
            }
            """;
        var project=new FirmamentProjectSnapshot("main.firmasm",[KeyValuePair.Create("main.firmasm",root),KeyValuePair.Create("parts/bracket.firmament",part)]);
        var result=new AssemblyM1Pipeline().CompileProject(project); Accepted(result);
        Assert.Equal(2,result.Geometry!.InstanceBodies.Count); Assert.Single(result.Ir!.Mates);
    }

    [Fact]
    public void InventoryShadowingIsLexicalAndSiblingInventoriesRemainPrivate()
    {
        var declaration="Inventory Record Components { bracket: Part = Firmament(\"parts/bracket\").Bracket }";
        var local="Inventory Record Components { bracket: Part = Firmament(\"parts/other\").Bracket }";
        var source=Root.Replace("Assembly Machine {", "Assembly Machine { "+local);
        var result=new AssemblyM1Pipeline().CompileProject(Project(source,KeyValuePair.Create("parts/other.firmament",Bracket.Replace("30mm","40mm"))));
        Accepted(result); Assert.Equal(20,Assert.Single(result.Geometry!.Artifact.Definitions).Metrics.Maximum[0]);
        var hidden="Subassembly Private { "+declaration+" <Assembly Private></Assembly> Anchor: Private; }\n"+Root.Replace(declaration,"");
        var rejected=new AssemblyM1Pipeline().CompileProject(Project(hidden));
        Assert.False(rejected.IsSuccess); Assert.Null(rejected.Geometry);
        Assert.Contains(rejected.Diagnostics,d => d.Code=="firmament-inventory-not-visible");
    }

    [Fact]
    public void InventoryDependencyCyclesAndIncludedSyntaxErrorsRetainTheirSourceFile()
    {
        var cyclic=Project(Root,KeyValuePair.Create("parts/other.firmament","Inventory Record Loop { root: Assembly = Firmament(\"../main\").Machine } Model Other { units: mm Box stock { size: [1mm,1mm,1mm] } }"));
        var root=Root.Replace("parts/bracket", "parts/other").Replace(".Bracket", ".Other");
        var result=new AssemblyM1Pipeline().CompileProject(new("main.firmasm",cyclic.Documents.Select(p => p.Key=="main.firmasm" ? KeyValuePair.Create(p.Key,root) : p)));
        Assert.Contains(result.Diagnostics,d => d.Code=="firmament-module-cycle" && d.SourceSpan?.Source=="parts/other.firmament");
        var invalid=new FirmamentProjectSnapshot("main.firmasm",[
            KeyValuePair.Create("main.firmasm","include \"parts/broken\";\nAssembly Root { <Assembly Root></Assembly> Anchor: Root; }"),
            KeyValuePair.Create("parts/broken.firmament","Record Broken { width: }")]);
        var syntax=new AssemblyM0Parser().ParseProject(invalid);
        Assert.False(syntax.IsSuccess); Assert.Contains(syntax.Diagnostics,d => d.SourceSpan?.Source=="parts/broken.firmament" && d.SourceSpan.Start>0);
    }

    [Theory]
    [InlineData("Inventory Things { }")]
    [InlineData("Inventory Record Things() { }")]
    [InlineData("Inventory<> Record Things { }")]
    [InlineData("Inventory Record Things<> { }")]
    [InlineData("Inventory Record Things { typo: Part = Step(\"anything\") extra: 3 }")]
    public void MalformedInventoriesCannotDisappear(string inventory)
    {
        var result=new AssemblyM1Pipeline().CompileProject(Project(inventory+"\nAssembly Root { <Assembly Root></Assembly> Anchor: Root; }"));
        Assert.False(result.IsSuccess); Assert.NotEmpty(result.Diagnostics);
    }

    [Fact]
    public void ATemplateMustBeSpecializedInsideItsDefiningModuleBeforeInventoryExport()
    {
        var library="Template<width: Length> Struct Bracket { Box stock { size: [width,20mm,6mm] } }";
        var project=new FirmamentProjectSnapshot("main.firmasm",[KeyValuePair.Create("main.firmasm",Root),KeyValuePair.Create("parts/bracket.firmament",library)]);
        var rejected=new AssemblyM1Pipeline().CompileProject(project);
        Assert.False(rejected.IsSuccess); Assert.Contains(rejected.Diagnostics,d => d.Code=="firmament-inventory-specialization-required");
        library=library.Replace("Struct Bracket", "Struct Family")+"\nStruct Bracket = Family<width: 30mm>";
        var accepted=new AssemblyM1Pipeline().CompileProject(new("main.firmasm",[KeyValuePair.Create("main.firmasm",Root),KeyValuePair.Create("parts/bracket.firmament",library)]));
        Accepted(accepted); Assert.Equal(15,Assert.Single(accepted.Geometry!.Artifact.Definitions).Metrics.Maximum[0]);
    }

    [Fact]
    public void FileBackedStepUsesTheExistingExactImporterAndContributesAHashedDependency()
    {
        var directory=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../artifacts/local/firmament-inventory/step-"+Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(directory);
        try
        {
            // This adapter witness intentionally exports its own asymmetric Part, then
            // imports that exact artifact; it is not a second import of the shared corpus.
            var original=FirmamentBuildAndExport.CompileSource(Bracket); Assert.True(original.IsSuccess);
            File.WriteAllText(Path.Combine(directory,"bearing.step"),original.Value!.StepText);
            var source=Root.Replace("Firmament(\"parts/bracket\").Bracket", "Step(\"bearing\")");
            var path=Path.Combine(directory,"main.firmament"); File.WriteAllText(path,source);
            var built=new AssemblyM1Pipeline().CompileFile(path); Accepted(built);
            Assert.Equal(15,Assert.Single(built.Geometry!.Artifact.Definitions).Metrics.Maximum[0]);
            Assert.Contains(built.Ir!.SourceDependencies!,d => d.Path.EndsWith("bearing.step",StringComparison.Ordinal) && d.Sha256.Length==64);
        }
        finally { Directory.Delete(directory,true); }
    }

    [Fact]
    public void FileAndSnapshotProjectsHaveTheSameDefinitionIdentitiesAndGeometry()
    {
        var root=FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Regression/Inventory/machine.firmasm");
        var file=new AssemblyM1Pipeline().CompileFile(root); Accepted(file);
        var directory=Path.GetDirectoryName(root)!;
        var names=new[] {"machine.firmasm","drive.firmament","parts/bracket.firmament","parts/wide-bracket.firmament"};
        var snapshot=new AssemblyM1Pipeline().CompileProject(new("machine.firmasm",names.Select(name => KeyValuePair.Create(name,File.ReadAllText(Path.Combine(directory,name))))));
        Accepted(snapshot);
        // The overall artifact digest includes intentionally different file/snapshot
        // provenance. Compare the actual definition bytes and world geometry instead.
        Assert.Equal(file.Geometry!.DefinitionBodies.Keys.Order(),snapshot.Geometry!.DefinitionBodies.Keys.Order());
        Assert.Equal(file.Geometry.Artifact.Definitions.Select(d => d.StepSha256),snapshot.Geometry.Artifact.Definitions.Select(d => d.StepSha256));
        Assert.Equal(file.Geometry.Artifact.Instances.Select(i => string.Join(",",i.WorldTransform.Matrix)),snapshot.Geometry.Artifact.Instances.Select(i => string.Join(",",i.WorldTransform.Matrix)));
        Assert.Equal(4,file.Geometry.InstanceBodies.Count);
        Assert.Contains(file.Ir!.InventoryBindings,b => b.Name=="Components.drive" && b.Kind=="Subassembly");
    }

    [Fact]
    public void InvalidLocatorEscapesAreDiagnosticsRatherThanReaderExceptions()
    {
        var source=Root.Replace("parts/bracket",@"parts\q");
        var result=new AssemblyM1Pipeline().CompileProject(Project(source));
        Assert.False(result.IsSuccess); Assert.Contains(result.Diagnostics,d => d.Code=="firmament-module-locator-invalid");
    }

    [Fact]
    public void SnapshotCannotBypassTheBinaryAssetBoundaryThroughAnImportedPart()
    {
        const string part="Model Bracket { units: mm InlineStep source { file: \"C:/unavailable.step\" } }";
        var result=new AssemblyM1Pipeline().CompileProject(new("main.firmasm",[KeyValuePair.Create("main.firmasm",Root),KeyValuePair.Create("parts/bracket.firmament",part)]));
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics,d => d.Code=="assembly-profile-external-resource-unsupported" && d.SourceSpan?.Source=="parts/bracket.firmament");
    }

    [Fact]
    public void ShortIncludesComposeConcreteAndLegacyTemplatePartsWithoutCatalogAliases()
    {
        var path=FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Regression/Inventory/include-simple.firmament");
        var result=new AssemblyM1Pipeline().CompileFile(path); Accepted(result);
        Assert.Equal(2,result.Geometry!.InstanceBodies.Count); Assert.Equal(2,result.Geometry.DefinitionBodies.Count);
    }

    [Fact]
    public void ADeclarationOnlyInventoryModuleNamesItsRequiredConsumerRatherThanARecordHeaderError()
    {
        var source=File.ReadAllText(FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Regression/Inventory/drive.firmament"));
        var parsed=Aetheris.Kernel.Firmament.FirmamentV2.FirmamentV2Parser.Parse(source);
        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.SyntaxDiagnostics,d => d.Code=="firmament-module-context-required" && d.SourceSpan is not null && d.Stage=="Module binding");
        Assert.False(FirmamentBuildAndExport.CompileSource(source).IsSuccess);
    }

    [Fact]
    public void AlreadyResolvedSourceRetainsItsExplicitProjectContext()
    {
        const string source="Assembly Root { <Assembly Root><Part Shade = SectionChainFile<\"shade.firmament\">></Part></Assembly> Anchor: Root.Shade; }";
        var project=new FirmamentProjectSnapshot("main.firmasm",[KeyValuePair.Create("main.firmasm",source),KeyValuePair.Create("shade.firmament",Bracket)]);
        var result=new AssemblyM0Parser().Parse(source,"main.firmasm",project);
        Assert.True(result.IsSuccess,string.Join("\n",result.Diagnostics));
        Assert.Contains(result.Source!.SourceDependencies!,d => d.Path=="shade.firmament");
    }

    [Fact]
    public void PatternOccurrencesBindInventoryBeforeExpansionAndProtectTheIterationName()
    {
        const string source="""
            Inventory Record Parts { bracket: Part = Firmament("parts/bracket").Bracket }
            Linear Sites Seats { keys: [Left,Right]; start: [0mm,0mm,0mm]; step: [50mm,0mm,0mm]; }
            Assembly Machine {
              <Assembly Machine>
                Pattern Mounts over Seats {
                  site => <Part Item = Parts.bracket>
                    Placement { from: Origin; to: World; translateLocal: [site.X,site.Y,site.Z]; }
                  </Part>
                }
              </Assembly>
              Anchor: Machine;
            }
            """;
        var result=new AssemblyM1Pipeline().CompileProject(Project(source)); Accepted(result);
        Assert.Equal(2,result.Geometry!.InstanceBodies.Count); Assert.Single(result.Geometry.DefinitionBodies);
        Assert.Contains(result.Ir!.Instances,i => i.Path.ToString()=="Machine.Mounts.Left.Item");
        var shadowed=source.Replace("site =>","Parts =>").Replace("site.X","Parts.X").Replace("site.Y","Parts.Y").Replace("site.Z","Parts.Z");
        var rejected=new AssemblyM1Pipeline().CompileProject(Project(shadowed));
        Assert.False(rejected.IsSuccess); Assert.Null(rejected.Geometry);
        Assert.Contains(rejected.Diagnostics,d => d.Code=="firmament-inventory-not-visible");
    }

}
