using Aetheris.Forge.Abstractions;
using Aetheris.Forge.Extensions;
using Aetheris.Forge.Host;
using Aetheris.Forge.Testing;
using MyCompany.SecretGeometry;
using MyCompany.SecretGeometry.Generated;
using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Forge.Host.Tests;

public sealed class ForgeHostM1Tests
{
    private static readonly string ModulePath = Path.Combine(
        FindRepositoryRoot(),
        "Aetheris.Forge.SampleExtension",
        "Templates",
        "SecretGeometry.firmament");

    [Fact]
    public void GeneratedBindingInvokesTemplateCapabilityAndValidatesBrepCirAndProvenance()
    {
        var host = Host();
        var module = host.LoadModule(ModulePath);
        var invocation = ForgeTemplates.SecretCoupon(module, new SecretCouponSpec(24d, 16d, 6d), "PrivateCoupon")
            .WithTargets(ForgeLoweringTarget.Brep, ForgeLoweringTarget.Cir);

        var first = invocation.Compile();
        var second = ForgeTemplates.SecretCoupon(module, new SecretCouponSpec(24d, 16d, 6d), "PrivateCoupon")
            .WithTargets(ForgeLoweringTarget.Brep, ForgeLoweringTarget.Cir)
            .Compile();

        ForgeExtensionAssertions.RequireDeterministic(first, second);
        ForgeExtensionAssertions.RequireValidBrep(first.Artifact!.Body!);
        ForgeExtensionAssertions.RequireStepRoundTrip(first.Artifact.StepText);
        ForgeExtensionAssertions.RequireCompleteProvenance(first.Artifact);
        ForgeExtensionAssertions.RequireCirAssociation(first.Artifact);
        Assert.Single(first.Artifact.Capabilities);
        Assert.Equal(SecretCouponCapability.CapabilityId.Value, first.Artifact.Capabilities[0].CapabilityId);
        Assert.Contains("record-arguments=Spec", first.Artifact.Provenance[1].Evidence, StringComparison.Ordinal);
        Assert.True(first.Artifact.Body!.Topology.Faces.Count() >= 6);
        var semantic = Assert.IsType<Aetheris.Semantics.SemanticValueDescriptor>(first.Artifact.SemanticOutput);
        Assert.Equal("Body", semantic.SemanticType);
        Assert.Contains("BodyCapable", semantic.Capabilities);
        Assert.Equal(new[] { "LoadRegion", "TopFace" }, semantic.Members.Select(member => member.StableId.Split('.').Last()).Order(StringComparer.Ordinal));
        Assert.All(semantic.Members, member =>
        {
            Assert.Contains("BoundaryRegionCapable", member.Capabilities);
            Assert.Contains("ExactBrepFace", member.BindingKinds);
            Assert.Contains(member.Provenance, item => item.Stage == "forge-capability");
        });
        Assert.Equal(semantic.StableId, second.Artifact!.SemanticOutput!.StableId);
    }

    [Fact]
    public void TemplateMetadataIsStableAndBindingMismatchStopsBeforeLowering()
    {
        var host = Host();
        var module = host.LoadModule(ModulePath);
        var template = module.ResolveTemplate("SecretCoupon");
        Assert.Equal("SecretCoupon", template.Metadata.GeneratedBindingName);
        var parameter = Assert.Single(template.Metadata.Parameters);
        Assert.Equal("Spec", parameter.Name);
        Assert.Equal("SecretCouponSpec", parameter.TypeName);

        var result = template.Invoke("BadCoupon").Bind("Spec", new ForgeLength(10)).Compile();
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "forge-template-parameter-mismatch");
        Assert.Equal(TimeSpan.Zero, result.ExtensionLoweringTime);
    }

    [Fact]
    public void MissingExtensionProducesTypedDiagnostic()
    {
        var host = new ForgeHost(
            manifest: new ForgeExtensionManifest([new(SecretGeometryExtension.ExtensionId, SecretGeometryExtension.ExtensionVersion)]));
        var result = ForgeTemplates.SecretCoupon(host.LoadModule(ModulePath), new SecretCouponSpec(20, 12, 4)).Compile();
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "forge-extension-missing");
    }

    [Fact]
    public void UnsafeExtensionRequiresExplicitHostConsent()
    {
        var denied = new ForgeHost([new UnsafeExtension()]);
        var result = ForgeTemplates.SecretCoupon(denied.LoadModule(ModulePath), new SecretCouponSpec(20, 12, 4)).Compile();
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "forge-extension-unsafe-consent-required"
            && diagnostic.Message.Contains("UNSAFE", StringComparison.Ordinal));

        var allowed = new ForgeHost([new UnsafeExtension()], options: new ForgeHostOptions(AllowUnsafeExtensions: true));
        Assert.Contains(allowed.Capabilities, capability => capability.Id.Value == "Unsafe.Capability");
    }

    [Fact]
    public void EnumCaseIsTypedAndNeverStringCoerced()
    {
        var value = ForgeValue.EnumCase("MetricBoltSize", "M8");
        Assert.Equal("MetricBoltSize", value.TypeName);
        Assert.IsType<ForgeEnumCase>(value);
        Assert.Equal("M8", ((ForgeEnumCase)value).CaseName);
        Assert.Throws<ArgumentException>(() => ForgeValue.EnumCase("MetricBoltSize", "M8; Inject"));
    }

    [Fact]
    public void RegistryOrdersMultipleExtensionsAndRejectsCapabilityCollisions()
    {
        var registry = new ForgeExtensionRegistry();
        registry.RegisterExtension(new NamedExtension("Zeta.Extension", "Zeta.Capability"));
        registry.RegisterExtension(new NamedExtension("Alpha.Extension", "Alpha.Capability"));
        Assert.Equal(new[] { "Alpha.Capability", "Zeta.Capability" }, registry.InspectCapabilities().Select(descriptor => descriptor.Id.Value));

        var collision = Assert.Throws<ForgeExtensionRegistrationException>(() => registry.RegisterExtension(new NamedExtension("Collision.Extension", "Alpha.Capability")));
        Assert.Equal("forge-capability-id-collision", collision.Code);
        Assert.Equal(new[] { "Alpha.Capability", "Zeta.Capability" }, registry.InspectCapabilities().Select(descriptor => descriptor.Id.Value));
    }

    [Fact]
    public void VersionConflictAndNonDeterministicCapabilitiesAreRejected()
    {
        var registry = new ForgeExtensionRegistry();
        registry.RegisterExtension(new NamedExtension("Versioned.Extension", "Versioned.One"));
        var conflict = Assert.Throws<ForgeExtensionRegistrationException>(() => registry.RegisterExtension(new NamedExtension("Versioned.Extension", "Versioned.Two", new Version(2, 0, 0))));
        Assert.Equal("forge-extension-version-conflict", conflict.Code);

        var nondeterministic = Assert.Throws<ForgeExtensionRegistrationException>(() => new ForgeExtensionRegistry().RegisterExtension(new NamedExtension("Random.Extension", "Random.Capability", determinism: ForgeCapabilityDeterminism.ExperimentalNonDeterministic)));
        Assert.Equal("forge-capability-nondeterministic", nondeterministic.Code);
    }

    [Fact]
    public void HostReportsRegistrationConflictAndExecutorWrapsPluginException()
    {
        var host = new ForgeHost([
            new NamedExtension("First.Extension", SecretCouponCapability.CapabilityId.Value),
            new NamedExtension("Second.Extension", SecretCouponCapability.CapabilityId.Value),
        ]);
        var compile = ForgeTemplates.SecretCoupon(host.LoadModule(ModulePath), new SecretCouponSpec(20, 12, 4)).Compile();
        Assert.Contains(compile.Diagnostics, diagnostic => diagnostic.Code == "forge-capability-id-collision");

        var registry = new ForgeExtensionRegistry();
        registry.RegisterExtension(new ThrowingExtension());
        var execution = ForgeCapabilityExecutor.Execute(
            registry,
            new ForgeCapabilityId("Throwing.Capability"),
            new ForgeCapabilityInvocationContext("test", "test", "test", new HashSet<ForgeLoweringTarget> { ForgeLoweringTarget.ConstructionIr }),
            new ForgeCapabilityArguments(new Dictionary<string, ForgeCapabilityValue>()));
        Assert.False(execution.IsSuccess);
        Assert.Contains(execution.Diagnostics, diagnostic => diagnostic.Code == "forge-capability-exception" && diagnostic.CapabilityId == "Throwing.Capability");
    }

    [Fact]
    public void ImportedStepResourceUsesCanonicalStepPipelineEvidence()
    {
        var path = Path.Combine(FindRepositoryRoot(), "testdata", "firmament", "inline-step", "canonical-box-10x8x6.step");
        var resource = ImportedStepResource.Load("VendorPart", path);
        Assert.True(resource.Canonical);
        Assert.Equal(Path.GetFullPath(path), resource.Path);
        Assert.Equal(64, resource.ContentHash.Length);
    }

    [Fact]
    public void HostInvokesNativeFirmamentTemplateWithoutApplicationSourceGeneration()
    {
        const string source = """
            Concept HostBoxConcept {
                Bounds: Box3
                TopPlane: Plane
                ChamferDistance: Length
            }
            Template < Width: Length, Depth: Length, Height: Length >
            Struct HostBox: HostBoxConcept {
                Concept Struct Design: HostBoxConcept {
                    Bounds: Box3 {
                        Size: [Width, Depth, Height]
                    }
                    TopPlane: Bounds.Face(+Z)
                    ChamferDistance: 1mm
                }
                Box Body {
                    Bounds: Design.Bounds
                }
                Modify Body {
                    EdgeFinish TopBreak {
                        Face: Design.TopPlane
                        Target: Boundary
                        Kind: Chamfer
                        Distance: Design.ChamferDistance
                    }
                }
                Expose {
                    Bounds: Design.Bounds
                    TopPlane: Body.Top
                    ChamferDistance: Design.ChamferDistance
                }
            }
            """;
        var expanded = FirmamentTemplateHostBridge.Expand(source, "HostBox", "SdkBox", new Dictionary<string, FirmamentHostArgument>
        {
            ["Width"] = new("20mm"), ["Depth"] = new("12mm"), ["Height"] = new("5mm"),
        }, out var bridgeDiagnostics);
        Assert.NotNull(expanded);
        var parsed = FirmamentV2Parser.Parse(expanded!.ExpandedSource);
        Assert.True(parsed.IsSuccess, string.Join(Environment.NewLine, parsed.Diagnostics) + Environment.NewLine + expanded.ExpandedSource);
        var result = new ForgeHost()
            .LoadModule("NativeBox", source)
            .ResolveTemplate("HostBox")
            .Invoke("SdkBox")
            .Bind("Width", new ForgeLength(20))
            .Bind("Depth", new ForgeLength(12))
            .Bind("Height", new ForgeLength(5))
            .Compile();
        ForgeExtensionAssertions.RequireSuccessfulCompilation(result);
        ForgeExtensionAssertions.RequireStepRoundTrip(result.Artifact!.StepText);
        Assert.Empty(result.Artifact.Capabilities);
    }

    private static ForgeHost Host() => new(
        [new SecretGeometryExtension()],
        new ForgeExtensionManifest([new(SecretGeometryExtension.ExtensionId, SecretGeometryExtension.ExtensionVersion)]));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Aetheris repository root not found.");
    }

    private sealed class NamedExtension : IForgeExtension
    {
        private readonly string capabilityId;
        private readonly ForgeCapabilityDeterminism determinism;
        public NamedExtension(string id, string capabilityId, Version? version = null, ForgeCapabilityDeterminism determinism = ForgeCapabilityDeterminism.Deterministic)
        { Id = id; this.capabilityId = capabilityId; Version = version ?? new Version(1, 0, 0); this.determinism = determinism; }
        public string Id { get; }
        public Version Version { get; }
        public void Register(ForgeExtensionRegistry registry) => registry.RegisterCapability(new StubCapability(Id, Version, capabilityId, determinism));
    }

    private sealed class StubCapability : IForgeCapability
    {
        public StubCapability(string extension, Version extensionVersion, string id, ForgeCapabilityDeterminism determinism) => Descriptor = new(
            new(id), new Version(1, 0, 0), extension, extensionVersion, "test", [], ForgeOutputClassification.SemanticOnly,
            new HashSet<ForgeLoweringTarget> { ForgeLoweringTarget.ConstructionIr }, determinism, "semantic", "test", extension + "/" + id);
        public ForgeCapabilityDescriptorV1 Descriptor { get; }
        public ForgeCapabilityExecutionResult Execute(ForgeCapabilityInvocationContext context, ForgeCapabilityArguments arguments) =>
            ForgeCapabilityExecutionResult.Failure(new ForgeExtensionDiagnostic("not-invoked", ForgeDiagnosticSeverity.Error, "test"));
    }

    private sealed class ThrowingExtension : IForgeExtension
    {
        public string Id => "Throwing.Extension";
        public Version Version => new(1, 0, 0);
        public void Register(ForgeExtensionRegistry registry) => registry.RegisterCapability(new ThrowingCapability());
    }

    private sealed class UnsafeExtension : IForgeExtension
    {
        public string Id => "Unsafe.Extension";
        public Version Version => new(1, 0, 0);
        public ForgeExtensionSafety Safety => ForgeExtensionSafety.UNSAFE;
        public void Register(ForgeExtensionRegistry registry) => registry.RegisterCapability(
            new StubCapability(Id, Version, "Unsafe.Capability", ForgeCapabilityDeterminism.Deterministic));
    }

    private sealed class ThrowingCapability : IForgeCapability
    {
        public ForgeCapabilityDescriptorV1 Descriptor { get; } = new(
            new("Throwing.Capability"), new Version(1, 0, 0), "Throwing.Extension", new Version(1, 0, 0), "test", [],
            ForgeOutputClassification.ConstructionIr, new HashSet<ForgeLoweringTarget> { ForgeLoweringTarget.ConstructionIr },
            ForgeCapabilityDeterminism.Deterministic, "exact", "test", "throwing/test");
        public ForgeCapabilityExecutionResult Execute(ForgeCapabilityInvocationContext context, ForgeCapabilityArguments arguments) =>
            throw new InvalidOperationException("private implementation detail");
    }
}
