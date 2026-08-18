using Aetheris.Forge.Abstractions.FirmamentInterop;
using Aetheris.Forge.Standard;
using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class ForgeCsA2StandardConceptTests
{
    [Fact]
    public void ForgeCsA2_StandardRuntimeConceptPack_RegistersAllBuiltInsDeterministically()
    {
        var pack = new StandardForgeRuntimeConceptPack();
        var registry = new InspectableForgeRegistry();

        pack.Register(registry);

        Assert.Equal(
            [
                new ConceptId("hole", "Counterbore"),
                new ConceptId("hole", "Countersink"),
                new ConceptId("hole", "Shaft"),
                new ConceptId("process", "CNC")
            ],
            registry.RegisteredIds.OrderBy(id => id.Family, StringComparer.Ordinal).ThenBy(id => id.Concept, StringComparer.Ordinal).ToArray());

        Assert.True(registry.TryResolve(new ConceptId("process", "CNC"), out var process));
        Assert.IsType<CncProcessConcept>(process);
        Assert.True(registry.TryResolve(new ConceptId("hole", "Shaft"), out var shaft));
        Assert.IsType<ShaftHoleConcept>(shaft);
        Assert.True(registry.TryResolve(new ConceptId("hole", "Counterbore"), out var counterbore));
        Assert.IsType<CounterboreHoleConcept>(counterbore);
        Assert.True(registry.TryResolve(new ConceptId("hole", "Countersink"), out var countersink));
        Assert.IsType<CountersinkHoleConcept>(countersink);

        var duplicate = Assert.Throws<InvalidOperationException>(() => pack.Register(registry));
        Assert.Equal("Forge concept 'process<CNC>' is already registered.", duplicate.Message);
    }

    [Fact]
    public void ForgeCsA2_CSharpSchemas_MatchExpectedFields()
    {
        AssertSchema(
            new CncProcessConcept(),
            ("material", ConceptSchemaValueKind.Material, true, false),
            ("minimumToolRadius", ConceptSchemaValueKind.Length, true, false));

        AssertSchema(
            new ShaftHoleConcept(),
            ("diameter", ConceptSchemaValueKind.Length, true, false),
            ("target", ConceptSchemaValueKind.Target, true, false));

        AssertSchema(
            new CounterboreHoleConcept(),
            ("counterboreDepth", ConceptSchemaValueKind.Length, true, false),
            ("counterboreDiameter", ConceptSchemaValueKind.Length, true, false),
            ("diameter", ConceptSchemaValueKind.Length, true, false),
            ("target", ConceptSchemaValueKind.Target, true, false));

        AssertSchema(
            new CountersinkHoleConcept(),
            ("angle", ConceptSchemaValueKind.Angle, true, false),
            ("countersinkDiameter", ConceptSchemaValueKind.Length, true, false),
            ("diameter", ConceptSchemaValueKind.Length, true, false),
            ("target", ConceptSchemaValueKind.Target, true, false));
    }

    [Fact]
    public void ForgeCsA2_CSharpSchemas_MatchCurrentPhase1Registry()
    {
        var runtimeRegistry = new InspectableForgeRegistry();
        new StandardForgeRuntimeConceptPack().Register(runtimeRegistry);

        var phase1Descriptors = FirmamentV2ForgeConceptRegistry.EnumerateDescriptors();
        Assert.Equal(4, phase1Descriptors.Count);

        foreach (var phase1Descriptor in phase1Descriptors)
        {
            var conceptId = new ConceptId(phase1Descriptor.FamilyName, phase1Descriptor.ConceptName);
            Assert.True(runtimeRegistry.TryResolve(conceptId, out var concept));

            var schema = BuildSchema(concept);
            var expectedFields = phase1Descriptor.Fields.Values
                .OrderBy(field => field.Name, StringComparer.Ordinal)
                .Select(field => new ConceptSchemaField(field.Name, Map(field.Kind), field.Required, false))
                .ToArray();

            Assert.Equal(expectedFields, schema.Fields);
        }
    }

    [Fact]
    public void ForgeCsA2_ExistingFirmamentV2ConceptFixtures_StillPassWithoutBehaviorChange()
    {
        var conceptFixture = ParseFixture("Language/valid/concept-applications-forge.valid.firmfixture");
        Assert.True(conceptFixture.IsSuccess, string.Join(", ", conceptFixture.Diagnostics));
        var conceptReport = FirmamentV2ValidationReportBuilder.Build(conceptFixture, "concept-applications-forge.valid.firmfixture");
        Assert.Equal("valid", conceptReport.Status);
        Assert.All(conceptReport.Concepts, concept => Assert.Equal("valid", concept.Status));
        Assert.All(conceptReport.Concepts, concept => Assert.Equal("not-run", concept.DfmStatus));
        Assert.All(conceptReport.Concepts, concept =>
        {
            Assert.NotNull(concept.RuntimeValidation);
            Assert.Equal("Aetheris.Standard", concept.RuntimeValidation!.Provider);
            Assert.Equal("valid", concept.RuntimeValidation.Status);
        });

        var reportFixture = ParseFixture("Language/valid/v2-phase1-validation-report.valid.firmfixture");
        Assert.True(reportFixture.IsSuccess, string.Join(", ", reportFixture.Diagnostics));
        var report = FirmamentV2ValidationReportBuilder.Build(reportFixture, "v2-phase1-validation-report.valid.firmfixture");
        Assert.Equal("valid-with-deferred-export", report.Status);
        Assert.Equal(2, report.Summary.ConceptCount);
    }

    private static ConceptSchemaBuilder BuildSchema(IForgeConcept concept)
    {
        var schema = new ConceptSchemaBuilder();
        concept.Define(schema);
        return schema;
    }

    private static void AssertSchema(IForgeConcept concept, params (string Name, ConceptSchemaValueKind Kind, bool Required, bool RequiresTolerance)[] expectedFields)
    {
        var schema = BuildSchema(concept);
        Assert.Equal(
            expectedFields.Select(field => new ConceptSchemaField(field.Name, field.Kind, field.Required, field.RequiresTolerance)).OrderBy(field => field.Name, StringComparer.Ordinal).ToArray(),
            schema.Fields);
    }

    private static ConceptSchemaValueKind Map(FirmamentV2ForgeFieldKind fieldKind) => fieldKind switch
    {
        FirmamentV2ForgeFieldKind.Target => ConceptSchemaValueKind.Target,
        FirmamentV2ForgeFieldKind.Length => ConceptSchemaValueKind.Length,
        FirmamentV2ForgeFieldKind.Angle => ConceptSchemaValueKind.Angle,
        FirmamentV2ForgeFieldKind.Material => ConceptSchemaValueKind.Material,
        _ => throw new ArgumentOutOfRangeException(nameof(fieldKind), fieldKind, "Unexpected Phase 1 Forge field kind.")
    };

    private static FirmamentV2ParseResult ParseFixture(string relative)
    {
        var path = Path.Combine(FindRepoRoot(), "fixtures", relative);
        return FirmamentV2Parser.Parse(File.ReadAllText(path));
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

    private sealed class InspectableForgeRegistry : IForgeRegistry
    {
        private readonly Dictionary<ConceptId, IForgeConcept> concepts = new();

        public IReadOnlyList<ConceptId> RegisteredIds => concepts.Keys.ToArray();

        public void Register(IForgeConcept concept)
        {
            ArgumentNullException.ThrowIfNull(concept);
            if (concepts.ContainsKey(concept.Id))
            {
                throw new InvalidOperationException($"Forge concept '{concept.Id}' is already registered.");
            }

            concepts.Add(concept.Id, concept);
        }

        public bool TryResolve(ConceptId id, out IForgeConcept concept) => concepts.TryGetValue(id, out concept!);
    }

    private sealed class EmptyVariables : IFirmamentVariables
    {
        public static EmptyVariables Instance { get; } = new();

        public IReadOnlyList<FirmamentVariable> All => [];

        public FirmamentValue GetRequired(string name) => throw new KeyNotFoundException($"Firmament variable '{name}' was not found.");

        public bool TryGet(string name, out FirmamentValue value)
        {
            value = null!;
            return false;
        }
    }
}
