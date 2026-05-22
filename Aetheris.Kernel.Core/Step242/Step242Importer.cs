using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Import;
using Aetheris.Kernel.Core.Judgment;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;
using System.Threading;

namespace Aetheris.Kernel.Core.Step242;

public static class Step242Importer
{
    private const double AreaEps = 1e-8d;
    private const double PointOnSurfaceEps = 1e-5d;
    private const double TinyCoedgeGapSnapEps = 2.5e-5d;
    private const double AngleUnwrapEps = 1e-8d;
    private const double ContainmentEps = 1e-8d;
    private const string PlanarCrossingInsideRecoveryCandidate = "planar_crossing_inside_recover_as_inner";
    private const string PlanarCrossingInsideRejectCandidate = "planar_crossing_inside_reject";
    private static readonly AsyncLocal<ICollection<LoopRoleCircularSamplingDiagnostic>?> CircularSamplingDiagnosticsSink = new();
    private static readonly AsyncLocal<ICollection<LoopRoleCoedgeGapDiagnostic>?> CoedgeGapDiagnosticsSink = new();
    private static readonly AsyncLocal<ICollection<LoopRoleCylinderProjectionDiagnostic>?> CylinderProjectionDiagnosticsSink = new();
    private static readonly AsyncLocal<ICollection<LoopRoleTorusProjectionDiagnostic>?> TorusProjectionDiagnosticsSink = new();
    private static readonly AsyncLocal<ICollection<PlanarMultiBoundJudgmentDiagnostic>?> PlanarMultiBoundJudgmentDiagnosticsSink = new();

    public static IDisposable CaptureLoopRoleCircularSamplingDiagnostics(ICollection<LoopRoleCircularSamplingDiagnostic> sink)
    {
        var previous = CircularSamplingDiagnosticsSink.Value;
        CircularSamplingDiagnosticsSink.Value = sink;
        return new CircularSamplingDiagnosticsScope(previous);
    }

    public static IDisposable CaptureLoopRoleCoedgeGapDiagnostics(ICollection<LoopRoleCoedgeGapDiagnostic> sink)
    {
        var previous = CoedgeGapDiagnosticsSink.Value;
        CoedgeGapDiagnosticsSink.Value = sink;
        return new CoedgeGapDiagnosticsScope(previous);
    }

    public static IDisposable CaptureLoopRoleCylinderProjectionDiagnostics(ICollection<LoopRoleCylinderProjectionDiagnostic> sink)
    {
        var previous = CylinderProjectionDiagnosticsSink.Value;
        CylinderProjectionDiagnosticsSink.Value = sink;
        return new CylinderProjectionDiagnosticsScope(previous);
    }

    public static IDisposable CaptureLoopRoleTorusProjectionDiagnostics(ICollection<LoopRoleTorusProjectionDiagnostic> sink)
    {
        var previous = TorusProjectionDiagnosticsSink.Value;
        TorusProjectionDiagnosticsSink.Value = sink;
        return new TorusProjectionDiagnosticsScope(previous);
    }

    public static IDisposable CapturePlanarMultiBoundJudgmentDiagnostics(ICollection<PlanarMultiBoundJudgmentDiagnostic> sink)
    {
        var previous = PlanarMultiBoundJudgmentDiagnosticsSink.Value;
        PlanarMultiBoundJudgmentDiagnosticsSink.Value = sink;
        return new PlanarMultiBoundJudgmentDiagnosticsScope(previous);
    }

    public static KernelResult<BrepBody> ImportBody(string stepText)
    {
        var orchestrator = ImportOrchestrator.CreateDefault();
        var result = orchestrator.Import(new ImportRequest(stepText));
        return result.BodyResult;
    }

    private static KernelResult<BrepBody> ImportAuto(Step242ParsedDocument document)
    {
        if (Step242TessellatedImportLane.HasTessellatedRoot(document))
        {
            return Step242TessellatedImportLane.ImportFromParsedDocument(document);
        }

        return Step242ExactBRepImportLane.ImportExactBrep(document);
    }

    internal static KernelResult<BrepBody> ImportExactBrepCore(Step242ParsedDocument document)
    {
        var rigidRootClassification = Step242RigidRootClassifier.Classify(document);
        if (rigidRootClassification.Kind == Step242RigidRootClassificationKind.MissingRigidRoot)
        {
            return Step242ImportSharedUtilities.NotImplementedFailure<BrepBody>(
                "Missing MANIFOLD_SOLID_BREP or BREP_WITH_VOIDS root entity.",
                "Importer.TopologyRoot");
        }

        if (rigidRootClassification.Kind == Step242RigidRootClassificationKind.AssemblyLikeMultipleRigidRoots)
        {
            return Step242ImportSharedUtilities.NotImplementedFailure<BrepBody>(
                $"STEP input is assembly-like: detected {rigidRootClassification.RigidRoots.Count} exact BRep rigid roots (MANIFOLD_SOLID_BREP/BREP_WITH_VOIDS). Single-part exact BRep import accepts exactly one rigid root; route this input through assembly extraction/import.",
                "Importer.AssemblyLike.StepMultiRoot");
        }

        var brepEntity = rigidRootClassification.SingleRigidRoot;
        var shellRoleDiagnostics = new List<KernelDiagnostic>();
        var shellFaceEntityIds = new List<IReadOnlyList<int>>();

        var isBrepWithVoids = string.Equals(brepEntity.Name, "BREP_WITH_VOIDS", StringComparison.OrdinalIgnoreCase);
        var outerShellRefResult = Step242SubsetDecoder.ReadReference(brepEntity, 1, isBrepWithVoids ? "BREP_WITH_VOIDS outer shell" : "MANIFOLD_SOLID_BREP shell");
        if (!outerShellRefResult.IsSuccess)
        {
            return KernelResult<BrepBody>.Failure(outerShellRefResult.Diagnostics);
        }

        var outerShellFacesResult = ReadShellFaceIds(document, outerShellRefResult.Value.TargetId, "outer", shellRoleDiagnostics);
        if (!outerShellFacesResult.IsSuccess)
        {
            return KernelResult<BrepBody>.Failure(outerShellFacesResult.Diagnostics);
        }

        shellFaceEntityIds.Add(outerShellFacesResult.Value);

        if (isBrepWithVoids)
        {
            var voidShellRefsResult = Step242SubsetDecoder.ReadReferenceList(brepEntity, 2, "BREP_WITH_VOIDS void shells");
            if (!voidShellRefsResult.IsSuccess)
            {
                return KernelResult<BrepBody>.Failure(voidShellRefsResult.Diagnostics);
            }

            if (voidShellRefsResult.Value.Count == 0)
            {
                return Step242ImportSharedUtilities.ValidationFailure<BrepBody>("BREP_WITH_VOIDS must reference at least one void shell.", "Importer.TopologyRoot.BrepWithVoids");
            }

            foreach (var voidShellRef in voidShellRefsResult.Value)
            {
                var voidShellFacesResult = ReadShellFaceIds(document, voidShellRef.TargetId, "void", shellRoleDiagnostics);
                if (!voidShellFacesResult.IsSuccess)
                {
                    return KernelResult<BrepBody>.Failure(voidShellFacesResult.Diagnostics);
                }

                shellFaceEntityIds.Add(voidShellFacesResult.Value);
            }
        }

        var faceEntityIds = shellFaceEntityIds.SelectMany(ids => ids).ToList();
        AppendSupportedOrphanPlanarFaces(document, faceEntityIds);

        var builder = new TopologyBuilder();
        var geometry = new BrepGeometryStore();
        var bindings = new BrepBindingModel();

        var vertexMap = new Dictionary<int, (VertexId VertexId, Point3D Point)>();
        var edgeMap = new Dictionary<int, EdgeId>();
        var coedges = new List<Coedge>();

        var nextCurveGeometryId = 1;
        var nextSurfaceGeometryId = 1;
        var faceIds = new List<FaceId>();
        var faceEntityToFaceId = new Dictionary<int, FaceId>();

        foreach (var faceEntityId in faceEntityIds)
        {
            var faceEntityResult = document.TryGetEntity(faceEntityId, "ADVANCED_FACE");
            if (!faceEntityResult.IsSuccess)
            {
                return KernelResult<BrepBody>.Failure(faceEntityResult.Diagnostics);
            }

            var faceEntity = faceEntityResult.Value;
            var advancedFaceOffset = faceEntity.Arguments.Count >= 4
                && (faceEntity.Arguments[0] is Step242StringValue || faceEntity.Arguments[0] is Step242OmittedValue)
                ? 1
                : 0;

            var surfaceResult = Step242SubsetDecoder.ReadEntityRefOrInlineConstructor(faceEntity, advancedFaceOffset + 1, "ADVANCED_FACE surface");
            if (!surfaceResult.IsSuccess)
            {
                return KernelResult<BrepBody>.Failure(surfaceResult.Diagnostics);
            }

            Step242ParsedEntity? surfaceEntity = null;
            string surfaceName;
            IReadOnlyList<Step242Value>? inlineSurfaceArguments = null;

            if (surfaceResult.Value.IsReference)
            {
                var surfaceEntityResult = document.TryGetEntity(surfaceResult.Value.ReferenceId!.Value);
                if (!surfaceEntityResult.IsSuccess)
                {
                    return KernelResult<BrepBody>.Failure(surfaceEntityResult.Diagnostics);
                }

                surfaceEntity = surfaceEntityResult.Value;
                surfaceName = surfaceEntityResult.Value.Name;
            }
            else
            {
                surfaceName = surfaceResult.Value.InlineName!;
                inlineSurfaceArguments = surfaceResult.Value.InlineArguments;
            }

            var isSphericalFace = string.Equals(surfaceName, "SPHERICAL_SURFACE", StringComparison.OrdinalIgnoreCase);
            var isConicalFace = string.Equals(surfaceName, "CONICAL_SURFACE", StringComparison.OrdinalIgnoreCase);

            var boundRefsResult = Step242SubsetDecoder.ReadAdvancedFaceBounds(faceEntity, advancedFaceOffset, "ADVANCED_FACE bounds");
            if (!boundRefsResult.IsSuccess)
            {
                return KernelResult<BrepBody>.Failure(boundRefsResult.Diagnostics);
            }

            if (boundRefsResult.Value.Count == 0 && !isSphericalFace)
            {
                return Failure("ADVANCED_FACE without bounds is unsupported in M23 subset.", $"Entity:{faceEntity.Id}");
            }

            var faceSameSenseResult = Step242SubsetDecoder.ReadBoolean(faceEntity, advancedFaceOffset + 2, "ADVANCED_FACE same_sense");
            if (!faceSameSenseResult.IsSuccess)
            {
                return KernelResult<BrepBody>.Failure(faceSameSenseResult.Diagnostics);
            }

            var bindSurfaceResult = DecodeSurfaceGeometry(document, surfaceEntity, surfaceName, inlineSurfaceArguments, faceSameSenseResult.Value, nextSurfaceGeometryId, faceEntity.Id);
            if (!bindSurfaceResult.IsSuccess)
            {
                return KernelResult<BrepBody>.Failure(bindSurfaceResult.Diagnostics);
            }

            var loopData = new List<LoopBuildData>(boundRefsResult.Value.Count);
            foreach (var boundRef in boundRefsResult.Value)
            {
                var boundEntityResult = document.TryGetEntity(boundRef.TargetId);
                if (!boundEntityResult.IsSuccess)
                {
                    return KernelResult<BrepBody>.Failure(boundEntityResult.Diagnostics);
                }

                var boundEntity = boundEntityResult.Value;
                var isFaceBound = string.Equals(boundEntity.Name, "FACE_BOUND", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(boundEntity.Name, "FACE_OUTER_BOUND", StringComparison.OrdinalIgnoreCase);
                if (!isFaceBound)
                {
                    return Failure($"Entity '{boundEntity.Name}' is unsupported in M23 import subset.", $"Entity:{boundEntity.Id}");
                }

                var loopRefResult = Step242SubsetDecoder.ReadReference(boundEntity, 1, "FACE_BOUND loop");
                if (!loopRefResult.IsSuccess)
                {
                    return KernelResult<BrepBody>.Failure(loopRefResult.Diagnostics);
                }

                var boundOrientationResult = Step242SubsetDecoder.ReadBoolean(boundEntity, 2, "FACE_BOUND orientation");
                if (!boundOrientationResult.IsSuccess)
                {
                    return KernelResult<BrepBody>.Failure(boundOrientationResult.Diagnostics);
                }

                _ = boundOrientationResult.Value;
                var isDeclaredOuter = string.Equals(boundEntity.Name, "FACE_OUTER_BOUND", StringComparison.OrdinalIgnoreCase);

                var loopEntityResult = document.TryGetEntity(loopRefResult.Value.TargetId);
                if (!loopEntityResult.IsSuccess)
                {
                    return KernelResult<BrepBody>.Failure(loopEntityResult.Diagnostics);
                }

                if (string.Equals(loopEntityResult.Value.Name, "VERTEX_LOOP", StringComparison.OrdinalIgnoreCase))
                {
                    var vertexLoopRefResult = Step242SubsetDecoder.ReadReference(loopEntityResult.Value, 1, "VERTEX_LOOP vertex");
                    if (!vertexLoopRefResult.IsSuccess)
                    {
                        return KernelResult<BrepBody>.Failure(vertexLoopRefResult.Diagnostics);
                    }

                    var vertexPointResult = Step242SubsetDecoder.ReadVertexPoint(document, vertexLoopRefResult.Value.TargetId);
                    if (!vertexPointResult.IsSuccess)
                    {
                        return KernelResult<BrepBody>.Failure(vertexPointResult.Diagnostics);
                    }

                    if (isSphericalFace)
                    {
                        // Singular spherical bounds can be represented with VERTEX_LOOP. They do not map
                        // to edge/coedge topology in the current subset, so they are treated as degenerate trims.
                        continue;
                    }

                    if (isConicalFace
                        && bindSurfaceResult.Value.SurfaceGeometry.Cone is ConeSurface cone
                        && (vertexPointResult.Value - cone.Apex).Length <= PointOnSurfaceEps)
                    {
                        // Narrow non-spherical support: singular conical apex trims represented as VERTEX_LOOP.
                        // These are topologically degenerate and intentionally omitted from edge/coedge topology.
                        continue;
                    }

                    return Failure("FACE_BOUND loop type 'VERTEX_LOOP' is unsupported for this face in M23 import subset.", $"Entity:{loopEntityResult.Value.Id}");
                }

                if (!string.Equals(loopEntityResult.Value.Name, "EDGE_LOOP", StringComparison.OrdinalIgnoreCase))
                {
                    return Failure($"FACE_BOUND loop type '{loopEntityResult.Value.Name}' is unsupported in M23 import subset.", $"Entity:{loopEntityResult.Value.Id}");
                }

                var orientedEdgeRefsResult = Step242SubsetDecoder.ReadReferenceList(loopEntityResult.Value, 1, "EDGE_LOOP coedges");
                if (!orientedEdgeRefsResult.IsSuccess)
                {
                    return KernelResult<BrepBody>.Failure(orientedEdgeRefsResult.Diagnostics);
                }

                if (orientedEdgeRefsResult.Value.Count == 0)
                {
                    return Failure("EDGE_LOOP must contain at least one ORIENTED_EDGE.", $"Entity:{loopEntityResult.Value.Id}");
                }

                var loopId = builder.AllocateLoopId();
                var coedgeIds = new List<CoedgeId>(orientedEdgeRefsResult.Value.Count);
                for (var i = 0; i < orientedEdgeRefsResult.Value.Count; i++)
                {
                    coedgeIds.Add(builder.AllocateCoedgeId());
                }

                var loopCoedges = new List<Coedge>(orientedEdgeRefsResult.Value.Count);
                var loopSamples = new List<Point3D>();
                var hasDisconnectedCoedgeGap = false;

                for (var i = 0; i < orientedEdgeRefsResult.Value.Count; i++)
                {
                    var orientedEdgeEntityResult = document.TryGetEntity(orientedEdgeRefsResult.Value[i].TargetId, "ORIENTED_EDGE");
                    if (!orientedEdgeEntityResult.IsSuccess)
                    {
                        return KernelResult<BrepBody>.Failure(orientedEdgeEntityResult.Diagnostics);
                    }

                    var orientedEdgeEntity = orientedEdgeEntityResult.Value;
                    var edgeCurveRefResult = Step242SubsetDecoder.ReadReference(orientedEdgeEntity, 3, "ORIENTED_EDGE edge element");
                    if (!edgeCurveRefResult.IsSuccess)
                    {
                        return KernelResult<BrepBody>.Failure(edgeCurveRefResult.Diagnostics);
                    }

                    var edgeCurveEntityResult = document.TryGetEntity(edgeCurveRefResult.Value.TargetId, "EDGE_CURVE");
                    if (!edgeCurveEntityResult.IsSuccess)
                    {
                        return KernelResult<BrepBody>.Failure(edgeCurveEntityResult.Diagnostics);
                    }

                    var edgeIdResult = EnsureEdge(
                        document,
                        edgeCurveEntityResult.Value,
                        builder,
                        geometry,
                        bindings,
                        vertexMap,
                        edgeMap,
                        ref nextCurveGeometryId);

                    if (!edgeIdResult.IsSuccess)
                    {
                        return KernelResult<BrepBody>.Failure(edgeIdResult.Diagnostics);
                    }

                    var edgeSameSenseResult = Step242SubsetDecoder.ReadBoolean(edgeCurveEntityResult.Value, 4, "EDGE_CURVE same_sense");
                    if (!edgeSameSenseResult.IsSuccess)
                    {
                        return KernelResult<BrepBody>.Failure(edgeSameSenseResult.Diagnostics);
                    }

                    var orientedSenseResult = Step242SubsetDecoder.ReadBoolean(orientedEdgeEntity, 4, "ORIENTED_EDGE orientation");
                    if (!orientedSenseResult.IsSuccess)
                    {
                        return KernelResult<BrepBody>.Failure(orientedSenseResult.Diagnostics);
                    }

                    var isReversed = orientedSenseResult.Value != edgeSameSenseResult.Value;

                    var coedge = new Coedge(
                        coedgeIds[i],
                        edgeIdResult.Value,
                        loopId,
                        coedgeIds[(i + 1) % coedgeIds.Count],
                        coedgeIds[(i + coedgeIds.Count - 1) % coedgeIds.Count],
                        IsReversed: isReversed);

                    loopCoedges.Add(coedge);
                    var sampleResult = SampleCoedgePoints(
                        bindings.GetEdgeBinding(edgeIdResult.Value),
                        geometry,
                        coedge.IsReversed,
                        faceEntity.Id,
                        loopId.Value,
                        coedge.Id.Value,
                        edgeIdResult.Value.Value);
                    if (!sampleResult.IsSuccess)
                    {
                        return KernelResult<BrepBody>.Failure(sampleResult.Diagnostics);
                    }

                    var boundaryClassification = AppendLoopSamples(
                        loopSamples,
                        sampleResult.Value,
                        bindSurfaceResult.Value.SurfaceGeometry,
                        faceEntity.Id,
                        loopId.Value,
                        coedge.Id.Value,
                        loopCoedges.Count > 1 ? loopCoedges[^2].Id.Value : (int?)null);
                    if (boundaryClassification == LoopCoedgeGapClassification.Disconnected)
                    {
                        hasDisconnectedCoedgeGap = true;
                    }
                }

                var closingGap = ClassifyCoedgeGap(loopSamples[^1], loopSamples[0], bindSurfaceResult.Value.SurfaceGeometry);
                ReportCoedgeGapDiagnostic(new LoopRoleCoedgeGapDiagnostic(
                    FaceEntityId: faceEntity.Id,
                    LoopId: loopId.Value,
                    PreviousCoedgeId: loopCoedges[^1].Id.Value,
                    NextCoedgeId: loopCoedges[0].Id.Value,
                    Gap3d: closingGap.Gap3d,
                    Gap2d: closingGap.Gap2d,
                    PreviousEdgeGeometryKind: DescribeCurveKind(bindings.GetEdgeBinding(loopCoedges[^1].EdgeId), geometry),
                    NextEdgeGeometryKind: DescribeCurveKind(bindings.GetEdgeBinding(loopCoedges[0].EdgeId), geometry),
                    Classification: closingGap.Classification,
                    WithinPointOnSurfaceEps: closingGap.Gap3d <= PointOnSurfaceEps,
                    WithinContainmentEps: closingGap.Gap3d <= ContainmentEps,
                    WouldCreateGhostSegmentWithoutNormalization: closingGap.Gap3d > PointOnSurfaceEps));

                if (closingGap.Classification == LoopCoedgeGapClassification.TinyNormalizable)
                {
                    loopSamples[0] = loopSamples[^1];
                }
                else if (closingGap.Classification == LoopCoedgeGapClassification.Disconnected)
                {
                    hasDisconnectedCoedgeGap = true;
                }

                loopData.Add(new LoopBuildData(loopId, loopCoedges, loopSamples, hasDisconnectedCoedgeGap, isDeclaredOuter));
            }

            var classifyResult = ClassifyAndNormalizeFaceLoops(faceEntity.Id, loopData, bindSurfaceResult.Value.SurfaceGeometry);
            if (!classifyResult.IsSuccess)
            {
                return KernelResult<BrepBody>.Failure(classifyResult.Diagnostics);
            }

            foreach (var loop in classifyResult.Value)
            {
                builder.AddLoop(new Loop(loop.LoopId, loop.Coedges.Select(c => c.Id).ToList()));
                coedges.AddRange(loop.Coedges);
            }

            var faceLoopIds = classifyResult.Value.Select(l => l.LoopId).ToList();

            var faceId = builder.AddFace(faceLoopIds);
            faceIds.Add(faceId);
            faceEntityToFaceId[faceEntityId] = faceId;

            var (surfaceGeometryId, surfaceGeometry) = bindSurfaceResult.Value;
            nextSurfaceGeometryId++;
            geometry.AddSurface(surfaceGeometryId, surfaceGeometry);
            bindings.AddFaceBinding(new FaceGeometryBinding(faceId, surfaceGeometryId));
        }

        foreach (var coedge in coedges)
        {
            builder.AddCoedge(coedge);
        }

        var shellIds = new List<ShellId>(shellFaceEntityIds.Count);
        var assignedFaceIds = new HashSet<FaceId>();
        var referencedFaceIds = new HashSet<FaceId>(
            shellFaceEntityIds
                .SelectMany(faceSet => faceSet)
                .Where(faceEntityToFaceId.ContainsKey)
                .Select(faceEntityId => faceEntityToFaceId[faceEntityId]));
        foreach (var shellFaceSet in shellFaceEntityIds)
        {
            var shellFaceIds = new List<FaceId>(shellFaceSet.Count);
            foreach (var shellFaceEntityId in shellFaceSet)
            {
                if (!faceEntityToFaceId.TryGetValue(shellFaceEntityId, out var shellFaceId))
                {
                    return Failure($"Shell references unknown face entity #{shellFaceEntityId}.", "Importer.TopologyRoot.BrepWithVoids");
                }

                shellFaceIds.Add(shellFaceId);
                assignedFaceIds.Add(shellFaceId);
            }

            if (shellIds.Count == 0)
            {
                foreach (var faceId in faceIds)
                {
                    if (assignedFaceIds.Contains(faceId) || referencedFaceIds.Contains(faceId))
                    {
                        continue;
                    }

                    shellFaceIds.Add(faceId);
                    assignedFaceIds.Add(faceId);
                }
            }

            shellIds.Add(builder.AddShell(shellFaceIds));
        }

        builder.AddBody(shellIds);

        BrepBodyShellRepresentation? shellRepresentation = null;
        if (shellIds.Count > 0)
        {
            shellRepresentation = new BrepBodyShellRepresentation(shellIds[0], shellIds.Skip(1).ToArray());
        }

        var body = new BrepBody(builder.Model, geometry, bindings, vertexMap.Values.ToDictionary(entry => entry.VertexId, entry => entry.Point), shellRepresentation: shellRepresentation);
        var validation = BrepBindingValidator.Validate(body, requireAllEdgeAndFaceBindings: true);
        if (!validation.IsSuccess)
        {
            return KernelResult<BrepBody>.Failure(validation.Diagnostics);
        }

        return KernelResult<BrepBody>.Success(body, validation.Diagnostics.Concat(shellRoleDiagnostics).ToArray());
    }

    private static KernelResult<IReadOnlyList<int>> ReadShellFaceIds(Step242ParsedDocument document, int shellEntityId, string role, ICollection<KernelDiagnostic> diagnostics)
    {
        var shellEntityResult = document.TryGetEntity(shellEntityId);
        if (!shellEntityResult.IsSuccess)
        {
            return KernelResult<IReadOnlyList<int>>.Failure(shellEntityResult.Diagnostics);
        }

        var shellEntity = shellEntityResult.Value;
        if (string.Equals(shellEntity.Name, "ORIENTED_CLOSED_SHELL", StringComparison.OrdinalIgnoreCase))
        {
            var orientedRefResult = Step242SubsetDecoder.ReadReference(shellEntity, 1, "ORIENTED_CLOSED_SHELL shell");
            if (!orientedRefResult.IsSuccess)
            {
                return KernelResult<IReadOnlyList<int>>.Failure(orientedRefResult.Diagnostics);
            }

            diagnostics.Add(new KernelDiagnostic(KernelDiagnosticCode.NotImplemented, KernelDiagnosticSeverity.Info, $"Resolved oriented {role} shell reference #{shellEntity.Id} -> #{orientedRefResult.Value.TargetId}.", "Importer.TopologyRoot.BrepWithVoids"));
            return ReadShellFaceIds(document, orientedRefResult.Value.TargetId, role, diagnostics);
        }

        if (!string.Equals(shellEntity.Name, "CLOSED_SHELL", StringComparison.OrdinalIgnoreCase))
        {
            return Step242ImportSharedUtilities.NotImplementedFailure<IReadOnlyList<int>>($"Unsupported {role} shell entity '{shellEntity.Name}' (expected CLOSED_SHELL or ORIENTED_CLOSED_SHELL).", "Importer.TopologyRoot.BrepWithVoids");
        }

        var faceRefsResult = Step242SubsetDecoder.ReadReferenceList(shellEntity, 1, $"CLOSED_SHELL {role} faces");
        if (!faceRefsResult.IsSuccess)
        {
            return KernelResult<IReadOnlyList<int>>.Failure(faceRefsResult.Diagnostics);
        }

        return KernelResult<IReadOnlyList<int>>.Success(faceRefsResult.Value.Select(r => r.TargetId).ToArray());
    }

    private static void AppendSupportedOrphanPlanarFaces(Step242ParsedDocument document, IList<int> faceEntityIds)
    {
        var known = new HashSet<int>(faceEntityIds);

        foreach (var entity in document.Entities)
        {
            if (!string.Equals(entity.Name, "ADVANCED_FACE", StringComparison.OrdinalIgnoreCase) || known.Contains(entity.Id))
            {
                continue;
            }

            var advancedFaceOffset = entity.Arguments.Count >= 4
                && (entity.Arguments[0] is Step242StringValue || entity.Arguments[0] is Step242OmittedValue)
                ? 1
                : 0;

            var surfaceResult = Step242SubsetDecoder.ReadEntityRefOrInlineConstructor(entity, advancedFaceOffset + 1, "ADVANCED_FACE surface");
            if (!surfaceResult.IsSuccess)
            {
                continue;
            }

            string surfaceName;
            if (surfaceResult.Value.IsReference)
            {
                var surfaceEntityResult = document.TryGetEntity(surfaceResult.Value.ReferenceId!.Value);
                if (!surfaceEntityResult.IsSuccess)
                {
                    continue;
                }

                surfaceName = surfaceEntityResult.Value.Name;
            }
            else
            {
                surfaceName = surfaceResult.Value.InlineName ?? string.Empty;
            }

            if (!string.Equals(surfaceName, "PLANE", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var boundRefsResult = Step242SubsetDecoder.ReadAdvancedFaceBounds(entity, advancedFaceOffset, "ADVANCED_FACE bounds");
            if (!boundRefsResult.IsSuccess || boundRefsResult.Value.Count == 0)
            {
                continue;
            }

            var allEdgeLoops = true;
            foreach (var boundRef in boundRefsResult.Value)
            {
                var boundEntityResult = document.TryGetEntity(boundRef.TargetId);
                if (!boundEntityResult.IsSuccess)
                {
                    allEdgeLoops = false;
                    break;
                }

                var loopRefResult = Step242SubsetDecoder.ReadReference(boundEntityResult.Value, 1, "FACE_BOUND loop");
                if (!loopRefResult.IsSuccess)
                {
                    allEdgeLoops = false;
                    break;
                }

                var loopEntityResult = document.TryGetEntity(loopRefResult.Value.TargetId);
                if (!loopEntityResult.IsSuccess || !string.Equals(loopEntityResult.Value.Name, "EDGE_LOOP", StringComparison.OrdinalIgnoreCase))
                {
                    allEdgeLoops = false;
                    break;
                }
            }

            if (!allEdgeLoops)
            {
                continue;
            }

            faceEntityIds.Add(entity.Id);
            known.Add(entity.Id);
        }
    }

    private static KernelResult<EdgeId> EnsureEdge(
        Step242ParsedDocument document,
        Step242ParsedEntity edgeCurveEntity,
        TopologyBuilder builder,
        BrepGeometryStore geometry,
        BrepBindingModel bindings,
        IDictionary<int, (VertexId VertexId, Point3D Point)> vertexMap,
        IDictionary<int, EdgeId> edgeMap,
        ref int nextCurveGeometryId)
    {
        if (edgeMap.TryGetValue(edgeCurveEntity.Id, out var existingEdgeId))
        {
            return KernelResult<EdgeId>.Success(existingEdgeId);
        }

        var startRefResult = Step242SubsetDecoder.ReadReference(edgeCurveEntity, 1, "EDGE_CURVE start");
        if (!startRefResult.IsSuccess)
        {
            return KernelResult<EdgeId>.Failure(startRefResult.Diagnostics);
        }

        var endRefResult = Step242SubsetDecoder.ReadReference(edgeCurveEntity, 2, "EDGE_CURVE end");
        if (!endRefResult.IsSuccess)
        {
            return KernelResult<EdgeId>.Failure(endRefResult.Diagnostics);
        }

        var lineRefResult = Step242SubsetDecoder.ReadReference(edgeCurveEntity, 3, "EDGE_CURVE geometry");
        if (!lineRefResult.IsSuccess)
        {
            return KernelResult<EdgeId>.Failure(lineRefResult.Diagnostics);
        }

        var sameSenseResult = Step242SubsetDecoder.ReadBoolean(edgeCurveEntity, 4, "EDGE_CURVE same_sense");
        if (!sameSenseResult.IsSuccess)
        {
            return KernelResult<EdgeId>.Failure(sameSenseResult.Diagnostics);
        }

        var edgeSameSense = sameSenseResult.Value;

        var startVertexResult = EnsureVertex(document, startRefResult.Value.TargetId, builder, vertexMap);
        if (!startVertexResult.IsSuccess)
        {
            return KernelResult<EdgeId>.Failure(startVertexResult.Diagnostics);
        }

        var endVertexResult = EnsureVertex(document, endRefResult.Value.TargetId, builder, vertexMap);
        if (!endVertexResult.IsSuccess)
        {
            return KernelResult<EdgeId>.Failure(endVertexResult.Diagnostics);
        }

        var curveEntityResult = document.TryGetEntity(lineRefResult.Value.TargetId);
        if (!curveEntityResult.IsSuccess)
        {
            return KernelResult<EdgeId>.Failure(curveEntityResult.Diagnostics);
        }

        var edgeId = builder.AddEdge(startVertexResult.Value.VertexId, endVertexResult.Value.VertexId);
        edgeMap.Add(edgeCurveEntity.Id, edgeId);

        var curveGeometryId = new CurveGeometryId(nextCurveGeometryId++);

        var bindCurveResult = DecodeCurveGeometry(
            document,
            curveEntityResult.Value,
            startVertexResult.Value.Point,
            endVertexResult.Value.Point,
            edgeSameSense,
            edgeCurveEntity.Id);
        if (!bindCurveResult.IsSuccess)
        {
            return KernelResult<EdgeId>.Failure(bindCurveResult.Diagnostics);
        }

        geometry.AddCurve(curveGeometryId, bindCurveResult.Value.CurveGeometry);
        bindings.AddEdgeBinding(new EdgeGeometryBinding(edgeId, curveGeometryId, bindCurveResult.Value.TrimInterval, edgeSameSense));

        return KernelResult<EdgeId>.Success(edgeId);
    }

    private static KernelResult<(VertexId VertexId, Point3D Point)> EnsureVertex(
        Step242ParsedDocument document,
        int vertexPointEntityId,
        TopologyBuilder builder,
        IDictionary<int, (VertexId VertexId, Point3D Point)> vertexMap)
    {
        if (vertexMap.TryGetValue(vertexPointEntityId, out var existingVertex))
        {
            return KernelResult<(VertexId VertexId, Point3D Point)>.Success(existingVertex);
        }

        var pointResult = Step242SubsetDecoder.ReadVertexPoint(document, vertexPointEntityId);
        if (!pointResult.IsSuccess)
        {
            return KernelResult<(VertexId VertexId, Point3D Point)>.Failure(pointResult.Diagnostics);
        }

        var vertex = (builder.AddVertex(), pointResult.Value);
        vertexMap.Add(vertexPointEntityId, vertex);
        return KernelResult<(VertexId VertexId, Point3D Point)>.Success(vertex);
    }

    private static KernelResult<(CurveGeometry CurveGeometry, ParameterInterval TrimInterval)> DecodeCurveGeometry(
        Step242ParsedDocument document,
        Step242ParsedEntity curveEntity,
        Point3D startPoint,
        Point3D endPoint,
        bool edgeSameSense,
        int edgeCurveEntityId)
    {
        var lineConstructor = Step242SubsetDecoder.TryGetConstructor(curveEntity.Instance, "LINE");
        if (lineConstructor is not null)
        {
            var lineResult = Step242SubsetDecoder.ReadLineCurve(document, WithConstructor(curveEntity, lineConstructor));
            if (!lineResult.IsSuccess)
            {
                return KernelResult<(CurveGeometry CurveGeometry, ParameterInterval TrimInterval)>.Failure(lineResult.Diagnostics);
            }

            var startParameter = ComputeLineParameter(lineResult.Value, startPoint);
            var endParameter = ComputeLineParameter(lineResult.Value, endPoint);

            if (endParameter < startParameter)
            {
                if (edgeSameSense)
                {
                    return FailureCurveBinding("EDGE_CURVE line parameterization is opposite to vertex ordering.", SourceFor(edgeCurveEntityId, "Importer.Geometry.EdgeCurveParameters"));
                }

                return KernelResult<(CurveGeometry CurveGeometry, ParameterInterval TrimInterval)>.Success((
                    CurveGeometry.FromLine(lineResult.Value),
                    new ParameterInterval(endParameter, startParameter)));
            }

            return KernelResult<(CurveGeometry CurveGeometry, ParameterInterval TrimInterval)>.Success((
                CurveGeometry.FromLine(lineResult.Value),
                new ParameterInterval(startParameter, endParameter)));
        }

        var circleConstructor = Step242SubsetDecoder.TryGetConstructor(curveEntity.Instance, "CIRCLE");
        if (circleConstructor is not null)
        {
            var circleResult = Step242SubsetDecoder.ReadCircleCurve(document, WithConstructor(curveEntity, circleConstructor));
            if (!circleResult.IsSuccess)
            {
                return KernelResult<(CurveGeometry CurveGeometry, ParameterInterval TrimInterval)>.Failure(circleResult.Diagnostics);
            }

            var trimResult = ComputeCircleTrim(circleResult.Value, startPoint, endPoint, edgeSameSense);
            if (!trimResult.IsSuccess)
            {
                return KernelResult<(CurveGeometry CurveGeometry, ParameterInterval TrimInterval)>.Failure(trimResult.Diagnostics);
            }

            return KernelResult<(CurveGeometry CurveGeometry, ParameterInterval TrimInterval)>.Success((
                CurveGeometry.FromCircle(circleResult.Value),
                trimResult.Value));
        }

        var ellipseConstructor = Step242SubsetDecoder.TryGetConstructor(curveEntity.Instance, "ELLIPSE");
        if (ellipseConstructor is not null)
        {
            var ellipseResult = Step242SubsetDecoder.ReadEllipseCurve(document, WithConstructor(curveEntity, ellipseConstructor));
            if (!ellipseResult.IsSuccess)
            {
                return KernelResult<(CurveGeometry CurveGeometry, ParameterInterval TrimInterval)>.Failure(ellipseResult.Diagnostics);
            }

            var trimResult = ComputeEllipseTrim(ellipseResult.Value, startPoint, endPoint, edgeSameSense);
            if (!trimResult.IsSuccess)
            {
                return KernelResult<(CurveGeometry CurveGeometry, ParameterInterval TrimInterval)>.Failure(trimResult.Diagnostics);
            }

            return KernelResult<(CurveGeometry CurveGeometry, ParameterInterval TrimInterval)>.Success((
                CurveGeometry.FromEllipse(ellipseResult.Value),
                trimResult.Value));
        }

        var splineEntity = ResolveBSplineCurveEntity(curveEntity);
        if (splineEntity is not null)
        {
            var splineResult = Step242SubsetDecoder.ReadBSplineCurveWithKnots(document, splineEntity);
            if (!splineResult.IsSuccess)
            {
                return KernelResult<(CurveGeometry CurveGeometry, ParameterInterval TrimInterval)>.Failure(splineResult.Diagnostics);
            }

            return KernelResult<(CurveGeometry CurveGeometry, ParameterInterval TrimInterval)>.Success((
                CurveGeometry.FromBSpline(splineResult.Value),
                new ParameterInterval(splineResult.Value.DomainStart, splineResult.Value.DomainEnd)));
        }

        if (string.Equals(curveEntity.Name, "AETHERIS_PLANAR_UNSUPPORTED_CURVE", StringComparison.OrdinalIgnoreCase))
        {
            return KernelResult<(CurveGeometry CurveGeometry, ParameterInterval TrimInterval)>.Success((
                CurveGeometry.FromUnsupported(curveEntity.Name),
                new ParameterInterval(0d, 1d)));
        }

        return FailureCurveBinding($"EDGE_CURVE geometry '{curveEntity.Name}' is unsupported.", SourceFor(curveEntity.Id, "Importer.EntityFamily"));
    }

    private static Step242ParsedEntity? ResolveBSplineCurveEntity(Step242ParsedEntity curveEntity)
    {
        var splineWithKnotsConstructor = Step242SubsetDecoder.TryGetConstructor(curveEntity.Instance, "B_SPLINE_CURVE_WITH_KNOTS");
        if (splineWithKnotsConstructor is null)
        {
            return null;
        }

        if (splineWithKnotsConstructor.Arguments.Count >= 9)
        {
            return WithConstructor(curveEntity, splineWithKnotsConstructor);
        }

        var splineConstructor = Step242SubsetDecoder.TryGetConstructor(curveEntity.Instance, "B_SPLINE_CURVE");
        if (splineConstructor is null || splineConstructor.Arguments.Count < 5 || splineWithKnotsConstructor.Arguments.Count < 3)
        {
            return null;
        }

        var normalizedArguments = new List<Step242Value>(9)
        {
            Step242OmittedValue.Instance,
            splineConstructor.Arguments[0],
            splineConstructor.Arguments[1],
            splineConstructor.Arguments[2],
            splineConstructor.Arguments[3],
            splineConstructor.Arguments[4],
            splineWithKnotsConstructor.Arguments[0],
            splineWithKnotsConstructor.Arguments[1],
            splineWithKnotsConstructor.Arguments[2]
        };

        var normalizedConstructor = new Step242EntityConstructor("B_SPLINE_CURVE_WITH_KNOTS", normalizedArguments);
        return WithConstructor(curveEntity, normalizedConstructor);
    }

    private static Step242ParsedEntity WithConstructor(Step242ParsedEntity entity, Step242EntityConstructor constructor) =>
        new(entity.Id, new Step242SimpleEntityInstance(constructor));

    private static KernelResult<(SurfaceGeometryId SurfaceGeometryId, SurfaceGeometry SurfaceGeometry)> DecodeSurfaceGeometry(
        Step242ParsedDocument document,
        Step242ParsedEntity? surfaceEntity,
        string surfaceName,
        IReadOnlyList<Step242Value>? inlineSurfaceArguments,
        bool faceSameSense,
        int nextSurfaceGeometryId,
        int faceEntityId)
    {
        var normalizedName = surfaceName.ToUpperInvariant();
        var surfaceToDecodeResult = surfaceEntity is null
            ? BuildInlineSurfaceEntity(faceEntityId, normalizedName, inlineSurfaceArguments)
            : KernelResult<Step242ParsedEntity>.Success(surfaceEntity);
        if (!surfaceToDecodeResult.IsSuccess)
        {
            return KernelResult<(SurfaceGeometryId SurfaceGeometryId, SurfaceGeometry SurfaceGeometry)>.Failure(surfaceToDecodeResult.Diagnostics);
        }

        var surfaceToDecode = surfaceToDecodeResult.Value;
        var geometryId = new SurfaceGeometryId(nextSurfaceGeometryId);

        if (string.Equals(normalizedName, "PLANE", StringComparison.Ordinal))
        {
            var planeResult = Step242SubsetDecoder.ReadPlaneSurface(document, surfaceToDecode);
            if (!planeResult.IsSuccess)
            {
                return KernelResult<(SurfaceGeometryId SurfaceGeometryId, SurfaceGeometry SurfaceGeometry)>.Failure(planeResult.Diagnostics);
            }

            var faceSurface = planeResult.Value;
            if (!faceSameSense)
            {
                if (!Direction3D.TryCreate(-faceSurface.Normal.ToVector(), out var reversedNormal))
                {
                    return OrientationFailure<(SurfaceGeometryId SurfaceGeometryId, SurfaceGeometry SurfaceGeometry)>(
                        "ADVANCED_FACE same_sense not supported for this face",
                        SourceFor(surfaceToDecode.Id, "Importer.Orientation.AdvancedFaceSense"));
                }

                faceSurface = new PlaneSurface(faceSurface.Origin, reversedNormal, faceSurface.UAxis);
            }

            return KernelResult<(SurfaceGeometryId SurfaceGeometryId, SurfaceGeometry SurfaceGeometry)>.Success((geometryId, SurfaceGeometry.FromPlane(faceSurface)));
        }

        if (string.Equals(normalizedName, "CYLINDRICAL_SURFACE", StringComparison.Ordinal))
        {
            var cylinderResult = Step242SubsetDecoder.ReadCylindricalSurface(document, surfaceToDecode);
            if (!cylinderResult.IsSuccess)
            {
                return KernelResult<(SurfaceGeometryId SurfaceGeometryId, SurfaceGeometry SurfaceGeometry)>.Failure(cylinderResult.Diagnostics);
            }

            return KernelResult<(SurfaceGeometryId SurfaceGeometryId, SurfaceGeometry SurfaceGeometry)>.Success((geometryId, SurfaceGeometry.FromCylinder(cylinderResult.Value)));
        }

        if (string.Equals(normalizedName, "SPHERICAL_SURFACE", StringComparison.Ordinal))
        {
            var sphereResult = Step242SubsetDecoder.ReadSphericalSurface(document, surfaceToDecode);
            if (!sphereResult.IsSuccess)
            {
                return KernelResult<(SurfaceGeometryId SurfaceGeometryId, SurfaceGeometry SurfaceGeometry)>.Failure(sphereResult.Diagnostics);
            }

            return KernelResult<(SurfaceGeometryId SurfaceGeometryId, SurfaceGeometry SurfaceGeometry)>.Success((geometryId, SurfaceGeometry.FromSphere(sphereResult.Value)));
        }

        if (string.Equals(normalizedName, "CONICAL_SURFACE", StringComparison.Ordinal))
        {
            var coneResult = Step242SubsetDecoder.ReadConicalSurface(document, surfaceToDecode);
            if (!coneResult.IsSuccess)
            {
                return KernelResult<(SurfaceGeometryId SurfaceGeometryId, SurfaceGeometry SurfaceGeometry)>.Failure(coneResult.Diagnostics);
            }

            return KernelResult<(SurfaceGeometryId SurfaceGeometryId, SurfaceGeometry SurfaceGeometry)>.Success((geometryId, SurfaceGeometry.FromCone(coneResult.Value)));
        }

        if (string.Equals(normalizedName, "TOROIDAL_SURFACE", StringComparison.Ordinal))
        {
            var torusResult = Step242SubsetDecoder.ReadToroidalSurface(document, surfaceToDecode);
            if (!torusResult.IsSuccess)
            {
                return KernelResult<(SurfaceGeometryId SurfaceGeometryId, SurfaceGeometry SurfaceGeometry)>.Failure(torusResult.Diagnostics);
            }

            return KernelResult<(SurfaceGeometryId SurfaceGeometryId, SurfaceGeometry SurfaceGeometry)>.Success((geometryId, SurfaceGeometry.FromTorus(torusResult.Value)));
        }

        var bSplineSurfaceEntity = ResolveBSplineSurfaceEntity(surfaceToDecode);
        if (bSplineSurfaceEntity is not null)
        {
            var surfaceResult = Step242SubsetDecoder.ReadBSplineSurfaceWithKnots(document, bSplineSurfaceEntity);
            if (!surfaceResult.IsSuccess)
            {
                return KernelResult<(SurfaceGeometryId SurfaceGeometryId, SurfaceGeometry SurfaceGeometry)>.Failure(surfaceResult.Diagnostics);
            }

            var recoveryDecision = Step242BsplineSurfaceRecoveryLane.Decide(surfaceToDecode, surfaceResult.Value);
            if (string.Equals(recoveryDecision.CandidateName, "analytic_cylinder", StringComparison.Ordinal)
                && recoveryDecision.RecoveredSurface is not null)
            {
                return KernelResult<(SurfaceGeometryId SurfaceGeometryId, SurfaceGeometry SurfaceGeometry)>.Success((
                    geometryId,
                    recoveryDecision.RecoveredSurface));
            }

            return KernelResult<(SurfaceGeometryId SurfaceGeometryId, SurfaceGeometry SurfaceGeometry)>.Success((
                geometryId,
                SurfaceGeometry.FromBSplineSurfaceWithKnots(surfaceResult.Value)));
        }

        return FailureSurfaceBinding($"ADVANCED_FACE surface '{surfaceName}' is unsupported.", SourceFor(surfaceToDecode.Id, "Importer.EntityFamily"));
    }

    private static Step242ParsedEntity? ResolveBSplineSurfaceEntity(Step242ParsedEntity surfaceEntity)
    {
        var splineWithKnots = Step242SubsetDecoder.TryGetConstructor(surfaceEntity.Instance, "B_SPLINE_SURFACE_WITH_KNOTS");
        if (splineWithKnots is null)
        {
            return null;
        }

        if (splineWithKnots.Arguments.Count >= 13)
        {
            return WithConstructor(surfaceEntity, splineWithKnots);
        }

        var splineSurface = Step242SubsetDecoder.TryGetConstructor(surfaceEntity.Instance, "B_SPLINE_SURFACE");
        if (splineSurface is null || splineSurface.Arguments.Count < 7 || splineWithKnots.Arguments.Count < 5)
        {
            return null;
        }

        var normalized = new List<Step242Value>(13)
        {
            Step242OmittedValue.Instance,
            splineSurface.Arguments[0],
            splineSurface.Arguments[1],
            splineSurface.Arguments[2],
            splineSurface.Arguments[3],
            splineSurface.Arguments[4],
            splineSurface.Arguments[5],
            splineSurface.Arguments[6],
            splineWithKnots.Arguments[0],
            splineWithKnots.Arguments[1],
            splineWithKnots.Arguments[2],
            splineWithKnots.Arguments[3],
            splineWithKnots.Arguments[4]
        };

        return WithConstructor(surfaceEntity, new Step242EntityConstructor("B_SPLINE_SURFACE_WITH_KNOTS", normalized));
    }

    private static KernelResult<Step242ParsedEntity> BuildInlineSurfaceEntity(int faceEntityId, string surfaceName, IReadOnlyList<Step242Value>? inlineArguments)
    {
        if (inlineArguments is null)
        {
            return FailureInlineSurface<Step242ParsedEntity>("ADVANCED_FACE surface: inline constructor arguments are required.", SourceFor(faceEntityId, "Importer.StepSyntax.InlineEntity"));
        }

        if (!string.Equals(surfaceName, "PLANE", StringComparison.Ordinal)
            && !string.Equals(surfaceName, "CYLINDRICAL_SURFACE", StringComparison.Ordinal)
            && !string.Equals(surfaceName, "CONICAL_SURFACE", StringComparison.Ordinal)
            && !string.Equals(surfaceName, "SPHERICAL_SURFACE", StringComparison.Ordinal)
            && !string.Equals(surfaceName, "TOROIDAL_SURFACE", StringComparison.Ordinal))
        {
            return FailureInlineSurface<Step242ParsedEntity>($"ADVANCED_FACE surface: inline constructor '{surfaceName}' is not supported in this subset.", SourceFor(faceEntityId, "Importer.StepSyntax.InlineEntity"));
        }

        var normalizedArgumentsResult = NormalizeInlineSurfaceArguments(surfaceName, inlineArguments, faceEntityId);
        if (!normalizedArgumentsResult.IsSuccess)
        {
            return KernelResult<Step242ParsedEntity>.Failure(normalizedArgumentsResult.Diagnostics);
        }

        return KernelResult<Step242ParsedEntity>.Success(new Step242ParsedEntity(
            faceEntityId,
            new Step242SimpleEntityInstance(new Step242EntityConstructor(surfaceName, normalizedArgumentsResult.Value))));
    }

    private static KernelResult<IReadOnlyList<Step242Value>> NormalizeInlineSurfaceArguments(string surfaceName, IReadOnlyList<Step242Value> inlineArguments, int faceEntityId)
    {
        if (string.Equals(surfaceName, "PLANE", StringComparison.Ordinal))
        {
            if (inlineArguments.Count == 1)
            {
                if (inlineArguments[0] is not Step242EntityReference)
                {
                    return FailureInlineSurface<IReadOnlyList<Step242Value>>("ADVANCED_FACE surface: inline PLANE constructor expects PLANE(#axis2_placement_3d).", SourceFor(faceEntityId, "Importer.StepSyntax.InlineEntity"));
                }

                return KernelResult<IReadOnlyList<Step242Value>>.Success([
                    Step242OmittedValue.Instance,
                    inlineArguments[0]
                ]);
            }

            if (inlineArguments.Count == 2)
            {
                if (inlineArguments[0] is not Step242OmittedValue || inlineArguments[1] is not Step242EntityReference)
                {
                    return FailureInlineSurface<IReadOnlyList<Step242Value>>("ADVANCED_FACE surface: inline PLANE constructor expects PLANE(#axis2_placement_3d).", SourceFor(faceEntityId, "Importer.StepSyntax.InlineEntity"));
                }

                return KernelResult<IReadOnlyList<Step242Value>>.Success(inlineArguments);
            }

            return FailureInlineSurface<IReadOnlyList<Step242Value>>("ADVANCED_FACE surface: inline PLANE constructor expects PLANE(#axis2_placement_3d).", SourceFor(faceEntityId, "Importer.StepSyntax.InlineEntity"));
        }

        if (string.Equals(surfaceName, "CYLINDRICAL_SURFACE", StringComparison.Ordinal))
        {
            if (inlineArguments.Count == 2)
            {
                if (inlineArguments[0] is not Step242EntityReference || inlineArguments[1] is not Step242NumberValue)
                {
                    return FailureInlineSurface<IReadOnlyList<Step242Value>>("Inline ADVANCED_FACE.surface constructor 'CYLINDRICAL_SURFACE' has unsupported argument shape.", SourceFor(faceEntityId, "Importer.StepSyntax.InlineEntity"));
                }

                return KernelResult<IReadOnlyList<Step242Value>>.Success([
                    Step242OmittedValue.Instance,
                    inlineArguments[0],
                    inlineArguments[1]
                ]);
            }

            if (inlineArguments.Count == 3)
            {
                if (inlineArguments[0] is not Step242OmittedValue || inlineArguments[1] is not Step242EntityReference || inlineArguments[2] is not Step242NumberValue)
                {
                    return FailureInlineSurface<IReadOnlyList<Step242Value>>("Inline ADVANCED_FACE.surface constructor 'CYLINDRICAL_SURFACE' has unsupported argument shape.", SourceFor(faceEntityId, "Importer.StepSyntax.InlineEntity"));
                }

                return KernelResult<IReadOnlyList<Step242Value>>.Success(inlineArguments);
            }

            return FailureInlineSurface<IReadOnlyList<Step242Value>>("Inline ADVANCED_FACE.surface constructor 'CYLINDRICAL_SURFACE' has unsupported argument shape.", SourceFor(faceEntityId, "Importer.StepSyntax.InlineEntity"));
        }

        if (string.Equals(surfaceName, "CONICAL_SURFACE", StringComparison.Ordinal))
        {
            if (inlineArguments.Count == 3)
            {
                if (inlineArguments[0] is not Step242EntityReference
                    || inlineArguments[1] is not Step242NumberValue
                    || inlineArguments[2] is not Step242NumberValue)
                {
                    return FailureInlineSurface<IReadOnlyList<Step242Value>>("Inline ADVANCED_FACE.surface constructor 'CONICAL_SURFACE' has unsupported argument shape.", SourceFor(faceEntityId, "Importer.StepSyntax.InlineEntity"));
                }

                return KernelResult<IReadOnlyList<Step242Value>>.Success([
                    Step242OmittedValue.Instance,
                    inlineArguments[0],
                    inlineArguments[1],
                    inlineArguments[2]
                ]);
            }

            if (inlineArguments.Count == 4)
            {
                if (inlineArguments[0] is not Step242OmittedValue
                    || inlineArguments[1] is not Step242EntityReference
                    || inlineArguments[2] is not Step242NumberValue
                    || inlineArguments[3] is not Step242NumberValue)
                {
                    return FailureInlineSurface<IReadOnlyList<Step242Value>>("Inline ADVANCED_FACE.surface constructor 'CONICAL_SURFACE' has unsupported argument shape.", SourceFor(faceEntityId, "Importer.StepSyntax.InlineEntity"));
                }

                return KernelResult<IReadOnlyList<Step242Value>>.Success(inlineArguments);
            }

            return FailureInlineSurface<IReadOnlyList<Step242Value>>("Inline ADVANCED_FACE.surface constructor 'CONICAL_SURFACE' has unsupported argument shape.", SourceFor(faceEntityId, "Importer.StepSyntax.InlineEntity"));
        }

        if (string.Equals(surfaceName, "SPHERICAL_SURFACE", StringComparison.Ordinal))
        {
            if (inlineArguments.Count == 2)
            {
                if (inlineArguments[0] is not Step242EntityReference || inlineArguments[1] is not Step242NumberValue)
                {
                    return FailureInlineSurface<IReadOnlyList<Step242Value>>("Inline ADVANCED_FACE.surface constructor 'SPHERICAL_SURFACE' has unsupported argument shape.", SourceFor(faceEntityId, "Importer.StepSyntax.InlineEntity"));
                }

                return KernelResult<IReadOnlyList<Step242Value>>.Success([
                    Step242OmittedValue.Instance,
                    inlineArguments[0],
                    inlineArguments[1]
                ]);
            }

            if (inlineArguments.Count == 3)
            {
                if (inlineArguments[0] is not Step242OmittedValue || inlineArguments[1] is not Step242EntityReference || inlineArguments[2] is not Step242NumberValue)
                {
                    return FailureInlineSurface<IReadOnlyList<Step242Value>>("Inline ADVANCED_FACE.surface constructor 'SPHERICAL_SURFACE' has unsupported argument shape.", SourceFor(faceEntityId, "Importer.StepSyntax.InlineEntity"));
                }

                return KernelResult<IReadOnlyList<Step242Value>>.Success(inlineArguments);
            }

            return FailureInlineSurface<IReadOnlyList<Step242Value>>("Inline ADVANCED_FACE.surface constructor 'SPHERICAL_SURFACE' has unsupported argument shape.", SourceFor(faceEntityId, "Importer.StepSyntax.InlineEntity"));
        }

        if (string.Equals(surfaceName, "TOROIDAL_SURFACE", StringComparison.Ordinal))
        {
            if (inlineArguments.Count == 3)
            {
                if (inlineArguments[0] is not Step242EntityReference
                    || inlineArguments[1] is not Step242NumberValue
                    || inlineArguments[2] is not Step242NumberValue)
                {
                    return FailureInlineSurface<IReadOnlyList<Step242Value>>("Inline ADVANCED_FACE.surface constructor 'TOROIDAL_SURFACE' has unsupported argument shape.", SourceFor(faceEntityId, "Importer.StepSyntax.InlineEntity"));
                }

                return KernelResult<IReadOnlyList<Step242Value>>.Success([
                    Step242OmittedValue.Instance,
                    inlineArguments[0],
                    inlineArguments[1],
                    inlineArguments[2]
                ]);
            }

            if (inlineArguments.Count == 4)
            {
                if (inlineArguments[0] is not Step242OmittedValue
                    || inlineArguments[1] is not Step242EntityReference
                    || inlineArguments[2] is not Step242NumberValue
                    || inlineArguments[3] is not Step242NumberValue)
                {
                    return FailureInlineSurface<IReadOnlyList<Step242Value>>("Inline ADVANCED_FACE.surface constructor 'TOROIDAL_SURFACE' has unsupported argument shape.", SourceFor(faceEntityId, "Importer.StepSyntax.InlineEntity"));
                }

                return KernelResult<IReadOnlyList<Step242Value>>.Success(inlineArguments);
            }

            return FailureInlineSurface<IReadOnlyList<Step242Value>>("Inline ADVANCED_FACE.surface constructor 'TOROIDAL_SURFACE' has unsupported argument shape.", SourceFor(faceEntityId, "Importer.StepSyntax.InlineEntity"));
        }

        return KernelResult<IReadOnlyList<Step242Value>>.Success(inlineArguments);
    }

    private static KernelResult<T> FailureInlineSurface<T>(string message, string source)
        => KernelResult<T>.Failure([
            new KernelDiagnostic(
                KernelDiagnosticCode.NotImplemented,
                KernelDiagnosticSeverity.Error,
                message,
                source)
        ]);

    private static KernelResult<ParameterInterval> ComputeEllipseTrim(Ellipse3Curve ellipse, Point3D startPoint, Point3D endPoint, bool edgeSameSense)
    {
        var tolerance = ComputeEllipseTrimTolerance(ellipse, startPoint, endPoint);

        var startAngleResult = ProjectPointToEllipseAngle(ellipse, startPoint, tolerance);
        if (!startAngleResult.IsSuccess)
        {
            return KernelResult<ParameterInterval>.Failure(startAngleResult.Diagnostics);
        }

        var endAngleResult = ProjectPointToEllipseAngle(ellipse, endPoint, tolerance);
        if (!endAngleResult.IsSuccess)
        {
            return KernelResult<ParameterInterval>.Failure(endAngleResult.Diagnostics);
        }

        var intervalResult = CanonicalizeCircleTrimInterval(startAngleResult.Value, endAngleResult.Value, tolerance, edgeSameSense);
        if (!intervalResult.IsSuccess)
        {
            return FailureEllipseTrim(intervalResult.Diagnostics[0].Message, intervalResult.Diagnostics[0].Source);
        }

        return intervalResult;
    }

    private static KernelResult<double> ProjectPointToEllipseAngle(Ellipse3Curve ellipse, Point3D point, double tolerance)
    {
        var centerToPoint = point - ellipse.Center;
        var normal = ellipse.Normal.ToVector();
        var normalComponent = centerToPoint.Dot(normal);
        if (double.Abs(normalComponent) > tolerance)
        {
            return FailureEllipseTrimAngle($"Unable to project elliptical trim point: off-ellipse plane deviation {double.Abs(normalComponent):G17} exceeds tolerance {tolerance:G17}.", "Importer.Geometry.EllipseTrim.OffPlane");
        }

        var inPlane = centerToPoint - (normal * normalComponent);
        var majorComponent = inPlane.Dot(ellipse.XAxis.ToVector());
        var minorComponent = inPlane.Dot(ellipse.YAxis.ToVector());

        var normalizedMajor = majorComponent / ellipse.MajorRadius;
        var normalizedMinor = minorComponent / ellipse.MinorRadius;
        var radialError = double.Abs((normalizedMajor * normalizedMajor) + (normalizedMinor * normalizedMinor) - 1d);
        if (radialError > System.Math.Max(1e-8d, tolerance / System.Math.Max(1d, ellipse.MajorRadius)))
        {
            return FailureEllipseTrimAngle($"Unable to project elliptical trim point: normalized radial deviation {radialError:G17} exceeds tolerance.", "Importer.Geometry.EllipseTrim.OffRadius");
        }

        return KernelResult<double>.Success(double.Atan2(normalizedMinor, normalizedMajor));
    }

    private static double ComputeEllipseTrimTolerance(Ellipse3Curve ellipse, Point3D startPoint, Point3D endPoint)
    {
        var scale = System.Math.Max(ellipse.MajorRadius, System.Math.Max((startPoint - ellipse.Center).Length, (endPoint - ellipse.Center).Length));
        return System.Math.Max(1e-6d, scale * 1e-8d);
    }

    private static KernelResult<ParameterInterval> ComputeCircleTrim(Circle3Curve circle, Point3D startPoint, Point3D endPoint, bool edgeSameSense)
    {
        var tolerance = ComputeCircleTrimTolerance(circle, startPoint, endPoint);
        if ((startPoint - endPoint).Length <= tolerance)
        {
            return KernelResult<ParameterInterval>.Success(new ParameterInterval(0d, 2d * double.Pi));
        }

        var startAngleResult = ProjectPointToCircleAngle(circle, startPoint, tolerance);
        if (!startAngleResult.IsSuccess)
        {
            return KernelResult<ParameterInterval>.Failure(startAngleResult.Diagnostics);
        }

        var endAngleResult = ProjectPointToCircleAngle(circle, endPoint, tolerance);
        if (!endAngleResult.IsSuccess)
        {
            return KernelResult<ParameterInterval>.Failure(endAngleResult.Diagnostics);
        }

        var intervalResult = CanonicalizeCircleTrimInterval(startAngleResult.Value, endAngleResult.Value, tolerance, edgeSameSense);
        if (!intervalResult.IsSuccess)
        {
            return KernelResult<ParameterInterval>.Failure(intervalResult.Diagnostics);
        }

        return KernelResult<ParameterInterval>.Success(intervalResult.Value);
    }

    private static KernelResult<double> ProjectPointToCircleAngle(Circle3Curve circle, Point3D point, double tolerance)
    {
        var radial = point - circle.Center;
        var normalComponent = radial.Dot(circle.Normal.ToVector());
        var inPlane = radial - (circle.Normal.ToVector() * normalComponent);
        var inPlaneLength = inPlane.Length;

        if (double.Abs(normalComponent) > tolerance)
        {
            return FailureCircleTrimAngle($"Unable to project circular trim point: off-circle plane deviation {double.Abs(normalComponent):G17} exceeds tolerance {tolerance:G17}.", "Importer.Geometry.CircleTrim.OffPlane");
        }

        if (double.Abs(inPlaneLength - circle.Radius) > tolerance)
        {
            return FailureCircleTrimAngle($"Unable to project circular trim point: radial deviation {double.Abs(inPlaneLength - circle.Radius):G17} exceeds tolerance {tolerance:G17}.", "Importer.Geometry.CircleTrim.OffRadius");
        }

        if (inPlaneLength <= tolerance)
        {
            return FailureCircleTrimAngle("Unable to project circular trim point: projected in-plane radius is degenerate.", "Importer.Geometry.CircleTrim.DegeneratePoint");
        }

        var projected = inPlane * (circle.Radius / inPlaneLength);
        var x = projected.Dot(circle.XAxis.ToVector());
        var y = projected.Dot(circle.YAxis.ToVector());
        var angle = double.Atan2(y, x);
        if (angle < 0d)
        {
            angle += 2d * double.Pi;
        }

        return KernelResult<double>.Success(angle);
    }

    private static double ComputeCircleTrimTolerance(Circle3Curve circle, Point3D startPoint, Point3D endPoint)
    {
        const double baseTolerance = 1e-6d;
        // Circle-trim projection tolerance is intentionally bounded and geometry-scale-aware:
        // - lower-bounded by baseTolerance to avoid denorm/zero-scale instability,
        // - scaled by local circle/endpoint size to absorb exporter drift observed in AP242 corpus files,
        // - scoped to circle-trim recovery only (not a global tolerance loosening knob).
        const double scaleFactor = 5e-4d;
        var startRadius = (startPoint - circle.Center).Length;
        var endRadius = (endPoint - circle.Center).Length;
        var scale = double.Max(1d, double.Max(circle.Radius, double.Max(startRadius, endRadius)));
        return double.Max(baseTolerance, scale * scaleFactor);
    }

    private static KernelResult<ParameterInterval> CanonicalizeCircleTrimInterval(double startAngle, double endAngle, double tolerance, bool edgeSameSense)
    {
        var start = NormalizeAngle(startAngle);
        var end = NormalizeAngle(endAngle);
        var canonicalStart = edgeSameSense ? start : end;
        var canonicalEnd = edgeSameSense ? end : start;
        var span = NormalizePositiveAngle(canonicalEnd - canonicalStart);
        var angleTolerance = tolerance;

        if (span <= angleTolerance)
        {
            return KernelResult<ParameterInterval>.Success(new ParameterInterval(0d, 2d * double.Pi));
        }

        if (span >= (2d * double.Pi) - angleTolerance)
        {
            return KernelResult<ParameterInterval>.Success(new ParameterInterval(0d, 2d * double.Pi));
        }

        var normalizedEnd = canonicalStart + span;
        if (normalizedEnd <= canonicalStart)
        {
            return FailureCircleTrim("Unable to compute circle trim interval with a positive span after canonicalization.", "Importer.Geometry.CircleTrim.Interval");
        }

        return KernelResult<ParameterInterval>.Success(new ParameterInterval(canonicalStart, normalizedEnd));
    }

    private static double NormalizeAngle(double angle)
    {
        var period = 2d * double.Pi;
        var normalized = angle % period;
        if (normalized < 0d)
        {
            normalized += period;
        }

        if (normalized >= period)
        {
            normalized -= period;
        }

        return normalized;
    }

    private static double NormalizePositiveAngle(double angle)
    {
        var period = 2d * double.Pi;
        var normalized = angle % period;
        if (normalized < 0d)
        {
            normalized += period;
        }

        return normalized;
    }

    private static double ComputeLineParameter(Line3Curve line, Point3D point)
    {
        var offset = point - line.Origin;
        return offset.Dot(line.Direction.ToVector());
    }

    private static KernelResult<IReadOnlyList<LoopBuildData>> ClassifyAndNormalizeFaceLoops(
        int faceEntityId,
        IReadOnlyList<LoopBuildData> loops,
        SurfaceGeometry surface)
    {
        if (loops.Count <= 1)
        {
            return KernelResult<IReadOnlyList<LoopBuildData>>.Success(loops);
        }

        return surface.Kind switch
        {
            SurfaceGeometryKind.Plane => ClassifyAndNormalizePlanarLoops(loops, surface.Plane!.Value),
            SurfaceGeometryKind.Cylinder => ClassifyAndNormalizeCylindricalLoops(faceEntityId, loops, surface.Cylinder!.Value),
            SurfaceGeometryKind.Torus => ClassifyAndNormalizeToroidalLoops(faceEntityId, loops, surface.Torus!.Value),
            SurfaceGeometryKind.Cone => ClassifyAndNormalizeConicalLoops(faceEntityId, loops, surface.Cone!.Value),
            SurfaceGeometryKind.Sphere => LoopRoleFailure<IReadOnlyList<LoopBuildData>>(
                "Multi-loop hole classification is unsupported for surface type 'Sphere'.",
                "Importer.LoopRole.UnsupportedSurfaceForHoles.Sphere"),
            _ => LoopRoleFailure<IReadOnlyList<LoopBuildData>>(
                $"Multi-loop hole classification is unsupported for surface type '{surface.Kind}'.",
                $"Importer.LoopRole.UnsupportedSurfaceForHoles.{surface.Kind}")
        };
    }

    private static KernelResult<IReadOnlyList<LoopBuildData>> ClassifyAndNormalizePlanarLoops(
        IReadOnlyList<LoopBuildData> loops,
        PlaneSurface plane)
    {
        var infos = new List<PlanarLoopInfo>(loops.Count);
        foreach (var loop in loops)
        {
            var projected = SimplifyClosedPolygon(ProjectLoopToPlane(loop.Samples, plane), ContainmentEps);
            var uniqueCount = CountUniquePoints(projected, ContainmentEps);
            var area = ComputeSignedArea(projected);
            if (uniqueCount < 3 || double.Abs(area) <= AreaEps)
            {
                continue;
            }

            infos.Add(new PlanarLoopInfo(loop, projected, area));
        }

        if (infos.Count == 0)
        {
            return LoopRoleFailure<IReadOnlyList<LoopBuildData>>("Loop projection is degenerate and no non-degenerate boundary remained.", "Importer.LoopRole.DegenerateLoop");
        }

        var containmentTolerance = ComputeContainmentTolerance(infos.SelectMany(i => i.ProjectedPoints));
        var areaTolerance = ComputeAreaTolerance(infos.SelectMany(i => i.ProjectedPoints));

        var infosWithSamples = new List<PlanarLoopInfoWithSample>(infos.Count);
        foreach (var info in infos)
        {
            var testPointResult = ChooseContainmentPoint(info.ProjectedPoints, containmentTolerance);
            if (!testPointResult.IsSuccess)
            {
                return KernelResult<IReadOnlyList<LoopBuildData>>.Failure(testPointResult.Diagnostics);
            }

            infosWithSamples.Add(new PlanarLoopInfoWithSample(info, testPointResult.Value));
        }

        var orderedByContainmentThenArea = infosWithSamples
            .Select(candidate =>
            {
                var containmentCount = infosWithSamples.Count(other => other.Info.Loop.LoopId != candidate.Info.Loop.LoopId
                    && IsLoopContainedByOuter(other.Info.ProjectedPoints, candidate.Info.ProjectedPoints, containmentTolerance));
                return new PlanarLoopOuterCandidate(candidate.Info, candidate.SamplePoint, containmentCount);
            })
            .OrderByDescending(c => c.ContainmentCount)
            .ThenByDescending(c => double.Abs(c.Info.SignedArea))
            .ThenBy(c => c.Info.Loop.LoopId.Value)
            .ToList();

        var outer = orderedByContainmentThenArea[0];
        if (orderedByContainmentThenArea.Count > 1
            && outer.ContainmentCount == orderedByContainmentThenArea[1].ContainmentCount
            && double.Abs(double.Abs(outer.Info.SignedArea) - double.Abs(orderedByContainmentThenArea[1].Info.SignedArea)) <= areaTolerance)
        {
            return LoopRoleFailure<IReadOnlyList<LoopBuildData>>("Unable to choose a unique outer loop.", "Importer.LoopRole.AmbiguousOuter");
        }

        var containedInners = new List<PlanarLoopInfo>();
        var declaredOuterCount = infosWithSamples.Count(i => i.Info.Loop.IsDeclaredOuter);

        foreach (var candidate in infosWithSamples.Where(i => i.Info.Loop.LoopId != outer.Info.Loop.LoopId))
        {
            var containment = EvaluateContainment(candidate.Info.ProjectedPoints, outer.Info.ProjectedPoints, containmentTolerance);
            var intersectionCount = CountPolygonIntersections(candidate.Info.ProjectedPoints, outer.Info.ProjectedPoints, containmentTolerance);
            var intersectsOuter = intersectionCount > 0;
            if ((!intersectsOuter && containment.OutsideCount == 0)
                || (intersectsOuter
                    && containment.OutsideCount == 0
                    && containment.MinDistanceToOuter > (containmentTolerance * 8d)))
            {
                containedInners.Add(candidate.Info);
                continue;
            }

            if (double.Abs(candidate.Info.SignedArea) <= areaTolerance * 4d
                || IsPointNearPolygonEdge(candidate.SamplePoint, outer.Info.ProjectedPoints, containmentTolerance * 4d))
            {
                continue;
            }

            if (intersectsOuter
                && containment.OutsideCount == 0
                && TryRecoverPlanarCrossingInnerWithJudgmentEngine(candidate.Info, outer.Info, containment, intersectionCount, containmentTolerance, loops.Count, declaredOuterCount))
            {
                containedInners.Add(candidate.Info);
                continue;
            }

            var failure = BuildInnerContainmentFailure(candidate.Info, outer.Info, containmentTolerance, areaTolerance, intersectionCount, candidate.Info.Loop.HasDisconnectedCoedgeGap || outer.Info.Loop.HasDisconnectedCoedgeGap);
            return LoopRoleFailure<IReadOnlyList<LoopBuildData>>(failure.Message, failure.Source);
        }

        var innerLoops = containedInners;
        for (var i = 0; i < innerLoops.Count; i++)
        {
            for (var j = i + 1; j < innerLoops.Count; j++)
            {
                if (LoopsOverlap(innerLoops[i], innerLoops[j], containmentTolerance))
                {
                    return LoopRoleFailure<IReadOnlyList<LoopBuildData>>("Inner loops overlap or are nested.", "Importer.LoopRole.InnerOverlap");
                }
            }
        }

        var normalizedOuter = NormalizeLoopWinding(outer.Info.Loop, outer.Info.SignedArea, shouldBePositive: true);
        var normalizedInners = innerLoops
            .OrderByDescending(i => double.Abs(i.SignedArea))
            .ThenBy(i => i.Loop.LoopId.Value)
            .Select(i => NormalizeLoopWinding(i.Loop, i.SignedArea, shouldBePositive: false))
            .ToList();

        var ordered = new List<LoopBuildData>(1 + normalizedInners.Count) { normalizedOuter };
        ordered.AddRange(normalizedInners);
        return KernelResult<IReadOnlyList<LoopBuildData>>.Success(ordered);
    }

    private static KernelResult<IReadOnlyList<LoopBuildData>> ClassifyAndNormalizeCylindricalLoops(
        int faceEntityId,
        IReadOnlyList<LoopBuildData> loops,
        CylinderSurface cylinder)
    {
        const string nonNormalizableSource = "Importer.LoopRole.CylinderNonNormalizableDegenerateProjection";
        const string ambiguousOuterSource = "Importer.LoopRole.CylinderAmbiguousOuter";
        const string containmentSource = "Importer.LoopRole.CylinderInnerContainmentFailed";

        var infos = new List<CylindricalLoopInfo>(loops.Count);
        var degenerate = new List<(LoopBuildData Loop, CylinderProjectionAnalysis Analysis)>(loops.Count);
        var maxUniqueCount = 0;
        var maxAbsArea = 0d;
        foreach (var loop in loops)
        {
            var projected = ProjectLoopToCylinder(loop.Samples, cylinder);
            var uniqueCount = CountUniquePoints(projected, ContainmentEps);
            var area = ComputeSignedArea(projected);
            var analysis = AnalyzeCylindricalProjection(uniqueCount, area, projected);
            maxUniqueCount = System.Math.Max(maxUniqueCount, uniqueCount);
            maxAbsArea = System.Math.Max(maxAbsArea, double.Abs(area));

            ReportCylinderProjectionDiagnostic(new LoopRoleCylinderProjectionDiagnostic(
                FaceEntityId: faceEntityId,
                LoopId: loop.LoopId.Value,
                PointCount: projected.Count,
                UniquePointCount: uniqueCount,
                SignedArea: area,
                AngularSpan: analysis.AngularSpan,
                AxialSpan: analysis.AxialSpan,
                SeamCrossings: analysis.SeamCrossings,
                RepeatedSeamPointCount: analysis.RepeatedSeamPointCount,
                Degeneracy: analysis.Degeneracy.ToString()));

            if (uniqueCount < 3 || double.Abs(area) <= AreaEps)
            {
                degenerate.Add((loop, analysis));
                continue;
            }

            infos.Add(new CylindricalLoopInfo(loop, projected, area, ComputePolygonCentroid(projected)));
        }

        var fullRevolutionConstantAxial = degenerate
            .Where(d => d.Analysis.Degeneracy == CylinderProjectionDegeneracy.FullRevolutionConstantAxial)
            .OrderBy(d => d.Loop.LoopId.Value)
            .ToArray();
        var otherDegenerate = degenerate
            .Where(d => d.Analysis.Degeneracy != CylinderProjectionDegeneracy.FullRevolutionConstantAxial)
            .OrderBy(d => d.Loop.LoopId.Value)
            .ToArray();

        if (fullRevolutionConstantAxial.Length > 0 && (otherDegenerate.Length > 0 || infos.Count > 0))
        {
            var normalizedMixedInners = infos
                .OrderByDescending(i => double.Abs(i.SignedArea))
                .ThenBy(i => i.Loop.LoopId.Value)
                .Select(i => NormalizeLoopWinding(i.Loop, i.SignedArea, shouldBePositive: false));

            var mixedOrdered = new List<LoopBuildData>(fullRevolutionConstantAxial.Length + otherDegenerate.Length + infos.Count);
            mixedOrdered.AddRange(fullRevolutionConstantAxial.Select(d => d.Loop));
            mixedOrdered.AddRange(otherDegenerate.Select(d => d.Loop));
            mixedOrdered.AddRange(normalizedMixedInners);
            return KernelResult<IReadOnlyList<LoopBuildData>>.Success(mixedOrdered);
        }

        if (infos.Count == 0)
        {
            if (degenerate.All(d => d.Analysis.Degeneracy == CylinderProjectionDegeneracy.FullRevolutionConstantAxial))
            {
                var canonical = degenerate
                    .OrderBy(d => d.Loop.LoopId.Value)
                    .Select(d => d.Loop)
                    .ToArray();
                return KernelResult<IReadOnlyList<LoopBuildData>>.Success(canonical);
            }

            var primary = degenerate
                .OrderByDescending(d => (int)d.Analysis.Degeneracy)
                .ThenBy(d => d.Loop.LoopId.Value)
                .FirstOrDefault();

            var explicitSource = primary.Analysis.Degeneracy switch
            {
                CylinderProjectionDegeneracy.DegenerateAngularSpan => "Importer.LoopRole.CylinderDegenerateAngularSpan",
                CylinderProjectionDegeneracy.DegenerateAxialSpan => "Importer.LoopRole.CylinderDegenerateAxialSpan",
                CylinderProjectionDegeneracy.RepeatedSeamProjectionCollapse => "Importer.LoopRole.CylinderRepeatedSeamProjectionCollapse",
                _ => nonNormalizableSource
            };

            return LoopRoleFailure<IReadOnlyList<LoopBuildData>>($"Cylinder loop normalization failed: all {loops.Count} loop(s) projected degenerate (maxUnique={maxUniqueCount}, maxAbsArea={maxAbsArea:E6}, faceId={faceEntityId}, primaryLoopId={primary.Loop.LoopId.Value}, primaryDegeneracy={primary.Analysis.Degeneracy}).", explicitSource);
        }

        var orderedByArea = infos
            .OrderByDescending(i => double.Abs(i.SignedArea))
            .ThenBy(i => i.Loop.LoopId.Value)
            .ToArray();

        var outer = orderedByArea[0];
        if (orderedByArea.Length > 1)
        {
            var areaTolerance = ComputeAreaTolerance(orderedByArea.SelectMany(i => i.ProjectedPoints));
            if (double.Abs(double.Abs(outer.SignedArea) - double.Abs(orderedByArea[1].SignedArea)) <= areaTolerance)
            {
                return LoopRoleFailure<IReadOnlyList<LoopBuildData>>("Unable to choose a unique cylindrical outer loop.", ambiguousOuterSource);
            }
        }

        var containmentTolerance = ComputeContainmentTolerance(outer.ProjectedPoints);
        var containedInners = new List<CylindricalLoopInfo>();
        foreach (var candidate in infos.Where(i => i.Loop.LoopId != outer.Loop.LoopId))
        {
            var alignedCandidate = AlignPolygonToReference(candidate.ProjectedPoints, candidate.Centroid.X, outer.Centroid.X);
            var sampleResult = ChooseContainmentPoint(alignedCandidate, containmentTolerance);
            if (!sampleResult.IsSuccess)
            {
                return LoopRoleFailure<IReadOnlyList<LoopBuildData>>("Unable to classify cylindrical inner loop containment.", containmentSource);
            }

            if (!IsPointInPolygon(sampleResult.Value, outer.ProjectedPoints, containmentTolerance))
            {
                return LoopRoleFailure<IReadOnlyList<LoopBuildData>>($"Cylindrical inner loop {candidate.Loop.LoopId.Value} could not be normalized inside selected outer loop {outer.Loop.LoopId.Value}.", containmentSource);
            }

            containedInners.Add(candidate with { ProjectedPoints = alignedCandidate, Centroid = ComputePolygonCentroid(alignedCandidate) });
        }

        var normalizedOuter = NormalizeLoopWinding(outer.Loop, outer.SignedArea, shouldBePositive: true);
        var normalizedInners = containedInners
            .OrderByDescending(i => double.Abs(i.SignedArea))
            .ThenBy(i => i.Loop.LoopId.Value)
            .Select(i => NormalizeLoopWinding(i.Loop, i.SignedArea, shouldBePositive: false))
            .ToList();

        var ordered = new List<LoopBuildData>(1 + normalizedInners.Count) { normalizedOuter };
        ordered.AddRange(normalizedInners);
        return KernelResult<IReadOnlyList<LoopBuildData>>.Success(ordered);
    }

    private static KernelResult<IReadOnlyList<LoopBuildData>> ClassifyAndNormalizeConicalLoops(
        int faceEntityId,
        IReadOnlyList<LoopBuildData> loops,
        ConeSurface cone)
    {
        const string nonNormalizableSource = "Importer.LoopRole.ConeNonNormalizableDegenerateProjection";

        var degenerate = loops
            .Select(loop =>
            {
                var projected = ProjectLoopToCone(loop.Samples, cone);
                var uniqueCount = CountUniquePoints(projected, ContainmentEps);
                var area = ComputeSignedArea(projected);
                return (Loop: loop, Analysis: AnalyzeConicalProjection(uniqueCount, area, projected));
            })
            .OrderBy(d => d.Loop.LoopId.Value)
            .ToArray();

        if (degenerate.All(d => d.Analysis.Degeneracy == ConeProjectionDegeneracy.FullRevolutionConstantAxial))
        {
            var preservedUses = degenerate
                .OrderByDescending(d => d.Analysis.AxialMean)
                .ThenBy(d => d.Loop.LoopId.Value)
                .Select(d => d.Loop)
                .ToArray();
            return KernelResult<IReadOnlyList<LoopBuildData>>.Success(preservedUses);
        }

        var primary = degenerate
            .OrderByDescending(d => (int)d.Analysis.Degeneracy)
            .ThenBy(d => d.Loop.LoopId.Value)
            .First();

        var explicitSource = primary.Analysis.Degeneracy switch
        {
            ConeProjectionDegeneracy.DegenerateAngularSpan => "Importer.LoopRole.ConeDegenerateAngularSpan",
            ConeProjectionDegeneracy.DegenerateAxialSpan => "Importer.LoopRole.ConeDegenerateAxialSpan",
            ConeProjectionDegeneracy.RepeatedSeamProjectionCollapse => "Importer.LoopRole.ConeRepeatedSeamProjectionCollapse",
            _ => nonNormalizableSource
        };

        return LoopRoleFailure<IReadOnlyList<LoopBuildData>>(
            $"Cone loop normalization failed: faceId={faceEntityId}, apex=({cone.Apex.X:G17},{cone.Apex.Y:G17},{cone.Apex.Z:G17}), placementRadius={cone.PlacementRadius:G17}, semiAngle={cone.SemiAngleRadians:G17}, loopCount={loops.Count}, primaryLoopId={primary.Loop.LoopId.Value}, primaryDegeneracy={primary.Analysis.Degeneracy}.",
            explicitSource);
    }

    private static KernelResult<IReadOnlyList<LoopBuildData>> ClassifyAndNormalizeToroidalLoops(
        int faceEntityId,
        IReadOnlyList<LoopBuildData> loops,
        TorusSurface torus)
    {
        const string degenerateSource = "Importer.LoopRole.TorusDegenerateProjection";
        const string ambiguousOuterSource = "Importer.LoopRole.TorusAmbiguousOuter";
        const string containmentSource = "Importer.LoopRole.TorusInnerContainmentFailed";
        const string degenerateMajorSpanSource = "Importer.LoopRole.TorusDegenerateMajorSpan";
        const string degenerateMinorSpanSource = "Importer.LoopRole.TorusDegenerateMinorSpan";
        const string repeatedSeamSource = "Importer.LoopRole.TorusRepeatedSeamProjectionCollapse";
        const string repeatedMajorMinorSeamSource = "Importer.LoopRole.TorusRepeatedMajorMinorSeamProjectionCollapse";
        const string majorSeamUnwrapSource = "Importer.LoopRole.TorusMajorSeamUnwrapFailure";
        const string minorSeamUnwrapSource = "Importer.LoopRole.TorusMinorSeamUnwrapFailure";

        var infos = new List<CylindricalLoopInfo>(loops.Count);
        var maxUniqueCount = 0;
        var maxAbsArea = 0d;
        var degenerateAnalyses = new List<(LoopBuildData Loop, TorusProjectionAnalysis Analysis)>();
        foreach (var loop in loops)
        {
            var projectedRaw = ProjectLoopToTorus(loop.Samples, torus);
            var uniqueCountRaw = CountUniquePoints(projectedRaw, ContainmentEps);
            var areaRaw = ComputeSignedArea(projectedRaw);
            var analysisRaw = AnalyzeToroidalProjection(uniqueCountRaw, areaRaw, projectedRaw);

            var projected = DeduplicateToroidalSeamSamples(SimplifyClosedPolygon(projectedRaw, ContainmentEps));
            var uniqueCount = CountUniquePoints(projected, ContainmentEps);
            var area = ComputeSignedArea(projected);
            var analysis = AnalyzeToroidalProjection(uniqueCount, area, projected);
            maxUniqueCount = System.Math.Max(maxUniqueCount, uniqueCount);
            maxAbsArea = System.Math.Max(maxAbsArea, double.Abs(area));

            if (analysis.Degeneracy != TorusProjectionDegeneracy.None)
            {
                var recovered = TryRecoverToroidalProjectionFromCyclicUnwrap(loop.Samples, torus);
                if (recovered is not null)
                {
                    projected = DeduplicateToroidalSeamSamples(SimplifyClosedPolygon(recovered.Value.Projected, ContainmentEps));
                    uniqueCount = recovered.Value.UniquePointCount;
                    area = recovered.Value.SignedArea;
                    analysis = recovered.Value.Analysis;
                }
            }

            var majorRingCandidate = IsToroidalFullMajorRevolutionConstantMinor(analysis);

            ReportTorusProjectionDiagnostic(new LoopRoleTorusProjectionDiagnostic(
                FaceEntityId: faceEntityId,
                LoopId: loop.LoopId.Value,
                PointCount: projected.Count,
                UniquePointCount: uniqueCount,
                SignedArea: area,
                MajorSpan: analysis.MajorSpan,
                MinorSpan: analysis.MinorSpan,
                MajorSeamCrossings: analysis.MajorSeamCrossings,
                MinorSeamCrossings: analysis.MinorSeamCrossings,
                RepeatedMajorSeamPointCount: analysis.RepeatedMajorSeamPointCount,
                RepeatedMinorSeamPointCount: analysis.RepeatedMinorSeamPointCount,
                Degeneracy: analysis.Degeneracy.ToString(),
                InitialDegeneracy: analysisRaw.Degeneracy.ToString(),
                InitialSignedArea: areaRaw,
                FullMajorSpanNearConstantMinorCandidate: majorRingCandidate));

            if (uniqueCount < 3 || double.Abs(area) <= AreaEps)
            {
                degenerateAnalyses.Add((loop, analysis));
                continue;
            }

            infos.Add(new CylindricalLoopInfo(loop, projected, area, ComputePolygonCentroid(projected)));
        }

        if (infos.Count == 0)
        {
            if (degenerateAnalyses.Count > 0 && degenerateAnalyses.All(d => IsToroidalFullMajorRevolutionConstantMinor(d.Analysis)))
            {
                var preservedUses = degenerateAnalyses
                    .OrderBy(d => d.Loop.LoopId.Value)
                    .Select(d => d.Loop)
                    .ToArray();
                return KernelResult<IReadOnlyList<LoopBuildData>>.Success(preservedUses);
            }

            var primary = degenerateAnalyses
                .OrderByDescending(d => (int)d.Analysis.Degeneracy)
                .ThenBy(d => d.Loop.LoopId.Value)
                .FirstOrDefault();

            var explicitSource = primary.Analysis.Degeneracy switch
            {
                TorusProjectionDegeneracy.DegenerateMajorSpan => degenerateMajorSpanSource,
                TorusProjectionDegeneracy.DegenerateMinorSpan => degenerateMinorSpanSource,
                TorusProjectionDegeneracy.RepeatedSeamProjectionCollapse when primary.Analysis.RepeatedMajorSeamPointCount > 0 && primary.Analysis.RepeatedMinorSeamPointCount > 0 => repeatedMajorMinorSeamSource,
                TorusProjectionDegeneracy.RepeatedSeamProjectionCollapse => repeatedSeamSource,
                TorusProjectionDegeneracy.MajorPeriodSeamUnwrapFailure => majorSeamUnwrapSource,
                TorusProjectionDegeneracy.MinorPeriodSeamUnwrapFailure => minorSeamUnwrapSource,
                _ => degenerateSource
            };

            return LoopRoleFailure<IReadOnlyList<LoopBuildData>>($"Toroidal loop normalization failed: all {loops.Count} loop(s) projected degenerate (maxUnique={maxUniqueCount}, maxAbsArea={maxAbsArea:E6}, faceId={faceEntityId}, primaryLoopId={primary.Loop.LoopId.Value}, primaryDegeneracy={primary.Analysis.Degeneracy}).", explicitSource);
        }

        var orderedByArea = infos
            .OrderByDescending(i => double.Abs(i.SignedArea))
            .ThenBy(i => i.Loop.LoopId.Value)
            .ToArray();

        var outer = orderedByArea[0];
        if (orderedByArea.Length > 1)
        {
            var areaTolerance = ComputeAreaTolerance(orderedByArea.SelectMany(i => i.ProjectedPoints));
            if (double.Abs(double.Abs(outer.SignedArea) - double.Abs(orderedByArea[1].SignedArea)) <= areaTolerance)
            {
                return LoopRoleFailure<IReadOnlyList<LoopBuildData>>("Unable to choose a unique toroidal outer loop.", ambiguousOuterSource);
            }
        }

        var containmentTolerance = ComputeContainmentTolerance(outer.ProjectedPoints);
        var containedInners = new List<CylindricalLoopInfo>();
        foreach (var candidate in infos.Where(i => i.Loop.LoopId != outer.Loop.LoopId))
        {
            var alignedCandidate = AlignPolygonToReference(candidate.ProjectedPoints, candidate.Centroid.X, outer.Centroid.X);
            alignedCandidate = AlignPolygonToReferenceY(alignedCandidate, candidate.Centroid.Y, outer.Centroid.Y);
            var sampleResult = ChooseContainmentPoint(alignedCandidate, containmentTolerance);
            if (!sampleResult.IsSuccess)
            {
                return LoopRoleFailure<IReadOnlyList<LoopBuildData>>("Unable to classify toroidal inner loop containment.", containmentSource);
            }

            if (!IsPointInPolygon(sampleResult.Value, outer.ProjectedPoints, containmentTolerance))
            {
                return LoopRoleFailure<IReadOnlyList<LoopBuildData>>($"Toroidal inner loop {candidate.Loop.LoopId.Value} could not be normalized inside selected outer loop {outer.Loop.LoopId.Value}.", containmentSource);
            }

            containedInners.Add(candidate with { ProjectedPoints = alignedCandidate, Centroid = ComputePolygonCentroid(alignedCandidate) });
        }

        var normalizedOuter = NormalizeLoopWinding(outer.Loop, outer.SignedArea, shouldBePositive: true);
        var normalizedInners = containedInners
            .OrderByDescending(i => double.Abs(i.SignedArea))
            .ThenBy(i => i.Loop.LoopId.Value)
            .Select(i => NormalizeLoopWinding(i.Loop, i.SignedArea, shouldBePositive: false))
            .ToList();

        var ordered = new List<LoopBuildData>(1 + normalizedInners.Count) { normalizedOuter };
        ordered.AddRange(normalizedInners);
        return KernelResult<IReadOnlyList<LoopBuildData>>.Success(ordered);
    }

    private static LoopBuildData NormalizeLoopWinding(LoopBuildData loop, double signedArea, bool shouldBePositive)
    {
        if (shouldBePositive ? signedArea >= 0d : signedArea <= 0d)
        {
            return loop;
        }

        var flippedCoedges = loop.Coedges
            .Select(c => c with { IsReversed = !c.IsReversed })
            .ToList();

        return new LoopBuildData(loop.LoopId, flippedCoedges, loop.Samples, loop.HasDisconnectedCoedgeGap);
    }

    private static bool LoopsOverlap(PlanarLoopInfo a, PlanarLoopInfo b, double containmentTolerance)
    {
        var aPoint = ChooseContainmentPoint(a.ProjectedPoints, containmentTolerance);
        var bPoint = ChooseContainmentPoint(b.ProjectedPoints, containmentTolerance);
        if (!aPoint.IsSuccess || !bPoint.IsSuccess)
        {
            return true;
        }

        return IsPointInPolygon(aPoint.Value, b.ProjectedPoints, containmentTolerance) || IsPointInPolygon(bPoint.Value, a.ProjectedPoints, containmentTolerance);
    }

    private static ContainmentFailure BuildInnerContainmentFailure(
        PlanarLoopInfo inner,
        PlanarLoopInfo outer,
        double containmentTolerance,
        double areaTolerance,
        int intersectionCount,
        bool hasDisconnectedCoedges)
    {
        var containment = EvaluateContainment(inner.ProjectedPoints, outer.ProjectedPoints, containmentTolerance);
        var crossingOuter = intersectionCount > 0;

        var areaRatio = double.Abs(outer.SignedArea) <= areaTolerance
            ? 0d
            : double.Abs(inner.SignedArea) / double.Abs(outer.SignedArea);

        var reason = crossingOuter
            ? (containment.OutsideCount == 0 ? "crosses_outer_boundary_with_all_vertices_inside" : "crosses_outer_boundary_with_outside_vertices")
            : (containment.OutsideCount == containment.VertexCount ? "disjoint" : "partially_outside");

        var source = reason switch
        {
            "crosses_outer_boundary_with_all_vertices_inside" => "Importer.LoopRole.InnerBoundaryIntersectionWithContainedVerticesAfterNormalization",
            "crosses_outer_boundary_with_outside_vertices" when hasDisconnectedCoedges => "Importer.LoopRole.DisconnectedCoedges",
            "crosses_outer_boundary_with_outside_vertices" => "Importer.LoopRole.InnerBoundaryIntersectionWithOutsideVerticesAfterNormalization",
            "disjoint" => "Importer.LoopRole.InnerDisjointAfterNormalization",
            _ => "Importer.LoopRole.InnerPartiallyOutsideAfterNormalization"
        };

        var message = source == "Importer.LoopRole.DisconnectedCoedges"
            ? $"Planar loop contains disconnected consecutive coedges and cannot be normalized safely. innerLoopId={inner.Loop.LoopId.Value}, outerLoopId={outer.Loop.LoopId.Value}, outsideVertices={containment.OutsideCount}/{containment.VertexCount}, nearestOuterDistance={containment.MinDistanceToOuter:E6}, areaRatio={areaRatio:E6}, intersections={intersectionCount}."
            : $"Inner loop could not be normalized: {reason}. innerLoopId={inner.Loop.LoopId.Value}, outerLoopId={outer.Loop.LoopId.Value}, outsideVertices={containment.OutsideCount}/{containment.VertexCount}, nearestOuterDistance={containment.MinDistanceToOuter:E6}, areaRatio={areaRatio:E6}, intersections={intersectionCount}.";
        return new ContainmentFailure(message, source);
    }

    private static bool TryRecoverPlanarCrossingInnerWithJudgmentEngine(
        PlanarLoopInfo inner,
        PlanarLoopInfo outer,
        ContainmentEvaluation containment,
        int intersectionCount,
        double containmentTolerance,
        int loopCount,
        int declaredOuterCount)
    {
        var context = BuildPlanarCrossingInsideRecoveryContext(inner, outer, containment, intersectionCount, containmentTolerance, loopCount, declaredOuterCount);
        var engine = new JudgmentEngine<PlanarCrossingInsideRecoveryContext>();
        var result = engine.Evaluate(context, BuildPlanarCrossingInsideRecoveryCandidates());
        var selected = result.Selection?.Candidate.Name ?? PlanarCrossingInsideRejectCandidate;
        ReportPlanarMultiBoundJudgmentDiagnostic(new PlanarMultiBoundJudgmentDiagnostic(
            FaceLoopCount: context.LoopCount,
            OuterLoopId: context.OuterLoopId,
            InnerLoopId: context.InnerLoopId,
            OuterArea: context.OuterArea,
            InnerArea: context.InnerArea,
            AreaRatio: context.AreaRatio,
            ContainedVertexCount: context.ContainedVertexCount,
            VertexCount: context.VertexCount,
            IntersectionCount: context.IntersectionCount,
            MinDistanceToOuter: context.MinDistanceToOuter,
            HasDisconnectedCoedgeGap: context.HasDisconnectedCoedgeGap,
            HasSingleDeclaredOuterLoop: context.HasSingleDeclaredOuterLoop,
            SelectedCandidate: selected,
            CandidateRejections: string.Join(" | ", result.Rejections.Select(rejection => $"{rejection.CandidateName}:{rejection.Reason}"))));

        return string.Equals(selected, PlanarCrossingInsideRecoveryCandidate, StringComparison.Ordinal);
    }

    private static PlanarCrossingInsideRecoveryContext BuildPlanarCrossingInsideRecoveryContext(
        PlanarLoopInfo inner,
        PlanarLoopInfo outer,
        ContainmentEvaluation containment,
        int intersectionCount,
        double containmentTolerance,
        int loopCount,
        int declaredOuterCount)
    {
        var outerArea = double.Abs(outer.SignedArea);
        var innerArea = double.Abs(inner.SignedArea);
        var areaRatio = outerArea <= AreaEps ? 0d : innerArea / outerArea;
        var hasDisconnectedCoedgeGap = inner.Loop.HasDisconnectedCoedgeGap || outer.Loop.HasDisconnectedCoedgeGap;
        return new PlanarCrossingInsideRecoveryContext(
            InnerLoopId: inner.Loop.LoopId.Value,
            OuterLoopId: outer.Loop.LoopId.Value,
            LoopCount: loopCount,
            InnerArea: innerArea,
            OuterArea: outerArea,
            AreaRatio: areaRatio,
            IntersectionCount: intersectionCount,
            ContainedVertexCount: containment.VertexCount - containment.OutsideCount,
            VertexCount: containment.VertexCount,
            MinDistanceToOuter: containment.MinDistanceToOuter,
            HasDisconnectedCoedgeGap: hasDisconnectedCoedgeGap,
            HasSingleDeclaredOuterLoop: declaredOuterCount == 1,
            IsNearBoundaryContact: containment.MinDistanceToOuter <= containmentTolerance * 8d);
    }

    private static IReadOnlyList<JudgmentCandidate<PlanarCrossingInsideRecoveryContext>> BuildPlanarCrossingInsideRecoveryCandidates()
    {
        return
        [
            new JudgmentCandidate<PlanarCrossingInsideRecoveryContext>(
                Name: PlanarCrossingInsideRecoveryCandidate,
                IsAdmissible: When.All<PlanarCrossingInsideRecoveryContext>(
                    context => context.LoopCount >= 2,
                    context => context.IntersectionCount > 0,
                    context => context.ContainedVertexCount == context.VertexCount,
                    context => !context.HasDisconnectedCoedgeGap,
                    context => context.AreaRatio > 0d && context.AreaRatio < 0.98d,
                    context => context.IsNearBoundaryContact || context.HasSingleDeclaredOuterLoop),
                Score: context => (100d - context.IntersectionCount) + ((1d - context.AreaRatio) * 10d),
                RejectionReason: context =>
                    $"requires bounded planar crossing-all-inside recovery facts (loopCount={context.LoopCount}, intersections={context.IntersectionCount}, contained={context.ContainedVertexCount}/{context.VertexCount}, disconnected={context.HasDisconnectedCoedgeGap}, areaRatio={context.AreaRatio:E6}, nearBoundary={context.IsNearBoundaryContact}, singleDeclaredOuter={context.HasSingleDeclaredOuterLoop})",
                TieBreakerPriority: 0),
            new JudgmentCandidate<PlanarCrossingInsideRecoveryContext>(
                Name: PlanarCrossingInsideRejectCandidate,
                IsAdmissible: _ => true,
                Score: _ => -1d,
                RejectionReason: _ => "bounded planar crossing-all-inside recovery candidate is not admissible",
                TieBreakerPriority: 1)
        ];
    }

    private static ContainmentEvaluation EvaluateContainment(
        IReadOnlyList<UvPoint> inner,
        IReadOnlyList<UvPoint> outer,
        double containmentTolerance)
    {
        var outsideCount = 0;
        var vertexCount = 0;
        var minDistanceToOuter = double.MaxValue;

        for (var i = 0; i < inner.Count - 1; i++)
        {
            var point = inner[i];
            vertexCount++;
            minDistanceToOuter = double.Min(minDistanceToOuter, DistancePointToPolygon(point, outer));
            if (!IsPointInPolygon(point, outer, containmentTolerance))
            {
                outsideCount++;
            }
        }

        if (double.IsPositiveInfinity(minDistanceToOuter) || minDistanceToOuter == double.MaxValue)
        {
            minDistanceToOuter = 0d;
        }

        return new ContainmentEvaluation(outsideCount, vertexCount, minDistanceToOuter);
    }

    private static bool IsLoopContainedByOuter(
        IReadOnlyList<UvPoint> inner,
        IReadOnlyList<UvPoint> outer,
        double containmentTolerance)
    {
        if (CountPolygonIntersections(inner, outer, containmentTolerance) > 0)
        {
            return false;
        }

        var containment = EvaluateContainment(inner, outer, containmentTolerance);
        return containment.OutsideCount == 0;
    }

    private static double DistancePointToPolygon(UvPoint point, IReadOnlyList<UvPoint> polygon)
    {
        var minDistance = double.MaxValue;
        for (var i = 0; i < polygon.Count - 1; i++)
        {
            minDistance = double.Min(minDistance, DistancePointToSegment(point, polygon[i], polygon[i + 1]));
        }

        return minDistance;
    }

    private static int CountPolygonIntersections(IReadOnlyList<UvPoint> a, IReadOnlyList<UvPoint> b, double tolerance)
    {
        var intersections = 0;
        for (var i = 0; i < a.Count - 1; i++)
        {
            if (SegmentLengthSquared(a[i], a[i + 1]) <= (tolerance * tolerance))
            {
                continue;
            }

            for (var j = 0; j < b.Count - 1; j++)
            {
                if (SegmentLengthSquared(b[j], b[j + 1]) <= (tolerance * tolerance))
                {
                    continue;
                }

                if (SegmentsIntersect(a[i], a[i + 1], b[j], b[j + 1], tolerance))
                {
                    intersections++;
                }
            }
        }

        return intersections;
    }

    private static bool SegmentsIntersect(UvPoint a0, UvPoint a1, UvPoint b0, UvPoint b1, double tolerance)
    {
        var o1 = Orientation(a0, a1, b0);
        var o2 = Orientation(a0, a1, b1);
        var o3 = Orientation(b0, b1, a0);
        var o4 = Orientation(b0, b1, a1);

        if (IsProperStraddle(o1, o2, tolerance) && IsProperStraddle(o3, o4, tolerance))
        {
            return true;
        }

        return (double.Abs(o1) <= tolerance && IsPointOnSegment(b0, a0, a1, tolerance))
            || (double.Abs(o2) <= tolerance && IsPointOnSegment(b1, a0, a1, tolerance))
            || (double.Abs(o3) <= tolerance && IsPointOnSegment(a0, b0, b1, tolerance))
            || (double.Abs(o4) <= tolerance && IsPointOnSegment(a1, b0, b1, tolerance));
    }

    private static bool IsProperStraddle(double a, double b, double tolerance)
        => (a > tolerance && b < -tolerance) || (a < -tolerance && b > tolerance);

    private static bool IsPointOnSegment(UvPoint p, UvPoint a, UvPoint b, double tolerance)
    {
        var minX = double.Min(a.X, b.X) - tolerance;
        var maxX = double.Max(a.X, b.X) + tolerance;
        var minY = double.Min(a.Y, b.Y) - tolerance;
        var maxY = double.Max(a.Y, b.Y) + tolerance;
        return p.X >= minX && p.X <= maxX && p.Y >= minY && p.Y <= maxY;
    }

    private static double SegmentLengthSquared(UvPoint a, UvPoint b)
    {
        var delta = b - a;
        return delta.Dot(delta);
    }

    private static double Orientation(UvPoint a, UvPoint b, UvPoint c)
        => ((b.X - a.X) * (c.Y - a.Y)) - ((b.Y - a.Y) * (c.X - a.X));

    private static KernelResult<UvPoint> ChooseContainmentPoint(IReadOnlyList<UvPoint> polygon, double containmentTolerance)
    {
        if (polygon.Count < 4)
        {
            return LoopRoleFailure<UvPoint>("Unable to choose a containment sample point.", "Importer.LoopRole.DegenerateLoop");
        }

        var centroid = ComputePolygonCentroid(polygon);
        if (!IsPointNearPolygonEdge(centroid, polygon, containmentTolerance) && IsPointInPolygon(centroid, polygon, containmentTolerance))
        {
            return KernelResult<UvPoint>.Success(centroid);
        }

        for (var i = 0; i < polygon.Count - 1; i++)
        {
            var start = polygon[i];
            var end = polygon[i + 1];
            var midpoint = new UvPoint((start.X + end.X) * 0.5d, (start.Y + end.Y) * 0.5d);
            var towardCentroid = centroid - midpoint;
            var candidate = midpoint + (towardCentroid * 0.125d);
            if (!IsPointNearPolygonEdge(candidate, polygon, containmentTolerance) && IsPointInPolygon(candidate, polygon, containmentTolerance))
            {
                return KernelResult<UvPoint>.Success(candidate);
            }
        }

        return LoopRoleFailure<UvPoint>("Unable to choose a containment sample point.", "Importer.LoopRole.DegenerateLoop");
    }

    private static UvPoint ComputePolygonCentroid(IReadOnlyList<UvPoint> polygon)
    {
        var sumX = 0d;
        var sumY = 0d;
        var count = 0;

        for (var i = 0; i < polygon.Count - 1; i++)
        {
            sumX += polygon[i].X;
            sumY += polygon[i].Y;
            count++;
        }

        if (count == 0)
        {
            return new UvPoint(0d, 0d);
        }

        return new UvPoint(sumX / count, sumY / count);
    }

    private static bool IsPointNearPolygonEdge(UvPoint point, IReadOnlyList<UvPoint> polygon, double containmentTolerance)
    {
        for (var i = 0; i < polygon.Count - 1; i++)
        {
            if (DistancePointToSegment(point, polygon[i], polygon[i + 1]) <= containmentTolerance)
            {
                return true;
            }
        }

        return false;
    }

    private static double DistancePointToSegment(UvPoint p, UvPoint a, UvPoint b)
    {
        var ab = b - a;
        var ap = p - a;
        var denom = ab.Dot(ab);
        if (denom <= AreaEps)
        {
            return (p - a).Length;
        }

        var t = ap.Dot(ab) / denom;
        if (t < 0d)
        {
            t = 0d;
        }
        else if (t > 1d)
        {
            t = 1d;
        }
        var closest = a + (ab * t);
        return (p - closest).Length;
    }

    private static bool IsPointInPolygon(UvPoint point, IReadOnlyList<UvPoint> polygon, double containmentTolerance)
    {
        var inside = false;
        for (var i = 0; i < polygon.Count - 1; i++)
        {
            var a = polygon[i];
            var b = polygon[i + 1];

            if (DistancePointToSegment(point, a, b) <= containmentTolerance)
            {
                return true;
            }

            var intersects = ((a.Y > point.Y) != (b.Y > point.Y))
                             && (point.X < ((b.X - a.X) * (point.Y - a.Y) / ((b.Y - a.Y) + AngleUnwrapEps)) + a.X);
            if (intersects)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private static int CountUniquePoints(IReadOnlyList<UvPoint> points, double tolerance)
    {
        var unique = new List<UvPoint>();
        foreach (var point in points)
        {
            if (unique.Any(u => (u - point).Length <= tolerance))
            {
                continue;
            }

            unique.Add(point);
        }

        return unique.Count;
    }


    private static List<UvPoint> SimplifyClosedPolygon(IReadOnlyList<UvPoint> polygon, double tolerance)
    {
        var simplified = new List<UvPoint>(polygon.Count + 1);
        foreach (var point in polygon)
        {
            if (simplified.Count > 0 && (simplified[^1] - point).Length <= tolerance)
            {
                continue;
            }

            simplified.Add(point);
        }

        if (simplified.Count == 0)
        {
            return simplified;
        }

        if ((simplified[0] - simplified[^1]).Length > tolerance)
        {
            simplified.Add(simplified[0]);
        }
        else
        {
            simplified[^1] = simplified[0];
        }

        return simplified;
    }

    private static List<UvPoint> ProjectLoopToPlane(IReadOnlyList<Point3D> samples, PlaneSurface plane)
    {
        var uv = new List<UvPoint>(samples.Count + 1);
        foreach (var sample in samples)
        {
            var offset = sample - plane.Origin;
            uv.Add(new UvPoint(offset.Dot(plane.UAxis.ToVector()), offset.Dot(plane.VAxis.ToVector())));
        }

        if (uv.Count > 0 && (uv[0] - uv[^1]).Length > ContainmentEps)
        {
            uv.Add(uv[0]);
        }

        return uv;
    }

    private static List<UvPoint> ProjectLoopToCylinder(IReadOnlyList<Point3D> samples, CylinderSurface cylinder)
    {
        var axis = cylinder.Axis.ToVector();
        var xAxis = cylinder.XAxis.ToVector();
        var yAxis = cylinder.YAxis.ToVector();

        var uv = new List<UvPoint>(samples.Count + 1);
        double? previous = null;
        var revolutions = 0d;
        foreach (var sample in samples)
        {
            var offset = sample - cylinder.Origin;
            var axial = offset.Dot(axis);
            var radial = offset - (axis * axial);
            var angle = NormalizeToZeroTwoPi(double.Atan2(radial.Dot(yAxis), radial.Dot(xAxis)));
            if (previous.HasValue)
            {
                var delta = angle - previous.Value;
                if (delta > double.Pi)
                {
                    revolutions -= 2d * double.Pi;
                }
                else if (delta < -double.Pi)
                {
                    revolutions += 2d * double.Pi;
                }
            }

            uv.Add(new UvPoint(angle + revolutions, axial));
            previous = angle;
        }

        if (uv.Count > 0 && (uv[0] - uv[^1]).Length > ContainmentEps)
        {
            uv.Add(uv[0]);
        }

        return uv;
    }

    private static List<UvPoint> ProjectLoopToTorus(IReadOnlyList<Point3D> samples, TorusSurface torus)
    {
        var axis = torus.Axis.ToVector();
        var xAxis = torus.XAxis.ToVector();
        var yAxis = torus.YAxis.ToVector();

        var uv = new List<UvPoint>(samples.Count + 1);
        double? previousU = null;
        var uRevolutions = 0d;
        double? previousV = null;
        var vRevolutions = 0d;
        foreach (var sample in samples)
        {
            var offset = sample - torus.Center;
            var axial = offset.Dot(axis);
            var inPlane = offset - (axis * axial);
            var radial = inPlane.Length;

            var u = NormalizeToZeroTwoPi(double.Atan2(inPlane.Dot(yAxis), inPlane.Dot(xAxis)));
            if (previousU.HasValue)
            {
                var deltaU = u - previousU.Value;
                if (deltaU > double.Pi)
                {
                    uRevolutions -= 2d * double.Pi;
                }
                else if (deltaU < -double.Pi)
                {
                    uRevolutions += 2d * double.Pi;
                }
            }

            var v = NormalizeToZeroTwoPi(double.Atan2(axial, radial - torus.MajorRadius));
            if (previousV.HasValue)
            {
                var deltaV = v - previousV.Value;
                if (deltaV > double.Pi)
                {
                    vRevolutions -= 2d * double.Pi;
                }
                else if (deltaV < -double.Pi)
                {
                    vRevolutions += 2d * double.Pi;
                }
            }

            uv.Add(new UvPoint(u + uRevolutions, v + vRevolutions));
            previousU = u;
            previousV = v;
        }

        if (uv.Count > 0 && (uv[0] - uv[^1]).Length > ContainmentEps)
        {
            uv.Add(uv[0]);
        }

        return uv;
    }

    private static List<UvPoint> ProjectLoopToCone(IReadOnlyList<Point3D> samples, ConeSurface cone)
    {
        var axis = cone.Axis.ToVector();
        var reference = cone.ReferenceAxis.ToVector();
        var projectedReference = reference - (axis * reference.Dot(axis));
        var xAxis = Direction3D.Create(projectedReference).ToVector();
        var yAxis = Direction3D.Create(axis.Cross(xAxis)).ToVector();

        var uv = new List<UvPoint>(samples.Count + 1);
        double? previous = null;
        var revolutions = 0d;
        foreach (var sample in samples)
        {
            var offset = sample - cone.Apex;
            var axial = offset.Dot(axis);
            var radial = offset - (axis * axial);
            var angle = NormalizeToZeroTwoPi(double.Atan2(radial.Dot(yAxis), radial.Dot(xAxis)));
            if (previous.HasValue)
            {
                var delta = angle - previous.Value;
                if (delta > double.Pi)
                {
                    revolutions -= 2d * double.Pi;
                }
                else if (delta < -double.Pi)
                {
                    revolutions += 2d * double.Pi;
                }
            }

            uv.Add(new UvPoint(angle + revolutions, axial));
            previous = angle;
        }

        if (uv.Count > 0 && (uv[0] - uv[^1]).Length > ContainmentEps)
        {
            uv.Add(uv[0]);
        }

        return uv;
    }

    private static (IReadOnlyList<UvPoint> Projected, int UniquePointCount, double SignedArea, TorusProjectionAnalysis Analysis)? TryRecoverToroidalProjectionFromCyclicUnwrap(
        IReadOnlyList<Point3D> samples,
        TorusSurface torus)
    {
        if (samples.Count < 4)
        {
            return null;
        }

        var best = default((IReadOnlyList<UvPoint> Projected, int UniquePointCount, double SignedArea, TorusProjectionAnalysis Analysis)?);
        var bestScore = double.NegativeInfinity;
        var bodyCount = samples.Count - 1;
        var loopBody = samples.Take(bodyCount).ToArray();

        for (var start = 0; start < bodyCount; start++)
        {
            var rotated = new Point3D[bodyCount];
            for (var i = 0; i < bodyCount; i++)
            {
                rotated[i] = loopBody[(i + start) % bodyCount];
            }

            var projected = DeduplicateToroidalSeamSamples(SimplifyClosedPolygon(ProjectLoopToTorus(rotated, torus), ContainmentEps));
            var unique = CountUniquePoints(projected, ContainmentEps);
            var area = ComputeSignedArea(projected);
            var analysis = AnalyzeToroidalProjection(unique, area, projected);

            var score = unique * 1_000_000d + double.Abs(area);
            if (analysis.Degeneracy == TorusProjectionDegeneracy.None && score > bestScore)
            {
                best = (projected, unique, area, analysis);
                bestScore = score;
            }
        }

        return best;
    }

    private static List<UvPoint> DeduplicateToroidalSeamSamples(IReadOnlyList<UvPoint> projected)
    {
        if (projected.Count <= 1)
        {
            return projected.ToList();
        }

        var deduplicated = new List<UvPoint>(projected.Count);
        deduplicated.Add(projected[0]);

        for (var i = 1; i < projected.Count; i++)
        {
            var current = projected[i];
            var previous = deduplicated[^1];
            if (IsRepeatedToroidalSeamSample(previous, current))
            {
                continue;
            }

            deduplicated.Add(current);
        }

        if (deduplicated.Count > 1 && IsRepeatedToroidalSeamSample(deduplicated[^1], deduplicated[0]))
        {
            deduplicated.RemoveAt(deduplicated.Count - 1);
        }

        if (deduplicated.Count > 0 && (deduplicated[0] - deduplicated[^1]).Length > ContainmentEps)
        {
            deduplicated.Add(deduplicated[0]);
        }

        return deduplicated;
    }

    private static bool IsRepeatedToroidalSeamSample(UvPoint a, UvPoint b)
    {
        if ((a - b).Length <= ContainmentEps)
        {
            return true;
        }

        var aU = NormalizeToZeroTwoPi(a.X);
        var bU = NormalizeToZeroTwoPi(b.X);
        var aV = NormalizeToZeroTwoPi(a.Y);
        var bV = NormalizeToZeroTwoPi(b.Y);
        var nearMajorSeam = (aU <= 1e-6d || aU >= (2d * double.Pi) - 1e-6d) && (bU <= 1e-6d || bU >= (2d * double.Pi) - 1e-6d);
        if (nearMajorSeam && double.Abs(a.Y - b.Y) <= ContainmentEps)
        {
            return true;
        }

        var nearMinorSeam = (aV <= 1e-6d || aV >= (2d * double.Pi) - 1e-6d) && (bV <= 1e-6d || bV >= (2d * double.Pi) - 1e-6d);
        return nearMinorSeam && double.Abs(a.X - b.X) <= ContainmentEps;
    }

    private static CylinderProjectionAnalysis AnalyzeCylindricalProjection(int uniqueCount, double signedArea, IReadOnlyList<UvPoint> projected)
    {
        if (projected.Count == 0)
        {
            return new CylinderProjectionAnalysis(0d, 0d, 0, 0, CylinderProjectionDegeneracy.InsufficientUniquePoints);
        }

        var minX = projected.Min(p => p.X);
        var maxX = projected.Max(p => p.X);
        var minY = projected.Min(p => p.Y);
        var maxY = projected.Max(p => p.Y);
        var angularSpan = maxX - minX;
        var axialSpan = maxY - minY;
        var seamCrossings = 0;
        var repeatedSeamPointCount = 0;
        for (var i = 1; i < projected.Count; i++)
        {
            var delta = projected[i].X - projected[i - 1].X;
            if (double.Abs(delta) > double.Pi)
            {
                seamCrossings++;
            }

            var a = NormalizeToZeroTwoPi(projected[i - 1].X);
            var b = NormalizeToZeroTwoPi(projected[i].X);
            var nearSeam = (a <= 1e-6d || a >= (2d * double.Pi) - 1e-6d) && (b <= 1e-6d || b >= (2d * double.Pi) - 1e-6d);
            if (nearSeam && double.Abs(projected[i].Y - projected[i - 1].Y) <= ContainmentEps)
            {
                repeatedSeamPointCount++;
            }
        }

        var degeneracy = CylinderProjectionDegeneracy.None;
        if (uniqueCount < 3)
        {
            degeneracy = CylinderProjectionDegeneracy.InsufficientUniquePoints;
        }
        else if (double.Abs(signedArea) <= AreaEps)
        {
            if (axialSpan <= ContainmentEps && angularSpan >= (2d * double.Pi) - 1e-3d)
            {
                degeneracy = CylinderProjectionDegeneracy.FullRevolutionConstantAxial;
            }
            else if (repeatedSeamPointCount > 0)
            {
                degeneracy = CylinderProjectionDegeneracy.RepeatedSeamProjectionCollapse;
            }
            else if (angularSpan <= 1e-6d)
            {
                degeneracy = CylinderProjectionDegeneracy.DegenerateAngularSpan;
            }
            else if (axialSpan <= ContainmentEps)
            {
                degeneracy = CylinderProjectionDegeneracy.DegenerateAxialSpan;
            }
            else
            {
                degeneracy = CylinderProjectionDegeneracy.NearZeroArea;
            }
        }

        return new CylinderProjectionAnalysis(angularSpan, axialSpan, seamCrossings, repeatedSeamPointCount, degeneracy);
    }

    private static bool IsToroidalFullMajorRevolutionConstantMinor(TorusProjectionAnalysis analysis)
    {
        var almostFullMajorTurn = analysis.MajorSpan >= (2d * double.Pi) - 1e-3d
            || analysis.MajorSeamCrossings > 0;
        var nearConstantMinor = analysis.MinorSpan <= 1e-6d;
        var supportedDegeneracy = analysis.Degeneracy == TorusProjectionDegeneracy.RepeatedSeamProjectionCollapse
            || analysis.Degeneracy == TorusProjectionDegeneracy.DegenerateMinorSpan;

        // Major-ring/band boundaries are non-contractible loops that can project as near-zero-area
        // traces in toroidal UV despite being valid topological boundaries on the surface.
        return almostFullMajorTurn
            && nearConstantMinor
            && supportedDegeneracy;
    }

    private static TorusProjectionAnalysis AnalyzeToroidalProjection(int uniqueCount, double signedArea, IReadOnlyList<UvPoint> projected)
    {
        if (projected.Count == 0)
        {
            return new TorusProjectionAnalysis(0d, 0d, 0, 0, 0, 0, TorusProjectionDegeneracy.InsufficientUniquePoints);
        }

        var minU = projected.Min(p => p.X);
        var maxU = projected.Max(p => p.X);
        var minV = projected.Min(p => p.Y);
        var maxV = projected.Max(p => p.Y);
        var majorSpan = maxU - minU;
        var minorSpan = maxV - minV;
        var majorSeamCrossings = 0;
        var minorSeamCrossings = 0;
        var repeatedMajorSeamPointCount = 0;
        var repeatedMinorSeamPointCount = 0;

        for (var i = 1; i < projected.Count; i++)
        {
            var deltaU = projected[i].X - projected[i - 1].X;
            var deltaV = projected[i].Y - projected[i - 1].Y;
            if (double.Abs(deltaU) > double.Pi)
            {
                majorSeamCrossings++;
            }

            if (double.Abs(deltaV) > double.Pi)
            {
                minorSeamCrossings++;
            }

            var aU = NormalizeToZeroTwoPi(projected[i - 1].X);
            var bU = NormalizeToZeroTwoPi(projected[i].X);
            var nearMajorSeam = (aU <= 1e-6d || aU >= (2d * double.Pi) - 1e-6d) && (bU <= 1e-6d || bU >= (2d * double.Pi) - 1e-6d);
            if (nearMajorSeam && double.Abs(projected[i].Y - projected[i - 1].Y) <= ContainmentEps)
            {
                repeatedMajorSeamPointCount++;
            }

            var aV = NormalizeToZeroTwoPi(projected[i - 1].Y);
            var bV = NormalizeToZeroTwoPi(projected[i].Y);
            var nearMinorSeam = (aV <= 1e-6d || aV >= (2d * double.Pi) - 1e-6d) && (bV <= 1e-6d || bV >= (2d * double.Pi) - 1e-6d);
            if (nearMinorSeam && double.Abs(projected[i].X - projected[i - 1].X) <= ContainmentEps)
            {
                repeatedMinorSeamPointCount++;
            }
        }

        var degeneracy = TorusProjectionDegeneracy.None;
        if (uniqueCount < 3)
        {
            degeneracy = TorusProjectionDegeneracy.InsufficientUniquePoints;
        }
        else if (double.Abs(signedArea) <= AreaEps)
        {
            if ((repeatedMajorSeamPointCount + repeatedMinorSeamPointCount) > 0)
            {
                degeneracy = TorusProjectionDegeneracy.RepeatedSeamProjectionCollapse;
            }
            else if (majorSpan <= 1e-6d)
            {
                degeneracy = TorusProjectionDegeneracy.DegenerateMajorSpan;
            }
            else if (minorSpan <= 1e-6d)
            {
                degeneracy = TorusProjectionDegeneracy.DegenerateMinorSpan;
            }
            else if (majorSeamCrossings > 0 && minorSeamCrossings == 0)
            {
                degeneracy = TorusProjectionDegeneracy.MajorPeriodSeamUnwrapFailure;
            }
            else if (minorSeamCrossings > 0 && majorSeamCrossings == 0)
            {
                degeneracy = TorusProjectionDegeneracy.MinorPeriodSeamUnwrapFailure;
            }
            else
            {
                degeneracy = TorusProjectionDegeneracy.NearZeroArea;
            }
        }

        return new TorusProjectionAnalysis(
            majorSpan,
            minorSpan,
            majorSeamCrossings,
            minorSeamCrossings,
            repeatedMajorSeamPointCount,
            repeatedMinorSeamPointCount,
            degeneracy);
    }

    private static ConeProjectionAnalysis AnalyzeConicalProjection(int uniqueCount, double signedArea, IReadOnlyList<UvPoint> projected)
    {
        if (projected.Count == 0)
        {
            return new ConeProjectionAnalysis(0d, 0d, 0, 0, 0d, ConeProjectionDegeneracy.InsufficientUniquePoints);
        }

        var minU = projected.Min(p => p.X);
        var maxU = projected.Max(p => p.X);
        var minV = projected.Min(p => p.Y);
        var maxV = projected.Max(p => p.Y);
        var angularSpan = maxU - minU;
        var axialSpan = maxV - minV;
        var seamCrossings = 0;
        var repeatedSeamPointCount = 0;
        var axialMean = projected.Average(p => p.Y);

        for (var i = 1; i < projected.Count; i++)
        {
            var delta = projected[i].X - projected[i - 1].X;
            if (double.Abs(delta) > double.Pi)
            {
                seamCrossings++;
            }

            var a = NormalizeToZeroTwoPi(projected[i - 1].X);
            var b = NormalizeToZeroTwoPi(projected[i].X);
            var nearSeam = (a <= 1e-6d || a >= (2d * double.Pi) - 1e-6d) && (b <= 1e-6d || b >= (2d * double.Pi) - 1e-6d);
            if (nearSeam && double.Abs(projected[i].Y - projected[i - 1].Y) <= ContainmentEps)
            {
                repeatedSeamPointCount++;
            }
        }

        var degeneracy = ConeProjectionDegeneracy.None;
        if (uniqueCount < 3)
        {
            degeneracy = ConeProjectionDegeneracy.InsufficientUniquePoints;
        }
        else if (double.Abs(signedArea) <= AreaEps)
        {
            if (axialSpan <= ContainmentEps && angularSpan >= (2d * double.Pi) - 1e-3d)
            {
                degeneracy = ConeProjectionDegeneracy.FullRevolutionConstantAxial;
            }
            else if (repeatedSeamPointCount > 0)
            {
                degeneracy = ConeProjectionDegeneracy.RepeatedSeamProjectionCollapse;
            }
            else if (angularSpan <= 1e-6d)
            {
                degeneracy = ConeProjectionDegeneracy.DegenerateAngularSpan;
            }
            else if (axialSpan <= ContainmentEps)
            {
                degeneracy = ConeProjectionDegeneracy.DegenerateAxialSpan;
            }
            else
            {
                degeneracy = ConeProjectionDegeneracy.NearZeroArea;
            }
        }

        return new ConeProjectionAnalysis(angularSpan, axialSpan, seamCrossings, repeatedSeamPointCount, axialMean, degeneracy);
    }

    private static IReadOnlyList<UvPoint> AlignPolygonToReference(IReadOnlyList<UvPoint> polygon, double currentX, double referenceX)
    {
        var twoPi = 2d * double.Pi;
        var delta = referenceX - currentX;
        var turns = double.Round(delta / twoPi);
        var shift = turns * twoPi;
        if (double.Abs(shift) <= AngleUnwrapEps)
        {
            return polygon;
        }

        return polygon.Select(p => new UvPoint(p.X + shift, p.Y)).ToArray();
    }

    private static IReadOnlyList<UvPoint> AlignPolygonToReferenceY(IReadOnlyList<UvPoint> polygon, double currentY, double referenceY)
    {
        var twoPi = 2d * double.Pi;
        var delta = referenceY - currentY;
        var turns = double.Round(delta / twoPi);
        var shift = turns * twoPi;
        if (double.Abs(shift) <= AngleUnwrapEps)
        {
            return polygon;
        }

        return polygon.Select(p => new UvPoint(p.X, p.Y + shift)).ToArray();
    }

    private static double NormalizeToZeroTwoPi(double angle)
    {
        var twoPi = 2d * double.Pi;
        var normalized = angle % twoPi;
        if (normalized < 0d)
        {
            normalized += twoPi;
        }

        return normalized;
    }

    private static double ComputeSignedArea(IReadOnlyList<UvPoint> polygon)
    {
        var area = 0d;
        for (var i = 0; i < polygon.Count - 1; i++)
        {
            var p0 = polygon[i];
            var p1 = polygon[i + 1];
            area += (p0.X * p1.Y) - (p1.X * p0.Y);
        }

        return 0.5d * area;
    }

    private static double ComputeContainmentTolerance(IEnumerable<UvPoint> points)
    {
        var array = points.ToArray();
        if (array.Length == 0)
        {
            return ContainmentEps;
        }

        var minX = array.Min(p => p.X);
        var maxX = array.Max(p => p.X);
        var minY = array.Min(p => p.Y);
        var maxY = array.Max(p => p.Y);
        var diagonal = double.Sqrt(((maxX - minX) * (maxX - minX)) + ((maxY - minY) * (maxY - minY)));
        return System.Math.Max(ContainmentEps, diagonal * 1e-6d);
    }

    private static double ComputeAreaTolerance(IEnumerable<UvPoint> points)
    {
        var array = points.ToArray();
        if (array.Length == 0)
        {
            return AreaEps;
        }

        var minX = array.Min(p => p.X);
        var maxX = array.Max(p => p.X);
        var minY = array.Min(p => p.Y);
        var maxY = array.Max(p => p.Y);
        var extentArea = (maxX - minX) * (maxY - minY);
        return System.Math.Max(AreaEps, extentArea * 1e-8d);
    }

    private static void AppendLoopSamples(IList<Point3D> loopSamples, IReadOnlyList<Point3D> edgeSamples)
    {
        _ = AppendLoopSamples(loopSamples, edgeSamples, null, -1, -1, -1, null);
    }

    private static LoopCoedgeGapClassification AppendLoopSamples(
        IList<Point3D> loopSamples,
        IReadOnlyList<Point3D> edgeSamples,
        SurfaceGeometry? surface,
        int faceEntityId,
        int loopId,
        int coedgeId,
        int? previousCoedgeId)
    {
        var normalizedEdgeSamples = edgeSamples.ToList();
        var boundaryClassification = LoopCoedgeGapClassification.Negligible;
        if (loopSamples.Count > 0 && normalizedEdgeSamples.Count > 0)
        {
            var previousPoint = loopSamples[^1];
            var currentPoint = normalizedEdgeSamples[0];
            var gap = ClassifyCoedgeGap(previousPoint, currentPoint, surface);
            boundaryClassification = gap.Classification;
            ReportCoedgeGapDiagnostic(new LoopRoleCoedgeGapDiagnostic(
                FaceEntityId: faceEntityId,
                LoopId: loopId,
                PreviousCoedgeId: previousCoedgeId,
                NextCoedgeId: coedgeId,
                Gap3d: gap.Gap3d,
                Gap2d: gap.Gap2d,
                PreviousEdgeGeometryKind: null,
                NextEdgeGeometryKind: null,
                Classification: gap.Classification,
                WithinPointOnSurfaceEps: gap.Gap3d <= PointOnSurfaceEps,
                WithinContainmentEps: gap.Gap3d <= ContainmentEps,
                WouldCreateGhostSegmentWithoutNormalization: gap.Gap3d > PointOnSurfaceEps));

            if (gap.Classification == LoopCoedgeGapClassification.TinyNormalizable)
            {
                normalizedEdgeSamples[0] = previousPoint;
            }
        }

        foreach (var point in normalizedEdgeSamples)
        {
            if (loopSamples.Count > 0)
            {
                var last = loopSamples[^1];
                if ((last - point).Length <= PointOnSurfaceEps)
                {
                    continue;
                }
            }

            loopSamples.Add(point);
        }

        return boundaryClassification;
    }

    private static KernelResult<IReadOnlyList<Point3D>> SampleCoedgePoints(
        EdgeGeometryBinding edgeBinding,
        BrepGeometryStore geometry,
        bool isReversed,
        int faceEntityId,
        int loopId,
        int coedgeId,
        int edgeId)
    {
        if (!geometry.TryGetCurve(edgeBinding.CurveGeometryId, out var curve) || curve is null)
        {
            return LoopRoleFailure<IReadOnlyList<Point3D>>("Missing edge curve geometry for loop sampling.", "Importer.LoopRole.WindingConflict");
        }

        List<Point3D> points;
        switch (curve.Kind)
        {
            case CurveGeometryKind.Line3:
                var line = curve.Line3!.Value;
                var lineRange = edgeBinding.TrimInterval ?? new ParameterInterval(0d, 1d);
                points = [line.Evaluate(lineRange.Start), line.Evaluate(lineRange.End)];
                break;
            case CurveGeometryKind.Circle3:
                var circle = curve.Circle3!.Value;
                var trim = edgeBinding.TrimInterval ?? new ParameterInterval(0d, 2d * double.Pi);
                var span = trim.End - trim.Start;
                var isFullCircle = double.Abs(span - (2d * double.Pi)) <= AngleUnwrapEps;
                var legacyPointCount = isFullCircle ? 5 : 3;
                var segmentCount = ComputeAdaptiveCircleSegmentCount(span);
                points = SampleCircularTrim(circle, trim, segmentCount);
                ReportCircularSamplingDiagnostic(new LoopRoleCircularSamplingDiagnostic(
                    FaceEntityId: faceEntityId,
                    LoopId: loopId,
                    CoedgeId: coedgeId,
                    EdgeId: edgeId,
                    TrimStart: trim.Start,
                    TrimEnd: trim.End,
                    TrimSpan: span,
                    IsFullCircle: isFullCircle,
                    LegacyPointCount: legacyPointCount,
                    AdaptivePointCount: points.Count));
                break;
            case CurveGeometryKind.Ellipse3:
                var ellipse = curve.Ellipse3!.Value;
                var ellipseTrim = edgeBinding.TrimInterval ?? new ParameterInterval(0d, 2d * double.Pi);
                var ellipseSpan = ellipseTrim.End - ellipseTrim.Start;
                var ellipseSegmentCount = ComputeAdaptiveCircleSegmentCount(ellipseSpan);
                points = SampleEllipticalTrim(ellipse, ellipseTrim, ellipseSegmentCount);
                break;
            case CurveGeometryKind.BSpline3:
                var spline = curve.BSpline3!.Value;
                var splineTrim = edgeBinding.TrimInterval ?? new ParameterInterval(spline.DomainStart, spline.DomainEnd);
                var splineMid = splineTrim.Start + ((splineTrim.End - splineTrim.Start) * 0.5d);
                points = [spline.Evaluate(splineTrim.Start), spline.Evaluate(splineMid), spline.Evaluate(splineTrim.End)];
                break;
            default:
                points = [];
                break;
        }

        if (isReversed)
        {
            points.Reverse();
        }

        return KernelResult<IReadOnlyList<Point3D>>.Success(points);
    }

    private static int ComputeAdaptiveCircleSegmentCount(double span)
    {
        const double maxSegmentAngle = double.Pi / 4d;
        var normalizedSpan = double.Abs(span);
        var segmentCount = (int)double.Ceiling(normalizedSpan / maxSegmentAngle);
        return System.Math.Max(2, segmentCount);
    }

    private static List<Point3D> SampleCircularTrim(Circle3Curve circle, ParameterInterval trim, int segmentCount)
    {
        var points = new List<Point3D>(segmentCount + 1);
        var step = (trim.End - trim.Start) / segmentCount;
        for (var i = 0; i <= segmentCount; i++)
        {
            points.Add(circle.Evaluate(trim.Start + (step * i)));
        }

        return points;
    }

    private static List<Point3D> SampleEllipticalTrim(Ellipse3Curve ellipse, ParameterInterval trim, int segmentCount)
    {
        var points = new List<Point3D>(segmentCount + 1);
        var step = (trim.End - trim.Start) / segmentCount;
        for (var i = 0; i <= segmentCount; i++)
        {
            points.Add(ellipse.Evaluate(trim.Start + (step * i)));
        }

        return points;
    }

    private static void ReportCircularSamplingDiagnostic(LoopRoleCircularSamplingDiagnostic diagnostic)
    {
        var sink = CircularSamplingDiagnosticsSink.Value;
        sink?.Add(diagnostic);
    }

    private static void ReportCoedgeGapDiagnostic(LoopRoleCoedgeGapDiagnostic diagnostic)
    {
        var sink = CoedgeGapDiagnosticsSink.Value;
        sink?.Add(diagnostic);
    }

    private static void ReportCylinderProjectionDiagnostic(LoopRoleCylinderProjectionDiagnostic diagnostic)
    {
        var sink = CylinderProjectionDiagnosticsSink.Value;
        sink?.Add(diagnostic);
    }

    private static void ReportTorusProjectionDiagnostic(LoopRoleTorusProjectionDiagnostic diagnostic)
    {
        var sink = TorusProjectionDiagnosticsSink.Value;
        sink?.Add(diagnostic);
    }

    private static void ReportPlanarMultiBoundJudgmentDiagnostic(PlanarMultiBoundJudgmentDiagnostic diagnostic)
    {
        var sink = PlanarMultiBoundJudgmentDiagnosticsSink.Value;
        sink?.Add(diagnostic);
    }

    private static (double Gap3d, double Gap2d, LoopCoedgeGapClassification Classification) ClassifyCoedgeGap(
        Point3D previousEnd,
        Point3D nextStart,
        SurfaceGeometry? surface)
    {
        var gap3d = (previousEnd - nextStart).Length;
        var gap2d = gap3d;
        if (surface is not null)
        {
            gap2d = surface.Kind switch
            {
                SurfaceGeometryKind.Plane => ComputePlanarGap(previousEnd, nextStart, surface.Plane!.Value),
                _ => gap3d
            };
        }

        var classification = gap3d <= PointOnSurfaceEps
            ? LoopCoedgeGapClassification.Negligible
            : gap3d <= TinyCoedgeGapSnapEps
                ? LoopCoedgeGapClassification.TinyNormalizable
                : LoopCoedgeGapClassification.Disconnected;
        return (gap3d, gap2d, classification);
    }

    private static double ComputePlanarGap(Point3D a, Point3D b, PlaneSurface plane)
    {
        var delta = b - a;
        var du = delta.Dot(plane.UAxis.ToVector());
        var dv = delta.Dot(plane.VAxis.ToVector());
        return double.Sqrt((du * du) + (dv * dv));
    }

    private static string? DescribeCurveKind(EdgeGeometryBinding edgeBinding, BrepGeometryStore geometry)
    {
        if (!geometry.TryGetCurve(edgeBinding.CurveGeometryId, out var curve) || curve is null)
        {
            return null;
        }

        return curve.Kind.ToString();
    }

    public sealed record LoopRoleCircularSamplingDiagnostic(
        int FaceEntityId,
        int LoopId,
        int CoedgeId,
        int EdgeId,
        double TrimStart,
        double TrimEnd,
        double TrimSpan,
        bool IsFullCircle,
        int LegacyPointCount,
        int AdaptivePointCount);

    public sealed record LoopRoleCoedgeGapDiagnostic(
        int FaceEntityId,
        int LoopId,
        int? PreviousCoedgeId,
        int? NextCoedgeId,
        double Gap3d,
        double Gap2d,
        string? PreviousEdgeGeometryKind,
        string? NextEdgeGeometryKind,
        LoopCoedgeGapClassification Classification,
        bool WithinPointOnSurfaceEps,
        bool WithinContainmentEps,
        bool WouldCreateGhostSegmentWithoutNormalization);

    public enum LoopCoedgeGapClassification
    {
        Negligible,
        TinyNormalizable,
        Disconnected
    }

    public sealed record LoopRoleCylinderProjectionDiagnostic(
        int FaceEntityId,
        int LoopId,
        int PointCount,
        int UniquePointCount,
        double SignedArea,
        double AngularSpan,
        double AxialSpan,
        int SeamCrossings,
        int RepeatedSeamPointCount,
        string Degeneracy);

    public sealed record LoopRoleTorusProjectionDiagnostic(
        int FaceEntityId,
        int LoopId,
        int PointCount,
        int UniquePointCount,
        double SignedArea,
        double MajorSpan,
        double MinorSpan,
        int MajorSeamCrossings,
        int MinorSeamCrossings,
        int RepeatedMajorSeamPointCount,
        int RepeatedMinorSeamPointCount,
        string Degeneracy,
        string InitialDegeneracy,
        double InitialSignedArea,
        bool FullMajorSpanNearConstantMinorCandidate);

    public sealed record PlanarMultiBoundJudgmentDiagnostic(
        int FaceLoopCount,
        int OuterLoopId,
        int InnerLoopId,
        double OuterArea,
        double InnerArea,
        double AreaRatio,
        int ContainedVertexCount,
        int VertexCount,
        int IntersectionCount,
        double MinDistanceToOuter,
        bool HasDisconnectedCoedgeGap,
        bool HasSingleDeclaredOuterLoop,
        string SelectedCandidate,
        string CandidateRejections);

    private enum CylinderProjectionDegeneracy
    {
        None = 0,
        InsufficientUniquePoints = 1,
        NearZeroArea = 2,
        FullRevolutionConstantAxial = 3,
        DegenerateAngularSpan = 4,
        DegenerateAxialSpan = 5,
        RepeatedSeamProjectionCollapse = 6
    }

    private sealed record CylinderProjectionAnalysis(
        double AngularSpan,
        double AxialSpan,
        int SeamCrossings,
        int RepeatedSeamPointCount,
        CylinderProjectionDegeneracy Degeneracy);

    private enum TorusProjectionDegeneracy
    {
        None = 0,
        InsufficientUniquePoints = 1,
        NearZeroArea = 2,
        DegenerateMajorSpan = 3,
        DegenerateMinorSpan = 4,
        RepeatedSeamProjectionCollapse = 5,
        MajorPeriodSeamUnwrapFailure = 6,
        MinorPeriodSeamUnwrapFailure = 7
    }

    private sealed record TorusProjectionAnalysis(
        double MajorSpan,
        double MinorSpan,
        int MajorSeamCrossings,
        int MinorSeamCrossings,
        int RepeatedMajorSeamPointCount,
        int RepeatedMinorSeamPointCount,
        TorusProjectionDegeneracy Degeneracy);

    private enum ConeProjectionDegeneracy
    {
        None = 0,
        InsufficientUniquePoints = 1,
        NearZeroArea = 2,
        FullRevolutionConstantAxial = 3,
        DegenerateAngularSpan = 4,
        DegenerateAxialSpan = 5,
        RepeatedSeamProjectionCollapse = 6
    }

    private sealed record ConeProjectionAnalysis(
        double AngularSpan,
        double AxialSpan,
        int SeamCrossings,
        int RepeatedSeamPointCount,
        double AxialMean,
        ConeProjectionDegeneracy Degeneracy);

    private sealed class CircularSamplingDiagnosticsScope(ICollection<LoopRoleCircularSamplingDiagnostic>? previous) : IDisposable
    {
        public void Dispose()
        {
            CircularSamplingDiagnosticsSink.Value = previous;
        }
    }

    private sealed class CoedgeGapDiagnosticsScope(ICollection<LoopRoleCoedgeGapDiagnostic>? previous) : IDisposable
    {
        public void Dispose()
        {
            CoedgeGapDiagnosticsSink.Value = previous;
        }
    }

    private sealed class CylinderProjectionDiagnosticsScope(ICollection<LoopRoleCylinderProjectionDiagnostic>? previous) : IDisposable
    {
        public void Dispose()
        {
            CylinderProjectionDiagnosticsSink.Value = previous;
        }
    }

    private sealed class TorusProjectionDiagnosticsScope(ICollection<LoopRoleTorusProjectionDiagnostic>? previous) : IDisposable
    {
        public void Dispose()
        {
            TorusProjectionDiagnosticsSink.Value = previous;
        }
    }

    private sealed class PlanarMultiBoundJudgmentDiagnosticsScope(ICollection<PlanarMultiBoundJudgmentDiagnostic>? previous) : IDisposable
    {
        public void Dispose()
        {
            PlanarMultiBoundJudgmentDiagnosticsSink.Value = previous;
        }
    }

    private static KernelResult<T> LoopRoleFailure<T>(string message, string source) =>
        KernelResult<T>.Failure([new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, message, source)]);

    private static KernelResult<BrepBody> Failure(string message, string source) =>
        Step242ImportSharedUtilities.NotImplementedFailure<BrepBody>(message, source);

    private static KernelResult<EdgeId> FailureEdge(string message, string source) =>
        Step242ImportSharedUtilities.NotImplementedFailure<EdgeId>(message, source);

    private static KernelResult<(CurveGeometry CurveGeometry, ParameterInterval TrimInterval)> FailureCurveBinding(string message, string source) =>
        Step242ImportSharedUtilities.NotImplementedFailure<(CurveGeometry CurveGeometry, ParameterInterval TrimInterval)>(message, source);

    private static KernelResult<(SurfaceGeometryId SurfaceGeometryId, SurfaceGeometry SurfaceGeometry)> FailureSurfaceBinding(string message, string source) =>
        Step242ImportSharedUtilities.NotImplementedFailure<(SurfaceGeometryId SurfaceGeometryId, SurfaceGeometry SurfaceGeometry)>(message, source);

    private static KernelResult<ParameterInterval> FailureCircleTrim(string message, string source) =>
        Step242ImportSharedUtilities.ValidationFailure<ParameterInterval>(message, source);

    private static KernelResult<double> FailureCircleTrimAngle(string message, string source) =>
        Step242ImportSharedUtilities.ValidationFailure<double>(message, source);

    private static KernelResult<ParameterInterval> FailureEllipseTrim(string message, string? source) =>
        Step242ImportSharedUtilities.ValidationFailure<ParameterInterval>(message, source ?? "Importer.Geometry.EllipseTrim");

    private static KernelResult<double> FailureEllipseTrimAngle(string message, string source) =>
        Step242ImportSharedUtilities.ValidationFailure<double>(message, source);

    private static KernelResult<T> OrientationFailure<T>(string message, string source) =>
        KernelResult<T>.Failure([new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, message, source)]);

    private static string SourceFor(int _entityId, string stableSource) => stableSource;

    private sealed record LoopBuildData(LoopId LoopId, IReadOnlyList<Coedge> Coedges, IReadOnlyList<Point3D> Samples, bool HasDisconnectedCoedgeGap = false, bool IsDeclaredOuter = false);

    private sealed record PlanarLoopInfo(LoopBuildData Loop, IReadOnlyList<UvPoint> ProjectedPoints, double SignedArea);

    private sealed record PlanarLoopInfoWithSample(PlanarLoopInfo Info, UvPoint SamplePoint);

    private sealed record PlanarLoopOuterCandidate(PlanarLoopInfo Info, UvPoint SamplePoint, int ContainmentCount);

    private sealed record CylindricalLoopInfo(LoopBuildData Loop, IReadOnlyList<UvPoint> ProjectedPoints, double SignedArea, UvPoint Centroid);

    private sealed record ContainmentEvaluation(int OutsideCount, int VertexCount, double MinDistanceToOuter);

    private sealed record ContainmentFailure(string Message, string Source);

    private sealed record PlanarCrossingInsideRecoveryContext(
        int InnerLoopId,
        int OuterLoopId,
        int LoopCount,
        double InnerArea,
        double OuterArea,
        double AreaRatio,
        int IntersectionCount,
        int ContainedVertexCount,
        int VertexCount,
        double MinDistanceToOuter,
        bool HasDisconnectedCoedgeGap,
        bool HasSingleDeclaredOuterLoop,
        bool IsNearBoundaryContact);

    private readonly record struct UvPoint(double X, double Y)
    {
        public static UvPoint operator +(UvPoint a, UvPoint b) => new(a.X + b.X, a.Y + b.Y);

        public static UvPoint operator -(UvPoint a, UvPoint b) => new(a.X - b.X, a.Y - b.Y);

        public static UvPoint operator *(UvPoint a, double s) => new(a.X * s, a.Y * s);

        public double Dot(UvPoint other) => (X * other.X) + (Y * other.Y);

        public double Length => double.Sqrt((X * X) + (Y * Y));
    }
}
