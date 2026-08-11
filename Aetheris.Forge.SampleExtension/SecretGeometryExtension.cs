using System.Globalization;
using Aetheris.Forge.Extensions;
using Aetheris.Kernel.Core.Construction;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Semantics;

namespace MyCompany.SecretGeometry;

public sealed class SecretGeometryExtension : IForgeExtension
{
    public const string ExtensionId = "MyCompany.SecretGeometry";
    public static readonly Version ExtensionVersion = new(1, 0, 0);
    public string Id => ExtensionId;
    public Version Version => ExtensionVersion;
    public ForgeExtensionSafety Safety => ForgeExtensionSafety.Safe;
    public void Register(ForgeExtensionRegistry registry) => registry.RegisterCapability(new SecretCouponCapability());
}

public sealed class SecretCouponCapability : IForgeCapability
{
    public static readonly ForgeCapabilityId CapabilityId = new("MyCompany.SecretGeometry.SecretCoupon");

    public ForgeCapabilityDescriptorV1 Descriptor { get; } = new(
        CapabilityId,
        new Version(1, 0, 0),
        SecretGeometryExtension.ExtensionId,
        SecretGeometryExtension.ExtensionVersion,
        "Private coupon sizing policy lowered to Aetheris standard prismatic ConstructionIR.",
        [
            new("Width", ForgeCapabilityParameterType.Length, Description: "Finished coupon width."),
            new("Depth", ForgeCapabilityParameterType.Length, Description: "Finished coupon depth."),
            new("Height", ForgeCapabilityParameterType.Length, Description: "Finished coupon height."),
        ],
        ForgeOutputClassification.ConstructionIr,
        new HashSet<ForgeLoweringTarget> { ForgeLoweringTarget.ConstructionIr, ForgeLoweringTarget.Brep, ForgeLoweringTarget.Cir },
        ForgeCapabilityDeterminism.Deterministic,
        "Exact planar, linear, closed manifold prism; no mesh-derived geometry.",
        "Width, Depth, and Height must be finite positive millimetre values.",
        "MyCompany.SecretGeometry/SecretCoupon/v1");

    public ForgeCapabilityExecutionResult Execute(ForgeCapabilityInvocationContext context, ForgeCapabilityArguments arguments)
    {
        var width = Positive(arguments.RequiredNumber("Width", ForgeCapabilityParameterType.Length), "Width");
        var depth = Positive(arguments.RequiredNumber("Depth", ForgeCapabilityParameterType.Length), "Depth");
        var height = Positive(arguments.RequiredNumber("Height", ForgeCapabilityParameterType.Length), "Height");
        var profile = new[]
        {
            new Point3D(-width / 2d, -depth / 2d, 0d),
            new Point3D(width / 2d, -depth / 2d, 0d),
            new Point3D(width / 2d, depth / 2d, 0d),
            new Point3D(-width / 2d, depth / 2d, 0d),
        };
        var signature = string.Join("x", new[] { width, depth, height }.Select(value => value.ToString("R", CultureInfo.InvariantCulture)));
        var sourceIdentity = $"{CapabilityId.Value}@1.0.0:{signature}";
        var descriptor = new ContinuumConstructionDescriptor(
            sourceIdentity,
            context.InvocationIdentity,
            [new(-height / 2d, profile), new(height / 2d, profile)],
            [0, 1, 2, 3],
            ["PrismaticExtrude"],
            [context.SourceIdentity, context.TemplateIdentity, CapabilityId.Value, SecretGeometryExtension.ExtensionId + "@1.0.0"]);
        var body = ForgeCapabilityExecutor.MaterializeConstruction(descriptor);
        var planarFaces = body.Bindings.FaceBindings.Select(binding =>
            (binding.FaceId, Plane: body.Geometry.GetSurface(binding.SurfaceGeometryId).Plane)).Where(item => item.Plane is not null).ToArray();
        var topFace = planarFaces.Single(item => item.Plane!.Value.Normal.ToVector().Z > 0.9d).FaceId;
        var loadFace = planarFaces.Single(item => item.Plane!.Value.Normal.ToVector().X > 0.9d).FaceId;
        var semanticSpan = SemanticSourceSpan.Generated(context.SourceIdentity);
        var semanticProvenance = new[]
        {
            new SemanticProvenance("template-specialization", context.TemplateIdentity, context.InvocationIdentity, semanticSpan),
            new SemanticProvenance("forge-capability", CapabilityId.Value + "@1.0.0", SecretGeometryExtension.ExtensionId + "@1.0.0", semanticSpan),
        };
        SemanticValue Face(string name, Aetheris.Kernel.Core.Topology.FaceId face) => new(
            $"forge:{sourceIdentity}.{name}", new("BoundaryRegion"),
            [new BoundaryRegionCapability(), new SelectableCapability(), new ExactGeometryCapability(), new AnalysisRegionCapability(), new ModifyTargetCapability()],
            [new ExactBrepFaceBinding(body, face, $"forge:{sourceIdentity}.{name}"), new ConstructionIdentityBinding(sourceIdentity)],
            provenance: semanticProvenance, generatedSourceSpan: semanticSpan, exposedName: name);
        var semanticRoot = new SemanticValue($"forge:{sourceIdentity}", new("Body"),
            [new BodyCapability(), new SelectableCapability(), new ExactGeometryCapability(), new ModifyTargetCapability()],
            [new ExactBrepBodyBinding(body, $"forge:{sourceIdentity}"), new ConstructionIdentityBinding(sourceIdentity)],
            [Face("TopFace", topFace), Face("LoadRegion", loadFace)], semanticProvenance, generatedSourceSpan: semanticSpan);
        return ForgeCapabilityExecutionResult.Success(new ForgeCapabilityOutput(
            descriptor,
            ExactBrep: body,
            ContinuumConstruction: descriptor,
            Provenance: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["capability"] = CapabilityId.Value + "@1.0.0",
                ["extension"] = SecretGeometryExtension.ExtensionId + "@1.0.0",
                ["source"] = context.SourceIdentity,
                ["template"] = context.TemplateIdentity,
                ["construction"] = sourceIdentity,
            },
            SemanticRoot: semanticRoot));
    }

    private static double Positive(double value, string name)
    {
        if (!double.IsFinite(value) || value <= 0d) throw new ForgeCapabilityAdmissionException($"{name} must be finite and greater than zero.");
        return value;
    }
}
