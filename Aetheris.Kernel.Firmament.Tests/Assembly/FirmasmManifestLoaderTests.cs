using Aetheris.Kernel.Firmament.Assembly;

namespace Aetheris.Kernel.Firmament.Tests.Assembly;

public sealed class FirmasmManifestLoaderTests
{
    [Fact]
    public void LoadFromFile_NativeOnlyManifest_LoadsSuccessfully()
    {
        var loader = new FirmasmManifestLoader();
        var path = FirmamentCorpusHarness.ResolveFixtureFullPath("testdata/firmasm/examples/native-only.firmasm");

        var result = loader.LoadFromFile(path);

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("1", result.Value.Manifest.ManifestVersion);
        Assert.Equal("native_demo", result.Value.Manifest.Assembly.Name);
        Assert.All(result.Value.LoadedParts.Values, part => Assert.IsType<FirmasmLoadedNativeFirmamentPart>(part));
    }

    [Fact]
    public void LoadFromFile_MixedNativeAndStep_LoadsSuccessfully()
    {
        var loader = new FirmasmManifestLoader();
        var path = FirmamentCorpusHarness.ResolveFixtureFullPath("testdata/firmasm/examples/mixed-native-step.firmasm");

        var result = loader.LoadFromFile(path);

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.IsType<FirmasmLoadedNativeFirmamentPart>(result.Value.LoadedParts["base"]);
        Assert.IsType<FirmasmLoadedOpaqueStepPart>(result.Value.LoadedParts["vendor_motor"]);

        var motorInstance = Assert.Single(result.Value.Manifest.Instances, instance => instance.Id == "motor_1");
        Assert.Equal(3, motorInstance.Transform.Translate.Count);
        Assert.Equal(3, motorInstance.Transform.RotateDegXyz!.Count);
    }

    [Fact]
    public void Parse_InvalidStructure_RejectsClearly()
    {
        var loader = new FirmasmManifestLoader();
        var path = FirmamentCorpusHarness.ResolveFixtureFullPath("testdata/firmasm/fixtures/invalid-structure.firmasm");

        var result = loader.LoadFromFile(path);

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Contains("Missing required section 'instances'", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadFromFile_MissingStep_RejectsClearly()
    {
        var loader = new FirmasmManifestLoader();
        var path = FirmamentCorpusHarness.ResolveFixtureFullPath("testdata/firmasm/fixtures/invalid-step-missing-file.firmasm");

        var result = loader.LoadFromFile(path);

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Contains("was not found", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_RejectsUnknownPartKind()
    {
        var loader = new FirmasmManifestLoader();
        var source = """
        {
          "manifest": { "version": "1" },
          "assembly": { "name": "demo", "units": "mm" },
          "parts": {
            "base": { "kind": "mesh", "source": "./base.firmament" }
          },
          "instances": [
            {
              "id": "base_1",
              "part": "base",
              "transform": { "translate": [0, 0, 0] }
            }
          ]
        }
        """;

        var result = loader.Parse(source);

        Assert.False(result.IsSuccess);
        Assert.Contains("Allowed kinds: 'firmament', 'step'", Assert.Single(result.Diagnostics).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_RejectsInvalidTransformShape()
    {
        var loader = new FirmasmManifestLoader();
        var source = """
        {
          "manifest": { "version": "1" },
          "assembly": { "name": "demo", "units": "mm" },
          "parts": {
            "base": { "kind": "firmament", "source": "./base.firmament" }
          },
          "instances": [
            {
              "id": "base_1",
              "part": "base",
              "transform": { "translate": [0, 0] }
            }
          ]
        }
        """;

        var result = loader.Parse(source);

        Assert.False(result.IsSuccess);
        Assert.Contains("exactly 3 numeric values", Assert.Single(result.Diagnostics).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_RejectsDuplicateInstanceIds()
    {
        var loader = new FirmasmManifestLoader();
        var source = """
        {
          "manifest": { "version": "1" },
          "assembly": { "name": "demo", "units": "mm" },
          "parts": {
            "base": { "kind": "firmament", "source": "./base.firmament" }
          },
          "instances": [
            {
              "id": "base_1",
              "part": "base",
              "transform": { "translate": [0, 0, 0] }
            },
            {
              "id": "base_1",
              "part": "base",
              "transform": { "translate": [10, 0, 0] }
            }
          ]
        }
        """;

        var result = loader.Parse(source);

        Assert.False(result.IsSuccess);
        Assert.Contains("Duplicate instance id 'base_1'", Assert.Single(result.Diagnostics).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_RejectsUnknownPartReference()
    {
        var loader = new FirmasmManifestLoader();
        var source = """
        {
          "manifest": { "version": "1" },
          "assembly": { "name": "demo", "units": "mm" },
          "parts": {
            "base": { "kind": "firmament", "source": "./base.firmament" }
          },
          "instances": [
            {
              "id": "missing_1",
              "part": "missing",
              "transform": { "translate": [0, 0, 0] }
            }
          ]
        }
        """;

        var result = loader.Parse(source);

        Assert.False(result.IsSuccess);
        Assert.Contains("references unknown part 'missing'", Assert.Single(result.Diagnostics).Message, StringComparison.Ordinal);
    }
}
