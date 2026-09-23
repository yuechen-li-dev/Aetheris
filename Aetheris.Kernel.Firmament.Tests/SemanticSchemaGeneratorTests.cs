using Aetheris.Firmament.SchemaGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentSchemaGeneratorTests
{
    private const string Prelude = """
        using System;
        namespace Aetheris.Kernel.Firmament.FirmamentV2 {
          [AttributeUsage(AttributeTargets.Class)] public sealed class FirmamentConstructAttribute(string id, string name) : Attribute { }
          [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)] public sealed class FirmamentFieldAttribute(string id, string name, FirmamentSchemaValueKind kind) : Attribute { public bool SourceEditable { get; init; } public string? ProjectionMember { get; init; } }
          public enum FirmamentSchemaValueKind { Length, Angle, Scalar, Count, Boolean, String, Choice, Vector, Point, Profile, ConstructionPlane, FaceSelector, BodySelector, ConstructReference, Box3, TypeRequirement }
        }
        """;

    [Fact]
    public void EquivalentDeclarationOrder_ProducesIdenticalGeneratedSource()
    {
        const string a = """
            [Aetheris.Kernel.Firmament.FirmamentV2.FirmamentConstruct("Box", "Box")]
            [Aetheris.Kernel.Firmament.FirmamentV2.FirmamentField("Size", "Size", Aetheris.Kernel.Firmament.FirmamentV2.FirmamentSchemaValueKind.Vector)]
            public class BoxSpec { }
            [Aetheris.Kernel.Firmament.FirmamentV2.FirmamentConstruct("Hole", "Hole")]
            public class HoleSpec { }
            """;
        const string b = """
            [Aetheris.Kernel.Firmament.FirmamentV2.FirmamentConstruct("Hole", "Hole")]
            public class HoleSpec { }
            [Aetheris.Kernel.Firmament.FirmamentV2.FirmamentConstruct("Box", "Box")]
            [Aetheris.Kernel.Firmament.FirmamentV2.FirmamentField("Size", "Size", Aetheris.Kernel.Firmament.FirmamentV2.FirmamentSchemaValueKind.Vector)]
            public class BoxSpec { }
            """;
        Assert.Equal(Generate(Prelude + a).Source, Generate(Prelude + b).Source);
    }

    [Fact]
    public void DuplicateFieldId_IsCompileTimeError()
    {
        const string source = """
            [Aetheris.Kernel.Firmament.FirmamentV2.FirmamentConstruct("Box", "Box")]
            [Aetheris.Kernel.Firmament.FirmamentV2.FirmamentField("Size", "Size", Aetheris.Kernel.Firmament.FirmamentV2.FirmamentSchemaValueKind.Vector)]
            [Aetheris.Kernel.Firmament.FirmamentV2.FirmamentField("Size", "Other", Aetheris.Kernel.Firmament.FirmamentV2.FirmamentSchemaValueKind.Vector)]
            public class BoxSpec { }
            """;
        Assert.Contains(Generate(Prelude + source).Diagnostics, d => d.Id == "FMS001" && d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void SourceEditableFieldWithoutStaticProjector_IsCompileTimeError()
    {
        const string source = """
            [Aetheris.Kernel.Firmament.FirmamentV2.FirmamentConstruct("Hole", "Hole")]
            [Aetheris.Kernel.Firmament.FirmamentV2.FirmamentField("Diameter", "Diameter", Aetheris.Kernel.Firmament.FirmamentV2.FirmamentSchemaValueKind.Length, SourceEditable = true)]
            public class HoleSpec { }
            """;
        Assert.Contains(Generate(Prelude + source).Diagnostics, d => d.Id == "FMS001" && d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void StaticProjectorEmitsTypedGeneratedGetter()
    {
        const string source = """
            public sealed class HoleInstance { public double Diameter { get; init; } }
            [Aetheris.Kernel.Firmament.FirmamentV2.FirmamentConstruct("Hole", "Hole")]
            [Aetheris.Kernel.Firmament.FirmamentV2.FirmamentField("Diameter", "Diameter", Aetheris.Kernel.Firmament.FirmamentV2.FirmamentSchemaValueKind.Length, SourceEditable = true, ProjectionMember = nameof(ProjectDiameter))]
            public class HoleSpec { public static double ProjectDiameter(HoleInstance instance) => instance.Diameter; }
            """;
        var generated = Generate(Prelude + source);
        Assert.DoesNotContain(generated.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains("ProjectHoleDiameter(global::HoleInstance instance)", generated.Source);
    }

    private static (string Source, IReadOnlyList<Diagnostic> Diagnostics) Generate(string source)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("SyntheticSchema", [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview))],
            references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new SemanticSchemaGenerator());
        driver = driver.RunGenerators(compilation);
        var result = Assert.Single(driver.GetRunResult().Results);
        return (Assert.Single(result.GeneratedSources).SourceText.ToString(), result.Diagnostics);
    }
}
