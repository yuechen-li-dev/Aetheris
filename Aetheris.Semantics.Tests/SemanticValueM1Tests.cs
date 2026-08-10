using Aetheris.Forge.Extensions;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Materializer;
using Aetheris.Semantics;
using Aetheris.FEA.Analysis;
using MyCompany.SecretGeometry;

namespace Aetheris.Semantics.Tests;

public sealed class SemanticValueM1Tests
{
    private static readonly SemanticSourceSpan Span = new("fixture.firmament", 10, 4);

    [Fact]
    public void ConceptPath_NormalizesToDeterministicProfileCapableSemanticValue()
    {
        const string source = """
            Concept Path Outline {
              Start: Point2(0mm, 0mm)
              Heading: 0deg
              Line Bottom { Length: 10mm }
              Line Right { Turn: 90deg Length: 5mm }
              Line Top { Turn: 90deg Length: 10mm }
              Close Left
            }
            Profile Plate From Outline
            Extrude Solid { Profile: Plate From: 0mm To: 2mm }
            """;
        var first = Assert.Single(FirmamentSemanticValues.FromProfilesAndConceptPaths(source));
        var second = Assert.Single(FirmamentSemanticValues.FromProfilesAndConceptPaths(source));

        Assert.Equal(first.StableIdentity, second.StableIdentity);
        Assert.True(first.Capabilities.Supports<ProfileCapability>());
        Assert.True(first.Capabilities.Supports<ComposeOperandCapability>());
        Assert.Empty(SemanticValueValidator.Validate(first));
        var consumed = ProfileSemanticConsumer.RequireProfile(new(first, [], Span), "Compose");
        Assert.True(consumed.IsSuccess, string.Join("; ", consumed.Diagnostics.Select(item => item.Message)));
    }

    [Fact]
    public void ScalarPassedToProfileConsumerGetsCapabilityDiagnostic()
    {
        var scalar = new SemanticValue("scalar:1", new("Length"));
        var result = ProfileSemanticConsumer.RequireProfile(new(scalar, [], Span), "Profile");
        Assert.Equal(SemanticValueValidator.MissingCapability, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void ClaimedProfileWithoutExactBindingIsRejected()
    {
        var falseClaim = new SemanticValue("bad:profile", new("Profile2D"), [new ProfileCapability()]);
        var diagnostic = Assert.Single(SemanticValueValidator.Validate(falseClaim));
        Assert.Equal(SemanticValueValidator.NoExactBinding, diagnostic.Code);
    }

    [Fact]
    public void BodyPassedToBoundaryConsumerGetsCapabilityDiagnostic()
    {
        var body = new SemanticValue("body:analytic", new("Body"));
        var diagnostic = SemanticValueValidator.Require<BoundaryRegionCapability>(new(body, [], Span));
        Assert.Equal(SemanticValueValidator.MissingCapability, diagnostic!.Code);
    }

    [Fact]
    public void MissingPrivateAndRawTopologyMembersUseSegmentSpan()
    {
        var root = new SemanticValue("root", new("Struct"), exposedMembers:
            [new SemanticValue("root.public", new("Point2"), exposedName: "Public")]);
        var reference = new SemanticReference(root, [], Span);
        var segmentSpan = new SemanticSourceSpan("fixture.firmament", 42, 7);

        Assert.False(SemanticValueValidator.TryResolveMember(reference, new("Private", segmentSpan), out _, out var privateDiagnostic));
        Assert.Equal(SemanticValueValidator.PathMemberMissing, privateDiagnostic!.Code);
        Assert.Equal(segmentSpan, privateDiagnostic.SourceSpan);
        Assert.False(SemanticValueValidator.TryResolveMember(reference, new("Face37", segmentSpan), out _, out var rawDiagnostic));
        Assert.Equal(SemanticValueValidator.PathMemberMissing, rawDiagnostic!.Code);
    }

    [Fact]
    public void MeshIdCannotMasqueradeAsAnalysisRegion()
    {
        var meshElement = new SemanticValue("mesh:element:7", new("MeshElement"));
        var diagnostic = SemanticValueValidator.Require<BoundaryRegionCapability>(new(meshElement, [], Span));
        Assert.Equal(SemanticValueValidator.MissingCapability, diagnostic!.Code);
    }

    [Fact]
    public void StableIdentityCollisionsAreRejected()
    {
        var root = new SemanticValue("root", new("Struct"), exposedMembers:
        [
            new SemanticValue("duplicate", new("Point2"), exposedName: "A"),
            new SemanticValue("duplicate", new("Point2"), exposedName: "B"),
        ]);
        Assert.Contains(SemanticValueValidator.Validate(root), diagnostic => diagnostic.Code == "semantic-value-stable-identity-collision");
    }

    [Fact]
    public void ForgeFalseProfileClaimIsRejectedByOrdinarySemanticValidation()
    {
        var registry = new ForgeExtensionRegistry();
        registry.RegisterExtension(new InvalidSemanticExtension());
        var result = ForgeCapabilityExecutor.Execute(registry, InvalidSemanticCapability.Id,
            new("invocation", "source", "template", new HashSet<ForgeLoweringTarget>()), new(new Dictionary<string, ForgeCapabilityValue>()));
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == SemanticValueValidator.ForgeOutputInvalid);
    }

    [Fact]
    public void ForgeSemanticMemberUsesOrdinaryPathSelectionAndFeaConsumers()
    {
        var registry = new ForgeExtensionRegistry();registry.RegisterExtension(new SecretGeometryExtension());
        var arguments = new ForgeCapabilityArguments(new Dictionary<string, ForgeCapabilityValue>
        {
            ["Width"] = new(ForgeCapabilityParameterType.Length, 24d, "24mm"),
            ["Depth"] = new(ForgeCapabilityParameterType.Length, 16d, "16mm"),
            ["Height"] = new(ForgeCapabilityParameterType.Length, 6d, "6mm"),
        });
        var execution=ForgeCapabilityExecutor.Execute(registry,SecretCouponCapability.CapabilityId,
            new("Coupon","fixture.firmament","template:coupon",new HashSet<ForgeLoweringTarget>{ForgeLoweringTarget.Brep}),arguments);
        Assert.True(execution.IsSuccess,string.Join("; ",execution.Diagnostics.Select(item=>item.Message)));
        var root=execution.Output!.SemanticRoot!;var rootRef=new SemanticReference(root,[],Span);
        Assert.True(SemanticValueValidator.TryResolveMember(rootRef,new("TopFace",Span),out var topFace,out var pathDiagnostic),pathDiagnostic?.Message);

        var selection=SemanticValueSelectionConsumer.Resolve(topFace!,"ForgeTop",null,SemanticSelectionRequirement.ExactlyOne);
        Assert.True(selection.Succeeded,string.Join("; ",selection.Diagnostics.Select(item=>item.Message)));
        var analysis=AnalysisSemanticRegionNormalizer.Normalize(topFace!);
        Assert.Null(analysis.Diagnostic);
        Assert.Equal("ExactBrepFace",analysis.Region!.ExactBindingKind);
        Assert.StartsWith("forge:",analysis.Region.SemanticStableId,StringComparison.Ordinal);
    }

    private sealed class InvalidSemanticExtension : IForgeExtension
    {
        public string Id => "tests.invalid-semantic";
        public Version Version => new(1, 0, 0);
        public void Register(ForgeExtensionRegistry registry) => registry.RegisterCapability(new InvalidSemanticCapability());
    }

    private sealed class InvalidSemanticCapability : IForgeCapability
    {
        public static readonly ForgeCapabilityId Id = new("tests.invalid-semantic.profile");
        public ForgeCapabilityDescriptorV1 Descriptor { get; } = new(Id, new(1, 0, 0), "tests.invalid-semantic", new(1, 0, 0),
            "negative fixture", [], ForgeOutputClassification.SemanticOnly, new HashSet<ForgeLoweringTarget>(), ForgeCapabilityDeterminism.Deterministic,
            "none", "bounded negative test", "tests.invalid-semantic/profile/v1");
        public ForgeCapabilityExecutionResult Execute(ForgeCapabilityInvocationContext context, ForgeCapabilityArguments arguments) =>
            ForgeCapabilityExecutionResult.Success(new(null, Provenance: new Dictionary<string, string> { ["capability"] = Id.Value },
                SemanticRoot: new SemanticValue("forge:invalid", new("Profile2D"), [new ProfileCapability()],
                    provenance: [new("forge-capability", Id.Value + "@1.0.0", "tests.invalid-semantic@1.0.0")])));
    }
}
