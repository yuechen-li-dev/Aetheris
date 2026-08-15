using System.Collections.ObjectModel;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Semantics;

/// <summary>Authored or generated compiler location. Offsets are zero-based.</summary>
public sealed record SemanticSourceSpan(string Source, int Start, int Length)
{
    public static SemanticSourceSpan Generated(string source) => new(source, 0, 0);
}

/// <summary>One acyclic, ordered step in the history of a semantic value.</summary>
public sealed record SemanticProvenance(string Stage, string Identity, string Evidence, SemanticSourceSpan? SourceSpan = null);

public sealed record SemanticType(string Name, bool IsCollection = false)
{
    public override string ToString() => Name + (IsCollection ? "[]" : string.Empty);
}

// Capabilities are structural contracts, rather than one exclusive kind enum.
public interface ISemanticCapability { string Name { get; } }
public sealed record ProfileCapability : ISemanticCapability { public string Name => "ProfileCapable"; }
public sealed record SelectableCapability : ISemanticCapability { public string Name => "Selectable"; }
public sealed record ExactGeometryCapability : ISemanticCapability { public string Name => "ExactGeometryCapable"; }
public sealed record BoundaryRegionCapability : ISemanticCapability { public string Name => "BoundaryRegionCapable"; }
public sealed record ModifyTargetCapability : ISemanticCapability { public string Name => "ModifyTargetCapable"; }
public sealed record ComposeOperandCapability : ISemanticCapability { public string Name => "ComposeOperandCapable"; }
public sealed record BodyCapability : ISemanticCapability { public string Name => "BodyCapable"; }
public sealed record MaterialRegionCapability : ISemanticCapability { public string Name => "MaterialRegionCapable"; }
public sealed record AnalysisRegionCapability : ISemanticCapability { public string Name => "AnalysisRegionCapable"; }
public sealed record AxisCapability : ISemanticCapability { public string Name => "AxisCapable"; }
public sealed record PlaneCapability : ISemanticCapability { public string Name => "PlaneCapable"; }
public sealed record PointCapability : ISemanticCapability { public string Name => "PointCapable"; }
public sealed record DimensionalCapability : ISemanticCapability { public string Name => "DimensionalCapable"; }
public sealed record CurveCapability : ISemanticCapability { public string Name => "CurveCapable"; }
public sealed record BoundaryEdgeCapability : ISemanticCapability { public string Name => "BoundaryEdgeCapable"; }
/// <summary>A fully oriented, authoring-stable rigid assembly datum.</summary>
public sealed record DatumFrameCapability : ISemanticCapability { public string Name => "DatumFrameCapable"; }

public sealed class SemanticCapabilitySet
{
    private readonly IReadOnlyDictionary<Type, ISemanticCapability> values;
    public SemanticCapabilitySet(IEnumerable<ISemanticCapability>? capabilities = null)
    {
        values = new ReadOnlyDictionary<Type, ISemanticCapability>((capabilities ?? [])
            .GroupBy(capability => capability.GetType())
            .ToDictionary(group => group.Key, group => group.Single()));
    }
    public IReadOnlyList<ISemanticCapability> Values => values.Values.OrderBy(value => value.Name, StringComparer.Ordinal).ToArray();
    public bool Supports<T>() where T : class, ISemanticCapability => values.ContainsKey(typeof(T));
    public bool Supports(Type capabilityType) => values.ContainsKey(capabilityType);
}

/// <summary>A bounded exact downstream representation. It is not a service locator.</summary>
public abstract record SemanticBinding(string Kind, string StableBindingIdentity);

public sealed record ExactBrepBodyBinding(BrepBody Body, string BodyStableId)
    : SemanticBinding("ExactBrepBody", BodyStableId);

public sealed record ExactBrepFaceBinding(BrepBody Body, FaceId Face, string RegionStableId)
    : SemanticBinding("ExactBrepFace", RegionStableId);

public sealed record ExactBrepRegionBinding(BrepBody Body, IReadOnlyList<FaceId> Faces, string RegionStableId)
    : SemanticBinding("ExactBrepRegion", RegionStableId);

/// <summary>An authoring-stable directed curve. It deliberately does not expose a raw BRep EdgeId.</summary>
public sealed record ExactCurveBinding(
    CurveGeometry Curve,
    double ParameterStart,
    double ParameterEnd,
    bool FollowsNativeParameter,
    string CurveStableId)
    : SemanticBinding("ExactCurve", CurveStableId);

/// <summary>Implemented by a producer that owns an exact, validated profile representation.</summary>
public abstract record ExactProfileBinding(string ProfileStableId)
    : SemanticBinding("ResolvedProfile2D", ProfileStableId)
{
    public abstract IReadOnlyList<string> ValidateExactProfile();
}

/// <summary>Source-grounded selection evidence; implementations remain producer-owned.</summary>
public abstract record ExactSelectionBinding(string SelectionStableId)
    : SemanticBinding("ExactSelection", SelectionStableId);

public sealed record ConstructionIdentityBinding(string ConstructionStableId)
    : SemanticBinding("ConstructionIdentity", ConstructionStableId);

public sealed record ImportedEntityBinding(string ResourceIdentity, IReadOnlyList<string> EntityIds)
    : SemanticBinding("ImportedEntities", ResourceIdentity + ":" + string.Join(",", EntityIds.Order(StringComparer.Ordinal)));

/// <summary>Exact analytic assembly datum. Coordinates are nominal millimetres in definition-local space.</summary>
public sealed record ExactAxisBinding(
    double OriginX, double OriginY, double OriginZ,
    double DirectionX, double DirectionY, double DirectionZ,
    string AxisStableId) : SemanticBinding("ExactAxis", AxisStableId);

public sealed record ExactPlaneBinding(
    double OriginX, double OriginY, double OriginZ,
    double NormalX, double NormalY, double NormalZ,
    string PlaneStableId) : SemanticBinding("ExactPlane", PlaneStableId);

/// <summary>
/// Exact nominal frame in definition-local millimetres. X/Y/Z are an
/// orthonormal right-handed basis. Unlike a plane, this binds all six rigid
/// placement degrees of freedom without borrowing identity from a BRep face.
/// </summary>
public sealed record ExactDatumFrameBinding(
    double OriginX, double OriginY, double OriginZ,
    double XAxisX, double XAxisY, double XAxisZ,
    double YAxisX, double YAxisY, double YAxisZ,
    double ZAxisX, double ZAxisY, double ZAxisZ,
    string FrameStableId) : SemanticBinding("ExactDatumFrame", FrameStableId);

public sealed record ExactPointBinding(double X, double Y, double Z, string PointStableId)
    : SemanticBinding("ExactPoint", PointStableId);

/// <summary>Symbolic engineering dimension. Tolerance never perturbs nominal geometry.</summary>
public sealed record TolerancedDimensionBinding(
    double Nominal, double LowerTolerance, double UpperTolerance, string Unit,
    string DimensionStableId, string? Direction = null)
    : SemanticBinding("TolerancedDimension", DimensionStableId)
{
    public double Minimum => Nominal + LowerTolerance;
    public double Maximum => Nominal + UpperTolerance;
}

/// <summary>Exact analytic/BRep boundary identity normalized before AnalysisIR; never a mesh identifier.</summary>
public sealed record ExactAnalysisRegionBinding(string BodyStableId, string RegionPath, string? ExactBrepFaceId = null)
    : SemanticBinding("ExactAnalysisRegion", BodyStableId + ":" + RegionPath + ":" + (ExactBrepFaceId ?? "analytic"));

public sealed class SemanticValue
{
    private readonly IReadOnlyDictionary<string, SemanticValue> members;
    private readonly IReadOnlyDictionary<Type, SemanticBinding> bindings;

    public SemanticValue(
        string stableIdentity,
        SemanticType type,
        IEnumerable<ISemanticCapability>? capabilities = null,
        IEnumerable<SemanticBinding>? bindings = null,
        IEnumerable<SemanticValue>? exposedMembers = null,
        IEnumerable<SemanticProvenance>? provenance = null,
        SemanticSourceSpan? authoredSourceSpan = null,
        SemanticSourceSpan? generatedSourceSpan = null,
        string? exposedName = null)
    {
        if (string.IsNullOrWhiteSpace(stableIdentity)) throw new ArgumentException("Stable semantic identity is required.", nameof(stableIdentity));
        StableIdentity = stableIdentity;
        Type = type ?? throw new ArgumentNullException(nameof(type));
        Capabilities = new SemanticCapabilitySet(capabilities);
        this.bindings = new ReadOnlyDictionary<Type, SemanticBinding>((bindings ?? [])
            .GroupBy(binding => binding.GetType())
            .ToDictionary(group => group.Key, group => group.Single()));
        this.members = new ReadOnlyDictionary<string, SemanticValue>((exposedMembers ?? [])
            .ToDictionary(member => member.ExposedName ?? throw new ArgumentException("An exposed member requires ExposedName."), StringComparer.Ordinal));
        Provenance = (provenance ?? []).ToArray();
        AuthoredSourceSpan = authoredSourceSpan;
        GeneratedSourceSpan = generatedSourceSpan;
        ExposedName = exposedName;
    }

    public string StableIdentity { get; }
    public SemanticType Type { get; }
    public SemanticCapabilitySet Capabilities { get; }
    public IReadOnlyList<SemanticBinding> Bindings => bindings.Values.OrderBy(binding => binding.Kind, StringComparer.Ordinal).ToArray();
    public IReadOnlyDictionary<string, SemanticValue> ExposedMembers => members;
    public IReadOnlyList<SemanticProvenance> Provenance { get; }
    public SemanticSourceSpan? AuthoredSourceSpan { get; }
    public SemanticSourceSpan? GeneratedSourceSpan { get; }
    public string? ExposedName { get; }
    public bool TryBinding<T>(out T binding) where T : SemanticBinding
    {
        if (bindings.TryGetValue(typeof(T), out var value)) { binding = (T)value; return true; }
        var assignable = bindings.Values.OfType<T>().FirstOrDefault();
        if (assignable is not null) { binding = assignable; return true; }
        binding = null!; return false;
    }
}

public sealed record SemanticPathSegment(string Name, SemanticSourceSpan SourceSpan);

/// <summary>Resolution context for one stable value. Path syntax never becomes a string lookup API.</summary>
public sealed record SemanticReference(
    SemanticValue Value,
    IReadOnlyList<SemanticPathSegment> ResolvedSegments,
    SemanticSourceSpan ConsumerSourceSpan);

public sealed record SemanticDiagnostic(string Code, string Message, SemanticSourceSpan? SourceSpan = null);

public static class SemanticValueValidator
{
    public const string MissingCapability = "semantic-value-missing-capability";
    public const string NoExactBinding = "semantic-value-no-exact-binding";
    public const string PathMemberMissing = "semantic-path-member-missing";
    public const string PathMemberNotExposed = "semantic-path-member-not-exposed";
    public const string ForgeOutputInvalid = "forge-semantic-output-invalid";

    public static IReadOnlyList<SemanticDiagnostic> Validate(SemanticValue root)
    {
        var diagnostics = new List<SemanticDiagnostic>();
        var identities = new HashSet<string>(StringComparer.Ordinal);
        Visit(root, diagnostics, identities);
        return diagnostics;
    }

    public static SemanticDiagnostic? Require<T>(SemanticReference reference) where T : class, ISemanticCapability =>
        reference.Value.Capabilities.Supports<T>() ? null : new(MissingCapability,
            $"Semantic value '{reference.Value.StableIdentity}' of type '{reference.Value.Type}' lacks {newCapabilityName<T>()}.", reference.ConsumerSourceSpan);

    public static bool TryResolveMember(SemanticReference parent, SemanticPathSegment segment, out SemanticReference? resolved, out SemanticDiagnostic? diagnostic)
    {
        if (!parent.Value.ExposedMembers.TryGetValue(segment.Name, out var member))
        {
            resolved = null;
            diagnostic = new(PathMemberMissing, $"'{parent.Value.StableIdentity}' exists, but does not expose '{segment.Name}'.", segment.SourceSpan);
            return false;
        }
        resolved = new(member, [.. parent.ResolvedSegments, segment], parent.ConsumerSourceSpan);
        diagnostic = null;
        return true;
    }

    private static void Visit(SemanticValue value, List<SemanticDiagnostic> diagnostics, HashSet<string> identities)
    {
        if (!identities.Add(value.StableIdentity))
            diagnostics.Add(new("semantic-value-stable-identity-collision", $"Stable semantic identity '{value.StableIdentity}' occurs more than once.", value.AuthoredSourceSpan));
        RequireBinding<ProfileCapability, ExactProfileBinding>(value, diagnostics);
        if (value.Capabilities.Supports<BoundaryRegionCapability>()
            && !value.TryBinding<ExactBrepFaceBinding>(out _)
            && !value.TryBinding<ExactBrepRegionBinding>(out _)
            && !value.TryBinding<ExactAnalysisRegionBinding>(out _))
            diagnostics.Add(new(NoExactBinding, $"BoundaryRegionCapable on '{value.StableIdentity}' requires an exact BRep face or region binding.", value.AuthoredSourceSpan));
        RequireBinding<BodyCapability, ExactBrepBodyBinding>(value, diagnostics);
        RequireBinding<AxisCapability, ExactAxisBinding>(value, diagnostics);
        RequireBinding<PlaneCapability, ExactPlaneBinding>(value, diagnostics);
        RequireBinding<PointCapability, ExactPointBinding>(value, diagnostics);
        RequireBinding<DimensionalCapability, TolerancedDimensionBinding>(value, diagnostics);
        RequireBinding<CurveCapability, ExactCurveBinding>(value, diagnostics);
        RequireBinding<BoundaryEdgeCapability, ExactCurveBinding>(value, diagnostics);
        RequireBinding<DatumFrameCapability, ExactDatumFrameBinding>(value, diagnostics);
        RequireAnyExactBinding<ExactGeometryCapability>(value, diagnostics);
        RequireAnyExactBinding<SelectableCapability>(value, diagnostics);
        RequireBinding<ComposeOperandCapability, ExactProfileBinding>(value, diagnostics);
        RequireAnyExactBinding<ModifyTargetCapability>(value, diagnostics);
        if (value.TryBinding<ExactProfileBinding>(out var profile))
            diagnostics.AddRange(profile.ValidateExactProfile().Select(message => new SemanticDiagnostic(NoExactBinding, message, value.AuthoredSourceSpan)));
        foreach (var member in value.ExposedMembers.OrderBy(pair => pair.Key, StringComparer.Ordinal)) Visit(member.Value, diagnostics, identities);
    }

    private static void RequireBinding<TCapability, TBinding>(SemanticValue value, List<SemanticDiagnostic> diagnostics)
        where TCapability : class, ISemanticCapability where TBinding : SemanticBinding
    {
        if (value.Capabilities.Supports<TCapability>() && !value.TryBinding<TBinding>(out _))
            diagnostics.Add(new(NoExactBinding, $"{newCapabilityName<TCapability>()} on '{value.StableIdentity}' requires {typeof(TBinding).Name}.", value.AuthoredSourceSpan));
    }

    private static void RequireAnyExactBinding<TCapability>(SemanticValue value, List<SemanticDiagnostic> diagnostics)
        where TCapability : class, ISemanticCapability
    {
        if (!value.Capabilities.Supports<TCapability>()) return;
        if (!value.Bindings.Any(binding => binding is ExactProfileBinding or ExactBrepBodyBinding or ExactBrepFaceBinding or ExactBrepRegionBinding or ExactCurveBinding or ExactSelectionBinding or ExactAnalysisRegionBinding or ExactPointBinding or ExactAxisBinding or ExactPlaneBinding or ExactDatumFrameBinding or TolerancedDimensionBinding))
            diagnostics.Add(new(NoExactBinding, $"{newCapabilityName<TCapability>()} on '{value.StableIdentity}' requires exact producer evidence.", value.AuthoredSourceSpan));
    }

    private static string newCapabilityName<T>() where T : class, ISemanticCapability =>
        ((ISemanticCapability)Activator.CreateInstance(typeof(T))!).Name;
}

public sealed record SemanticValueDescriptor(
    string StableId,
    string? Name,
    string SemanticType,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<string> BindingKinds,
    IReadOnlyList<SemanticValueDescriptor> Members,
    IReadOnlyList<SemanticProvenance> Provenance,
    SemanticSourceSpan? AuthoredSourceSpan,
    SemanticSourceSpan? GeneratedSourceSpan)
{
    public static SemanticValueDescriptor From(SemanticValue value) => new(
        value.StableIdentity,
        value.ExposedName,
        value.Type.ToString(),
        value.Capabilities.Values.Select(capability => capability.Name).ToArray(),
        value.Bindings.Select(binding => binding.Kind).ToArray(),
        value.ExposedMembers.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => From(pair.Value)).ToArray(),
        value.Provenance,
        value.AuthoredSourceSpan,
        value.GeneratedSourceSpan);
}
