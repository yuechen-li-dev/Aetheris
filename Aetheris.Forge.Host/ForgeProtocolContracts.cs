using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aetheris.Forge.Host;

public static class ForgeHostProtocol
{
    public const int Version = 1;
    public const string Name = "Forge Host Protocol";
}

public enum ForgeProtocolDiagnosticSeverity { Info, Warning, Error }
public enum ForgeArtifactKind { StepAp242, FlatStep, Svg }

public sealed record ForgeHostInfo(
    int ProtocolVersion,
    string ProtocolName,
    string AetherisVersion,
    IReadOnlyList<string> Capabilities,
    string Concurrency);

public sealed record ForgeTemplateSummary(
    string Id,
    string DisplayName,
    string Version);

public sealed record ForgeTemplateListResponse(
    int ProtocolVersion,
    IReadOnlyList<ForgeTemplateSummary> Templates);

public sealed record ForgeTemplateParameterDescription(
    string Name,
    string Type,
    bool Required,
    string? Default,
    string? Dimension,
    string? Unit,
    IReadOnlyList<string>? AllowedValues,
    IReadOnlyList<ForgeTemplateParameterDescription>? Fields,
    string Category,
    string? Constraint);

public sealed record ForgeTemplateConstraintDescription(
    string Name,
    string Expression);

public sealed record ForgeTemplateDescription(
    int ProtocolVersion,
    string Id,
    string DisplayName,
    string Version,
    string Documentation,
    IReadOnlyList<ForgeTemplateParameterDescription> Parameters,
    IReadOnlyList<ForgeArtifactKind> Artifacts,
    string Signature,
    string OutputKind,
    IReadOnlyList<ForgeTemplateConstraintDescription> Constraints);

public sealed record ForgeTemplateInvocationRequest(
    int ProtocolVersion,
    IReadOnlyDictionary<string, JsonElement>? Arguments,
    IReadOnlyList<ForgeArtifactKind>? Artifacts);

public sealed record ForgeProtocolDiagnostic(
    string Code,
    ForgeProtocolDiagnosticSeverity Severity,
    string Message,
    string? Target = null,
    string? Source = null);

public sealed record ForgeArtifact(
    ForgeArtifactKind Kind,
    string Name,
    string ContentType,
    long Size,
    string Sha256,
    string Path);

public sealed record ForgeInvocationIdentity(
    int ProtocolVersion,
    string Template,
    string TemplateVersion,
    string AetherisVersion,
    string? Specialization);

public sealed record ForgeTemplateInvocationResult(
    bool Success,
    ForgeInvocationIdentity Identity,
    IReadOnlyList<ForgeProtocolDiagnostic> Diagnostics,
    IReadOnlyList<ForgeArtifact> Artifacts,
    double ExecutionMilliseconds);

public sealed record ForgeProtocolErrorResponse(
    int ProtocolVersion,
    IReadOnlyList<ForgeProtocolDiagnostic> Diagnostics);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(ForgeHostInfo))]
[JsonSerializable(typeof(ForgeTemplateListResponse))]
[JsonSerializable(typeof(ForgeTemplateDescription))]
[JsonSerializable(typeof(ForgeTemplateInvocationRequest))]
[JsonSerializable(typeof(ForgeTemplateInvocationResult))]
[JsonSerializable(typeof(ForgeProtocolErrorResponse))]
[JsonSerializable(typeof(string))]
internal partial class ForgeProtocolJsonContext : JsonSerializerContext;
