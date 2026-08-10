using System.Collections.ObjectModel;
using Aetheris.Forge.Abstractions;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Construction;
using Aetheris.Semantics;

namespace Aetheris.Forge.Extensions;

public enum ForgeOutputClassification
{
    SemanticOnly = 1,
    ConstructionIr = 2,
    ExactBrep = 3,
    ContinuumRegion = 4,
    SurfaceMeshDerived = 5,
    Analysis = 6,
}

public enum ForgeLoweringTarget
{
    ConstructionIr = 1,
    Brep = 2,
    Cir = 3,
    Analysis = 4,
}

public enum ForgeCapabilityDeterminism
{
    Deterministic = 1,
    ExperimentalNonDeterministic = 2,
}

public enum ForgeCapabilityParameterType
{
    Length = 1,
    Angle = 2,
    Integer = 3,
    Real = 4,
    Boolean = 5,
    String = 6,
    ImportedStepResource = 7,
}

public sealed record ForgeCapabilityId(string Value)
{
    public override string ToString() => Value;
}

public sealed record ForgeCapabilityParameter(
    string Name,
    ForgeCapabilityParameterType Type,
    bool Required = true,
    string? DefaultValue = null,
    string? Description = null);

public sealed record ForgeCapabilityDescriptorV1(
    ForgeCapabilityId Id,
    Version Version,
    string ExtensionId,
    Version ExtensionVersion,
    string Description,
    IReadOnlyList<ForgeCapabilityParameter> Inputs,
    ForgeOutputClassification OutputClassification,
    IReadOnlySet<ForgeLoweringTarget> SupportedTargets,
    ForgeCapabilityDeterminism Determinism,
    string ExactnessContract,
    string AdmissionContract,
    string ProvenanceIdentity);

public sealed record ForgeCapabilityValue(
    ForgeCapabilityParameterType Type,
    object Value,
    string CanonicalValue);

public sealed class ForgeCapabilityArguments
{
    private readonly IReadOnlyDictionary<string, ForgeCapabilityValue> values;

    public ForgeCapabilityArguments(IReadOnlyDictionary<string, ForgeCapabilityValue> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        this.values = new ReadOnlyDictionary<string, ForgeCapabilityValue>(
            values.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));
    }

    public IReadOnlyDictionary<string, ForgeCapabilityValue> Values => values;

    public double RequiredNumber(string name, ForgeCapabilityParameterType expectedType)
    {
        if (!values.TryGetValue(name, out var value) || value.Type != expectedType || value.Value is not double number)
            throw new ForgeCapabilityAdmissionException($"Parameter '{name}' must be {expectedType}.");
        return number;
    }

    public string RequiredString(string name)
    {
        if (!values.TryGetValue(name, out var value) || value.Type != ForgeCapabilityParameterType.String || value.Value is not string text)
            throw new ForgeCapabilityAdmissionException($"Parameter '{name}' must be String.");
        return text;
    }
}

public sealed record ForgeCapabilityInvocationContext(
    string InvocationIdentity,
    string SourceIdentity,
    string TemplateIdentity,
    IReadOnlySet<ForgeLoweringTarget> RequestedTargets);

public sealed record ForgeCapabilityOutput(
    ContinuumConstructionDescriptor? Construction,
    BrepBody? ExactBrep = null,
    ContinuumConstructionDescriptor? ContinuumConstruction = null,
    IReadOnlyDictionary<string, string>? Provenance = null,
    SemanticValue? SemanticRoot = null);

public sealed record ForgeExtensionDiagnostic(
    string Code,
    ForgeDiagnosticSeverity Severity,
    string Message,
    string? CapabilityId = null,
    string? SourceIdentity = null);

public sealed record ForgeCapabilityExecutionResult(
    ForgeCapabilityOutput? Output,
    IReadOnlyList<ForgeExtensionDiagnostic> Diagnostics)
{
    public bool IsSuccess => Output is not null && Diagnostics.All(diagnostic => diagnostic.Severity != ForgeDiagnosticSeverity.Error);
    public static ForgeCapabilityExecutionResult Success(ForgeCapabilityOutput output, params ForgeExtensionDiagnostic[] diagnostics) => new(output, diagnostics);
    public static ForgeCapabilityExecutionResult Failure(params ForgeExtensionDiagnostic[] diagnostics) => new(null, diagnostics);
}

public interface IForgeCapability
{
    ForgeCapabilityDescriptorV1 Descriptor { get; }
    ForgeCapabilityExecutionResult Execute(ForgeCapabilityInvocationContext context, ForgeCapabilityArguments arguments);
}

public interface IForgeExtension
{
    string Id { get; }
    Version Version { get; }
    void Register(ForgeExtensionRegistry registry);
}

public sealed record ForgeExtensionRequirement(string ExtensionId, Version Version);

public sealed class ForgeExtensionManifest
{
    public ForgeExtensionManifest(IEnumerable<ForgeExtensionRequirement>? requirements = null)
    {
        Requirements = (requirements ?? [])
            .OrderBy(requirement => requirement.ExtensionId, StringComparer.Ordinal)
            .ThenBy(requirement => requirement.Version)
            .ToArray();
    }

    public IReadOnlyList<ForgeExtensionRequirement> Requirements { get; }
}

public sealed class ForgeExtensionRegistry
{
    private readonly SortedDictionary<string, IForgeCapability> capabilities = new(StringComparer.Ordinal);
    private readonly SortedDictionary<string, Version> extensions = new(StringComparer.Ordinal);

    public void RegisterExtension(IForgeExtension extension)
    {
        ArgumentNullException.ThrowIfNull(extension);
        if (extensions.TryGetValue(extension.Id, out var existing))
            throw new ForgeExtensionRegistrationException(
                existing == extension.Version ? "forge-extension-duplicate" : "forge-extension-version-conflict",
                $"Extension '{extension.Id}' is already registered at version {existing}; version {extension.Version} cannot be added.");
        extensions.Add(extension.Id, extension.Version);
        try
        {
            extension.Register(this);
        }
        catch (Exception exception)
        {
            extensions.Remove(extension.Id);
            foreach (var capability in capabilities.Where(pair => pair.Value.Descriptor.ExtensionId == extension.Id).Select(pair => pair.Key).ToArray())
                capabilities.Remove(capability);
            if (exception is ForgeExtensionRegistrationException) throw;
            throw new ForgeExtensionRegistrationException(
                "forge-extension-initialization-failed",
                $"Extension '{extension.Id}' version {extension.Version} failed during explicit initialization: {exception.GetType().Name}: {exception.Message}",
                exception);
        }
    }

    public void RegisterCapability(IForgeCapability capability)
    {
        ArgumentNullException.ThrowIfNull(capability);
        ValidateDescriptor(capability.Descriptor);
        if (!extensions.TryGetValue(capability.Descriptor.ExtensionId, out var extensionVersion)
            || extensionVersion != capability.Descriptor.ExtensionVersion)
            throw new ForgeExtensionRegistrationException(
                "forge-capability-extension-identity-mismatch",
                $"Capability '{capability.Descriptor.Id}' does not name the extension currently being registered.");
        if (capabilities.TryGetValue(capability.Descriptor.Id.Value, out var existing))
            throw new ForgeExtensionRegistrationException(
                existing.Descriptor.Version == capability.Descriptor.Version ? "forge-capability-id-collision" : "forge-capability-version-conflict",
                $"Capability '{capability.Descriptor.Id}' is already registered at version {existing.Descriptor.Version}; version {capability.Descriptor.Version} cannot be added.");
        capabilities.Add(capability.Descriptor.Id.Value, capability);
    }

    public bool TryResolve(ForgeCapabilityId id, out IForgeCapability capability) => capabilities.TryGetValue(id.Value, out capability!);

    public IReadOnlyList<ForgeCapabilityDescriptorV1> InspectCapabilities() => capabilities.Values.Select(capability => capability.Descriptor).ToArray();

    public IReadOnlyList<ForgeExtensionDiagnostic> ValidateManifest(ForgeExtensionManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return manifest.Requirements.SelectMany(requirement =>
        {
            if (!extensions.TryGetValue(requirement.ExtensionId, out var version))
                return new[] { Error("forge-extension-missing", $"Required extension '{requirement.ExtensionId}' version {requirement.Version} is not registered.") };
            return version == requirement.Version
                ? Array.Empty<ForgeExtensionDiagnostic>()
                : new[] { Error("forge-extension-version-conflict", $"Required extension '{requirement.ExtensionId}' version {requirement.Version} conflicts with registered version {version}.") };
        }).ToArray();
    }

    private static void ValidateDescriptor(ForgeCapabilityDescriptorV1 descriptor)
    {
        if (string.IsNullOrWhiteSpace(descriptor.Id.Value) || string.IsNullOrWhiteSpace(descriptor.ExtensionId)
            || string.IsNullOrWhiteSpace(descriptor.ProvenanceIdentity) || string.IsNullOrWhiteSpace(descriptor.AdmissionContract))
            throw new ForgeExtensionRegistrationException("forge-capability-descriptor-invalid", "Capability identity, extension identity, provenance, and admission contracts are required.");
        if (descriptor.Determinism != ForgeCapabilityDeterminism.Deterministic)
            throw new ForgeExtensionRegistrationException("forge-capability-nondeterministic", $"Capability '{descriptor.Id}' is non-deterministic and cannot enter the M1 compiler registry.");
        var duplicate = descriptor.Inputs.GroupBy(input => input.Name, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
            throw new ForgeExtensionRegistrationException("forge-capability-parameter-collision", $"Capability '{descriptor.Id}' declares parameter '{duplicate.Key}' more than once.");
    }

    private static ForgeExtensionDiagnostic Error(string code, string message) => new(code, ForgeDiagnosticSeverity.Error, message);
}

public sealed class ForgeCapabilityAdmissionException : Exception
{
    public ForgeCapabilityAdmissionException(string message) : base(message) { }
}

public sealed class ForgeExtensionRegistrationException : Exception
{
    public ForgeExtensionRegistrationException(string code, string message, Exception? innerException = null) : base(message, innerException) => Code = code;
    public string Code { get; }
}
