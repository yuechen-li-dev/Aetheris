using Aetheris.Kernel.Firmament.Assembly;

namespace Aetheris.Kernel.Firmament.Tests.Assembly;

/// <summary>BUILD-PERF-M2: definitions compile from the declarations they depend on.</summary>
public sealed class AssemblyDeclarationScopeTests
{
    private const string Catalog = """
        Record Spec { Width: Length }
        Static Standard: Spec { Width: 4mm }
        Function twice(value: Length) -> Length =
          value * 2
        Template<H: Length> Struct Pad {
          Rect2 Outline { Center: [0mm,0mm]; Size: [twice(Standard.Width),10mm] }
          Profile P { Loop Outer { Outline |> TraceLoop } }
          Extrude Body { Profile: P From: 0mm To: H }
        }
        Template<R: Length> Struct Peg {
          Circle2 Outline { center: [0mm,0mm] radius: R }
          Profile P { Loop Outer { Outline |> TraceLoop } }
          Extrude Body { profile: P from: 0mm to: 5mm }
        }
        Concept Struct Layout {
          Axis Spindle { origin: [0mm, 0mm, 0mm]; direction: [0, 0, 1]; reference: [1, 0, 0]; }
        }
        """;

    [Fact]
    public void ClosureKeepsTransitiveDependenciesAndDropsUnrelatedDeclarations()
    {
        var pad = AssemblyDeclarationScope.Closure(Catalog, "Pad")!;
        Assert.Contains("Record Spec", pad, StringComparison.Ordinal);
        Assert.Contains("Static Standard", pad, StringComparison.Ordinal);
        // A multi-line expression stays with its declaration.
        Assert.Contains("value * 2", pad, StringComparison.Ordinal);
        Assert.DoesNotContain("Struct Peg", pad, StringComparison.Ordinal);
        Assert.DoesNotContain("Concept Struct Layout", pad, StringComparison.Ordinal);

        var peg = AssemblyDeclarationScope.Closure(Catalog, "Peg")!;
        Assert.Contains("Struct Peg", peg, StringComparison.Ordinal);
        Assert.DoesNotContain("Struct Pad", peg, StringComparison.Ordinal);
        Assert.DoesNotContain("Function twice", peg, StringComparison.Ordinal);

        Assert.Null(AssemblyDeclarationScope.Closure(Catalog, "Missing"));
    }

    [Fact]
    public void UnnamedDeclarationsFollowTheClosuresTheyMention()
    {
        var catalog = Catalog + "\nModify Pad {\n  Hole<Shaft> Bore { On: +Z Center: Point2(0mm, 0mm) Diameter: 2mm End: ThroughAll }\n}\n";
        Assert.Contains("Modify Pad", AssemblyDeclarationScope.Closure(catalog, "Pad")!, StringComparison.Ordinal);
        Assert.DoesNotContain("Modify Pad", AssemblyDeclarationScope.Closure(catalog, "Peg")!, StringComparison.Ordinal);
    }

    public static TheoryData<string> CanonicalAssemblies()
    {
        var data = new TheoryData<string>();
        var root = FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/AssemblyInterfaces");
        // The guitar (56 definitions) is covered by the showcase hash budgets (Aetheris.Web.Perf).
        foreach (var file in Directory.GetFiles(root, "*.firm*", SearchOption.AllDirectories)
                     .Where(file => !file.Contains("GuitarX0", StringComparison.Ordinal)).Order(StringComparer.Ordinal))
            data.Add(Path.GetRelativePath(root, file).Replace('\\', '/'));
        return data;
    }

    [Theory]
    [MemberData(nameof(CanonicalAssemblies))]
    public void ScopedDeclarationsBuildTheSameAssemblyAsTheWholeCatalog(string fixture)
    {
        var path = FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/AssemblyInterfaces/" + fixture);
        var scoped = Compile(path, scope: true);
        var whole = Compile(path, scope: false);
        Assert.Equal(whole.IsSuccess, scoped.IsSuccess);
        if (!whole.IsSuccess) return;
        Assert.Equal(Codes(whole), Codes(scoped));
        Assert.Equal(Definitions(whole), Definitions(scoped));
        Assert.Equal(whole.Ir!.Instances.Select(i => (i.StableId, string.Join(',', i.ResolvedTransform?.Matrix ?? []))),
            scoped.Ir!.Instances.Select(i => (i.StableId, string.Join(',', i.ResolvedTransform?.Matrix ?? []))));
    }

    [Fact]
    public void IncludeEditInvalidatesOnlyTheDefinitionsThatUseIt()
    {
        var path = FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/AssemblyInterfaces/IndustrialAtlas/atlas-industrial.firmament");
        var directory = Path.GetDirectoryName(path)!;
        var documents = Directory.GetFiles(directory, "*.firmament")
            .ToDictionary(Path.GetFileName, file => File.ReadAllText(file).Replace("\r\n", "\n"), StringComparer.Ordinal)!;
        using var session = new FirmamentCompilationSession();
        FirmamentProjectSnapshot Project() => new("atlas-industrial.firmament", documents);
        var first = session.CompileProject(Project());
        Assert.True(first.IsSuccess, string.Join("\n", first.Diagnostics.Select(d => d.Message)));
        var gripper = documents["gripper.firmament"];
        documents["gripper.firmament"] = gripper + "\n// a comment only the gripper's definitions see\n";
        var edited = session.CompileProject(Project());
        Assert.True(edited.IsSuccess);
        Assert.True(edited.Reuse!.ReusedDefinitions > 0, "an edit to one include must not rebuild every definition");
        Assert.True(edited.Reuse.RebuiltDefinitions < edited.Reuse.Definitions.Count);
        var fresh = new AssemblyM1Pipeline().CompileProject(Project());
        Assert.Equal(Definitions(fresh), Definitions(edited));
    }

    private static AssemblyM1CompilationResult Compile(string path, bool scope)
    {
        AssemblyDefinitionMaterializer.ScopeDeclarations = scope;
        try { return new AssemblyM1Pipeline().CompileFile(path); }
        finally { AssemblyDefinitionMaterializer.ScopeDeclarations = true; }
    }

    private static string[] Codes(AssemblyM1CompilationResult result) =>
        result.Diagnostics.Select(d => d.Code).Order(StringComparer.Ordinal).ToArray();

    private static string[] Definitions(AssemblyM1CompilationResult result) =>
        result.Geometry!.Artifact.Definitions.Select(d => d.DefinitionIdentity + "=" + d.StepSha256).Order(StringComparer.Ordinal).ToArray();
}
