using System.Text.Json;
using System.Text.Json.Serialization;
using Aetheris.Forge.Extensions;
using Aetheris.Forge.Sdk;
using Aetheris.Forge.Testing;
using MyCompany.SecretGeometry;
using MyCompany.SecretGeometry.Generated;

var root = FindRepositoryRoot();
var output = Path.Combine(root, "docs", "forge", "artifacts", "m1");
Directory.CreateDirectory(output);
var modulePath = Path.Combine(root, "Aetheris.Forge.SampleExtension", "Templates", "SecretGeometry.firmament");
var manifest = new ForgeExtensionManifest([new(SecretGeometryExtension.ExtensionId, SecretGeometryExtension.ExtensionVersion)]);
var host = new ForgeHost([new SecretGeometryExtension()], manifest);
var module = host.LoadModule(modulePath);
var first = ForgeTemplates.SecretCoupon(module, new SecretCouponSpec(24d, 16d, 6d), "PrivateCoupon")
    .WithTargets(ForgeLoweringTarget.Brep, ForgeLoweringTarget.Cir)
    .Compile();
var second = ForgeTemplates.SecretCoupon(module, new SecretCouponSpec(24d, 16d, 6d), "PrivateCoupon")
    .WithTargets(ForgeLoweringTarget.Brep, ForgeLoweringTarget.Cir)
    .Compile();
ForgeExtensionAssertions.RequireDeterministic(first, second);
ForgeExtensionAssertions.RequireCompleteProvenance(first.Artifact!);
ForgeExtensionAssertions.RequireValidBrep(first.Artifact!.Body!);
ForgeExtensionAssertions.RequireCirAssociation(first.Artifact);
ForgeExtensionAssertions.RequireStepRoundTrip(first.Artifact.StepText);

var json = new JsonSerializerOptions
{
    WriteIndented = true,
    Converters = { new JsonStringEnumConverter() },
};
Write("capabilities.json", host.Capabilities);
Write("template-metadata.json", module.Templates);
Write("invocation.json", new
{
    module = module.Name,
    template = "SecretCoupon",
    instance = "PrivateCoupon",
    generatedBinding = "ForgeTemplates.SecretCoupon(module, new SecretCouponSpec(24, 16, 6))",
    parameters = new { Spec = new { type = "SecretCouponSpec", Width = "24mm", Depth = "16mm", Height = "6mm" } },
    targets = new[] { "Brep", "Cir" },
    success = first.IsSuccess,
    diagnostics = first.Diagnostics,
});
Write("provenance.json", first.Artifact.Provenance);
Write("brep-validation.json", new
{
    bindingValidation = "passed",
    stepExport = "passed",
    stepReimport = "passed",
    vertices = first.Artifact.Body!.Topology.Vertices.Count(),
    edges = first.Artifact.Body.Topology.Edges.Count(),
    faces = first.Artifact.Body.Topology.Faces.Count(),
    shells = first.Artifact.Body.Topology.Shells.Count(),
});
Write("cir-association.json", first.Artifact.Cir!);
Write("determinism.json", new
{
    repeatedRuns = 2,
    sameArtifactHash = first.Artifact.ArtifactHash == second.Artifact!.ArtifactHash,
    sameStepText = first.Artifact.StepText == second.Artifact.StepText,
    artifactHash = first.Artifact.ArtifactHash,
    capability = first.Artifact.Capabilities.Single(),
});
Write("performance.json", new
{
    units = "milliseconds",
    first = Timing(first),
    warm = Timing(second),
    note = "Developer-machine wall-clock evidence; not a benchmark or release gate.",
});
File.WriteAllText(Path.Combine(output, "sample-secret-coupon.step"), first.Artifact.StepText, new System.Text.UTF8Encoding(false));
Console.WriteLine(JsonSerializer.Serialize(new { output, first.Artifact.ArtifactHash, first.IsSuccess }, json));

object Timing(ForgeCompilationResult result) => new
{
    registration = result.RegistrationTime.TotalMilliseconds,
    capabilityResolution = result.ResolutionTime.TotalMilliseconds,
    templateInvocation = result.TemplateInvocationTime.TotalMilliseconds,
    extensionLowering = result.ExtensionLoweringTime.TotalMilliseconds,
    compilerAndValidation = result.CompilerLoweringTime.TotalMilliseconds,
};

void Write(string name, object value) => File.WriteAllText(Path.Combine(output, name), JsonSerializer.Serialize(value, json) + Environment.NewLine, new System.Text.UTF8Encoding(false));

static string FindRepositoryRoot()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent;
    return directory?.FullName ?? throw new DirectoryNotFoundException("Aetheris repository root not found.");
}
