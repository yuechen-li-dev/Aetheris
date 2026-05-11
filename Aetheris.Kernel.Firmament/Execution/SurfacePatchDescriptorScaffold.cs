using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Judgment;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Firmament.Execution;

/// <summary>
/// CIR-F8.1 scaffold: descriptor-first path toward surface-family materialization.
/// Existing pair-specific materializers remain compatibility/fast paths.
/// </summary>
internal enum SurfacePatchFamily
{
    Planar,
    Cylindrical,
    Conical,
    Spherical,
    Toroidal,
    Spline,
    Prismatic,
    Unsupported
}

internal enum TrimCurveFamily
{
    Line,
    Circle,
    Ellipse,
    BSpline,
    Polyline,
    AlgebraicImplicit,
    Unsupported
}

internal enum TrimCurveCapability
{
    ExactSupported,
    SpecialCaseOnly,
    Deferred,
    Unsupported
}

internal enum FacePatchOrientationRole
{
    Unknown,
    Forward,
    Reversed
}

internal sealed record SourceSurfaceDescriptor(
    SurfacePatchFamily Family,
    string? ParameterPayloadReference,
    BoundedPlanarPatchGeometry? BoundedPlanarGeometry,
    CylindricalSurfaceGeometryEvidence? CylindricalGeometryEvidence,
    Transform3D Transform,
    string Provenance,
    string? OwningCirNodeKind,
    int? ReplayOpIndex,
    FacePatchOrientationRole OrientationRole);

internal enum BoundedPlanarPatchGeometryKind
{
    Rectangle,
    Circle
}

internal readonly record struct BoundedPlanarPatchGeometry(
    BoundedPlanarPatchGeometryKind Kind,
    Point3D Corner00,
    Point3D Corner10,
    Point3D Corner11,
    Point3D Corner01,
    Point3D Center,
    Vector3D Normal,
    double Radius)
{
    internal static BoundedPlanarPatchGeometry CreateRectangle(Point3D corner00, Point3D corner10, Point3D corner11, Point3D corner01, Vector3D normal)
        => new(BoundedPlanarPatchGeometryKind.Rectangle, corner00, corner10, corner11, corner01, Point3D.Origin, normal, 0d);

    internal static BoundedPlanarPatchGeometry CreateCircle(Point3D center, Vector3D normal, double radius)
        => new(BoundedPlanarPatchGeometryKind.Circle, Point3D.Origin, Point3D.Origin, Point3D.Origin, Point3D.Origin, center, normal, radius);
}

internal readonly record struct CylindricalSurfaceGeometryEvidence(
    Point3D AxisOrigin,
    Vector3D AxisDirection,
    double Radius,
    double Height,
    Point3D BottomCenter,
    Point3D TopCenter);

internal sealed record TrimCurveDescriptor(
    TrimCurveFamily Family,
    string? ParameterPayloadReference,
    string Provenance,
    int? ReplayOpIndex,
    ParameterInterval? Domain,
    TrimCurveCapability Capability);

internal sealed record FacePatchDescriptor(
    SourceSurfaceDescriptor SourceSurface,
    IReadOnlyList<TrimCurveDescriptor> OuterLoop,
    IReadOnlyList<IReadOnlyList<TrimCurveDescriptor>> InnerLoops,
    FacePatchOrientationRole Orientation,
    string Role,
    IReadOnlyList<string> AdjacencyHints);

internal sealed record SurfaceMaterializerAdmissibility(
    bool IsAdmissible,
    string Reason,
    double Score,
    bool IsDeferred = false);

internal interface ISurfaceFamilyMaterializer
{
    SurfacePatchFamily Family { get; }
    string Name { get; }
    SurfaceMaterializerAdmissibility Evaluate(FacePatchDescriptor patch);
}

internal static class SurfaceFamilyMaterializerRegistry
{
    private static readonly JudgmentEngine<FacePatchDescriptor> Engine = new();
    private static readonly ISurfaceFamilyMaterializer[] Materializers =
    [
        new PlanarSurfaceMaterializer(),
        new CylindricalSurfaceMaterializer(),
        new ConicalSurfaceMaterializer(),
        new SphericalSurfaceMaterializer(),
        new ToroidalSurfaceMaterializer(),
        new SplineSurfaceMaterializer()
    ];

    internal static SurfaceFamilyMaterializerEvaluation Evaluate(FacePatchDescriptor patch)
    {
        var evaluations = Materializers.Select(m => new { Materializer = m, Admissibility = m.Evaluate(patch) }).ToArray();
        var candidates = evaluations.Select((entry, i) => new JudgmentCandidate<FacePatchDescriptor>(entry.Materializer.Name, _ => entry.Admissibility.IsAdmissible, _ => entry.Admissibility.Score, _ => entry.Admissibility.Reason, i)).ToArray();
        var judgment = Engine.Evaluate(patch, candidates);
        var rejected = evaluations
            .Where(e => !e.Admissibility.IsAdmissible)
            .Select(e => new JudgmentRejection(e.Materializer.Name, e.Admissibility.Reason))
            .ToArray();
        if (!judgment.IsSuccess)
        {
            return new(null, false, "No surface-family materializer admitted patch.", rejected);
        }

        return new(Materializers.Single(m => m.Name == judgment.Selection!.Value.Candidate.Name), true, "admissible", rejected);
    }
}

internal sealed record SurfaceFamilyMaterializerEvaluation(
    ISurfaceFamilyMaterializer? Selected,
    bool IsSuccess,
    string Message,
    IReadOnlyList<JudgmentRejection> Rejections);

internal sealed class PlanarSurfaceMaterializer : ISurfaceFamilyMaterializer
{
    internal sealed record PlanarPatchSetEntry(
        FacePatchCandidate Candidate,
        SurfaceMaterializationResult? Emission,
        bool Emitted,
        EmittedTopologyIdentityMap? IdentityMap,
        IReadOnlyList<string> Diagnostics);

    internal sealed record PlanarPatchSetMaterializationResult(
        bool Success,
        IReadOnlyList<BrepBody> EmittedBodies,
        int EmittedCount,
        int SkippedCount,
        bool FullMaterialization,
        IReadOnlyList<PlanarPatchSetEntry> Entries,
        IReadOnlyList<string> RemainingBlockers,
        IReadOnlyList<string> Diagnostics);

    internal enum PlanarLoopSupportStatus
    {
        Supported,
        Deferred
    }

    internal enum InnerLoopOrientationPolicy
    {
        FollowFaceBoundConvention,
        Deferred
    }

    internal sealed record PlanarLoopEmissionPolicy(
        bool SupportsOuterRectangle,
        bool SupportsOuterCircle,
        bool SupportsInnerCircle,
        bool SupportsMultipleInnerLoops,
        InnerLoopOrientationPolicy InnerLoopOrientation,
        PlanarLoopSupportStatus Status,
        string Diagnostic);

    public SurfacePatchFamily Family => SurfacePatchFamily.Planar;
    public string Name => "surface_family_planar";

    internal static PlanarLoopEmissionPolicy GetLoopEmissionPolicy()
        => new(
            SupportsOuterRectangle: true,
            SupportsOuterCircle: true,
            SupportsInnerCircle: true,
            SupportsMultipleInnerLoops: false,
            InnerLoopOrientation: InnerLoopOrientationPolicy.FollowFaceBoundConvention,
            Status: PlanarLoopSupportStatus.Supported,
            Diagnostic: "PlanarSurfaceMaterializer supports one rectangular outer loop plus one canonical retained inner circular loop.");

    internal sealed record RectWithInnerCircleEmissionRequest(
        SourceSurfaceDescriptor Source,
        RetainedCircularLoopGeometry? InnerCircle,
        IReadOnlyList<RetainedCircularLoopGeometry>? InnerCircles,
        MaterializationReadinessReport Readiness);

    public SurfaceMaterializerAdmissibility Evaluate(FacePatchDescriptor patch)
    {
        if (patch.SourceSurface.Family != SurfacePatchFamily.Planar) return new(false, "Source surface family mismatch.", 0d);
        if (patch.OuterLoop.Any(t => t.Capability != TrimCurveCapability.ExactSupported) || patch.InnerLoops.SelectMany(l => l).Any(t => t.Capability != TrimCurveCapability.ExactSupported))
        {
            return new(false, "Trim capability requires exact-supported curves for planar scaffold.", 0d);
        }

        return new(true, "admissible", 10d);
    }

    internal SurfaceMaterializationResult Emit(FacePatchDescriptor patch, MaterializationReadinessReport readiness)
    {
        if (patch.SourceSurface.Family != SurfacePatchFamily.Planar)
        {
            return new(false, null, SurfacePatchFamily.Unsupported, false, ["unsupported-surface-family: PlanarSurfaceMaterializer only supports planar source patches."]);
        }

        if (readiness.OverallReadiness is EmissionReadiness.NotApplicable or EmissionReadiness.Deferred or EmissionReadiness.Unsupported)
        {
            return new(false, null, SurfacePatchFamily.Planar, false, ["readiness-gate-rejected: no readiness, no emission."]);
        }

        if (patch.InnerLoops.Count > 0)
        {
            if (patch.InnerLoops.Count > 1)
            {
                return new(false, null, SurfacePatchFamily.Planar, false,
                [
                    "planar-trimmed-loop-rejected: multiple inner loops are unsupported in CIR-F10.4 scope.",
                    "PlanarSurfaceMaterializer inner circular loop emission deferred: multiple inner loops are explicitly out of scope for bounded planar policy milestone."
                ]);
            }

            return new(false, null, SurfacePatchFamily.Planar, false, ["planar-trimmed-loop-rejected: use bounded rectangle-with-inner-circle emission path requiring canonical retained circular loop geometry evidence."]);
        }

        if (patch.OuterLoop.Count > 0)
        {
            return new(false, null, SurfacePatchFamily.Planar, false, ["planar-f9-limited-scope: supports only rectangular untrimmed planar patches."]);
        }

        if (TryBuildCircularEmissionInput(patch.SourceSurface, out var circle, out var circleDiagnostic))
        {
            return EmitCircularBody(circle!.Value);
        }

        if (!TryParseRectanglePayload(patch.SourceSurface.ParameterPayloadReference, out var points, out var parseReason))
        {
            return new(false, null, SurfacePatchFamily.Planar, false, [
                $"planar-f9-payload-invalid: {parseReason}",
                $"planar-f10.3-circle-evaluation: {circleDiagnostic}"
            ]);
        }

        return EmitRectangleBody(points!);
    }

    internal SurfaceMaterializationResult EmitRectangleWithInnerCircle(RectWithInnerCircleEmissionRequest request)
    {
        if (request.Readiness.OverallReadiness is EmissionReadiness.NotApplicable or EmissionReadiness.Deferred or EmissionReadiness.Unsupported)
        {
            return new(false, null, SurfacePatchFamily.Planar, false, ["readiness-gate-rejected: no readiness, no emission."]);
        }

        if (request.Source.BoundedPlanarGeometry is not { Kind: BoundedPlanarPatchGeometryKind.Rectangle } outer)
        {
            return new(false, null, SurfacePatchFamily.Planar, false, ["outer-rectangle-missing: bounded planar rectangle geometry is required."]);
        }

        var circles = request.InnerCircles ?? (request.InnerCircle is { } one ? [one] : []);
        if (circles.Count == 0) return new(false, null, SurfacePatchFamily.Planar, false, ["inner-circle-missing: canonical retained circular loop geometry evidence is required."]);
        if (circles.Count > 1) return new(false, null, SurfacePatchFamily.Planar, false, ["inner-circle-unsupported: multiple inner loops are unsupported in CIR-F10.7."]);

        return EmitRectangleWithInnerCircleBody(outer, circles[0]);
    }

    internal PlanarPatchSetMaterializationResult EmitSupportedPlanarPatches(CirNode root, NativeGeometryReplayLog? replayLog = null)
    {
        var generation = FacePatchCandidateGenerator.Generate(root, replayLog);
        var entries = new List<PlanarPatchSetEntry>();
        var emittedBodies = new List<BrepBody>();
        var diagnostics = new List<string> { "scope-note: partial planar patch set only; no shell assembly attempted." };
        var blockers = new List<string> { "remaining-blocker: cylindrical side surface emission not implemented (CylindricalSurfaceMaterializer topology emission pending)." };

        foreach (var candidate in generation.Candidates)
        {
            if (candidate.RetentionRole != FacePatchRetentionRole.BaseBoundaryRetainedOutsideTool)
            {
                entries.Add(new PlanarPatchSetEntry(candidate, null, false, null, [$"skipped-candidate-role: retention role {candidate.RetentionRole} is not base-boundary retained outside tool."]));
                continue;
            }

            if (candidate.SourceSurface.Family != SurfacePatchFamily.Planar)
            {
                entries.Add(new PlanarPatchSetEntry(candidate, null, false, null, ["skipped-non-planar-candidate: planar patch set emitter supports planar family only."]));
                continue;
            }

            if (candidate.Readiness is FacePatchCandidateReadiness.RetentionDeferred or FacePatchCandidateReadiness.TrimDeferred or FacePatchCandidateReadiness.Unsupported)
            {
                entries.Add(new PlanarPatchSetEntry(candidate, null, false, null, [$"skipped-candidate-readiness: {candidate.Readiness}."]));
                continue;
            }

            var readiness = new MaterializationReadinessReport(true, EmissionReadiness.EvidenceReadyForEmission, [], [], 1, 1, 1, 0, 0, 0, 0, [], false);
            var oracleCandidateLoops = candidate.RetainedRegionLoops
                .Where(l => l.LoopKind == RetainedRegionLoopKind.InnerTrim)
                .ToArray();
            var retainedLoops = oracleCandidateLoops
                .Where(l => l.Status == RetainedRegionLoopStatus.ExactReady || l.Status == RetainedRegionLoopStatus.SpecialCaseReady)
                .ToArray();
            var retainedCircles = retainedLoops.Where(l => l.CircularGeometry is not null).Select(l => l.CircularGeometry!.Value).ToArray();
            var entryDiagnostics = new List<string>();
            var oracleCircles = new List<RetainedCircularLoopGeometry>();
            foreach (var loop in oracleCandidateLoops)
            {
                if (!loop.OracleTrimStrongEvidence)
                {
                    if (loop.OracleTrimRepresentation is not null)
                    {
                        entryDiagnostics.Add("oracle-trim-consumption-rejected: broad/deferred oracle evidence is not materialization-grade.");
                    }

                    continue;
                }

                if (loop.OracleTrimRoutingDiagnostic != "oracle-trim: selected-opposite-field-used")
                {
                    entryDiagnostics.Add("oracle-trim-consumption-rejected: selected-opposite-field routing confirmation missing.");
                    continue;
                }

                var oracle = loop.OracleTrimRepresentation;
                if (oracle is null)
                {
                    continue;
                }

                if (oracle.Kind != TieredTrimRepresentationKind.AnalyticCircle)
                {
                    entryDiagnostics.Add("oracle-trim-consumption-rejected: numerical-only/deferred oracle representation is not consumable.");
                    continue;
                }

                if (!oracle.AcceptedInternalAnalyticCandidate || oracle.ExactStepExported || oracle.BRepTopologyEmitted)
                {
                    entryDiagnostics.Add("oracle-trim-consumption-rejected: strong oracle flags do not satisfy materialization constraints.");
                    continue;
                }

                if (OracleTrimLoopGeometryConverter.TryConvertAnalyticCircle(candidate.SourceSurface, oracle, loop.CircularGeometry?.OrderingToken, out var converted, out var convertedDiagnostics))
                {
                    oracleCircles.Add(converted);
                    entryDiagnostics.AddRange(convertedDiagnostics);
                    entryDiagnostics.Add("oracle-trim-analytic-circle-consumed: converted analytic-circle evidence was admitted for inner-loop emission.");
                }
                else
                {
                    entryDiagnostics.AddRange(convertedDiagnostics);
                    entryDiagnostics.Add("oracle-trim-consumption-rejected: uv-to-world conversion unsafe.");
                }
            }

            if (retainedCircles.Length == 1 && oracleCircles.Count == 1)
            {
                var binder = retainedCircles[0];
                var oracle = oracleCircles[0];
                var centerDelta = (binder.Center - oracle.Center).Length;
                var radiusDelta = Math.Abs(binder.Radius - oracle.Radius);
                if (centerDelta <= 1e-4 && radiusDelta <= 1e-4)
                {
                    entryDiagnostics.Add("oracle-trim-binder-agreement: binder and oracle circles agree within tolerance.");
                }
                else
                {
                    entryDiagnostics.Add($"oracle-trim-binder-mismatch: centerDelta={centerDelta:G6} radiusDelta={radiusDelta:G6}; conservative skip applied.");
                    entries.Add(new PlanarPatchSetEntry(candidate, null, false, null, entryDiagnostics));
                    continue;
                }
            }

            var chosenCircles = oracleCircles.Count > 0 ? oracleCircles.ToArray() : retainedCircles;
            if (oracleCandidateLoops.Any() && oracleCircles.Count == 0 && retainedCircles.Length > 0) entryDiagnostics.Add("oracle-trim-fallback-to-binder: oracle evidence unavailable/rejected; using binder-derived retained circular geometry.");
            SurfaceMaterializationResult? emission = null;
            if (chosenCircles.Length == 0)
            {
                if (candidate.SourceSurface.BoundedPlanarGeometry is not { Kind: BoundedPlanarPatchGeometryKind.Rectangle })
                {
                    entries.Add(new PlanarPatchSetEntry(candidate, null, false, null, ["skipped-missing-rectangular-geometry: rectangular bounded planar geometry is required.", "skipped-missing-retained-circle-geometry: no exact-ready/special-case-ready canonical retained circular loop geometry found for this candidate."]));
                    continue;
                }

                if (!PlanarPatchPayloadBuilder.TryBuildRectanglePayload(candidate.SourceSurface, out var rectanglePayload, out var payloadDiagnostic))
                {
                    entries.Add(new PlanarPatchSetEntry(candidate, null, false, null, [$"skipped-missing-rectangular-geometry: {payloadDiagnostic}"]));
                    continue;
                }

                var normalizedSource = candidate.SourceSurface with { ParameterPayloadReference = rectanglePayload };
                var normalizedPatch = candidate.ProposedPatch with { SourceSurface = normalizedSource };
                emission = Emit(normalizedPatch, readiness);
                if (!emission.Success)
                {
                    entries.Add(new PlanarPatchSetEntry(candidate, emission, false, emission.IdentityMap, emission.Diagnostics));
                    continue;
                }

                emittedBodies.Add(emission.Body!);
                entries.Add(new PlanarPatchSetEntry(candidate, emission, true, emission.IdentityMap, ["emitted-untrimmed-planar-patch: retained planar rectangle emitted without inner loops."]));
                continue;
            }

            if (chosenCircles.Length > 1)
            {
                entries.Add(new PlanarPatchSetEntry(candidate, null, false, null, [$"skipped-multiple-inner-loops: found {chosenCircles.Length} exact-ready retained circular loops; only one is supported in CIR-F10.8."]));
                continue;
            }

            var normalizedTrimmedSource = candidate.SourceSurface;
            if (!PlanarPatchPayloadBuilder.TryBuildRectanglePayload(candidate.SourceSurface, out var trimmedPayload, out _))
            {
                entries.Add(new PlanarPatchSetEntry(candidate, null, false, null, ["skipped-missing-rectangular-geometry: bounded rectangular payload derivation failed for trimmed planar patch.", "skipped-inner-circle-evidence-unusable: canonical inner loop evidence existed, but rectangle payload normalization failed."]));
                continue;
            }

            normalizedTrimmedSource = normalizedTrimmedSource with { ParameterPayloadReference = trimmedPayload };
            emission = EmitRectangleWithInnerCircle(new RectWithInnerCircleEmissionRequest(normalizedTrimmedSource, chosenCircles[0], null, readiness));
            if (!emission.Success)
            {
                entries.Add(new PlanarPatchSetEntry(candidate, emission, false, emission.IdentityMap, emission.Diagnostics));
                continue;
            }

            emittedBodies.Add(emission.Body!);
            var innerTokenPresent = emission.IdentityMap?.Entries.Any(x => x.Role == EmittedTopologyRole.InnerCircularTrim && x.TrimIdentityToken is not null) == true;
            entries.Add(new PlanarPatchSetEntry(candidate, emission, true, emission.IdentityMap,
            [
                ..entryDiagnostics,
                "emitted-inner-circle-planar-patch: retained planar rectangle emitted with one canonical inner circular loop.",
                innerTokenPresent
                    ? "emitted-identity-token-attached: inner circular trim token attached on emitted planar topology."
                    : "emitted-identity-token-missing: inner circular trim topology emitted but token evidence was unavailable."
            ]));
        }

        var emittedCount = entries.Count(e => e.Emitted);
        var skippedCount = entries.Count - emittedCount;
        diagnostics.Add($"planar-patch-set-summary: emitted={emittedCount} skipped={skippedCount} total={entries.Count}");
        diagnostics.Add("full-materialization: false (planar subset only).");
        diagnostics.AddRange(generation.Diagnostics);
        return new PlanarPatchSetMaterializationResult(
            Success: emittedCount > 0,
            EmittedBodies: emittedBodies,
            EmittedCount: emittedCount,
            SkippedCount: skippedCount,
            FullMaterialization: false,
            Entries: entries,
            RemainingBlockers: blockers,
            Diagnostics: diagnostics.Distinct().ToArray());
    }

    private static SurfaceMaterializationResult EmitRectangleWithInnerCircleBody(BoundedPlanarPatchGeometry rect, RetainedCircularLoopGeometry inner)
    {
        var p = new[] { rect.Corner00, rect.Corner10, rect.Corner11, rect.Corner01 };
        var builder = new TopologyBuilder();
        var v = new[] { builder.AddVertex(), builder.AddVertex(), builder.AddVertex(), builder.AddVertex(), builder.AddVertex() };
        var e = new[] { builder.AddEdge(v[0], v[1]), builder.AddEdge(v[1], v[2]), builder.AddEdge(v[2], v[3]), builder.AddEdge(v[3], v[0]), builder.AddEdge(v[4], v[4]) };

        var outerLoopId = builder.AllocateLoopId();
        var outerCoedgeIds = new[] { builder.AllocateCoedgeId(), builder.AllocateCoedgeId(), builder.AllocateCoedgeId(), builder.AllocateCoedgeId() };
        for (var i = 0; i < 4; i++) builder.AddCoedge(new Coedge(outerCoedgeIds[i], e[i], outerLoopId, outerCoedgeIds[(i + 1) % 4], outerCoedgeIds[(i + 3) % 4], IsReversed: false));
        builder.AddLoop(new Loop(outerLoopId, outerCoedgeIds));

        var innerLoopId = builder.AllocateLoopId();
        var innerCoedgeId = builder.AllocateCoedgeId();
        builder.AddCoedge(new Coedge(innerCoedgeId, e[4], innerLoopId, innerCoedgeId, innerCoedgeId, IsReversed: true));
        builder.AddLoop(new Loop(innerLoopId, [innerCoedgeId]));

        var face = builder.AddFace([outerLoopId, innerLoopId]);
        var shell = builder.AddShell([face]);
        builder.AddBody([shell]);

        var normal = Direction3D.Create(rect.Normal);
        var xAxis = Direction3D.Create(p[1] - p[0]);
        var geometry = new BrepGeometryStore();
        geometry.AddCurve(new CurveGeometryId(1), CurveGeometry.FromLine(new Line3Curve(p[0], Direction3D.Create(p[1] - p[0]))));
        geometry.AddCurve(new CurveGeometryId(2), CurveGeometry.FromLine(new Line3Curve(p[1], Direction3D.Create(p[2] - p[1]))));
        geometry.AddCurve(new CurveGeometryId(3), CurveGeometry.FromLine(new Line3Curve(p[2], Direction3D.Create(p[3] - p[2]))));
        geometry.AddCurve(new CurveGeometryId(4), CurveGeometry.FromLine(new Line3Curve(p[3], Direction3D.Create(p[0] - p[3]))));
        geometry.AddCurve(new CurveGeometryId(5), CurveGeometry.FromCircle(new Circle3Curve(inner.Center, Direction3D.Create(inner.Normal), inner.Radius, BuildReferenceAxis(Direction3D.Create(inner.Normal)))));
        geometry.AddSurface(new SurfaceGeometryId(1), SurfaceGeometry.FromPlane(new PlaneSurface(p[0], normal, xAxis)));

        var bindings = new BrepBindingModel();
        for (var i = 0; i < 4; i++) bindings.AddEdgeBinding(new EdgeGeometryBinding(e[i], new CurveGeometryId(i + 1), new ParameterInterval(0d, 1d)));
        bindings.AddEdgeBinding(new EdgeGeometryBinding(e[4], new CurveGeometryId(5), new ParameterInterval(0d, 2d * double.Pi)));
        bindings.AddFaceBinding(new FaceGeometryBinding(face, new SurfaceGeometryId(1)));
        InternalTrimIdentityToken? innerToken = string.IsNullOrWhiteSpace(inner.OrderingToken)
            ? (InternalTrimIdentityToken?)null
            : new InternalTrimIdentityToken("emitted:planar", inner.OrderingToken, inner.OrderingToken, TrimCurveFamily.Circle, "inner-trim", $"planar-inner:{inner.OrderingToken}");
        var identityEntries = new List<EmittedTopologyIdentityEntry>
        {
            new($"loop:{outerLoopId.Value}", EmittedTopologyKind.Loop, null, EmittedTopologyRole.OuterBoundary, "follow-face-boundary", ["outer boundary intentionally unmapped/internal-only."]),
            new($"edge:{e[4].Value}", EmittedTopologyKind.Edge, innerToken, EmittedTopologyRole.InnerCircularTrim, "reverse-for-tool-cavity", [innerToken is null ? "inner circular trim token missing from retained loop evidence; token not fabricated." : "inner circular trim token attached from retained loop evidence."]),
            new($"coedge:{innerCoedgeId.Value}", EmittedTopologyKind.Coedge, innerToken, EmittedTopologyRole.InnerCircularTrim, "reverse-for-tool-cavity", [innerToken is null ? "inner circular trim token missing from retained loop evidence; coedge left unmapped." : "inner circular trim token attached on emitted coedge."]),
            new($"loop:{innerLoopId.Value}", EmittedTopologyKind.Loop, innerToken, EmittedTopologyRole.InnerCircularTrim, "reverse-for-tool-cavity", [innerToken is null ? "inner circular trim token missing from retained loop evidence; loop identity recorded without token." : "inner circular trim token attached on emitted inner loop."])
        };
        return new(true, new BrepBody(builder.Model, geometry, bindings), SurfacePatchFamily.Planar, true,
        [
            "inner-loop-emitted: one circular inner loop emitted from canonical retained loop geometry.",
            "orientation-policy-applied: inner loop coedge orientation follows face-bound convention for cavity loops.",
            "topology-emitted: planar trimmed face emitted as one face with outer rectangle then inner circle.",
            "scope-note: full shell/solid assembly not attempted."
        ], new EmittedTopologyIdentityMap(identityEntries));
    }

    private static SurfaceMaterializationResult EmitRectangleBody(IReadOnlyList<Point3D> p)
    {
        var builder = new TopologyBuilder();
        var v = new[] { builder.AddVertex(), builder.AddVertex(), builder.AddVertex(), builder.AddVertex() };
        var e = new[] { builder.AddEdge(v[0], v[1]), builder.AddEdge(v[1], v[2]), builder.AddEdge(v[2], v[3]), builder.AddEdge(v[3], v[0]) };
        var loopId = builder.AllocateLoopId();
        var coedgeIds = new[] { builder.AllocateCoedgeId(), builder.AllocateCoedgeId(), builder.AllocateCoedgeId(), builder.AllocateCoedgeId() };
        for (var i = 0; i < 4; i++)
        {
            var next = coedgeIds[(i + 1) % 4];
            var prev = coedgeIds[(i + 3) % 4];
            builder.AddCoedge(new Coedge(coedgeIds[i], e[i], loopId, next, prev, IsReversed: false));
        }

        builder.AddLoop(new Loop(loopId, coedgeIds));
        var face = builder.AddFace([loopId]);
        var shell = builder.AddShell([face]);
        builder.AddBody([shell]);

        var geometry = new BrepGeometryStore();
        geometry.AddCurve(new CurveGeometryId(1), CurveGeometry.FromLine(new Line3Curve(p[0], Direction3D.Create(p[1] - p[0]))));
        geometry.AddCurve(new CurveGeometryId(2), CurveGeometry.FromLine(new Line3Curve(p[1], Direction3D.Create(p[2] - p[1]))));
        geometry.AddCurve(new CurveGeometryId(3), CurveGeometry.FromLine(new Line3Curve(p[2], Direction3D.Create(p[3] - p[2]))));
        geometry.AddCurve(new CurveGeometryId(4), CurveGeometry.FromLine(new Line3Curve(p[3], Direction3D.Create(p[0] - p[3]))));

        var normal = Direction3D.Create((p[1] - p[0]).Cross(p[3] - p[0]));
        var xAxis = Direction3D.Create(p[1] - p[0]);
        geometry.AddSurface(new SurfaceGeometryId(1), SurfaceGeometry.FromPlane(new PlaneSurface(p[0], normal, xAxis)));

        var bindings = new BrepBindingModel();
        for (var i = 0; i < 4; i++) bindings.AddEdgeBinding(new EdgeGeometryBinding(e[i], new CurveGeometryId(i + 1), new ParameterInterval(0d, 1d)));
        bindings.AddFaceBinding(new FaceGeometryBinding(face, new SurfaceGeometryId(1)));

        return new(true, new BrepBody(builder.Model, geometry, bindings), SurfacePatchFamily.Planar, true,
            ["topology-emitted: planar rectangular patch emitted as a minimal single-face body."]);
    }

    private readonly record struct CircularPatchEmissionInput(Point3D Center, Vector3D Normal, double Radius);


    private static SurfaceMaterializationResult EmitCircularBody(CircularPatchEmissionInput circle)
    {
        var builder = new TopologyBuilder();
        var vertex = builder.AddVertex();
        var edge = builder.AddEdge(vertex, vertex);

        var loopId = builder.AllocateLoopId();
        var coedgeId = builder.AllocateCoedgeId();
        builder.AddCoedge(new Coedge(coedgeId, edge, loopId, coedgeId, coedgeId, IsReversed: false));
        builder.AddLoop(new Loop(loopId, [coedgeId]));

        var face = builder.AddFace([loopId]);
        var shell = builder.AddShell([face]);
        builder.AddBody([shell]);

        var normal = Direction3D.Create(circle.Normal);
        var xAxis = BuildReferenceAxis(normal);

        var geometry = new BrepGeometryStore();
        geometry.AddCurve(new CurveGeometryId(1), CurveGeometry.FromCircle(new Circle3Curve(circle.Center, normal, circle.Radius, xAxis)));
        geometry.AddSurface(new SurfaceGeometryId(1), SurfaceGeometry.FromPlane(new PlaneSurface(circle.Center, normal, xAxis)));

        var bindings = new BrepBindingModel();
        bindings.AddEdgeBinding(new EdgeGeometryBinding(edge, new CurveGeometryId(1), new ParameterInterval(0d, 2d * double.Pi)));
        bindings.AddFaceBinding(new FaceGeometryBinding(face, new SurfaceGeometryId(1)));

        return new(true, new BrepBody(builder.Model, geometry, bindings), SurfacePatchFamily.Planar, true,
            [
                "readiness-gate-accepted: no readiness, no emission rule satisfied for circular planar patch.",
                "circular-geometry-accepted: bounded planar circle center/normal/radius consumed.",
                "topology-emitted: planar circular patch emitted as a minimal single-face body with one circular outer loop."
            ]);
    }

    private static bool TryBuildCircularEmissionInput(SourceSurfaceDescriptor source, out CircularPatchEmissionInput? input, out string diagnostic)
    {
        input = null;
        if (source.BoundedPlanarGeometry is not { } bounded || bounded.Kind != BoundedPlanarPatchGeometryKind.Circle)
        {
            diagnostic = "no-circle-bounded-geometry";
            return false;
        }

        if (bounded.Radius <= 0d || !double.IsFinite(bounded.Radius))
        {
            diagnostic = "invalid-circle-radius";
            return false;
        }

        if (bounded.Normal.Length <= 1e-12d)
        {
            diagnostic = "invalid-circle-normal";
            return false;
        }

        input = new CircularPatchEmissionInput(bounded.Center, bounded.Normal, bounded.Radius);
        diagnostic = "ok";
        return true;
    }

    private static Direction3D BuildReferenceAxis(Direction3D normal)
    {
        var n = normal.ToVector();
        var seed = double.Abs(n.Z) < 0.95d ? new Vector3D(0d, 0d, 1d) : new Vector3D(0d, 1d, 0d);
        var projected = seed - (n * seed.Dot(n));
        return Direction3D.TryCreate(projected, out var axis) ? axis : Direction3D.Create(new Vector3D(1d, 0d, 0d));
    }

    private static bool TryParseRectanglePayload(string? payload, out IReadOnlyList<Point3D>? points, out string reason)
    {
        points = null;
        reason = "missing rectangular payload.";
        if (string.IsNullOrWhiteSpace(payload) || !payload.StartsWith("rect3d:", StringComparison.Ordinal)) return false;
        var segments = payload[7..].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length != 4)
        {
            reason = "rect3d payload must contain exactly 4 points.";
            return false;
        }

        var parsed = new List<Point3D>(4);
        foreach (var segment in segments)
        {
            var xyz = segment.Split(',', StringSplitOptions.TrimEntries);
            if (xyz.Length != 3 || !double.TryParse(xyz[0], out var x) || !double.TryParse(xyz[1], out var y) || !double.TryParse(xyz[2], out var z))
            {
                reason = "rect3d point values must be numeric x,y,z triples.";
                return false;
            }
            parsed.Add(new Point3D(x, y, z));
        }

        points = parsed;
        reason = "ok";
        return true;
    }
}

internal static class PlanarPatchPayloadBuilder
{
    internal static bool TryBuildRectanglePayload(SourceSurfaceDescriptor source, out string? payload, out string diagnostic)
    {
        payload = null;
        if (source.Family != SurfacePatchFamily.Planar)
        {
            diagnostic = $"payload-derivation-rejected: source family '{source.Family}' is not planar.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(source.ParameterPayloadReference))
        {
            diagnostic = "payload-derivation-rejected: source descriptor missing rectangle geometry payload reference.";
            return false;
        }

        if (source.ParameterPayloadReference.StartsWith("rect3d:", StringComparison.Ordinal))
        {
            payload = source.ParameterPayloadReference;
            diagnostic = "payload-derivation-succeeded: source descriptor already provides bounded rect3d payload.";
            return true;
        }

        if (source.BoundedPlanarGeometry is { } bounded)
        {
            if (bounded.Kind == BoundedPlanarPatchGeometryKind.Circle)
            {
                diagnostic = "payload-derivation-rejected: PlanarPatchPayloadBuilder currently supports rectangular planar payloads only; circular planar cap emission is deferred.";
                return false;
            }

            payload = $"rect3d:{FormatPoint(bounded.Corner00)};{FormatPoint(bounded.Corner10)};{FormatPoint(bounded.Corner11)};{FormatPoint(bounded.Corner01)}";
            diagnostic = "payload-derivation-succeeded: derived rect3d payload from bounded planar source geometry.";
            return true;
        }

        diagnostic = $"payload-derivation-rejected: planar source payload '{source.ParameterPayloadReference}' does not encode bounded rectangle corners; descriptor needs explicit rectangle geometry.";
        return false;
    }

    private static string FormatPoint(Point3D p) => $"{p.X:G17},{p.Y:G17},{p.Z:G17}";
}

internal sealed record SurfaceMaterializationResult(
    bool Success,
    BrepBody? Body,
    SurfacePatchFamily EmittedSurfaceFamily,
    bool TopologyEmissionImplemented,
    IReadOnlyList<string> Diagnostics,
    EmittedTopologyIdentityMap? IdentityMap = null);

internal enum EmittedTopologyKind
{
    Edge,
    Coedge,
    Loop,
    Face,
    Seam
}

internal enum EmittedTopologyRole
{
    OuterBoundary,
    InnerCircularTrim,
    CylindricalTopBoundary,
    CylindricalBottomBoundary,
    CylindricalSeam,
    Unmapped
}

internal sealed record EmittedTopologyIdentityEntry(
    string LocalTopologyKey,
    EmittedTopologyKind Kind,
    InternalTrimIdentityToken? TrimIdentityToken,
    EmittedTopologyRole Role,
    string OrientationPolicy,
    IReadOnlyList<string> Diagnostics);

internal sealed record EmittedTopologyIdentityMap(
    IReadOnlyList<EmittedTopologyIdentityEntry> Entries)
{
    internal static readonly EmittedTopologyIdentityMap Empty = new([]);
}

internal sealed class CylindricalSurfaceMaterializer : ISurfaceFamilyMaterializer
{
    internal sealed record RetainedWallEmissionRequest(
        FacePatchCandidate Candidate,
        MaterializationReadinessReport Readiness);

    public SurfacePatchFamily Family => SurfacePatchFamily.Cylindrical;
    public string Name => "surface_family_cylindrical";
    public SurfaceMaterializerAdmissibility Evaluate(FacePatchDescriptor patch)
        => patch.SourceSurface.Family == SurfacePatchFamily.Cylindrical
            && patch.OuterLoop.All(t => t.Capability == TrimCurveCapability.ExactSupported)
            ? new(true, "admissible", 9d)
            : new(false, "Requires cylindrical source and exact-supported trims.", 0d);

    internal SurfaceMaterializationResult EmitRetainedWall(RetainedWallEmissionRequest request)
    {
        var candidate = request.Candidate;
        if (request.Readiness.OverallReadiness is EmissionReadiness.NotApplicable or EmissionReadiness.Deferred or EmissionReadiness.Unsupported)
        {
            return new(false, null, SurfacePatchFamily.Cylindrical, false, ["readiness-gate-rejected: no readiness, no emission."]);
        }

        if (candidate.SourceSurface.Family != SurfacePatchFamily.Cylindrical)
        {
            return new(false, null, SurfacePatchFamily.Unsupported, false, ["candidate-rejected: source family is not cylindrical."]);
        }

        if (candidate.RetentionRole != FacePatchRetentionRole.ToolBoundaryRetainedInsideBase)
        {
            return new(false, null, SurfacePatchFamily.Cylindrical, false, ["candidate-rejected: candidate is not tool-side retained cylindrical patch."]);
        }

        if (candidate.SourceSurface.CylindricalGeometryEvidence is not { } evidence)
        {
            return new(false, null, SurfacePatchFamily.Cylindrical, false, ["cylindrical-evidence-missing: canonical cylindrical geometry evidence is required."]);
        }

        if (evidence.Radius <= 1e-12d || evidence.Height <= 1e-12d)
        {
            return new(false, null, SurfacePatchFamily.Cylindrical, false, ["cylindrical-span-missing: cylindrical radius/height evidence is degenerate."]);
        }

        var mouthLoops = candidate.RetainedRegionLoops
            .Where(l => l.LoopKind == RetainedRegionLoopKind.MouthTrim && l.TrimCurveFamily == TrimCurveFamily.Circle)
            .ToArray();
        if (mouthLoops.Length == 0)
        {
            return new(false, null, SurfacePatchFamily.Cylindrical, false, ["cylindrical-loop-evidence-missing: required circular mouth-trim loop evidence is absent."]);
        }

        if (candidate.LoopReadiness is RetainedRegionLoopStatus.Deferred or RetainedRegionLoopStatus.Unsupported)
        {
            return new(false, null, SurfacePatchFamily.Cylindrical, false, [$"cylindrical-loop-readiness-rejected: loop readiness is {candidate.LoopReadiness}."]);
        }

        var axisDirection = evidence.AxisDirection;
        if (axisDirection.Length <= 1e-12d)
        {
            return new(false, null, SurfacePatchFamily.Cylindrical, false, ["unsupported-transform-noncanonical: cylinder axis direction is degenerate."]);
        }

        var axis = Direction3D.Create(axisDirection);
        var isCanonical = double.Abs(double.Abs(axis.ToVector().Dot(new Vector3D(0d, 0d, 1d))) - 1d) <= 1e-9d;
        if (!isCanonical)
        {
            return new(false, null, SurfacePatchFamily.Cylindrical, false, ["unsupported-transform-noncanonical: bounded cylindrical wall emission currently supports translation-safe canonical axis only."]);
        }

        var builder = new TopologyBuilder();
        var seamStart = builder.AddVertex();
        var seamEnd = builder.AddVertex();
        var topVertex = builder.AddVertex();
        var bottomVertex = builder.AddVertex();
        var seamEdge = builder.AddEdge(seamStart, seamEnd);
        var topCircleEdge = builder.AddEdge(topVertex, topVertex);
        var bottomCircleEdge = builder.AddEdge(bottomVertex, bottomVertex);
        var loopId = builder.AllocateLoopId();
        var coedgeIds = new[] { builder.AllocateCoedgeId(), builder.AllocateCoedgeId(), builder.AllocateCoedgeId(), builder.AllocateCoedgeId() };
        builder.AddCoedge(new Coedge(coedgeIds[0], seamEdge, loopId, coedgeIds[1], coedgeIds[3], IsReversed: false));
        builder.AddCoedge(new Coedge(coedgeIds[1], topCircleEdge, loopId, coedgeIds[2], coedgeIds[0], IsReversed: false));
        builder.AddCoedge(new Coedge(coedgeIds[2], seamEdge, loopId, coedgeIds[3], coedgeIds[1], IsReversed: true));
        builder.AddCoedge(new Coedge(coedgeIds[3], bottomCircleEdge, loopId, coedgeIds[0], coedgeIds[2], IsReversed: true));
        builder.AddLoop(new Loop(loopId, coedgeIds));
        var face = builder.AddFace([loopId]);
        var shell = builder.AddShell([face]);
        builder.AddBody([shell]);

        var geometry = new BrepGeometryStore();
        var topCenter = evidence.TopCenter;
        var bottomCenter = evidence.BottomCenter;
        var xAxis = BuildReferenceAxis(axis);
        geometry.AddCurve(new CurveGeometryId(1), CurveGeometry.FromLine(new Line3Curve(new Point3D(bottomCenter.X + evidence.Radius, bottomCenter.Y, bottomCenter.Z), axis)));
        geometry.AddCurve(new CurveGeometryId(2), CurveGeometry.FromCircle(new Circle3Curve(topCenter, axis, evidence.Radius, xAxis)));
        geometry.AddCurve(new CurveGeometryId(3), CurveGeometry.FromCircle(new Circle3Curve(bottomCenter, axis, evidence.Radius, xAxis)));
        geometry.AddSurface(new SurfaceGeometryId(1), SurfaceGeometry.FromCylinder(new CylinderSurface(bottomCenter, axis, evidence.Radius, xAxis)));
        var bindings = new BrepBindingModel();
        bindings.AddEdgeBinding(new EdgeGeometryBinding(seamEdge, new CurveGeometryId(1), new ParameterInterval(0d, evidence.Height)));
        bindings.AddEdgeBinding(new EdgeGeometryBinding(topCircleEdge, new CurveGeometryId(2), new ParameterInterval(0d, 2d * double.Pi)));
        bindings.AddEdgeBinding(new EdgeGeometryBinding(bottomCircleEdge, new CurveGeometryId(3), new ParameterInterval(0d, 2d * double.Pi)));
        bindings.AddFaceBinding(new FaceGeometryBinding(face, new SurfaceGeometryId(1)));

        var body = new BrepBody(builder.Model, geometry, bindings);
        var validation = BrepBindingValidator.Validate(body, requireAllEdgeAndFaceBindings: true);
        if (!validation.IsSuccess)
        {
            return new(false, null, SurfacePatchFamily.Cylindrical, false, ["cylindrical-wall-emission-failed: emitted cylindrical wall topology failed BRep binding validation."]);
        }
        InternalTrimIdentityToken? topToken = null;
        InternalTrimIdentityToken? bottomToken = null;
        foreach (var loop in mouthLoops)
        {
            if (loop.CircularGeometry is not { } circular || string.IsNullOrWhiteSpace(circular.OrderingToken)) continue;
            var token = new InternalTrimIdentityToken("emitted:cylindrical", circular.OrderingToken, circular.OrderingToken, TrimCurveFamily.Circle, loop.LoopKind.ToString(), $"cyl-loop:{circular.OrderingToken}");
            if (circular.OrientationPolicy == RetainedRegionLoopOrientationPolicy.UseCandidateOrientation) topToken ??= token;
            else bottomToken ??= token;
        }
        var cylIdentity = new EmittedTopologyIdentityMap(
        [
            new($"edge:{seamEdge.Value}", EmittedTopologyKind.Seam, null, EmittedTopologyRole.CylindricalSeam, "self-closure-forward-reverse", ["cylindrical seam role tagged for self-closure evidence."]),
            new($"edge:{topCircleEdge.Value}", EmittedTopologyKind.Edge, topToken, EmittedTopologyRole.CylindricalTopBoundary, "follow-mouth-loop", [topToken is null ? "cylindrical top boundary token missing or ambiguous in retained loop evidence." : "cylindrical top boundary token attached from retained loop evidence."]),
            new($"edge:{bottomCircleEdge.Value}", EmittedTopologyKind.Edge, bottomToken, EmittedTopologyRole.CylindricalBottomBoundary, "follow-mouth-loop", [bottomToken is null ? "cylindrical bottom boundary token missing or ambiguous in retained loop evidence." : "cylindrical bottom boundary token attached from retained loop evidence."])
        ]);
        return new(true, body, SurfacePatchFamily.Cylindrical, true,
        [
            "cylindrical-wall-emitted: retained cylindrical wall patch emitted as one cylindrical face.",
            "seam-convention-applied: side face uses cylinder primitive seam strategy (one seam edge used forward/reversed).",
            "scope-note: full shell/solid assembly not attempted."
        ], cylIdentity);
    }

    private static Direction3D BuildReferenceAxis(Direction3D normal)
    {
        var n = normal.ToVector();
        var seed = double.Abs(n.Z) < 0.95d ? new Vector3D(0d, 0d, 1d) : new Vector3D(0d, 1d, 0d);
        var projected = seed - (n * seed.Dot(n));
        return Direction3D.TryCreate(projected, out var axis) ? axis : Direction3D.Create(new Vector3D(1d, 0d, 0d));
    }
}

internal sealed class ConicalSurfaceMaterializer : ISurfaceFamilyMaterializer
{
    public SurfacePatchFamily Family => SurfacePatchFamily.Conical;
    public string Name => "surface_family_conical";
    public SurfaceMaterializerAdmissibility Evaluate(FacePatchDescriptor patch)
        => patch.SourceSurface.Family == SurfacePatchFamily.Conical
            ? new(false, "Conical descriptor family recognized, materialization deferred in CIR-F8.1.", 1d, IsDeferred: true)
            : new(false, "Source surface family mismatch.", 0d);
}

internal sealed class SphericalSurfaceMaterializer : ISurfaceFamilyMaterializer
{
    public SurfacePatchFamily Family => SurfacePatchFamily.Spherical;
    public string Name => "surface_family_spherical";
    public SurfaceMaterializerAdmissibility Evaluate(FacePatchDescriptor patch)
        => patch.SourceSurface.Family == SurfacePatchFamily.Spherical
            && patch.OuterLoop.All(t => t.Capability == TrimCurveCapability.ExactSupported)
            ? new(true, "admissible", 8d)
            : new(false, "Requires spherical source and exact-supported trims.", 0d);
}

internal sealed class ToroidalSurfaceMaterializer : ISurfaceFamilyMaterializer
{
    public SurfacePatchFamily Family => SurfacePatchFamily.Toroidal;
    public string Name => "surface_family_toroidal";
    public SurfaceMaterializerAdmissibility Evaluate(FacePatchDescriptor patch)
        => patch.SourceSurface.Family == SurfacePatchFamily.Toroidal
            ? new(false, "Toroidal descriptor family recognized, materialization deferred in CIR-F8.1.", 1d, IsDeferred: true)
            : new(false, "Source surface family mismatch.", 0d);
}

internal sealed class SplineSurfaceMaterializer : ISurfaceFamilyMaterializer
{
    public SurfacePatchFamily Family => SurfacePatchFamily.Spline;
    public string Name => "surface_family_spline";
    public SurfaceMaterializerAdmissibility Evaluate(FacePatchDescriptor patch)
        => patch.SourceSurface.Family == SurfacePatchFamily.Spline
            ? new(false, "Spline descriptor family recognized, materialization deferred in CIR-F8.1.", 1d, IsDeferred: true)
            : new(false, "Source surface family mismatch.", 0d);
}
