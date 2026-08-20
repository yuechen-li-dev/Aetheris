using System.Security.Cryptography;
using System.Text.Json;
using Aetheris.Forge.Host;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Forge.Host.Tests;

public sealed class ForgeProtocolV1Tests
{
    private const string Enclosure = "Standard.SheetMetal.ElectronicsEnclosure";
    private const string Paperclip = "Standard.Products.Office.Paperclip";

    [Fact]
    public void InfoListAndDescriptionAreStableAndDerivedFromFirmamentSchema()
    {
        var host = new ForgeProtocolHost();
        var info = host.GetHostInfo();
        Assert.Equal(1, info.ProtocolVersion);
        Assert.Equal(["ListTemplates", "DescribeTemplate", "InvokeTemplate"], info.Capabilities);

        var list = host.ListTemplates();
        Assert.Equal(list.Templates.OrderBy(item => item.Id, StringComparer.Ordinal), list.Templates);
        Assert.Contains(list.Templates, item => item.Id == Enclosure);
        Assert.Contains(list.Templates, item => item.Id == Paperclip);
        Assert.DoesNotContain(list.Templates, item => item.Id.Contains("Aetheris.", StringComparison.Ordinal));

        var description = host.DescribeTemplate(Enclosure)!;
        Assert.StartsWith("ElectronicsEnclosure<", description.Signature, StringComparison.Ordinal);
        Assert.Equal("SheetMetal", description.OutputKind);
        Assert.Contains(description.Constraints, item => item.Name == "Positive");
        Assert.All(description.Parameters, parameter => Assert.Contains(parameter.Category, new[] { "type", "value", "record" }));
        var spec = Assert.Single(description.Parameters);
        Assert.Equal("record", spec.Type);
        Assert.Equal("mm", spec.Fields!.Single(item => item.Name == "Width").Unit);
        Assert.Equal("120mm", spec.Fields!.Single(item => item.Name == "Width").Default);
        Assert.All(spec.Fields!, field => Assert.False(field.Required));
        Assert.Equal(["Auto", "Rectangular", "Round"], spec.Fields!.Single(item => item.Name == "ReliefPolicy").AllowedValues);
        Assert.Equal([ForgeArtifactKind.StepAp242, ForgeArtifactKind.FlatStep, ForgeArtifactKind.Svg], description.Artifacts);
    }

    [Fact]
    public void PaperclipDescribeAndInvokeExposePolicyAndDeterministicStep()
    {
        using var first = TempDirectory.Create(); using var second = TempDirectory.Create();
        var host = new ForgeProtocolHost();
        var description = Assert.IsType<ForgeTemplateDescription>(host.DescribeTemplate(Paperclip));
        Assert.Equal([ForgeArtifactKind.StepAp242], description.Artifacts);
        var policy = Assert.Single(description.Parameters);
        Assert.Equal("PaperclipPolicy", policy.Type == "record" ? policy.Fields is null ? string.Empty : "PaperclipPolicy" : string.Empty);
        Assert.Contains(policy.Fields!, field => field.Name == "WireDiameter" && field.Unit == "mm");
        Assert.Contains(description.Constraints, constraint => constraint.Name == "OuterWidthExceedsInnerWidth");
        var arguments = new Dictionary<string, object?>
        {
            ["wireDiameter"] = "1.0 mm", ["overallLength"] = "35 mm", ["outerWidth"] = "10 mm",
            ["innerWidth"] = "6 mm", ["bendRadius"] = "1.2 mm", ["loopGap"] = "1.2 mm",
            ["material"] = "Standard.Materials.StainlessSteel.304_Annealed",
        };
        var a = host.InvokeTemplate(Paperclip, Request(arguments, ForgeArtifactKind.StepAp242), first.Path);
        var b = host.InvokeTemplate(Paperclip, Request(arguments, ForgeArtifactKind.StepAp242), second.Path);
        Assert.True(a.Success, string.Join(Environment.NewLine, a.Diagnostics.Select(item => item.Code + ": " + item.Message)));
        Assert.True(b.Success, string.Join(Environment.NewLine, b.Diagnostics.Select(item => item.Code + ": " + item.Message)));
        Assert.Equal(a.Identity.Specialization, b.Identity.Specialization);
        Assert.Equal(a.Artifacts.Single().Sha256, b.Artifacts.Single().Sha256);
        Assert.True(Step242Importer.ImportBody(File.ReadAllText(System.IO.Path.Combine(first.Path, "paperclip.step"))).IsSuccess);
    }

    [Fact]
    public void RealEnclosureInvocationProducesDeterministicStepFlatStepAndSvg()
    {
        using var first = TempDirectory.Create();
        using var second = TempDirectory.Create();
        var host = new ForgeProtocolHost();
        var request = Request(ValidArguments(), ForgeArtifactKind.StepAp242, ForgeArtifactKind.FlatStep, ForgeArtifactKind.Svg);

        var a = host.InvokeTemplate(Enclosure, request, first.Path);
        var b = host.InvokeTemplate(Enclosure, request, second.Path);

        Assert.True(a.Success, string.Join(Environment.NewLine, a.Diagnostics.Select(item => item.Code + ": " + item.Message)));
        Assert.True(b.Success, string.Join(Environment.NewLine, b.Diagnostics.Select(item => item.Code + ": " + item.Message)));
        Assert.Equal(a.Identity.Specialization, b.Identity.Specialization);
        Assert.Equal(a.Artifacts.Select(item => (item.Kind, item.Sha256)), b.Artifacts.Select(item => (item.Kind, item.Sha256)));
        Assert.All(a.Artifacts, artifact =>
        {
            var bytes = File.ReadAllBytes(System.IO.Path.Combine(first.Path, artifact.Path));
            Assert.Equal(artifact.Size, bytes.LongLength);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), artifact.Sha256);
            Assert.False(System.IO.Path.IsPathRooted(artifact.Path));
        });
        Assert.True(Step242Importer.ImportBody(File.ReadAllText(System.IO.Path.Combine(first.Path, "part.step"))).IsSuccess);
        Assert.True(Step242Importer.ImportBody(File.ReadAllText(System.IO.Path.Combine(first.Path, "part.flat.step"))).IsSuccess);
    }

    [Fact]
    public void EnclosureCanonicalStaticDefaultCanBeInvokedWithoutDuplicatedFields()
    {
        using var output = TempDirectory.Create();
        var result = new ForgeProtocolHost().InvokeTemplate(Enclosure,
            new ForgeTemplateInvocationRequest(1, new Dictionary<string, JsonElement>(), [ForgeArtifactKind.StepAp242]),
            output.Path);

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Select(item => item.Code + ": " + item.Message)));
        Assert.True(File.Exists(System.IO.Path.Combine(output.Path, "part.step")));
    }

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public void InvalidInvocationsReturnCanonicalStructuredDiagnostics(
        string template, IReadOnlyDictionary<string, object?> arguments, string expectedCode)
    {
        using var output = TempDirectory.Create();
        var result = new ForgeProtocolHost().InvokeTemplate(template, Request(arguments, ForgeArtifactKind.StepAp242), output.Path);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, item => item.Code == expectedCode);
        Assert.Empty(result.Artifacts);
    }

    public static TheoryData<string, IReadOnlyDictionary<string, object?>, string> InvalidRequests() => new()
    {
        { "Standard.SheetMetal.DoesNotExist", ValidArguments(), "forge-host-template-not-found" },
        { Enclosure, With("mystery", "1 mm"), "firmament-template-record-extra-field" },
        { Enclosure, With("width", true), "forge-host-argument-transport-type" },
        { Enclosure, With("width", "12 kg"), "firmament-template-record-field-type-mismatch" },
        { Enclosure, With("lidLipHeight", "1 mm"), "firmament-template-require-failed" },
    };

    [Fact]
    public void ProtocolVersionAndUnsupportedArtifactAreRejectedBeforeExecution()
    {
        using var output = TempDirectory.Create();
        var host = new ForgeProtocolHost();
        var version = host.InvokeTemplate(Enclosure, new(99, Elements(ValidArguments()), null), output.Path);
        Assert.Contains(version.Diagnostics, item => item.Code == "forge-host-protocol-version-unsupported");

        var artifact = host.InvokeTemplate(Enclosure, new(1, Elements(ValidArguments()), [(ForgeArtifactKind)999]), output.Path);
        Assert.Contains(artifact.Diagnostics, item => item.Code == "forge-host-artifact-unsupported");
    }

    [Fact]
    public void ProcessCommandReadsStructuredStdinAndWritesOnlyStructuredStdout()
    {
        using var outputDirectory = TempDirectory.Create();
        var request = JsonSerializer.Serialize(Request(ValidArguments(), ForgeArtifactKind.StepAp242),
            ForgeProtocolJsonContext.Default.ForgeTemplateInvocationRequest);
        var stdout = new StringWriter();
        var exitCode = Program.Run(["invoke", Enclosure, "--request", "-", "--out", outputDirectory.Path],
            new StringReader(request), stdout);
        var response = JsonSerializer.Deserialize(stdout.ToString(), ForgeProtocolJsonContext.Default.ForgeTemplateInvocationResult);
        Assert.Equal(0, exitCode);
        Assert.True(response!.Success);
        Assert.True(File.Exists(System.IO.Path.Combine(outputDirectory.Path, "part.step")));
    }

    [Theory]
    [InlineData(99, Enclosure, false, 2)]
    [InlineData(1, "Standard.SheetMetal.DoesNotExist", false, 3)]
    [InlineData(1, Enclosure, true, 4)]
    public void ProcessCommandUsesDocumentedFailureExitCodes(int protocolVersion, string template, bool violateSemanticRequirement, int expectedExitCode)
    {
        using var outputDirectory = TempDirectory.Create();
        var arguments = ValidArguments();
        if (violateSemanticRequirement) arguments["lidLipHeight"] = "1 mm";
        var request = JsonSerializer.Serialize(new ForgeTemplateInvocationRequest(protocolVersion, Elements(arguments), [ForgeArtifactKind.StepAp242]),
            ForgeProtocolJsonContext.Default.ForgeTemplateInvocationRequest);

        var exitCode = Program.Run(["invoke", template, "--request", "-", "--out", outputDirectory.Path],
            new StringReader(request), new StringWriter());

        Assert.Equal(expectedExitCode, exitCode);
    }

    [Fact]
    public void PublicInteropRequestInvokesTheProductionTemplate()
    {
        using var outputDirectory = TempDirectory.Create();
        var root = FindRepoRoot();
        var requestPath = System.IO.Path.Combine(root, "samples", "forge-interop-x1", "request.json");
        var request = JsonSerializer.Deserialize(File.ReadAllText(requestPath), ForgeProtocolJsonContext.Default.ForgeTemplateInvocationRequest);
        var result = new ForgeProtocolHost().InvokeTemplate(Enclosure, request!, outputDirectory.Path);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Select(item => item.Code + ": " + item.Message)));
        Assert.Equal([ForgeArtifactKind.StepAp242, ForgeArtifactKind.FlatStep, ForgeArtifactKind.Svg], result.Artifacts.Select(item => item.Kind));
        Assert.All(result.Artifacts, artifact => Assert.True(File.Exists(System.IO.Path.Combine(outputDirectory.Path, artifact.Path))));
    }

    private static ForgeTemplateInvocationRequest Request(IReadOnlyDictionary<string, object?> values, params ForgeArtifactKind[] artifacts) =>
        new(1, Elements(values), artifacts);

    private static IReadOnlyDictionary<string, JsonElement> Elements(IReadOnlyDictionary<string, object?> values) =>
        values.ToDictionary(pair => pair.Key, pair => JsonSerializer.SerializeToElement(pair.Value), StringComparer.Ordinal);

    private static Dictionary<string, object?> ValidArguments() => new(StringComparer.Ordinal)
    {
        ["width"] = "120 mm", ["height"] = "40 mm", ["depth"] = "80 mm", ["thickness"] = "1.5 mm",
        ["lidLipHeight"] = "8 mm", ["insideRadius"] = "2 mm", ["kFactor"] = 0.42, ["reliefPolicy"] = "Rectangular",
    };

    private static Dictionary<string, object?> Without(string name)
    {
        var values = ValidArguments(); values.Remove(name); return values;
    }

    private static Dictionary<string, object?> With(string name, object? value)
    {
        var values = ValidArguments(); values[name] = value; return values;
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(System.IO.Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }

    private sealed class TempDirectory : IDisposable
    {
        private TempDirectory(string path) => Path = path;
        public string Path { get; }
        public static TempDirectory Create()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "aetheris-forge-x1-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new(path);
        }
        public void Dispose() { if (Directory.Exists(Path)) Directory.Delete(Path, true); }
    }
}
