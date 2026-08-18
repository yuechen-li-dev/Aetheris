using System.Text.Json;
using Aetheris.Forge.Abstractions.FirmamentInterop;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.FirmamentV2.FirmamentInterop;
using InteropSourceSpan = Aetheris.Forge.Abstractions.FirmamentInterop.FirmamentSourceSpan;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class ForgeCsA1InteropTests
{
    [Fact]
    public void ForgeCsA1_FirmamentVariables_InteropPreservesTopLevelAndRecordValues()
    {
        var document = ParseDocument("""
            model VariablesInterop {
              units mm
              solid part: Box { size: [100, 60, 10] }

              let holeDiameter: length = 6.0mm tol 0.05mm

              let MountingPattern {
                spacingX: length = 80.0mm tol +0.10mm -0.05mm
                draftAngle: angle = 90deg
                holeCount: int = 4
                label: string = "A"
                inspectionRequired: bool = true
              }
            }
            """);

        var variables = new FirmamentV2VariablesAdapter(document);

        Assert.True(variables.TryGet("holeDiameter", out var holeDiameter));
        var holeDiameterValue = Assert.IsType<FirmamentScalarValue>(holeDiameter);
        Assert.Equal(FirmamentValueKind.Length, holeDiameterValue.Kind);
        Assert.Equal(6.0d, holeDiameterValue.NumericValue);
        Assert.Equal("mm", holeDiameterValue.Unit);
        Assert.NotNull(holeDiameterValue.SourceSpan);
        AssertTolerance(holeDiameterValue.Tolerance, FirmamentToleranceKind.Bilateral, 0.05d, 0.05d, "mm", FirmamentValueKind.Length);

        Assert.True(variables.TryGet("MountingPattern.spacingX", out var spacingX));
        var spacingXValue = Assert.IsType<FirmamentScalarValue>(spacingX);
        Assert.Equal(80.0d, spacingXValue.NumericValue);
        Assert.Equal("mm", spacingXValue.Unit);
        AssertTolerance(spacingXValue.Tolerance, FirmamentToleranceKind.Asymmetric, 0.10d, 0.05d, "mm", FirmamentValueKind.Length);

        Assert.True(variables.TryGet("MountingPattern.draftAngle", out var angle));
        var angleValue = Assert.IsType<FirmamentScalarValue>(angle);
        Assert.Equal(FirmamentValueKind.Angle, angleValue.Kind);
        Assert.Equal(90.0d, angleValue.NumericValue);
        Assert.Equal("deg", angleValue.Unit);

        Assert.True(variables.TryGet("MountingPattern.holeCount", out var count));
        Assert.Equal(4, Assert.IsType<FirmamentScalarValue>(count).Nominal);

        Assert.True(variables.TryGet("MountingPattern.label", out var label));
        Assert.Equal("A", Assert.IsType<FirmamentScalarValue>(label).Nominal);

        Assert.True(variables.TryGet("MountingPattern.inspectionRequired", out var enabled));
        Assert.Equal(true, Assert.IsType<FirmamentScalarValue>(enabled).Nominal);

        Assert.Contains(variables.All, variable => variable.Name == "holeDiameter");
        Assert.Contains(variables.All, variable => variable.Name == "MountingPattern.spacingX");
        Assert.Contains(variables.All, variable => variable.Name == "MountingPattern.draftAngle");
        Assert.Contains(variables.All, variable => variable.Name == "MountingPattern.holeCount");
        Assert.False(variables.TryGet("missingVariable", out _));

        var missing = Assert.Throws<KeyNotFoundException>(() => variables.GetRequired("missingVariable"));
        Assert.Equal("Firmament variable 'missingVariable' was not found.", missing.Message);
    }

    [Fact]
    public void ForgeCsA1_ConceptApplication_InteropPreservesProcessAndHoleConcepts()
    {
        var document = ParseFixture("Language/valid/concept-applications-forge.valid.firmfixture");

        var manufacturing = FirmamentV2ConceptApplicationAdapter.Adapt(Assert.Single(document.ManufacturingConcepts!));
        Assert.Equal(FirmamentConceptApplicationKind.Manufacturing, manufacturing.Kind);
        Assert.Equal(new ConceptId("process", "CNC"), manufacturing.ConceptId);
        Assert.Equal("process<CNC>", manufacturing.ConceptId.ToString());
        Assert.NotNull(manufacturing.SourceSpan);

        var processMaterial = Assert.Single(manufacturing.Fields, field => field.Name == "material");
        Assert.Equal(FirmamentFieldKind.Value, processMaterial.Kind);
        Assert.Equal("Aluminum6061", Assert.IsType<FirmamentScalarValue>(processMaterial.Value).Nominal);

        var feature = FirmamentV2ConceptApplicationAdapter.Adapt(Assert.Single(document.FeatureConcepts!));
        Assert.Equal(FirmamentConceptApplicationKind.Feature, feature.Kind);
        Assert.Equal("mountHole", feature.Name);
        Assert.Equal(new ConceptId("hole", "Countersink"), feature.ConceptId);
        Assert.Equal("hole<Countersink>", feature.ConceptId.ToString());
        Assert.NotNull(feature.SourceSpan);

        var target = Assert.Single(feature.Fields, field => field.Name == "target");
        Assert.Equal(FirmamentFieldKind.Target, target.Kind);
        Assert.Equal("part.region(\"mountHoleA\")", target.TargetSource);
        Assert.NotNull(target.SourceSpan);

        var diameter = Assert.Single(feature.Fields, field => field.Name == "diameter");
        var diameterValue = Assert.IsType<FirmamentScalarValue>(diameter.Value);
        Assert.Equal(FirmamentValueKind.Length, diameterValue.Kind);
        Assert.Equal(6.0d, diameterValue.NumericValue);
        Assert.Equal("mm", diameterValue.Unit);
        AssertTolerance(diameterValue.Tolerance, FirmamentToleranceKind.Bilateral, 0.05d, 0.05d, "mm", FirmamentValueKind.Length);

        var countersinkDiameter = Assert.Single(feature.Fields, field => field.Name == "countersinkDiameter");
        var countersinkValue = Assert.IsType<FirmamentScalarValue>(countersinkDiameter.Value);
        Assert.Equal(10.0d, countersinkValue.NumericValue);
        AssertTolerance(countersinkValue.Tolerance, FirmamentToleranceKind.Bilateral, 0.10d, 0.10d, "mm", FirmamentValueKind.Length);

        var angle = Assert.Single(feature.Fields, field => field.Name == "angle");
        var angleValue = Assert.IsType<FirmamentScalarValue>(angle.Value);
        Assert.Equal(FirmamentValueKind.Angle, angleValue.Kind);
        Assert.Equal(90.0d, angleValue.NumericValue);
        Assert.Equal("deg", angleValue.Unit);
    }

    [Fact]
    public void ForgeCsA1_ConceptIdAndRegistry_InteropAreUsableInProcess()
    {
        var registry = new ForgeConceptRegistry();
        var concept = new FakeConcept(new ConceptId("hole", "Countersink"));

        registry.Register(concept);

        Assert.True(registry.TryResolve(new ConceptId("hole", "Countersink"), out var resolved));
        Assert.Same(concept, resolved);

        var duplicate = Assert.Throws<InvalidOperationException>(() => registry.Register(new FakeConcept(new ConceptId("hole", "Countersink"))));
        Assert.Equal("Forge concept 'hole<Countersink>' is already registered.", duplicate.Message);

        var schema = new ConceptSchemaBuilder();
        concept.Define(schema);
        Assert.Collection(
            schema.Fields,
            field =>
            {
                Assert.Equal("angle", field.Name);
                Assert.Equal(ConceptSchemaValueKind.Angle, field.Kind);
                Assert.False(field.RequiresTolerance);
            },
            field =>
            {
                Assert.Equal("diameter", field.Name);
                Assert.Equal(ConceptSchemaValueKind.Length, field.Kind);
                Assert.True(field.RequiresTolerance);
            },
            field =>
            {
                Assert.Equal("target", field.Name);
                Assert.Equal(ConceptSchemaValueKind.Target, field.Kind);
                Assert.False(field.RequiresTolerance);
            });
    }

    [Fact]
    public void ForgeCsA1_Diagnostics_InteropPreservesSeverityAndMetadata()
    {
        var sourceSpan = new InteropSourceSpan(12, 5);
        var diagnostic = new FirmamentDiagnostic(
            "forge-cs-a1-probe",
            FirmamentDiagnosticSeverity.Warning,
            "Probe message",
            sourceSpan,
            "part.region(\"mountHoleA\")",
            "diameter");

        Assert.Equal(FirmamentDiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal(sourceSpan, diagnostic.SourceSpan);
        Assert.Equal("part.region(\"mountHoleA\")", diagnostic.Target);
        Assert.Equal("diameter", diagnostic.FieldName);

        var json = JsonSerializer.Serialize(diagnostic);
        Assert.Contains("\"Severity\":1", json, StringComparison.Ordinal);
        var roundTrip = JsonSerializer.Deserialize<FirmamentDiagnostic>(json);
        Assert.NotNull(roundTrip);
        Assert.Equal(diagnostic.Target, roundTrip.Target);
        Assert.Equal(diagnostic.FieldName, roundTrip.FieldName);

        var adapted = FirmamentV2DiagnosticAdapter.FromParserDiagnosticCode(
            FirmamentV2Parser.ToleranceDroppedThroughArithmetic,
            new FirmamentV2SourceSpan(40, 8),
            "part.region(\"mountHoleA\")",
            "diameter");

        Assert.Equal(FirmamentDiagnosticSeverity.Warning, adapted.Severity);
        Assert.Equal("part.region(\"mountHoleA\")", adapted.Target);
        Assert.Equal("diameter", adapted.FieldName);
        Assert.Equal(new InteropSourceSpan(40, 8), adapted.SourceSpan);
    }

    private static FirmamentV2Document ParseDocument(string source)
    {
        var result = FirmamentV2Parser.Parse(source);
        Assert.True(result.IsSuccess, string.Join(", ", result.Diagnostics));
        return result.Document!;
    }

    private static FirmamentV2Document ParseFixture(string relative)
    {
        var result = FirmamentV2Parser.Parse(File.ReadAllText(Path.Combine(FindRepoRoot(), "fixtures", relative)));
        Assert.True(result.IsSuccess, string.Join(", ", result.Diagnostics));
        return result.Document!;
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Aetheris.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Could not find repo root.");
    }

    private static void AssertTolerance(
        FirmamentTolerance? actual,
        FirmamentToleranceKind kind,
        double plus,
        double minus,
        string unit,
        FirmamentValueKind valueKind)
    {
        Assert.NotNull(actual);
        Assert.Equal(kind, actual.Kind);
        Assert.Equal(plus, actual.Plus);
        Assert.Equal(minus, actual.Minus);
        Assert.Equal(unit, actual.Unit);
        Assert.Equal(valueKind, actual.ValueKind);
        Assert.NotNull(actual.SourceSpan);
    }

    private sealed class FakeConcept : IForgeConcept
    {
        public FakeConcept(ConceptId id)
        {
            Id = id;
        }

        public ConceptId Id { get; }

        public void Define(ConceptSchemaBuilder schema)
        {
            schema.RequiredTarget("target");
            schema.RequiredLength("diameter").RequireTolerance();
            schema.RequiredAngle("angle");
        }

        public IEnumerable<FirmamentDiagnostic> Validate(ConceptValidationContext context)
        {
            yield break;
        }
    }
}
