using System.Runtime.InteropServices.JavaScript;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament;
using Aetheris.Kernel.Firmament.Assembly;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Web.Runtime;

public static partial class Program
{
    private const string ContractVersion = "aetheris/web-editor-contract/1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Dictionary<string, WebModelSession> Sessions = new(StringComparer.Ordinal);
    private static int _nextSession;

    public static void Main() { }

    [JSExport]
    public static string Invoke(string requestJson)
    {
        try
        {
            var request = JsonSerializer.Deserialize<WebRequest>(requestJson, JsonOptions)
                ?? throw new WebRuntimeException("invalid-request", "The runtime request was empty.");
            object result = request.Operation switch
            {
                "info" => Info(),
                "compile" => Compile(request),
                "languageComplete" => LanguageComplete(request),
                "languageSchema" => LanguageSchema(),
                "describeConstruct" => DescribeConstruct(request),
                "rewriteField" => RewriteField(request),
                "snapshot" => Session(request).Snapshot,
                "setProperty" => SetProperty(request),
                "setSource" => SetSource(request),
                "rebuild" => Rebuild(request),
                "exportStep" => ExportStep(request),
                "disposeSession" => DisposeSession(request),
                "disposeRuntime" => DisposeRuntime(),
                _ => throw new WebRuntimeException("unknown-operation", $"Unknown runtime operation '{request.Operation}'.")
            };
            return JsonSerializer.Serialize(new { ok = true, result }, JsonOptions);
        }
        catch (WebRuntimeException exception)
        {
            return JsonSerializer.Serialize(new { ok = false, error = new { code = exception.Code, message = exception.Message } }, JsonOptions);
        }
        catch (Exception exception)
        {
            return JsonSerializer.Serialize(new { ok = false, error = new { code = "internal-error", message = "Aetheris could not complete the operation.", details = exception.Message } }, JsonOptions);
        }
    }

    private static object Info() => new
    {
        packageVersion = "2.0.0-preview.3",
        runtimeVersion = typeof(Program).Assembly.GetName().Version?.ToString() ?? "2.0.0",
        contractVersion = ContractVersion,
        language = "Firmament",
        capabilities = new
        {
            compile = true, solidModeling = true, assembly = true, displayMesh = true,
            propertyInspection = true, parameterRebuild = true, stepExport = true,
            sheetMetal = false, fea = false, externalStepImport = false, forgeSubprocess = false,
            cancellation = false, worker = false
        }
    };

    private static object Compile(WebRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Source))
            throw new WebRuntimeException("source-required", "compile requires Firmament source text.");
        var id = $"model-{++_nextSession}";
        var session = new WebModelSession(id, request.Source, request.SourceName ?? "model.firmament");
        var result = session.Rebuild();
        if (result.Success) Sessions[id] = session;
        return result;
    }

    private static object LanguageComplete(WebRequest request)
    {
        if (request.Source is null || request.Offset is null || string.IsNullOrWhiteSpace(request.SourceRevision))
            throw new WebRuntimeException("language-input-required", "languageComplete requires source, offset, and sourceRevision.");
        return FirmamentLanguageService.Complete(request.Source, request.SourceName ?? "model.firmament",
            request.SourceRevision, request.Offset.Value);
    }

    private static object LanguageSchema() => new
    {
        version = FirmamentSemanticSchemas.Version,
        projectionVersion = FirmamentFieldProjector.Version,
        constructs = FirmamentSemanticSchemas.All.Select(construct => new
        {
            id = construct.Id.Value, construct.Name, construct.Context, construct.Entry,
            construct.Description, construct.CompatibilityAlias,
            fields = construct.Fields.Select(field => new
            {
                id = field.Id.Value, field.Name, kind = field.Kind.ToString(), unit = field.Unit.ToString(),
                field.Required, field.Default, field.Choices, field.Description, field.SourceEditable
            }),
            outputs = construct.Outputs.Select(item => new
            {
                id = item.Id.Value, item.Name, item.Kind, item.SourceAddressable, item.SourceRole
            })
        })
    };

    private static object SetProperty(WebRequest request)
    {
        var session = Session(request);
        if (string.IsNullOrWhiteSpace(request.PropertyId) || request.Value is null)
            throw new WebRuntimeException("property-value-required", "setProperty requires propertyId and a typed value.");
        session.SetProperty(request.PropertyId, request.Value);
        return new { accepted = true, revision = session.Revision, dirty = true };
    }

    private static object SetSource(WebRequest request)
    {
        var session = Session(request);
        if (request.Source is null) throw new WebRuntimeException("source-required", "setSource requires source text.");
        session.SetSource(request.Source, request.SourceName);
        return new { accepted = true, revision = session.Revision, dirty = true };
    }

    private static object DescribeConstruct(WebRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ConstructSemanticId))
            throw new WebRuntimeException("construct-required", "describeConstruct requires a semantic ID.");
        return Session(request).DescribeConstruct(request.ConstructSemanticId);
    }

    private static object RewriteField(WebRequest request)
    {
        if (request.Source is null || string.IsNullOrWhiteSpace(request.SourceRevision) ||
            string.IsNullOrWhiteSpace(request.ConstructSemanticId) || string.IsNullOrWhiteSpace(request.FieldId) ||
            request.FieldValue is null || request.BuildRevision is null)
            throw new WebRuntimeException("rewrite-input-required", "rewriteField requires source, revisions, semantic ID, field ID, and a typed value.");
        return Session(request).RewriteField(request);
    }

    private static object Rebuild(WebRequest request) => Session(request).Rebuild();

    private static object ExportStep(WebRequest request)
    {
        var session = Session(request);
        if (session.StepText is null) throw new WebRuntimeException("model-not-built", "The model has no valid STEP artifact.");
        return new { mediaType = "model/step", fileName = session.Name + ".step", base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(session.StepText)), revision = session.Revision };
    }

    private static object DisposeSession(WebRequest request)
    {
        if (request.SessionId is not null) Sessions.Remove(request.SessionId);
        return new { disposed = true };
    }

    private static object DisposeRuntime()
    {
        Sessions.Clear();
        return new { disposed = true };
    }

    private static WebModelSession Session(WebRequest request)
        => request.SessionId is not null && Sessions.TryGetValue(request.SessionId, out var session)
            ? session : throw new WebRuntimeException("session-not-found", "The model session was disposed or does not exist.");

    private sealed record WebRequest(string Operation, string? SessionId = null, string? Source = null,
        string? SourceName = null, string? PropertyId = null, WebPropertyValue? Value = null,
        string? SourceRevision = null, int? Offset = null, string? ConstructSemanticId = null,
        string? FieldId = null, FirmamentProjectedValue? FieldValue = null, int? BuildRevision = null);

    private sealed class WebRuntimeException(string code, string message) : Exception(message)
    {
        public string Code { get; } = code;
    }

    private sealed class WebModelSession(string id, string source, string sourceName)
    {
        private string _source = source;
        private string _sourceName = sourceName;
        private IReadOnlyList<WebProperty> _properties = [];
        private IReadOnlyList<FirmamentConstructProjection> _projections = [];
        private readonly Dictionary<string, WebPropertyValue> _overrides = new(StringComparer.Ordinal);
        private object? _lastSnapshot;
        public string Id { get; } = id;
        public string Name => Path.GetFileNameWithoutExtension(_sourceName);
        public int Revision { get; private set; }
        public string? StepText { get; private set; }
        public object Snapshot => _lastSnapshot ?? throw new WebRuntimeException("model-not-built", "The model has no valid snapshot.");

        public void SetSource(string nextSource, string? nextSourceName)
        {
            _source = nextSource;
            if (!string.IsNullOrWhiteSpace(nextSourceName)) _sourceName = nextSourceName;
            _overrides.Clear();
            _projections = [];
        }

        public object DescribeConstruct(string semanticId)
        {
            var projection = _projections.SingleOrDefault(item => item.SemanticId == semanticId);
            if (projection is null) throw new WebRuntimeException("construct-not-projected", $"No current field projection exists for '{semanticId}'.");
            return new
            {
                version = FirmamentFieldProjector.Version,
                constructId = projection.ConstructId.Value, semanticId = projection.SemanticId,
                sourceRevision = projection.SourceRevision, buildRevision = projection.BuildRevision,
                source = projection.ConstructSpan is { } span ? SourceRefAt(projection.SourceDocument, _source, span.Start, span.Length) : null,
                outputs = FirmamentSemanticSchemas.Get(projection.ConstructId.Value)!.Outputs.Select(item => new
                {
                    id = item.Id.Value, item.Name, item.Kind, item.SourceAddressable, item.SourceRole
                }),
                fields = projection.Fields.Select(field => new
                {
                    fieldId = field.FieldId.Value, name = field.Name, kind = field.Kind.ToString(), unit = field.Unit.ToString(),
                    effectiveValue = field.EffectiveValue, authoredValue = field.AuthoredValue, origin = field.Origin,
                    declaration = field.DeclarationSpan is { } declaration ? SourceRefAt(projection.SourceDocument, _source, declaration.Start, declaration.Length) : null,
                    source = field.ValueSpan is { } value ? SourceRefAt(projection.SourceDocument, _source, value.Start, value.Length) : null,
                    field.Editable, field.ReadOnlyReason
                })
            };
        }

        public object RewriteField(WebRequest request)
        {
            if (request.Source != _source || request.BuildRevision != Revision)
                throw new WebRuntimeException("stale_revision", "The requested source or build revision is stale.");
            var construct = _projections.SingleOrDefault(item => item.SemanticId == request.ConstructSemanticId);
            if (construct is null) throw new WebRuntimeException("construct-not-projected", "The construct has no current field projection.");
            var result = FirmamentFieldProjector.Rewrite(_source, request.SourceRevision!, construct,
                new FirmamentFieldId(request.FieldId!), request.FieldValue!);
            if (!result.Success) throw new WebRuntimeException(result.Code!, result.Message!);
            return new { source = result.NewSource, sourceRevision = result.NewSourceRevision,
                replaced = SourceRefAt(_sourceName, _source, result.ReplacedSpan!.Start, result.ReplacedSpan.Length),
                replacement = result.Replacement };
        }

        public void SetProperty(string propertyId, WebPropertyValue value)
        {
            var property = _properties.SingleOrDefault(candidate => candidate.Id == propertyId)
                ?? throw new WebRuntimeException("property-not-found", $"Editable property '{propertyId}' was not found.");
            if (!property.Writable) throw new WebRuntimeException("property-readonly", $"Property '{propertyId}' is read-only.");
            if (!double.IsFinite(value.Value)) throw new WebRuntimeException("property-value-invalid", "Property values must be finite.");
            if (!string.Equals(value.Unit, property.Unit, StringComparison.Ordinal))
                throw new WebRuntimeException("property-unit-mismatch", $"Property '{property.Name}' requires unit '{property.Unit}'.");
            if (property.Type == "Length" && value.Value <= 0)
                throw new WebRuntimeException("property-out-of-range", $"Property '{property.Name}' must be greater than zero.");
            _overrides[propertyId] = value;
        }

        public WebBuildResult Rebuild()
        {
            var sourceProperties = ParameterInspector.Inspect(Id, _sourceName, _source);
            var effective = ApplyOverrides(_source, sourceProperties, _overrides);
            var nextProperties = sourceProperties;
            foreach (var (propertyId, value) in _overrides)
            {
                var index = nextProperties.FindIndex(property => property.Id == propertyId);
                if (index >= 0) nextProperties[index] = nextProperties[index] with { Value = value.Value };
            }
            var assembly = Regex.IsMatch(effective, @"(?m)^\s*Assembly\s+", RegexOptions.CultureInvariant);
            var compiled = assembly ? CompileAssembly(effective, nextProperties) : CompilePart(effective, nextProperties);
            if (!compiled.Success)
                return compiled with { Revision = Revision, RetainedPreviousGeometry = _lastSnapshot is not null };

            Revision++;
            _properties = nextProperties;
            _projections = compiled.Projections ?? [];
            StepText = compiled.StepText;
            var changes = new
            {
                changedEntityIds = compiled.EntityIds,
                addedEntityIds = Revision == 1 ? compiled.EntityIds : Array.Empty<string>(),
                removedEntityIds = Array.Empty<string>(),
                meshChanged = true,
                diagnosticsChanged = false
            };
            _lastSnapshot = new
            {
                id = Id, name = Name, revision = Revision, source = _source, sourceName = _sourceName,
                tree = compiled.Tree, properties = _properties, mesh = compiled.Mesh,
                diagnostics = compiled.Diagnostics, changes, timings = compiled.Timings
            };
            return compiled with { Revision = Revision, Model = _lastSnapshot, Changes = changes, StepText = null };
        }

        private WebBuildResult CompilePart(string effective, List<WebProperty> properties)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var build = FirmamentBuildAndExport.CompileSource(effective);
            var compileMs = watch.Elapsed.TotalMilliseconds;
            if (!build.IsSuccess || build.Value is null)
                return WebBuildResult.Failed(Diagnostics(build.Diagnostics, _sourceName), compileMs);
            var step = build.Value.StepText;
            BrepBody displayBody;
            if (build.Value.RuntimeBody is { } canonicalBody) displayBody = canonicalBody;
            else
            {
                var import = Step242Importer.ImportBody(step);
                if (!import.IsSuccess || import.Value is null)
                    return WebBuildResult.Failed(Diagnostics(import.Diagnostics, _sourceName), compileMs);
                displayBody = import.Value;
            }
            watch.Restart();
            var definitionId = "definition:" + Sha(step)[..16].ToLowerInvariant();
            var entityId = build.Value.RuntimeCorrespondence?.BodyStableId
                ?? (string.IsNullOrWhiteSpace(build.Value.ExportedFeatureId) ? Id + ":body" : build.Value.ExportedFeatureId);
            var sourceMap = build.Value.RuntimeCorrespondence is { } correspondence
                ? new Aetheris.Kernel.Firmament.Materializer.GeometrySourceMap(correspondence) : null;
            WebSourceRef? CompilerSource(string symbol) =>
                sourceMap?.TryGetSourceSpan(symbol, out var span) == true
                    ? SourceRefAt(_sourceName, effective, span.Start, span.Length) : null;
            var boxSource = CompilerSource(entityId);
            // Preserve the exported body category in Kind; report the authored construct separately.
            var parsed = FirmamentV2Parser.Parse(effective);
            var semanticConstruct = boxSource is null ? null : parsed.Document?.Solids
                .FirstOrDefault(solid => string.Equals(solid.Name, entityId, StringComparison.Ordinal))?.RecordType;
            IReadOnlyList<FirmamentConstructProjection> projections = _overrides.Count == 0 && parsed.Document is { } document
                ? FirmamentFieldProjector.Project(document, effective, _sourceName, Revision + 1) : [];
            if (_overrides.Count == 0 && build.Value.WireForm is not null)
            {
                var wire = WireFormAuthoring.Parse(effective);
                if (wire.IsSuccess && wire.Value is not null)
                    projections = [.. projections, .. FirmamentFieldProjector.ProjectWireForm(wire.Value, effective, _sourceName, Revision + 1)];
            }
            if (_overrides.Count == 0 && build.Value.ExportedBodyCategory == "section-chain" && LoftAuthoringParser.IsLoftSource(effective))
            {
                var loft = LoftAuthoringParser.Compile(effective, materialize: false);
                if (FirmamentFieldProjector.ProjectLoft(loft, effective, _sourceName, Revision + 1) is { } projection)
                    projections = [.. projections, projection];
            }
            semanticConstruct ??= projections.FirstOrDefault(item => item.SemanticId == entityId)?.ConstructId.Value;
            var featureSources = (build.Value.Features ?? []).ToDictionary(feature => feature.FeatureId,
                feature => CompilerSource(feature.FeatureId), StringComparer.Ordinal);
            var mesh = WebMeshBuilder.Build(Name, definitionId, entityId, displayBody,
                build.Value.RuntimeCorrespondence, boxSource, Revision + 1, featureSources);
            var meshMs = watch.Elapsed.TotalMilliseconds;
            var featureNodes = (build.Value.EngineeringFeatures ?? []).Select(feature => new WebTreeNode(feature.FeatureId, feature.Kind, feature.Name, entityId, [], true,
                    projections.FirstOrDefault(item => item.SemanticId == feature.FeatureId)?.ConstructSpan is { } span
                        ? SourceRefAt(_sourceName, effective, span.Start, span.Length) : null))
                .Concat((build.Value.Features ?? []).Select(feature => new WebTreeNode(feature.FeatureId, feature.Kind, feature.Name, entityId, [], true, featureSources[feature.FeatureId], feature.Diameter))).ToArray();
            var projectedBodySource = projections.FirstOrDefault(item => item.SemanticId == entityId)?.ConstructSpan is { } bodySpan
                ? SourceRefAt(_sourceName, effective, bodySpan.Start, bodySpan.Length) : null;
            var body = new WebTreeNode(entityId, build.Value.ExportedBodyCategory, Name, Id, featureNodes.Select(item => item.Id).ToArray(), true,
                boxSource ?? projectedBodySource ?? SourceRef(_sourceName, 0, effective.Length), SemanticConstruct: semanticConstruct);
            var root = new WebTreeNode(Id, "Model", Name, null, [body.Id], true, SourceRef(_sourceName, 0, effective.Length));
            var tree = new { rootId = root.Id, nodes = new[] { root, body }.Concat(featureNodes).ToArray() };
            return WebBuildResult.Passed(step, tree, mesh, [Id, entityId, .. featureNodes.Select(item => item.Id)], compileMs, meshMs) with { Projections = projections };
        }

        private WebBuildResult CompileAssembly(string effective, List<WebProperty> properties)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var compilation = new AssemblyM1Pipeline().Compile(effective, _sourceName);
            var compileMs = watch.Elapsed.TotalMilliseconds;
            if (!compilation.IsSuccess || compilation.Ir is null || compilation.Geometry is null)
                return WebBuildResult.Failed(compilation.Diagnostics.Select(d => new WebDiagnostic(d.Severity == AssemblyDiagnosticSeverity.Error ? "error" : "warning", d.Code, d.Message, SourceRef(_sourceName, 0, effective.Length))).ToArray(), compileMs);
            var step = AssemblyIrAp242Exporter.Export(compilation);
            if (!step.IsSuccess) return WebBuildResult.Failed(Diagnostics(step.Diagnostics, _sourceName), compileMs);
            watch.Restart();
            var mesh = AssemblyDisplayMeshExporter.Export(compilation);
            var meshMs = watch.Elapsed.TotalMilliseconds;
            var nodes = compilation.Ir.Instances.OrderBy(instance => instance.Path.Segments.Count).Select(instance =>
                new WebTreeNode(instance.StableId, instance.Kind.ToString(), instance.Path.Segments.Last(), instance.ParentStableId, instance.ChildrenStableIds, true, null)).ToArray();
            var children = nodes.Where(node => node.ParentId == compilation.Ir.RootInstanceStableId).Select(node => node.Id).ToArray();
            var root = new WebTreeNode(compilation.Ir.RootInstanceStableId, "Assembly", compilation.Ir.Name, null, children, true, SourceRef(_sourceName, 0, effective.Length));
            var tree = new { rootId = root.Id, nodes = nodes.Prepend(root).DistinctBy(node => node.Id).ToArray() };
            return WebBuildResult.Passed(step.Value, tree, mesh, nodes.Select(node => node.Id).Prepend(root.Id).Distinct().ToArray(), compileMs, meshMs);
        }
    }

    private static string ApplyOverrides(string source, IReadOnlyList<WebProperty> properties, IReadOnlyDictionary<string, WebPropertyValue> overrides)
    {
        var edits = properties.Where(property => overrides.ContainsKey(property.Id)).OrderByDescending(property => property.Start).ToArray();
        var result = source;
        foreach (var property in edits)
        {
            var value = overrides[property.Id];
            result = result[..property.Start] + value.Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + value.Unit + result[(property.Start + property.Length)..];
        }
        return result;
    }

    private static IReadOnlyList<WebDiagnostic> Diagnostics(IEnumerable<KernelDiagnostic> diagnostics, string source)
        => diagnostics.Select(diagnostic => new WebDiagnostic(
            diagnostic.Severity switch { KernelDiagnosticSeverity.Error => "error", KernelDiagnosticSeverity.Warning => "warning", _ => "info" },
            diagnostic.Code.ToString(), diagnostic.Message, SourceRef(source, 0, 0), diagnostic.Source)).ToArray();

    private static WebSourceRef SourceRef(string source, int start, int length) => new(source, 1, 1, start, length);
    private static WebSourceRef SourceRefAt(string name, string text, int start, int length)
    {
        var prefix = text[..start];
        var line = 1 + prefix.Count(character => character == '\n');
        var lastBreak = prefix.LastIndexOf('\n');
        return new(name, line, start - lastBreak, start, length);
    }
    private static string Sha(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

internal static class ParameterInspector
{
    private static readonly Regex Assignment = new(@"(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*:\s*(?<literal>-?(?<number>\d+(?:\.\d+)?)\s*(?<unit>mm|cm|m|deg|rad))(?=\s*[,>\r\n])", RegexOptions.CultureInvariant);
    private static readonly Regex BoxSize = new(@"\bSize\s*:\s*\[\s*(?<width>-?\d+(?:\.\d+)?\s*(?:mm|cm|m))\s*,\s*(?<height>-?\d+(?:\.\d+)?\s*(?:mm|cm|m))\s*,\s*(?<thickness>-?\d+(?:\.\d+)?\s*(?:mm|cm|m))\s*\]", RegexOptions.CultureInvariant);
    private static readonly Regex HoleDiameter = new(@"\bDiameter\s*:\s*(?<diameter>-?\d+(?:\.\d+)?\s*(?:mm|cm|m))", RegexOptions.CultureInvariant);

    public static List<WebProperty> Inspect(string modelId, string sourceName, string source)
    {
        var specialization = Regex.Matches(source, @"=\s*[A-Za-z_][A-Za-z0-9_]*\s*<", RegexOptions.CultureInvariant).LastOrDefault();
        if (specialization is null) return InspectDirectModel(modelId, sourceName, source);
        var start = specialization.Index + specialization.Length;
        var depth = 1; var end = start;
        for (; end < source.Length && depth > 0; end++)
        {
            if (source[end] == '<') depth++;
            else if (source[end] == '>') depth--;
        }
        if (depth != 0) return [];
        var segment = source[start..(end - 1)];
        return Assignment.Matches(segment).Select(match =>
        {
            var unit = match.Groups["unit"].Value;
            var number = double.Parse(match.Groups["number"].Value, System.Globalization.CultureInfo.InvariantCulture);
            var literal = match.Groups["literal"];
            var absolute = start + literal.Index;
            var (line, column) = LineColumn(source, absolute);
            var name = match.Groups["name"].Value;
            return new WebProperty($"{modelId}.parameter.{name}", modelId, name, unit is "deg" or "rad" ? "Angle" : "Length", unit, number, true, absolute, literal.Length, new(sourceName, line, column, absolute, literal.Length));
        }).GroupBy(property => property.Name, StringComparer.Ordinal).Select(group => group.Last()).OrderBy(property => property.Name, StringComparer.Ordinal).ToList();
    }

    private static List<WebProperty> InspectDirectModel(string modelId, string sourceName, string source)
    {
        var result = new List<WebProperty>();
        var size = BoxSize.Match(source);
        if (size.Success)
        {
            Add("Width", size.Groups["width"]);
            Add("Height", size.Groups["height"]);
            Add("Thickness", size.Groups["thickness"]);
        }
        var diameter = HoleDiameter.Match(source);
        if (diameter.Success) Add("HoleDiameter", diameter.Groups["diameter"]);
        return result.OrderBy(property => property.Name, StringComparer.Ordinal).ToList();

        void Add(string name, Group literal)
        {
            var parsed = Regex.Match(literal.Value, @"(?<number>-?\d+(?:\.\d+)?)\s*(?<unit>mm|cm|m)", RegexOptions.CultureInvariant);
            var (line, column) = LineColumn(source, literal.Index);
            result.Add(new($"{modelId}.parameter.{name}", modelId, name, "Length", parsed.Groups["unit"].Value,
                double.Parse(parsed.Groups["number"].Value, System.Globalization.CultureInfo.InvariantCulture), true, literal.Index, literal.Length,
                new(sourceName, line, column, literal.Index, literal.Length)));
        }
    }

    private static (int Line, int Column) LineColumn(string source, int offset)
    {
        var line = 1; var column = 1;
        for (var i = 0; i < offset; i++) { if (source[i] == '\n') { line++; column = 1; } else column++; }
        return (line, column);
    }
}

internal static class WebMeshBuilder
{
    public static object Build(string name, string definitionId, string entityId, BrepBody body,
        Aetheris.Kernel.Firmament.Materializer.SemanticTopologyCorrespondence? correspondence = null,
        WebSourceRef? source = null, int buildRevision = 0, IReadOnlyDictionary<string, WebSourceRef?>? featureSources = null)
    {
        // Browser WASM has no blocking monitor wait. Use the existing synchronous
        // tessellator authority; the Worker transport supplies UI responsiveness.
        var tessellation = BrepDisplayTessellator.Tessellate(body, new DisplayTessellationOptions(double.Pi / 16, .3, 6, 64));
        if (!tessellation.IsSuccess) throw new InvalidOperationException(string.Join("; ", tessellation.Diagnostics.Select(item => item.Message)));
        var positions = new List<double>(); var normals = new List<double>(); var indices = new List<int>(); var ranges = new List<object>();
        var sourceMap = correspondence is null ? null : new Aetheris.Kernel.Firmament.Materializer.GeometrySourceMap(correspondence);
        foreach (var face in tessellation.Value.FacePatches.OrderBy(face => face.FaceId.Value))
        {
            var vertexOffset = positions.Count / 3; var triangleStart = indices.Count / 3;
            positions.AddRange(face.Positions.SelectMany(point => new[] { point.X, point.Y, point.Z }));
            normals.AddRange(face.Normals.SelectMany(normal => new[] { normal.X, normal.Y, normal.Z }));
            indices.AddRange(face.TriangleIndices.Select(index => index + vertexOffset));
            Aetheris.Kernel.Firmament.Materializer.SemanticTopologyDescendant? semanticFace = null;
            if (sourceMap is not null && sourceMap.TryGetByBrepFace(face.FaceId, out var mappedFace)) semanticFace = mappedFace;
            var owner = semanticFace?.ParentStableId is { } parent && featureSources?.ContainsKey(parent) == true ? parent : entityId;
            var faceSource = owner == entityId ? source : featureSources![owner];
            ranges.Add(new { startTriangle = triangleStart, triangleCount = face.TriangleIndices.Count / 3, faceId = $"face:{face.FaceId.Value}", semanticEntityId = owner,
                semanticTopologyId = semanticFace?.StableId, topologyKind = "Face", outputRole = semanticFace?.Role.ToString(), originFeature = semanticFace?.SourceStableId,
                sourceAddressability = semanticFace?.Addressability.ToString() ?? "RuntimeOnly",
                selector = semanticFace?.FirmamentSelector,
                selectorReason = semanticFace is null ? "No compiler-owned source topology mapping is available for this display face."
                    : semanticFace.FirmamentSelector is null ? "Construction identity is known, but Firmament has no qualified source selector for this topology." : null,
                source = semanticFace is null ? null : faceSource, buildRevision });
        }
        return new
        {
            schema = "aetheris/display-mesh/1",
            name,
            units = "mm",
            definitions = new[] { new { id = definitionId, identity = entityId, positions = positions.ToArray(), normals = normals.ToArray(), indices = indices.ToArray(), ranges,
                edges = tessellation.Value.EdgePolylines.Select(edge =>
                {
                    Aetheris.Kernel.Firmament.Materializer.SemanticTopologyDescendant? semanticEdge = null;
                    if (sourceMap is not null && sourceMap.TryGetByBrepEdge(edge.EdgeId, out var mappedEdge)) semanticEdge = mappedEdge;
                    var owner = semanticEdge?.ParentStableId is { } parent && featureSources?.ContainsKey(parent) == true ? parent : entityId;
                    return new { edgeId = $"edge:{edge.EdgeId.Value}", points = edge.Points.Select(point => new[] { point.X, point.Y, point.Z }).ToArray(), closed = edge.IsClosed,
                        semanticEntityId = owner, semanticTopologyId = semanticEdge?.StableId, topologyKind = "Edge", outputRole = semanticEdge?.Role.ToString(), originFeature = semanticEdge?.SourceStableId,
                        sourceAddressability = semanticEdge?.Addressability.ToString() ?? "RuntimeOnly", selector = semanticEdge?.FirmamentSelector,
                        selectorReason = semanticEdge is null ? "Display BRep edge has no construction-owned source correspondence."
                            : semanticEdge.FirmamentSelector is null ? "Construction identity is known, but Firmament has no qualified source selector for this edge." : null,
                        source = semanticEdge is null ? null : owner == entityId ? source : featureSources![owner], buildRevision };
                }).ToArray() } },
            occurrences = new[] { new { id = entityId + ":occurrence", path = name, parentId = (string?)null, definitionId, semanticEntityId = entityId, transform = new double[] { 1,0,0,0, 0,1,0,0, 0,0,1,0, 0,0,0,1 } } }
        };
    }
}

internal sealed record WebPropertyValue(double Value, string Unit);
internal sealed record WebSourceRef(string Source, int Line, int Column, int Start, int Length);
internal sealed record WebProperty(string Id, string OwnerEntityId, string Name, string Type, string Unit, double Value, bool Writable, int Start, int Length, WebSourceRef Source);
internal sealed record WebTreeNode(string Id, string Kind, string Name, string? ParentId, IReadOnlyList<string> Children, bool Visible, WebSourceRef? Source, double? HoleDiameterMm = null, string? SemanticConstruct = null);
internal sealed record WebDiagnostic(string Severity, string Code, string Message, WebSourceRef? Source = null, string? Details = null);
internal sealed record WebBuildResult(bool Success, int Revision, object? Model, IReadOnlyList<WebDiagnostic> Diagnostics, bool RetainedPreviousGeometry,
    object? Tree = null, object? Mesh = null, IReadOnlyList<string>? EntityIds = null, object? Timings = null, object? Changes = null, string? StepText = null,
    IReadOnlyList<FirmamentConstructProjection>? Projections = null)
{
    public static WebBuildResult Failed(IReadOnlyList<WebDiagnostic> diagnostics, double compileMs) => new(false, 0, null, diagnostics, false, Timings: new { compileMilliseconds = compileMs });
    public static WebBuildResult Passed(string step, object tree, object mesh, IReadOnlyList<string> ids, double compileMs, double meshMs) => new(true, 0, null, [], false, tree, mesh, ids, new { compileMilliseconds = compileMs, meshMilliseconds = meshMs }, StepText: step);
}
