using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Aetheris.Continuum.Boundaries;
using Aetheris.Continuum.Cir;
using Aetheris.Continuum.Regions.Analytic;
using Aetheris.Forge.Abstractions;
using Aetheris.Forge.Extensions;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.FEA.Abaqus;
using Aetheris.FEA.Firmament;
using Aetheris.FEA.Mechanics;

namespace Aetheris.Forge.Sdk;

public sealed class ForgeHost
{
    private readonly ForgeExtensionRegistry registry;
    private readonly ForgeExtensionManifest manifest;
    private readonly TimeSpan registrationTime;
    private readonly IReadOnlyList<ForgeDiagnostic> registrationDiagnostics;

    public ForgeHost(
        IEnumerable<IForgeExtension>? extensions = null,
        ForgeExtensionManifest? manifest = null)
    {
        registry = new ForgeExtensionRegistry();
        var start = Stopwatch.GetTimestamp();
        var registrationFailures = new List<ForgeDiagnostic>();
        foreach (var extension in extensions ?? [])
        {
            try { registry.RegisterExtension(extension); }
            catch (ForgeExtensionRegistrationException exception)
            {
                registrationFailures.Add(Error(exception.Code, exception.Message, extension.Id));
            }
        }
        registrationTime = Stopwatch.GetElapsedTime(start);
        registrationDiagnostics = registrationFailures;
        this.manifest = manifest ?? new ForgeExtensionManifest();
    }

    public IReadOnlyList<ForgeCapabilityDescriptorV1> Capabilities => registry.InspectCapabilities();

    public ForgeModule LoadModule(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        return LoadModule(Path.GetFileNameWithoutExtension(fullPath), File.ReadAllText(fullPath), fullPath);
    }

    public ForgeModule LoadModule(string name, string source, string? sourcePath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(source);
        var metadata = FirmamentTemplateHostBridge.Inspect(source, out var diagnostics);
        return new ForgeModule(
            this,
            name,
            source,
            sourcePath,
            metadata.Select(template => new ForgeTemplateMetadata(
                name,
                template.Name,
                template.TargetKind,
                template.Parameters.Select(parameter => new ForgeTemplateParameter(
                    parameter.Name,
                    parameter.TypeName,
                    parameter.Kind == FirmamentTemplateParameterKind.Type,
                    parameter.DefaultExpression,
                    parameter.ConstraintConcept)).ToArray(),
                ForgeGeneratedNames.TemplateMethod(template.Name))).ToArray(),
            diagnostics.Select(code => Error(code, code, sourcePath)).ToArray());
    }

    internal ForgeCompilationResult Compile(ForgeInvocation invocation)
    {
        var diagnostics = new List<ForgeDiagnostic>();
        diagnostics.AddRange(invocation.Module.LoadDiagnostics);
        diagnostics.AddRange(registrationDiagnostics);
        diagnostics.AddRange(registry.ValidateManifest(manifest).Select(FromExtensionDiagnostic));
        if (diagnostics.Any(diagnostic => diagnostic.Severity == ForgeDiagnosticSeverity.Error))
            return Failure(diagnostics);

        var bindDiagnostics = ValidateTemplateBindings(invocation);
        diagnostics.AddRange(bindDiagnostics);
        if (bindDiagnostics.Count > 0) return Failure(diagnostics);

        var hostArguments = invocation.Bindings.ToDictionary(
            pair => pair.Key,
            pair => pair.Value is ForgeRecord record
                ? new FirmamentHostArgument(
                    record.RecordType,
                    record.RecordType,
                    record.Fields.ToDictionary(field => field.Key, field => field.Value.CanonicalLiteral, StringComparer.Ordinal))
                : new FirmamentHostArgument(pair.Value.CanonicalLiteral),
            StringComparer.Ordinal);
        var invocationStart = Stopwatch.GetTimestamp();
        var expansion = FirmamentTemplateHostBridge.Expand(
            invocation.Module.Source,
            invocation.Template.Metadata.Name,
            invocation.InstanceName,
            hostArguments,
            out var expansionDiagnostics);
        var invocationTime = Stopwatch.GetElapsedTime(invocationStart);
        diagnostics.AddRange(expansionDiagnostics.Select(code => Error(code, code, invocation.Module.SourcePath)));
        if (expansion is null) return Failure(diagnostics, invocationTime: invocationTime);

        var constructs = ForgeConstructParser.Parse(expansion.ExpandedSource, diagnostics);
        if (diagnostics.Any(diagnostic => diagnostic.Severity == ForgeDiagnosticSeverity.Error))
            return Failure(diagnostics, invocationTime: invocationTime);
        if (constructs.Count == 0)
            return CompileNative(invocation, expansion, diagnostics, invocationTime);
        if (constructs.Count != 1)
        {
            diagnostics.Add(Error("forge-m1-multiple-construction-outputs-unsupported", "M1 admits exactly one materialized Construct output per host invocation.", invocation.Module.SourcePath));
            return Failure(diagnostics, invocationTime: invocationTime);
        }

        var resolutionStart = Stopwatch.GetTimestamp();
        var construct = constructs[0];
        var id = new ForgeCapabilityId(construct.CapabilityId);
        var resolved = registry.TryResolve(id, out var capability);
        var resolutionTime = Stopwatch.GetElapsedTime(resolutionStart);
        if (!resolved)
        {
            diagnostics.Add(Error("forge-capability-missing", $"Capability '{id}' required by Template '{invocation.Template.Metadata.Name}' is not registered.", invocation.Module.SourcePath, id.Value));
            return Failure(diagnostics, invocationTime, resolutionTime);
        }

        var arguments = BindCapabilityArguments(capability.Descriptor, construct, invocation.Resources, diagnostics);
        if (arguments is null) return Failure(diagnostics, invocationTime, resolutionTime);
        var context = new ForgeCapabilityInvocationContext(
            construct.InstanceName,
            invocation.Module.SourcePath ?? invocation.Module.Name,
            expansion.SpecializationIdentity,
            invocation.RequestedTargets);
        var extensionStart = Stopwatch.GetTimestamp();
        var execution = ForgeCapabilityExecutor.Execute(registry, id, context, arguments);
        var extensionTime = Stopwatch.GetElapsedTime(extensionStart);
        diagnostics.AddRange(execution.Diagnostics.Select(FromExtensionDiagnostic));
        if (!execution.IsSuccess || execution.Output is null)
            return Failure(diagnostics, invocationTime, resolutionTime, extensionTime);

        var compilerStart = Stopwatch.GetTimestamp();
        BrepBody body;
        try
        {
            body = execution.Output.ExactBrep
                ?? ForgeCapabilityExecutor.MaterializeConstruction(execution.Output.Construction
                    ?? throw new ForgeCapabilityAdmissionException("Capability emitted neither standard ConstructionIR nor ExactBRep."));
        }
        catch (Exception exception) when (exception is ForgeCapabilityAdmissionException or ArgumentException or InvalidOperationException)
        {
            diagnostics.Add(Error("forge-capability-output-invalid", exception.Message, invocation.Module.SourcePath, id.Value));
            return Failure(diagnostics, invocationTime, resolutionTime, extensionTime);
        }
        var bindingValidation = BrepBindingValidator.Validate(body, true);
        if (!bindingValidation.IsSuccess)
        {
            diagnostics.Add(Error("forge-capability-brep-invalid", string.Join("; ", bindingValidation.Diagnostics.Select(item => item.Message)), invocation.Module.SourcePath, id.Value));
            return Failure(diagnostics, invocationTime, resolutionTime, extensionTime);
        }
        var exported = Step242Exporter.ExportBody(body, new Step242ExportOptions { ProductName = construct.InstanceName });
        if (!exported.IsSuccess)
        {
            diagnostics.Add(Error("forge-capability-step-export-failed", string.Join("; ", exported.Diagnostics.Select(item => item.Message)), invocation.Module.SourcePath, id.Value));
            return Failure(diagnostics, invocationTime, resolutionTime, extensionTime);
        }
        var reimport = Step242Importer.ImportBody(exported.Value);
        if (!reimport.IsSuccess || reimport.Value is null)
        {
            diagnostics.Add(Error("forge-capability-step-reimport-failed", string.Join("; ", reimport.Diagnostics.Select(item => item.Message)), invocation.Module.SourcePath, id.Value));
            return Failure(diagnostics, invocationTime, resolutionTime, extensionTime);
        }

        ForgeCirEvidence? cir = null;
        if (invocation.RequestedTargets.Contains(ForgeLoweringTarget.Cir))
        {
            cir = ValidateBoxCir(execution.Output.ContinuumConstruction ?? execution.Output.Construction, body, construct.InstanceName, diagnostics);
            if (cir is null) return Failure(diagnostics, invocationTime, resolutionTime, extensionTime);
        }
        var compilerTime = Stopwatch.GetElapsedTime(compilerStart);
        var descriptor = capability.Descriptor;
        var capabilityEvidence = new ForgeCapabilityEvidence(
            descriptor.Id.Value,
            descriptor.Version.ToString(),
            descriptor.ExtensionId,
            descriptor.ExtensionVersion.ToString(),
            descriptor.OutputClassification.ToString(),
            descriptor.SupportedTargets.OrderBy(target => target).Select(target => target.ToString()).ToArray());
        var provenance = new[]
        {
            new ForgeProvenanceEntry("host", invocation.Module.SourcePath ?? invocation.Module.Name, invocation.InstanceName),
            new ForgeProvenanceEntry("template", invocation.Template.Metadata.Name, expansion.SpecializationIdentity + ";record-arguments=" + string.Join(",", expansion.RecordArguments.Keys.Order(StringComparer.Ordinal))),
            new ForgeProvenanceEntry("capability", descriptor.Id.Value, $"{descriptor.ExtensionId}@{descriptor.ExtensionVersion};{descriptor.ProvenanceIdentity}"),
            new ForgeProvenanceEntry("construction", execution.Output.Construction?.SourceIdentity ?? "exact-brep", execution.Output.Construction?.SemanticRegionIdentity ?? construct.InstanceName),
            new ForgeProvenanceEntry("artifact", construct.InstanceName, "STEP AP242 export and reimport validated"),
        };
        var hash = ArtifactHash(exported.Value, expansion.SpecializationIdentity, [capabilityEvidence], provenance);
        return new ForgeCompilationResult(
            new ForgeCompilationArtifact(exported.Value, hash, body, cir, [capabilityEvidence], provenance,
                execution.Output.SemanticRoot is null ? null : Aetheris.Semantics.SemanticValueDescriptor.From(execution.Output.SemanticRoot)),
            diagnostics,
            registrationTime,
            resolutionTime,
            invocationTime,
            extensionTime,
            compilerTime);
    }

    internal ForgeAnalysisInvocationResult Analyze(ForgeInvocation invocation, MechanicsSolveOptions? options)
    {
        var diagnostics = new List<ForgeDiagnostic>();
        diagnostics.AddRange(invocation.Module.LoadDiagnostics);
        diagnostics.AddRange(registrationDiagnostics);
        diagnostics.AddRange(ValidateTemplateBindings(invocation));
        foreach (var binding in invocation.Bindings.Where(item => item.Value is ForgeImportedStep step
            && (!invocation.Resources.TryGetValue(step.ResourceName, out var value) || value is not ImportedStepResource)))
            diagnostics.Add(Error("forge-analysis-resource-missing", $"Template parameter '{binding.Key}' refers to a missing ImportedStep resource.", invocation.Module.SourcePath));
        if (diagnostics.Any(item => item.Severity == ForgeDiagnosticSeverity.Error))
            return new(null, null, null, diagnostics, TimeSpan.Zero, TimeSpan.Zero);
        var hostArguments = invocation.Bindings.ToDictionary(
            pair => pair.Key,
            pair => pair.Value is ForgeRecord record
                ? new FirmamentHostArgument(record.RecordType, record.RecordType, record.Fields.ToDictionary(field => field.Key, field => field.Value.CanonicalLiteral, StringComparer.Ordinal))
                : new FirmamentHostArgument(pair.Value.CanonicalLiteral), StringComparer.Ordinal);
        var started = Stopwatch.GetTimestamp();
        var expansion = FirmamentTemplateHostBridge.Expand(invocation.Module.Source, invocation.Template.Metadata.Name, invocation.InstanceName, hostArguments, out var expansionDiagnostics);
        var invocationTime = Stopwatch.GetElapsedTime(started);
        diagnostics.AddRange(expansionDiagnostics.Select(code => Error(code, code, invocation.Module.SourcePath)));
        if (expansion is null) return new(null, null, null, diagnostics, invocationTime, TimeSpan.Zero);
        var resources = invocation.Resources.Values.OfType<ImportedStepResource>().ToDictionary(
            item => item.Name, item => new FirmamentAnalysisResource(item.Name, item.ContentHash, item.Body), StringComparer.Ordinal);
        var compiled = FirmamentAnalysisCompiler.Compile(expansion.ExpandedSource, invocation.Module.SourcePath,
            invocation.Module.SourcePath is null ? null : Path.GetDirectoryName(invocation.Module.SourcePath), resources);
        diagnostics.AddRange(compiled.Diagnostics.Select(item => new ForgeDiagnostic(item.Code,
            item.Severity == Aetheris.FEA.Analysis.AnalysisDiagnosticSeverity.Error ? ForgeDiagnosticSeverity.Error : ForgeDiagnosticSeverity.Warning,
            item.Message, item.Provenance?.Source)));
        if (!compiled.IsSuccess || compiled.Analysis is null) return new(null, null, null, diagnostics, invocationTime, compiled.CompilationTime);
        var native = LinearElasticSolver.Solve(compiled.Analysis, options);
        diagnostics.AddRange(native.Diagnostics.Where(item => item.Severity == Aetheris.FEA.Analysis.AnalysisDiagnosticSeverity.Error).Select(item =>
            new ForgeDiagnostic(item.Code, ForgeDiagnosticSeverity.Error, item.Message, item.Provenance?.Source)));
        if (!native.IsSuccess) return new(compiled.Analysis, native, null, diagnostics, invocationTime, compiled.CompilationTime);
        var abaqus = AbaqusInpExporter.Export(compiled.Analysis,options?.DomainTransform);
        var validation = AbaqusInpValidator.Validate(abaqus.Text);
        if (!validation.IsValid) diagnostics.Add(Error("forge-analysis-abaqus-validation-failed", string.Join("; ", validation.Diagnostics), invocation.Module.SourcePath));
        return new(compiled.Analysis, native, abaqus, diagnostics, invocationTime, compiled.CompilationTime);
    }

    private ForgeCompilationResult CompileNative(
        ForgeInvocation invocation,
        FirmamentHostTemplateExpansion expansion,
        List<ForgeDiagnostic> diagnostics,
        TimeSpan invocationTime)
    {
        var start = Stopwatch.GetTimestamp();
        var result = FirmamentBuildAndExport.CompileSource(expansion.ExpandedSource, invocation.Module.SourcePath is null ? null : Path.GetDirectoryName(invocation.Module.SourcePath));
        var compilerTime = Stopwatch.GetElapsedTime(start);
        diagnostics.AddRange(result.Diagnostics.Select(item => new ForgeDiagnostic(
            item.Code.ToString(),
            item.Severity == Aetheris.Kernel.Core.Diagnostics.KernelDiagnosticSeverity.Error ? ForgeDiagnosticSeverity.Error : ForgeDiagnosticSeverity.Warning,
            item.Message,
            item.Source)));
        if (!result.IsSuccess) return Failure(diagnostics, invocationTime: invocationTime, compilerTime: compilerTime);
        var provenance = new[]
        {
            new ForgeProvenanceEntry("host", invocation.Module.SourcePath ?? invocation.Module.Name, invocation.InstanceName),
            new ForgeProvenanceEntry("template", invocation.Template.Metadata.Name, expansion.SpecializationIdentity + ";record-arguments=" + string.Join(",", expansion.RecordArguments.Keys.Order(StringComparer.Ordinal))),
            new ForgeProvenanceEntry("artifact", result.Value.ExportedFeatureId, result.Value.ExportedFeatureKind ?? result.Value.ExportedBodyCategory),
        };
        var hash = ArtifactHash(result.Value.StepText, expansion.SpecializationIdentity, [], provenance);
        return new ForgeCompilationResult(
            new ForgeCompilationArtifact(result.Value.StepText, hash, null, null, [], provenance),
            diagnostics,
            registrationTime,
            TimeSpan.Zero,
            invocationTime,
            TimeSpan.Zero,
            compilerTime);
    }

    private static IReadOnlyList<ForgeDiagnostic> ValidateTemplateBindings(ForgeInvocation invocation)
    {
        var diagnostics = new List<ForgeDiagnostic>();
        var schema = invocation.Template.Metadata.Parameters.ToDictionary(parameter => parameter.Name, StringComparer.Ordinal);
        foreach (var parameter in schema.Values.Where(parameter => parameter.DefaultValue is null))
            if (!invocation.Bindings.ContainsKey(parameter.Name)) diagnostics.Add(Error("forge-template-parameter-missing", $"Template parameter '{parameter.Name}' is required.", invocation.Module.SourcePath));
        foreach (var binding in invocation.Bindings)
        {
            if (!schema.TryGetValue(binding.Key, out var parameter))
                diagnostics.Add(Error("forge-template-parameter-unknown", $"Template parameter '{binding.Key}' is not declared.", invocation.Module.SourcePath));
            else if (parameter.IsTypeParameter && binding.Value is not ForgeType)
                diagnostics.Add(Error("forge-template-parameter-mismatch", $"Template parameter '{binding.Key}' requires a type argument.", invocation.Module.SourcePath));
            else if (!parameter.IsTypeParameter && binding.Value is ForgeRecord record && !string.Equals(record.RecordType, parameter.TypeName, StringComparison.Ordinal))
                diagnostics.Add(Error("forge-template-parameter-mismatch", $"Template parameter '{binding.Key}' expects Record '{parameter.TypeName}' but received '{record.RecordType}'.", invocation.Module.SourcePath));
            else if (!parameter.IsTypeParameter && binding.Value is not ForgeRecord && !string.Equals(binding.Value.TypeName, parameter.TypeName, StringComparison.Ordinal))
                diagnostics.Add(Error("forge-template-parameter-mismatch", $"Template parameter '{binding.Key}' expects '{parameter.TypeName}' but received '{binding.Value.TypeName}'.", invocation.Module.SourcePath));
        }
        return diagnostics;
    }

    private static ForgeCapabilityArguments? BindCapabilityArguments(
        ForgeCapabilityDescriptorV1 descriptor,
        ForgeConstruct construct,
        IReadOnlyDictionary<string, ForgeResource> resources,
        ICollection<ForgeDiagnostic> diagnostics)
    {
        var result = new Dictionary<string, ForgeCapabilityValue>(StringComparer.Ordinal);
        var schema = descriptor.Inputs.ToDictionary(input => input.Name, StringComparer.Ordinal);
        foreach (var pair in construct.Fields)
        {
            if (!schema.TryGetValue(pair.Key, out var parameter))
            {
                diagnostics.Add(Error("forge-capability-parameter-unknown", $"Capability '{descriptor.Id}' has no parameter '{pair.Key}'.", null, descriptor.Id.Value));
                continue;
            }
            if (!TryParseCapabilityValue(parameter.Type, pair.Value, resources, out var value, out var message))
                diagnostics.Add(Error("forge-capability-parameter-mismatch", $"Parameter '{pair.Key}': {message}", null, descriptor.Id.Value));
            else result[pair.Key] = value;
        }
        foreach (var parameter in descriptor.Inputs.Where(input => input.Required && input.DefaultValue is null))
            if (!result.ContainsKey(parameter.Name)) diagnostics.Add(Error("forge-capability-parameter-missing", $"Capability parameter '{parameter.Name}' is required.", null, descriptor.Id.Value));
        return diagnostics.Any(diagnostic => diagnostic.Severity == ForgeDiagnosticSeverity.Error) ? null : new ForgeCapabilityArguments(result);
    }

    private static bool TryParseCapabilityValue(
        ForgeCapabilityParameterType type,
        string text,
        IReadOnlyDictionary<string, ForgeResource> resources,
        out ForgeCapabilityValue value,
        out string message)
    {
        text = text.Trim();
        if (type == ForgeCapabilityParameterType.ImportedStepResource)
        {
            var name = text.TrimStart('$');
            if (resources.TryGetValue(name, out var resource) && resource is ImportedStepResource step && step.Canonical)
            {
                value = new(type, step, step.ContentHash); message = string.Empty; return true;
            }
            value = null!; message = $"resource '{name}' is missing or is not canonical Aetheris STEP"; return false;
        }
        if (type == ForgeCapabilityParameterType.String && text.StartsWith('"') && text.EndsWith('"'))
        {
            value = new(type, text[1..^1], text); message = string.Empty; return true;
        }
        if (type == ForgeCapabilityParameterType.Boolean && bool.TryParse(text, out var boolean))
        {
            value = new(type, boolean, boolean ? "true" : "false"); message = string.Empty; return true;
        }
        var numericText = type switch
        {
            ForgeCapabilityParameterType.Length when text.EndsWith("mm", StringComparison.Ordinal) => text[..^2],
            ForgeCapabilityParameterType.Angle when text.EndsWith("deg", StringComparison.Ordinal) => text[..^3],
            _ => text,
        };
        if (double.TryParse(numericText, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number))
        {
            value = new(type, number, text); message = string.Empty; return true;
        }
        value = null!; message = $"'{text}' is not a valid {type} value"; return false;
    }

    private static ForgeCirEvidence? ValidateBoxCir(
        Aetheris.Kernel.Core.Construction.ContinuumConstructionDescriptor? descriptor,
        BrepBody body,
        string identity,
        ICollection<ForgeDiagnostic> diagnostics)
    {
        if (descriptor is null || descriptor.Sections.Count != 2)
        {
            diagnostics.Add(Error("forge-capability-cir-output-missing", "CIR was requested but the capability did not emit a supported continuum construction descriptor.", null));
            return null;
        }
        var points = descriptor.Sections.SelectMany(section => section.ProfileVertices.Select(point => new Point3D(point.X, point.Y, section.AxialPosition))).ToArray();
        var bounds = new BoundingBox3D(
            new Point3D(points.Min(point => point.X), points.Min(point => point.Y), points.Min(point => point.Z)),
            new Point3D(points.Max(point => point.X), points.Max(point => point.Y), points.Max(point => point.Z)));
        var region = new AxisAlignedBoxRegion(new RegionId(identity + ":cir"), bounds);
        var association = new CirBrepAssociation(
            region.Id,
            identity,
            body.ShellRepresentation!.OuterShellId.Value.ToString(CultureInfo.InvariantCulture),
            identity,
            descriptor.SourceIdentity);
        var shell = new WholeShellBoundaryQuery(body, association, Transform3D.Identity);
        var consistency = BrepCirConsistencyChecker.Check(region, shell);
        if (!consistency.Passed)
        {
            diagnostics.Add(Error("forge-capability-cir-brep-inconsistent", consistency.Summary, null));
            return null;
        }
        return new ForgeCirEvidence(association, consistency);
    }

    private ForgeCompilationResult Failure(
        IReadOnlyList<ForgeDiagnostic> diagnostics,
        TimeSpan invocationTime = default,
        TimeSpan resolutionTime = default,
        TimeSpan extensionTime = default,
        TimeSpan compilerTime = default) =>
        new(null, diagnostics, registrationTime, resolutionTime, invocationTime, extensionTime, compilerTime);

    private static ForgeDiagnostic Error(string code, string message, string? source, string? capabilityId = null) =>
        new(code, ForgeDiagnosticSeverity.Error, message, source, capabilityId);

    private static ForgeDiagnostic FromExtensionDiagnostic(ForgeExtensionDiagnostic diagnostic) =>
        new(diagnostic.Code, diagnostic.Severity, diagnostic.Message, diagnostic.SourceIdentity, diagnostic.CapabilityId);

    private static string ArtifactHash(
        string step,
        string specialization,
        IReadOnlyList<ForgeCapabilityEvidence> capabilities,
        IReadOnlyList<ForgeProvenanceEntry> provenance)
    {
        var canonical = new StringBuilder(step).Append('\n').Append(specialization).Append('\n');
        foreach (var capability in capabilities.OrderBy(item => item.CapabilityId, StringComparer.Ordinal))
            canonical.Append(capability.CapabilityId).Append('@').Append(capability.CapabilityVersion).Append('|').Append(capability.ExtensionId).Append('@').Append(capability.ExtensionVersion).Append('\n');
        foreach (var entry in provenance) canonical.Append(entry.Stage).Append('|').Append(entry.Identity).Append('|').Append(entry.Evidence).Append('\n');
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }
}

public sealed class ForgeModule
{
    internal ForgeModule(ForgeHost host, string name, string source, string? sourcePath, IReadOnlyList<ForgeTemplateMetadata> templates, IReadOnlyList<ForgeDiagnostic> loadDiagnostics)
    {
        Host = host; Name = name; Source = source; SourcePath = sourcePath; Templates = templates; LoadDiagnostics = loadDiagnostics;
    }
    internal ForgeHost Host { get; }
    public string Name { get; }
    public string Source { get; }
    public string? SourcePath { get; }
    public IReadOnlyList<ForgeTemplateMetadata> Templates { get; }
    internal IReadOnlyList<ForgeDiagnostic> LoadDiagnostics { get; }
    public ForgeTemplate ResolveTemplate(string name)
    {
        var metadata = Templates.SingleOrDefault(template => template.Name == name)
            ?? throw new KeyNotFoundException($"Template '{name}' was not found in module '{Name}'.");
        return new ForgeTemplate(this, metadata);
    }
}

public sealed record ForgeTemplate(ForgeModule Module, ForgeTemplateMetadata Metadata)
{
    public ForgeInvocation Invoke(string instanceName) => new(Module.Host, Module, this, instanceName);
}

public sealed class ForgeInvocation
{
    private readonly Dictionary<string, ForgeValue> bindings = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ForgeResource> resources = new(StringComparer.Ordinal);
    internal ForgeInvocation(ForgeHost host, ForgeModule module, ForgeTemplate template, string instanceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceName);
        Host = host; Module = module; Template = template; InstanceName = instanceName;
    }
    internal ForgeHost Host { get; }
    public ForgeModule Module { get; }
    public ForgeTemplate Template { get; }
    public string InstanceName { get; }
    internal IReadOnlyDictionary<string, ForgeValue> Bindings => bindings;
    internal IReadOnlyDictionary<string, ForgeResource> Resources => resources;
    internal IReadOnlySet<ForgeLoweringTarget> RequestedTargets { get; private set; } = new HashSet<ForgeLoweringTarget> { ForgeLoweringTarget.Brep };
    public ForgeInvocation Bind(string name, ForgeValue value) { ArgumentNullException.ThrowIfNull(value); bindings[name] = value; return this; }
    public ForgeInvocation AddResource(ForgeResource resource) { ArgumentNullException.ThrowIfNull(resource); resources[resource.Name] = resource; return this; }
    public ForgeInvocation WithTargets(params ForgeLoweringTarget[] targets) { RequestedTargets = targets.ToHashSet(); return this; }
    public ForgeCompilationResult Compile() => Host.Compile(this);
    public ForgeAnalysisInvocationResult Analyze(MechanicsSolveOptions? options = null) => Host.Analyze(this, options);
}

public static class ForgeGeneratedNames
{
    public static string TemplateMethod(string templateName)
    {
        var cleaned = Regex.Replace(templateName, "[^A-Za-z0-9_]", "_", RegexOptions.CultureInvariant);
        return char.IsDigit(cleaned[0]) ? "Template_" + cleaned : cleaned;
    }
}

internal sealed record ForgeConstruct(string CapabilityId, string InstanceName, IReadOnlyDictionary<string, string> Fields);

internal static class ForgeConstructParser
{
    private static readonly Regex Header = new(@"\bConstruct\s+(?<capability>[A-Za-z_][A-Za-z0-9_.-]*)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{", RegexOptions.CultureInvariant);
    private static readonly Regex Field = new(@"(?m)^\s*(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*:\s*(?<value>[^\r\n}]+)\s*$", RegexOptions.CultureInvariant);

    public static IReadOnlyList<ForgeConstruct> Parse(string source, ICollection<ForgeDiagnostic> diagnostics)
    {
        var result = new List<ForgeConstruct>();
        foreach (Match header in Header.Matches(source))
        {
            var open = source.IndexOf('{', header.Index);
            var close = MatchingBrace(source, open);
            if (close < 0)
            {
                diagnostics.Add(new ForgeDiagnostic("forge-construct-malformed", ForgeDiagnosticSeverity.Error, "Construct block is missing its closing brace."));
                continue;
            }
            var fields = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Match field in Field.Matches(source[(open + 1)..close]))
            {
                if (!fields.TryAdd(field.Groups["name"].Value, field.Groups["value"].Value.Trim()))
                    diagnostics.Add(new ForgeDiagnostic("forge-construct-parameter-duplicate", ForgeDiagnosticSeverity.Error, $"Construct parameter '{field.Groups["name"].Value}' is duplicated."));
            }
            result.Add(new ForgeConstruct(header.Groups["capability"].Value, header.Groups["name"].Value, fields));
        }
        return result;
    }

    private static int MatchingBrace(string source, int open)
    {
        var depth = 0;
        for (var index = open; index < source.Length; index++)
        {
            if (source[index] == '{') depth++;
            else if (source[index] == '}' && --depth == 0) return index;
        }
        return -1;
    }
}
