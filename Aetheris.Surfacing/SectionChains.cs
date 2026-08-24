using System.Diagnostics;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;
using System.Text.Json.Serialization;
using Aetheris.Kernel.Core.Judgment;
using Aetheris.Kernel.Core.Visualization;

namespace Aetheris.Surfacing;

public readonly record struct SectionPoint2D(double X, double Y);

/// <summary>A complete, right-handed station frame. Profile coordinates are measured along XAxis/YAxis.</summary>
public sealed record SectionFrame(Point3D Origin, Direction3D XAxis, Direction3D YAxis, Direction3D Normal)
{
    public static SectionFrame Create(Point3D origin, Vector3D xAxis, Vector3D yAxis)
    {
        var x = Direction3D.Create(xAxis);
        var yProjected = yAxis - x.ToVector() * yAxis.Dot(x.ToVector());
        var y = Direction3D.Create(yProjected);
        var normal = Direction3D.Create(x.ToVector().Cross(y.ToVector()));
        return new(origin, x, y, normal);
    }

    public Point3D Transform(SectionPoint2D point) => Origin + XAxis.ToVector() * point.X + YAxis.ToVector() * point.Y;
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$curveKind")]
[JsonDerivedType(typeof(SectionProfileCurve.Line), "Line")]
[JsonDerivedType(typeof(SectionProfileCurve.Arc), "Arc")]
[JsonDerivedType(typeof(SectionProfileCurve.PolynomialBSpline), "PolynomialBSpline")]
public abstract record SectionProfileCurve
{
    public sealed record Line(SectionPoint2D Start, SectionPoint2D End) : SectionProfileCurve;
    public sealed record Arc(SectionPoint2D Center, double Radius, double StartAngleRadians, double SweepAngleRadians) : SectionProfileCurve;
    public sealed record PolynomialBSpline(
        int Degree,
        IReadOnlyList<SectionPoint2D> ControlPoints,
        IReadOnlyList<int> KnotMultiplicities,
        IReadOnlyList<double> KnotValues) : SectionProfileCurve;
}

public sealed record SectionProfileSpan(string SpanId, SectionProfileCurve Curve);
public sealed record SectionProfile(string StableId, IReadOnlyList<SectionProfileSpan> Spans, string SeamSpanId);
public sealed record Section(string SectionId, SectionFrame Frame, SectionProfile Profile);
public sealed record SectionSpanCorrespondence(string SourceSpanId, string TargetSpanId);
public sealed record AdjacentSectionCorrespondence(
    string SourceSectionId,
    string TargetSectionId,
    IReadOnlyList<SectionSpanCorrespondence> Spans,
    string Resolution = "Explicit");

public enum SectionTransitionPolicy { Ruled, SmoothPolynomial }
public enum SectionChainContinuity { G0, G1 }
public enum SectionChainSmoothPolicy { Fair }
public enum SectionTermination { Cap, Open }
public enum SectionChainStructureKind { ClosedSolid, OpenShell }

public sealed record SectionChain(
    string StableId,
    IReadOnlyList<Section> Sections,
    IReadOnlyList<AdjacentSectionCorrespondence> Correspondence,
    SectionTransitionPolicy TransitionPolicy,
    SectionTermination StartTermination,
    SectionTermination EndTermination,
    SectionChainContinuity Continuity = SectionChainContinuity.G0,
    SectionChainSmoothPolicy SmoothPolicy = SectionChainSmoothPolicy.Fair);

public static class SectionChainCanonical
{
    public static string Fingerprint(SectionChain chain)
    {
        var sections = chain.Sections.Select(section =>
        {
            var spans = section.Profile.Spans.Select(span => span.Curve switch
            {
                SectionProfileCurve.Line line => $"{span.SpanId}:L:{line.Start.X:R},{line.Start.Y:R}>{line.End.X:R},{line.End.Y:R}",
                SectionProfileCurve.Arc arc => $"{span.SpanId}:A:{arc.Center.X:R},{arc.Center.Y:R},{arc.Radius:R},{arc.StartAngleRadians:R},{arc.SweepAngleRadians:R}",
                SectionProfileCurve.PolynomialBSpline spline => $"{span.SpanId}:B:{spline.Degree}:{string.Join('/', spline.ControlPoints.Select(point => $"{point.X:R},{point.Y:R}"))}",
                _ => span.SpanId
            });
            return $"{section.SectionId}@{section.Frame.Origin.X:R},{section.Frame.Origin.Y:R},{section.Frame.Origin.Z:R}:{string.Join(',', spans)}";
        });
        return string.Join('|', chain.StableId, chain.TransitionPolicy, chain.Continuity, chain.SmoothPolicy, chain.StartTermination, chain.EndTermination, string.Join(';', sections));
    }
}

public sealed record SectionTransitionSurfaceEvidence(
    string SpanId,
    string SourceBoundaryId,
    string TargetBoundaryId,
    SurfaceGeometryKind SurfaceClass,
    SurfaceMaterializationKind MaterializationKind);

public sealed record SectionTransitionEvidence(
    string TransitionId,
    string SourceSectionId,
    string TargetSectionId,
    IReadOnlyList<SectionTransitionSurfaceEvidence> Surfaces);

public sealed record SectionChainTiming(
    double ProfilePreparationMilliseconds,
    double CorrespondenceResolutionMilliseconds,
    double PairwiseTransitionMilliseconds,
    double StitchingAndTerminationMilliseconds,
    double ValidationMilliseconds);

public sealed record SectionChainPcurveEvidence(
    int PcurveCount,
    int EdgeCount,
    double MaximumReconstructionDeviation,
    bool DomainValid,
    bool OrientationConsistent,
    bool LoopClosureValid,
    double Tolerance);

public sealed record SectionChainSelfIntersectionEvidence(
    string Method,
    bool Passed,
    double Tolerance,
    string AdmittedScope,
    int CandidatePairs,
    int QualifiedPairs);

public sealed record SectionChainMaterializationResult(
    SectionChain Chain,
    BrepBody? Body,
    SectionChainStructureKind StructureKind,
    IReadOnlyList<SectionTransitionEvidence> Transitions,
    IReadOnlyList<SurfacingDiagnostic> Diagnostics,
    SectionChainTiming Timing)
{
    public bool IsSuccess => Body is not null && Diagnostics.Count == 0;
    public SectionChainPcurveEvidence? Pcurves { get; init; }
    public SectionChainSelfIntersectionEvidence? SelfIntersection { get; init; }
    public SectionChainContinuityEvidence? ContinuityEvidence { get; init; }
    public SectionChainSmoothSelectionEvidence? SmoothSelection { get; init; }
    public string? PreviewSvg { get; init; }
}

public sealed record SectionChainBoundaryContinuityEvidence(string SectionId, double MaximumPositionError,
    double MaximumNormalAngleDegrees, double MaximumTangentPlaneAngleDegrees);
public sealed record SectionChainContinuityEvidence(double MaximumPositionError, double MaximumNormalAngleDegrees,
    double MaximumTangentPlaneAngleDegrees, string? WorstBoundary,
    IReadOnlyList<SectionChainBoundaryContinuityEvidence> Boundaries);
public sealed record SectionChainSmoothCandidateEvidence(string Policy, double MagnitudeScale, bool Eligible,
    double BendingEnergy, double CurvatureVariation, double MaximumOvershoot, string? RejectionReason, double Utility);
public sealed record SectionChainSmoothSelectionEvidence(string TangentDerivation, string StationSpacingMetric,
    string EndpointPolicy, int CandidateCount, string SelectedPolicy,
    IReadOnlyList<SectionChainSmoothCandidateEvidence> Candidates);

public sealed record SectionChainEditDelta(
    string ReplacedSection,
    IReadOnlyList<string> RecomputedTangentFields,
    IReadOnlyList<string> RebuiltTransitions,
    IReadOnlyList<string> PreservedTransitions,
    IReadOnlyList<string> PreservedTerminations);

public static class SectionChainEditor
{
    public static (SectionChain Chain, SectionChainEditDelta Delta) ReplaceSection(SectionChain source, Section replacement)
    {
        var index = source.Sections.ToList().FindIndex(section => section.SectionId == replacement.SectionId);
        if (index < 0) throw new ArgumentException($"Section '{replacement.SectionId}' does not exist.", nameof(replacement));
        var sections = source.Sections.ToArray();
        sections[index] = replacement;
        var tangentIndices = source.Continuity == SectionChainContinuity.G1
            ? Enumerable.Range(Math.Max(0, index - 1), Math.Min(sections.Length - 1, index + 1) - Math.Max(0, index - 1) + 1).ToArray()
            : Array.Empty<int>();
        var transitionIndices = source.Continuity == SectionChainContinuity.G1
            ? tangentIndices.SelectMany(i => new[] { i - 1, i }).Where(i => i >= 0 && i < sections.Length - 1).Distinct().Order().ToArray()
            : new[] { index - 1, index }.Where(i => i >= 0 && i < sections.Length - 1).ToArray();
        var rebuilt = transitionIndices.Select(i => TransitionId(sections[i].SectionId, sections[i + 1].SectionId)).ToList();
        var preserved = Enumerable.Range(0, sections.Length - 1)
            .Select(i => TransitionId(sections[i].SectionId, sections[i + 1].SectionId))
            .Where(id => !rebuilt.Contains(id, StringComparer.Ordinal)).ToArray();
        return (source with { Sections = sections }, new(replacement.SectionId,
            tangentIndices.Select(i => sections[i].SectionId).ToArray(), rebuilt, preserved,
            ["StartTermination", "EndTermination"]));
    }

    internal static string TransitionId(string source, string target) => $"{source}->{target}";
}

/// <summary>
/// Materializes the bounded X3 lane: one closed outer loop, ordered one-to-one semantic spans,
/// ruled adjacent transitions, and Cap/Open terminals. Every internal section edge is allocated
/// once and reused by both neighbouring transition faces.
/// </summary>
public static class SectionChainMaterializer
{
    private const double Tolerance = 1e-8;

    public static SectionChainMaterializationResult Materialize(SectionChain chain)
    {
        ArgumentNullException.ThrowIfNull(chain);
        var profileStarted = Stopwatch.GetTimestamp();
        var prepared = chain.Sections.Select(Prepare).ToArray();
        var profileMs = Stopwatch.GetElapsedTime(profileStarted).TotalMilliseconds;

        var correspondenceStarted = Stopwatch.GetTimestamp();
        var diagnostics = Validate(chain, prepared).ToList();
        var resolved = diagnostics.Count == 0 ? ResolveCorrespondence(chain) : [];
        var correspondenceMs = Stopwatch.GetElapsedTime(correspondenceStarted).TotalMilliseconds;
        if (diagnostics.Count > 0)
            return Failure(chain, diagnostics, profileMs, correspondenceMs);

        var transitionStarted = Stopwatch.GetTimestamp();
        var transitionPatches = new List<(PreparedTransition Transition, ISectionChainTransitionPatch[] Patches)>();
        SectionChainSmoothSelectionEvidence? smoothSelection = null;
        if (chain.TransitionPolicy == SectionTransitionPolicy.SmoothPolynomial)
        {
            var smooth = SmoothSectionChainBuilder.Build(chain, prepared.Select(item => item.Boundaries).ToArray(), resolved);
            if (!smooth.IsSuccess)
            {
                diagnostics.AddRange(smooth.Diagnostics);
                return Failure(chain, diagnostics, profileMs, correspondenceMs,
                    Stopwatch.GetElapsedTime(transitionStarted).TotalMilliseconds);
            }
            smoothSelection = smooth.Selection;
            for (var index = 0; index < smooth.Patches.Count; index++)
                transitionPatches.Add((new(index, prepared[index], prepared[index + 1], resolved[index]), smooth.Patches[index]));
        }
        else for (var transitionIndex = 0; transitionIndex < chain.Sections.Count - 1; transitionIndex++)
        {
            var source = prepared[transitionIndex];
            var target = prepared[transitionIndex + 1];
            var map = resolved[transitionIndex];
            var patches = new ISectionChainTransitionPatch[source.Boundaries.Length];
            for (var spanIndex = 0; spanIndex < source.Boundaries.Length; spanIndex++)
            {
                var targetIndex = map[spanIndex];
                var transitionId = SectionChainEditor.TransitionId(source.Section.SectionId, target.Section.SectionId);
                var ir = new RuledSurfaceIr($"{chain.StableId}:{transitionId}:{source.Section.Profile.Spans[spanIndex].SpanId}",
                    RuledConstructionKind.RuledTransition, source.Boundaries[spanIndex], target.Boundaries[targetIndex],
                    new(source.Boundaries[spanIndex].StableId, source.Section.SectionId, "source-section-span"),
                    new(target.Boundaries[targetIndex].StableId, target.Section.SectionId, "target-section-span"));
                var lowered = RuledSurfaceLowering.Lower(ir);
                if (!lowered.IsSuccess || lowered.Patch is null)
                    foreach (var item in lowered.Diagnostics)
                        diagnostics.Add(new SurfacingDiagnostic("section-chain-transition-invalid",
                            $"{transitionId}/{source.Section.Profile.Spans[spanIndex].SpanId}: {item.Message}"));
                else patches[spanIndex] = new RuledSectionChainTransitionPatch(lowered.Patch);
                if (lowered.Patch is { } patch && HasFoldover(new RuledSectionChainTransitionPatch(patch)))
                    diagnostics.Add(new("section-chain-transition-foldover",
                        $"{transitionId}/{source.Section.Profile.Spans[spanIndex].SpanId}: sampled ruled Jacobian is singular or reverses orientation."));
            }
            if (diagnostics.Count == 0)
                transitionPatches.Add((new(transitionIndex, source, target, map), patches));
        }
        var transitionMs = Stopwatch.GetElapsedTime(transitionStarted).TotalMilliseconds;
        if (diagnostics.Count > 0)
            return Failure(chain, diagnostics, profileMs, correspondenceMs, transitionMs);

        var selfIntersection = SectionChainSelfIntersectionValidator.Validate(chain,
            transitionPatches.SelectMany(entry => entry.Patches.Select((patch, span) =>
                new SectionChainValidationPatch(entry.Transition.Index,
                    SectionChainEditor.TransitionId(entry.Transition.Source.Section.SectionId, entry.Transition.Target.Section.SectionId),
                    entry.Transition.Source.Section.Profile.Spans[span].SpanId, patch))).ToArray());
        if (!selfIntersection.Passed)
        {
            diagnostics.Add(new("section-chain-self-intersection", selfIntersection.Detail));
            return Failure(chain, diagnostics, profileMs, correspondenceMs, transitionMs);
        }

        var stitchStarted = Stopwatch.GetTimestamp();
        BrepBody? body = null;
        IReadOnlyList<SectionTransitionEvidence> evidence = [];
        try
        {
            (body, evidence) = BuildBody(chain, prepared, transitionPatches);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            diagnostics.Add(new("section-chain-materialization-failed", exception.Message));
        }
        var stitchMs = Stopwatch.GetElapsedTime(stitchStarted).TotalMilliseconds;

        var validationStarted = Stopwatch.GetTimestamp();
        SectionChainPcurveEvidence? pcurveEvidence = null;
        if (body is not null)
        {
            var bindings = BrepBindingValidator.Validate(body, true);
            if (!bindings.IsSuccess)
                foreach (var item in bindings.Diagnostics)
                    diagnostics.Add(new SurfacingDiagnostic("section-chain-brep-invalid", item.Message));
            var edgeUseCounts = body.Topology.Coedges.GroupBy(coedge => coedge.EdgeId).ToDictionary(group => group.Key, group => group.Count());
            int? expectedUses = chain.StartTermination == SectionTermination.Cap && chain.EndTermination == SectionTermination.Cap ? 2 : null;
            if (expectedUses is not null && edgeUseCounts.Any(pair => pair.Value != expectedUses.Value))
                diagnostics.Add(new("section-chain-shared-topology-invalid", "A capped chain must use every edge exactly twice."));
            var pcurves = BrepPcurveValidator.Validate(body, 1e-5d, requireEveryCoedge: true);
            pcurveEvidence = new(pcurves.PcurveCount, pcurves.EdgeCount, pcurves.MaximumReconstructionDeviation,
                pcurves.DomainValid, pcurves.OrientationConsistent,
                pcurves.LoopClosureValid, 1e-5d);
            if (!pcurves.IsValid)
                foreach (var item in pcurves.Diagnostics)
                    diagnostics.Add(new("section-chain-pcurve-error", item));
        }
        var validationMs = Stopwatch.GetElapsedTime(validationStarted).TotalMilliseconds;
        var continuityEvidence = body is null ? null : MeasureContinuity(chain, transitionPatches);
        if (chain.Continuity == SectionChainContinuity.G1 && continuityEvidence is { MaximumTangentPlaneAngleDegrees: > 1e-3d })
            diagnostics.Add(new("section-chain-g1-verification-failed", $"Maximum tangent-plane discontinuity {continuityEvidence.MaximumTangentPlaneAngleDegrees:R} degrees exceeds 0.001 degrees."));
        if (diagnostics.Count > 0) body = null;
        return new(chain, body, Structure(chain), evidence, diagnostics,
            new(profileMs, correspondenceMs, transitionMs, stitchMs, validationMs))
        { Pcurves = pcurveEvidence, SelfIntersection = selfIntersection.Evidence, ContinuityEvidence = continuityEvidence,
            SmoothSelection = smoothSelection, PreviewSvg = body is null ? null : BrepWireframeSvgRenderer.Render(body).Svg };
    }

    private static IReadOnlyList<SurfacingDiagnostic> Validate(SectionChain chain, IReadOnlyList<PreparedSection> sections)
    {
        var diagnostics = new List<SurfacingDiagnostic>();
        if (string.IsNullOrWhiteSpace(chain.StableId)) diagnostics.Add(new("section-chain-id-invalid", "SectionChain requires a stable identity."));
        if (chain.TransitionPolicy == SectionTransitionPolicy.Ruled && chain.Continuity != SectionChainContinuity.G0)
            diagnostics.Add(new("section-chain-continuity-transition-incompatible", "Ruled transitions require Continuity G0."));
        if (chain.TransitionPolicy == SectionTransitionPolicy.SmoothPolynomial && chain.Continuity != SectionChainContinuity.G1)
            diagnostics.Add(new("section-chain-continuity-transition-incompatible", "SmoothPolynomial transitions currently require Continuity G1."));
        if (sections.Count < 2) diagnostics.Add(new("section-chain-section-count-invalid", "SectionChain requires at least two ordered sections."));
        if (sections.Select(section => section.Section.SectionId).Distinct(StringComparer.Ordinal).Count() != sections.Count)
            diagnostics.Add(new("section-chain-section-id-duplicate", "Section identities must be unique."));

        foreach (var prepared in sections)
        {
            var section = prepared.Section;
            var frame = section.Frame;
            var x = frame.XAxis.ToVector(); var y = frame.YAxis.ToVector(); var n = frame.Normal.ToVector();
            if (double.Abs(x.Dot(y)) > Tolerance || double.Abs(x.Dot(n)) > Tolerance || double.Abs(y.Dot(n)) > Tolerance
                || x.Cross(y).Dot(n) < 1d - Tolerance)
                diagnostics.Add(new("section-chain-frame-invalid", $"Section '{section.SectionId}' frame must be orthonormal and right-handed."));
            if (section.Profile.Spans.Count == 0)
                diagnostics.Add(new("section-chain-profile-empty", $"Section '{section.SectionId}' profile has no spans."));
            if (!section.Profile.Spans.Any(span => span.SpanId == section.Profile.SeamSpanId))
                diagnostics.Add(new("section-chain-profile-seam-ambiguous", $"Section '{section.SectionId}' seam '{section.Profile.SeamSpanId}' is not a profile span."));
            if (section.Profile.Spans.Select(span => span.SpanId).Distinct(StringComparer.Ordinal).Count() != section.Profile.Spans.Count)
                diagnostics.Add(new("section-chain-profile-span-duplicate", $"Section '{section.SectionId}' span identities must be unique."));
            for (var index = 0; index < prepared.StartPoints.Length; index++)
            {
                var next = (index + 1) % prepared.StartPoints.Length;
                if ((prepared.EndPoints[index] - prepared.StartPoints[next]).Length > Tolerance)
                    diagnostics.Add(new("section-chain-profile-open", $"Section '{section.SectionId}' spans {index + 1} and {next + 1} do not form a closed loop."));
                if ((prepared.EndPoints[index] - prepared.StartPoints[index]).Length <= Tolerance)
                    diagnostics.Add(new("section-chain-profile-span-degenerate", $"Section '{section.SectionId}' span '{section.Profile.Spans[index].SpanId}' is degenerate."));
            }
            if (SignedArea(section.Profile) <= Tolerance)
                diagnostics.Add(new("section-chain-profile-orientation-mismatch", $"Section '{section.SectionId}' profile must be counter-clockwise in its frame and use a stable seam."));
            if (ProfileSelfIntersects(section.Profile))
                diagnostics.Add(new("section-chain-profile-self-intersection", $"Section '{section.SectionId}' profile intersects itself."));
        }

        if (sections.Count > 1)
        {
            var count = sections[0].Section.Profile.Spans.Count;
            foreach (var section in sections.Skip(1))
                if (section.Section.Profile.Spans.Count != count)
                    diagnostics.Add(new("section-chain-correspondence-topology-mismatch", "X3 requires the same semantic span count in every section."));
        }

        for (var index = 0; index < sections.Count - 1; index++)
        {
            var source = sections[index]; var target = sections[index + 1];
            if ((target.Section.Frame.Origin - source.Section.Frame.Origin).Length <= Tolerance)
                diagnostics.Add(new("section-chain-section-spacing-invalid", $"Sections '{source.Section.SectionId}' and '{target.Section.SectionId}' are coincident."));
            var correspondence = chain.Correspondence.SingleOrDefault(item => item.SourceSectionId == source.Section.SectionId && item.TargetSectionId == target.Section.SectionId);
            var semanticIdentity = source.Section.Profile.Spans.Select(span => span.SpanId).SequenceEqual(target.Section.Profile.Spans.Select(span => span.SpanId));
            if (correspondence is null && !semanticIdentity)
                diagnostics.Add(new("section-chain-correspondence-missing", $"{source.Section.SectionId}:{target.Section.SectionId}: explicit span correspondence is required."));
            if (correspondence is not null)
            {
                if (correspondence.Spans.Count != source.Section.Profile.Spans.Count)
                    diagnostics.Add(new("section-chain-correspondence-incomplete", $"{source.Section.SectionId}:{target.Section.SectionId}: expected {source.Section.Profile.Spans.Count} span mappings."));
                for (var span = 0; span < Math.Min(correspondence.Spans.Count, source.Section.Profile.Spans.Count); span++)
                {
                    if (correspondence.Spans[span].SourceSpanId != source.Section.Profile.Spans[span].SpanId
                        || correspondence.Spans[span].TargetSpanId != target.Section.Profile.Spans[span].SpanId)
                        diagnostics.Add(new("section-chain-correspondence-order-invalid", $"{source.Section.SectionId}:{target.Section.SectionId}: mapping must preserve ordered seam-relative span topology."));
                }
            }
        }
        return diagnostics;
    }

    private static IReadOnlyList<int[]> ResolveCorrespondence(SectionChain chain)
    {
        var result = new List<int[]>();
        for (var index = 0; index < chain.Sections.Count - 1; index++)
        {
            var source = chain.Sections[index]; var target = chain.Sections[index + 1];
            var explicitMap = chain.Correspondence.SingleOrDefault(item => item.SourceSectionId == source.SectionId && item.TargetSectionId == target.SectionId);
            result.Add(explicitMap is null
                ? Enumerable.Range(0, source.Profile.Spans.Count).ToArray()
                : explicitMap.Spans.Select(mapping => target.Profile.Spans.ToList().FindIndex(span => span.SpanId == mapping.TargetSpanId)).ToArray());
        }
        return result;
    }

    private static (BrepBody Body, IReadOnlyList<SectionTransitionEvidence> Evidence) BuildBody(
        SectionChain chain,
        IReadOnlyList<PreparedSection> sections,
        IReadOnlyList<(PreparedTransition Transition, ISectionChainTransitionPatch[] Patches)> transitions)
    {
        var builder = new TopologyBuilder(); var geometry = new BrepGeometryStore(); var bindings = new BrepBindingModel();
        var points = new Dictionary<VertexId, Point3D>(); var nextCurve = 1; var nextSurface = 1;
        var vertices = new VertexId[sections.Count][]; var profileEdges = new EdgeId[sections.Count][];
        for (var sectionIndex = 0; sectionIndex < sections.Count; sectionIndex++)
        {
            var section = sections[sectionIndex]; var count = section.Boundaries.Length;
            vertices[sectionIndex] = new VertexId[count]; profileEdges[sectionIndex] = new EdgeId[count];
            for (var span = 0; span < count; span++)
            {
                vertices[sectionIndex][span] = builder.AddVertex();
                points[vertices[sectionIndex][span]] = section.StartPoints[span];
            }
            for (var span = 0; span < count; span++)
            {
                profileEdges[sectionIndex][span] = builder.AddEdge(vertices[sectionIndex][span], vertices[sectionIndex][(span + 1) % count]);
                BindBoundary(profileEdges[sectionIndex][span], section.Boundaries[span]);
            }
        }

        var faces = new List<FaceId>(); var transitionEvidence = new List<SectionTransitionEvidence>();
        foreach (var entry in transitions)
        {
            var transition = entry.Transition; var sourceIndex = transition.Index; var targetIndex = sourceIndex + 1;
            var longitudinal = new EdgeId[transition.Source.Boundaries.Length];
            for (var vertex = 0; vertex < longitudinal.Length; vertex++)
            {
                longitudinal[vertex] = builder.AddEdge(vertices[sourceIndex][vertex], vertices[targetIndex][vertex]);
                var curve = entry.Patches[vertex].LongitudinalBoundary(false);
                if (curve is null) BindLine(longitudinal[vertex], points[vertices[sourceIndex][vertex]], points[vertices[targetIndex][vertex]]);
                else BindSpline(longitudinal[vertex], curve.Value);
            }
            var surfaces = new List<SectionTransitionSurfaceEvidence>();
            for (var span = 0; span < transition.Source.Boundaries.Length; span++)
            {
                var next = (span + 1) % longitudinal.Length;
                var face = AddFace(builder,
                    [(profileEdges[sourceIndex][span], false), (longitudinal[next], false),
                     (profileEdges[targetIndex][transition.Map[span]], true), (longitudinal[span], true)]);
                var surfaceId = new SurfaceGeometryId(nextSurface++);
                geometry.AddSurface(surfaceId, entry.Patches[span].ExactSurface);
                bindings.AddFaceBinding(new(face, surfaceId)); faces.Add(face);
                surfaces.Add(new(transition.Source.Section.Profile.Spans[span].SpanId,
                    transition.Source.Boundaries[span].StableId, transition.Target.Boundaries[transition.Map[span]].StableId,
                    entry.Patches[span].ExactSurface.Kind, entry.Patches[span].MaterializationKind));
            }
            transitionEvidence.Add(new(SectionChainEditor.TransitionId(transition.Source.Section.SectionId, transition.Target.Section.SectionId),
                transition.Source.Section.SectionId, transition.Target.Section.SectionId, surfaces));
        }

        if (chain.StartTermination == SectionTermination.Cap)
        {
            var face = AddFace(builder, profileEdges[0].Reverse().Select(edge => (edge, true)).ToArray());
            var surfaceId = new SurfaceGeometryId(nextSurface++);
            geometry.AddSurface(surfaceId, SurfaceGeometry.FromPlane(new PlaneSurface(sections[0].Section.Frame.Origin,
                Direction3D.Create(-sections[0].Section.Frame.Normal.ToVector()), sections[0].Section.Frame.XAxis)));
            bindings.AddFaceBinding(new(face, surfaceId)); faces.Add(face);
        }
        if (chain.EndTermination == SectionTermination.Cap)
        {
            var last = sections.Count - 1;
            var face = AddFace(builder, profileEdges[last].Select(edge => (edge, false)).ToArray());
            var surfaceId = new SurfaceGeometryId(nextSurface++);
            geometry.AddSurface(surfaceId, SurfaceGeometry.FromPlane(new PlaneSurface(sections[last].Section.Frame.Origin,
                sections[last].Section.Frame.Normal, sections[last].Section.Frame.XAxis)));
            bindings.AddFaceBinding(new(face, surfaceId)); faces.Add(face);
        }
        var shell = builder.AddShell(faces); builder.AddBody([shell]);
        var pcurves = BoundedPcurveBuilder.Populate(builder.Model, geometry, bindings, 1e-5d);
        if (!pcurves.IsSuccess)
            throw new InvalidOperationException("SectionChain face-local pcurve construction failed: "
                + string.Join(" | ", pcurves.Diagnostics.Select(item => $"{item.Code}:{item.Entity}:{item.Message}")));
        return (new BrepBody(builder.Model, geometry, bindings, points), transitionEvidence);

        void BindBoundary(EdgeId edge, RuledBoundary boundary)
        {
            var curveId = new CurveGeometryId(nextCurve++);
            switch (boundary)
            {
                case RuledBoundary.Line line:
                    var delta = line.End - line.Start;
                    geometry.AddCurve(curveId, CurveGeometry.FromLine(new Line3Curve(line.Start, Direction3D.Create(delta))));
                    bindings.AddEdgeBinding(new(edge, curveId, new(0, delta.Length)));
                    break;
                case RuledBoundary.Arc arc when arc.SweepAngleRadians > 0:
                    geometry.AddCurve(curveId, CurveGeometry.FromCircle(new Circle3Curve(arc.Center, arc.Normal, arc.Radius, arc.ReferenceAxis)));
                    bindings.AddEdgeBinding(new(edge, curveId, new(arc.StartAngleRadians, arc.StartAngleRadians + arc.SweepAngleRadians)));
                    break;
                case RuledBoundary.Circle circle:
                    geometry.AddCurve(curveId, CurveGeometry.FromCircle(new Circle3Curve(circle.Center, circle.Normal, circle.Radius, circle.ReferenceAxis)));
                    bindings.AddEdgeBinding(new(edge, curveId, new(0, 2d * Math.PI)));
                    break;
                case RuledBoundary.BSpline spline:
                    geometry.AddCurve(curveId, CurveGeometry.FromBSpline(spline.Curve));
                    bindings.AddEdgeBinding(new(edge, curveId, new(spline.Curve.DomainStart, spline.Curve.DomainEnd)));
                    break;
                default: throw new InvalidOperationException($"Boundary '{boundary.StableId}' cannot be bound as a forward profile edge.");
            }
        }
        void BindLine(EdgeId edge, Point3D start, Point3D end)
        {
            var curveId = new CurveGeometryId(nextCurve++); var delta = end - start;
            geometry.AddCurve(curveId, CurveGeometry.FromLine(new Line3Curve(start, Direction3D.Create(delta))));
            bindings.AddEdgeBinding(new(edge, curveId, new(0, delta.Length)));
        }
        void BindSpline(EdgeId edge, BSpline3Curve curve)
        {
            var curveId = new CurveGeometryId(nextCurve++);
            geometry.AddCurve(curveId, CurveGeometry.FromBSpline(curve));
            bindings.AddEdgeBinding(new(edge, curveId, new(curve.DomainStart, curve.DomainEnd)));
        }
    }

    private static PreparedSection Prepare(Section section)
    {
        var boundaries = section.Profile.Spans.Select(span => Boundary(section, span)).ToArray();
        return new(section, boundaries, boundaries.Select(Start).ToArray(), boundaries.Select(End).ToArray());
    }

    private static RuledBoundary Boundary(Section section, SectionProfileSpan span)
    {
        var frame = section.Frame; var id = $"{section.SectionId}.{span.SpanId}";
        return span.Curve switch
        {
            SectionProfileCurve.Line line => new RuledBoundary.Line(id, frame.Transform(line.Start), frame.Transform(line.End)),
            SectionProfileCurve.Arc arc => new RuledBoundary.Arc(id, frame.Transform(arc.Center), frame.Normal, arc.Radius,
                frame.XAxis, arc.StartAngleRadians, arc.SweepAngleRadians),
            SectionProfileCurve.PolynomialBSpline spline => new RuledBoundary.BSpline(id, new BSpline3Curve(spline.Degree,
                spline.ControlPoints.Select(frame.Transform).ToArray(), spline.KnotMultiplicities, spline.KnotValues,
                "UNSPECIFIED", false, false, "UNSPECIFIED")),
            _ => throw new InvalidOperationException($"Profile span '{span.SpanId}' has no admitted curve representation.")
        };
    }

    private static Point3D Start(RuledBoundary boundary) => boundary switch
    {
        RuledBoundary.Line line => line.Start,
        RuledBoundary.Arc arc => arc.Center + ArcOffset(arc, arc.StartAngleRadians),
        RuledBoundary.Circle circle => circle.Center + circle.ReferenceAxis.ToVector() * circle.Radius,
        RuledBoundary.BSpline spline => spline.Curve.Evaluate(spline.Curve.DomainStart),
        _ => throw new InvalidOperationException()
    };

    private static Point3D End(RuledBoundary boundary) => boundary switch
    {
        RuledBoundary.Line line => line.End,
        RuledBoundary.Arc arc => arc.Center + ArcOffset(arc, arc.StartAngleRadians + arc.SweepAngleRadians),
        RuledBoundary.Circle circle => circle.Center + circle.ReferenceAxis.ToVector() * circle.Radius,
        RuledBoundary.BSpline spline => spline.Curve.Evaluate(spline.Curve.DomainEnd),
        _ => throw new InvalidOperationException()
    };

    private static Vector3D ArcOffset(RuledBoundary.Arc arc, double angle) =>
        arc.ReferenceAxis.ToVector() * (arc.Radius * Math.Cos(angle))
        + arc.Normal.ToVector().Cross(arc.ReferenceAxis.ToVector()) * (arc.Radius * Math.Sin(angle));

    private static double SignedArea(SectionProfile profile)
    {
        var points = Sample(profile, 12); var area = 0d;
        for (var i = 0; i < points.Count; i++)
        {
            var next = points[(i + 1) % points.Count];
            area += points[i].X * next.Y - next.X * points[i].Y;
        }
        return area / 2d;
    }

    private static bool ProfileSelfIntersects(SectionProfile profile)
    {
        var points = Sample(profile, 16);
        for (var i = 0; i < points.Count; i++)
        {
            var a = points[i]; var b = points[(i + 1) % points.Count];
            for (var j = i + 2; j < points.Count; j++)
            {
                if (i == 0 && j == points.Count - 1) continue;
                var c = points[j]; var d = points[(j + 1) % points.Count];
                if (SegmentsIntersect(a, b, c, d)) return true;
            }
        }
        return false;
    }

    private static IReadOnlyList<SectionPoint2D> Sample(SectionProfile profile, int curveSamples)
    {
        var result = new List<SectionPoint2D>();
        foreach (var span in profile.Spans)
        {
            var count = span.Curve is SectionProfileCurve.Line ? 1 : curveSamples;
            for (var i = 0; i < count; i++) result.Add(Evaluate(span.Curve, i / (double)count));
        }
        return result;
    }

    private static SectionPoint2D Evaluate(SectionProfileCurve curve, double parameter) => curve switch
    {
        SectionProfileCurve.Line line => Lerp(line.Start, line.End, parameter),
        SectionProfileCurve.Arc arc => new(arc.Center.X + arc.Radius * Math.Cos(arc.StartAngleRadians + parameter * arc.SweepAngleRadians),
            arc.Center.Y + arc.Radius * Math.Sin(arc.StartAngleRadians + parameter * arc.SweepAngleRadians)),
        SectionProfileCurve.PolynomialBSpline spline => EvaluateSpline(spline, parameter),
        _ => default
    };

    private static SectionPoint2D EvaluateSpline(SectionProfileCurve.PolynomialBSpline spline, double parameter)
    {
        var curve = new BSpline3Curve(spline.Degree, spline.ControlPoints.Select(point => new Point3D(point.X, point.Y, 0)).ToArray(),
            spline.KnotMultiplicities, spline.KnotValues, "UNSPECIFIED", false, false, "UNSPECIFIED");
        var point = curve.Evaluate(curve.DomainStart + Math.Clamp(parameter, 0, 1) * (curve.DomainEnd - curve.DomainStart));
        return new(point.X, point.Y);
    }

    private static bool SegmentsIntersect(SectionPoint2D a, SectionPoint2D b, SectionPoint2D c, SectionPoint2D d)
    {
        static double Orientation(SectionPoint2D p, SectionPoint2D q, SectionPoint2D r) =>
            (q.X - p.X) * (r.Y - p.Y) - (q.Y - p.Y) * (r.X - p.X);
        var o1 = Orientation(a, b, c); var o2 = Orientation(a, b, d); var o3 = Orientation(c, d, a); var o4 = Orientation(c, d, b);
        return o1 * o2 < -Tolerance && o3 * o4 < -Tolerance;
    }

    internal static bool HasFoldover(ISectionChainTransitionPatch patch)
    {
        var normals = new Vector3D[17, 5];
        for (var uIndex = 0; uIndex <= 16; uIndex++)
        {
            var u = uIndex / 16d;
            for (var vIndex = 0; vIndex <= 4; vIndex++)
            {
                var v = vIndex / 4d; const double h = 1e-5;
                var du = patch.Evaluate(Math.Min(1, u + h), v) - patch.Evaluate(Math.Max(0, u - h), v);
                var dv = patch.Evaluate(u, Math.Min(1, v + h)) - patch.Evaluate(u, Math.Max(0, v - h));
                if (!du.TryNormalize(out var tangentU) || !dv.TryNormalize(out var tangentV)) return true;
                var jacobian = tangentU.Cross(tangentV);
                if (!jacobian.TryNormalize(out var normal)) return true;
                normals[uIndex, vIndex] = normal;
                if (uIndex > 0 && normals[uIndex - 1, vIndex].Dot(normal) < -0.05d) return true;
                if (vIndex > 0 && normals[uIndex, vIndex - 1].Dot(normal) < -0.05d) return true;
            }
        }
        return false;
    }

    private static SectionChainContinuityEvidence MeasureContinuity(SectionChain chain,
        IReadOnlyList<(PreparedTransition Transition, ISectionChainTransitionPatch[] Patches)> transitions)
    {
        var boundaries = new List<SectionChainBoundaryContinuityEvidence>();
        for (var section = 1; section < chain.Sections.Count - 1; section++)
        {
            var maxPosition = 0d; var maxNormal = 0d; var maxTangent = 0d;
            for (var span = 0; span < transitions[section - 1].Patches.Length; span++)
            for (var sample = 0; sample <= 32; sample++)
            {
                var u = sample / 32d; const double h = 1e-6d;
                var incoming = transitions[section - 1].Patches[span];
                var outgoing = transitions[section].Patches[span];
                maxPosition = Math.Max(maxPosition, (incoming.Evaluate(u, 1d) - outgoing.Evaluate(u, 0d)).Length);
                var incomingDu = incoming.Evaluate(Math.Min(1d, u + h), 1d) - incoming.Evaluate(Math.Max(0d, u - h), 1d);
                var outgoingDu = outgoing.Evaluate(Math.Min(1d, u + h), 0d) - outgoing.Evaluate(Math.Max(0d, u - h), 0d);
                var incomingDv = incoming.Evaluate(u, 1d) - incoming.Evaluate(u, 1d - h);
                var outgoingDv = outgoing.Evaluate(u, h) - outgoing.Evaluate(u, 0d);
                if (incomingDv.TryNormalize(out var ti) && outgoingDv.TryNormalize(out var to))
                    maxTangent = Math.Max(maxTangent, AngleDegrees(ti, to));
                var ni = incomingDu.Cross(incomingDv); var no = outgoingDu.Cross(outgoingDv);
                if (ni.TryNormalize(out var nui) && no.TryNormalize(out var nuo))
                    maxNormal = Math.Max(maxNormal, AngleDegrees(nui, nuo));
            }
            boundaries.Add(new(chain.Sections[section].SectionId, maxPosition, maxNormal, maxTangent));
        }
        var worst = boundaries.OrderByDescending(item => item.MaximumTangentPlaneAngleDegrees).ThenBy(item => item.SectionId, StringComparer.Ordinal).FirstOrDefault();
        return new(boundaries.Count == 0 ? 0d : boundaries.Max(item => item.MaximumPositionError),
            boundaries.Count == 0 ? 0d : boundaries.Max(item => item.MaximumNormalAngleDegrees),
            boundaries.Count == 0 ? 0d : boundaries.Max(item => item.MaximumTangentPlaneAngleDegrees), worst?.SectionId, boundaries);

        static double AngleDegrees(Vector3D a, Vector3D b) => Math.Acos(Math.Clamp(a.Dot(b), -1d, 1d)) * 180d / Math.PI;
    }

    private static SectionPoint2D Lerp(SectionPoint2D a, SectionPoint2D b, double t) =>
        new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

    private static FaceId AddFace(TopologyBuilder builder, IReadOnlyList<(EdgeId Edge, bool Reversed)> uses)
    {
        var loop = builder.AllocateLoopId(); var ids = uses.Select(_ => builder.AllocateCoedgeId()).ToArray();
        for (var index = 0; index < ids.Length; index++)
            builder.AddCoedge(new Coedge(ids[index], uses[index].Edge, loop, ids[(index + 1) % ids.Length],
                ids[(index + ids.Length - 1) % ids.Length], uses[index].Reversed));
        builder.AddLoop(new Loop(loop, ids));
        return builder.AddFace([loop]);
    }

    private static SectionChainStructureKind Structure(SectionChain chain) =>
        chain.StartTermination == SectionTermination.Cap && chain.EndTermination == SectionTermination.Cap
            ? SectionChainStructureKind.ClosedSolid : SectionChainStructureKind.OpenShell;

    private static SectionChainMaterializationResult Failure(SectionChain chain, IReadOnlyList<SurfacingDiagnostic> diagnostics,
        double profileMs, double correspondenceMs, double transitionMs = 0) =>
        new(chain, null, Structure(chain), [], diagnostics, new(profileMs, correspondenceMs, transitionMs, 0, 0));

    private sealed record PreparedSection(Section Section, RuledBoundary[] Boundaries, Point3D[] StartPoints, Point3D[] EndPoints);
    private sealed record PreparedTransition(int Index, PreparedSection Source, PreparedSection Target, int[] Map);
}
