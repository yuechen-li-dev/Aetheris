using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Step242;

/// <summary>
/// Import-only patch. Its orientation is deliberately absent until the shell
/// graph and material-side solve have completed.
/// </summary>
internal sealed record ImportedFacePatch(
    FaceId FaceId,
    SurfaceGeometryId SurfaceGeometryId,
    SourceFaceOrientationEvidence SourceEvidence);

/// <summary>
/// Resolves STEP face orientation once from shell topology and geometric signed
/// volume. Source same_sense participates only in the final cross-check.
/// </summary>
internal static class StepFaceOrientationResolver
{
    private const double VolumeTolerance = 1e-10d;

    public static KernelResult<BrepBody> Resolve(
        BrepBody unresolvedBody,
        IReadOnlyList<ImportedFacePatch> importedFaces,
        IReadOnlyDictionary<FaceId, bool>? supportAlignedBoundary = null,
        IReadOnlyDictionary<LoopId, bool>? supportAlignedLoopBoundary = null)
    {
        ArgumentNullException.ThrowIfNull(unresolvedBody);
        ArgumentNullException.ThrowIfNull(importedFaces);
        var sourceEvidence = importedFaces.ToDictionary(face => face.FaceId, face => face.SourceEvidence);

        var diagnostics = new List<KernelDiagnostic>();
        var localOrientation = new Dictionary<FaceId, bool>();
        var faceToShell = new Dictionary<FaceId, ShellId>();
        var componentByFace = new Dictionary<FaceId, int>();
        var components = new List<ComponentState>();
        var shellStates = new List<ShellState>();
        var nextComponent = 0;

        foreach (var shell in unresolvedBody.Topology.Shells.OrderBy(shell => shell.Id.Value))
        {
            var faces = shell.FaceIds.OrderBy(face => face.Value).ToArray();
            foreach (var face in faces) faceToShell[face] = shell.Id;
            var edgeUses = CollectEdgeUses(unresolvedBody, faces);
            var nonManifold = edgeUses.Where(pair => pair.Value.Count > 2).OrderBy(pair => pair.Key.Value).ToArray();
            if (nonManifold.Length > 0)
            {
                return KernelResult<BrepBody>.Failure([
                    new KernelDiagnostic(
                        KernelDiagnosticCode.ValidationFailed,
                        KernelDiagnosticSeverity.Error,
                        $"step-orientation-nonmanifold: shell {shell.Id.Value}; edges {string.Join(",", nonManifold.Select(pair => $"{pair.Key.Value}({pair.Value.Count})"))}; canonical orientation was not guessed.",
                        "Importer.StepOrientation.NonManifoldOrientation")
                ]);
            }

            var graph = faces.ToDictionary(face => face, _ => new List<Relation>());
            foreach (var uses in edgeUses.Values.Where(uses => uses.Count == 2))
            {
                var left = uses[0];
                var right = uses[1];
                if (left.FaceId == right.FaceId) continue; // periodic seam on one face
                // Relate each raw coedge traversal to its support-space boundary
                // winding. This separates geometric face orientation from both
                // EDGE_CURVE parameter sense and source ADVANCED_FACE evidence.
                var leftBaseline = supportAlignedLoopBoundary?.GetValueOrDefault(
                    left.LoopId,
                    supportAlignedBoundary?.GetValueOrDefault(left.FaceId) ?? true) ?? true;
                var rightBaseline = supportAlignedLoopBoundary?.GetValueOrDefault(
                    right.LoopId,
                    supportAlignedBoundary?.GetValueOrDefault(right.FaceId) ?? true) ?? true;
                var mustDiffer = leftBaseline
                    ^ rightBaseline
                    ^ (left.IsReversed == right.IsReversed);
                var relationEvidence = $"edge={left.EdgeId.Value}; loops={left.LoopId.Value}/{right.LoopId.Value}; loop-baselines={leftBaseline}/{rightBaseline}; coedge-reversed={left.IsReversed}/{right.IsReversed}";
                graph[left.FaceId].Add(new Relation(right.FaceId, mustDiffer, relationEvidence));
                graph[right.FaceId].Add(new Relation(left.FaceId, mustDiffer, relationEvidence));
            }

            foreach (var seed in faces)
            {
                if (localOrientation.ContainsKey(seed)) continue;
                var componentId = nextComponent++;
                var componentFaces = new List<FaceId>();
                var queue = new Queue<FaceId>();
                localOrientation[seed] = supportAlignedBoundary?.GetValueOrDefault(seed) ?? true;
                queue.Enqueue(seed);
                var propagationConflict = false;
                (FaceId Left, FaceId Right)? firstConflict = null;
                string? firstConflictEvidence = null;
                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    componentFaces.Add(current);
                    componentByFace[current] = componentId;
                    foreach (var relation in graph[current].OrderBy(relation => relation.FaceId.Value))
                    {
                        var required = relation.MustDiffer ? !localOrientation[current] : localOrientation[current];
                        if (localOrientation.TryGetValue(relation.FaceId, out var existing))
                        {
                            if (existing != required)
                            {
                                propagationConflict = true;
                                firstConflict ??= (current, relation.FaceId);
                                firstConflictEvidence ??= relation.Evidence;
                            }
                            continue;
                        }
                        localOrientation[relation.FaceId] = required;
                        queue.Enqueue(relation.FaceId);
                    }
                }

                if (propagationConflict)
                {
                    // Some admitted STEP files reuse topological edges with an
                    // ORIENTED_EDGE cycle that cannot be made globally
                    // consistent.  The independently derived support-space
                    // boundary normal is stronger geometric evidence than an
                    // arbitrary graph seed, so retain it and make the degraded
                    // adjacency evidence explicit.
                    foreach (var faceId in componentFaces)
                    {
                        localOrientation[faceId] = supportAlignedBoundary?.GetValueOrDefault(faceId) ?? true;
                    }
                    diagnostics.Add(new KernelDiagnostic(
                        KernelDiagnosticCode.ValidationFailed,
                        KernelDiagnosticSeverity.Warning,
                        $"step-orientation-propagation-conflict: shell {shell.Id.Value}; component seed {seed.Value}; faces {firstConflict?.Left.Value}/{firstConflict?.Right.Value}; {firstConflictEvidence}; contradictory shared-edge requirements; support-space boundary orientation was used.",
                        "Importer.StepOrientation.OrientationPropagationConflict"));
                }

                components.Add(new ComponentState(componentId, shell.Id, componentFaces.OrderBy(face => face.Value).ToArray(), propagationConflict));
            }

            var isClosedParametricSurface = edgeUses.Count == 0 && faces.Length > 0
                && faces.All(faceId => unresolvedBody.Topology.GetFace(faceId).LoopIds.All(loopId =>
                        unresolvedBody.Topology.GetLoop(loopId).Kind == LoopKind.Vertex)
                    && unresolvedBody.TryGetFaceSurfaceGeometry(faceId, out var surface)
                    && surface?.Kind == Aetheris.Kernel.Core.Geometry.SurfaceGeometryKind.Sphere);
            var isClosed = (edgeUses.Count > 0 && edgeUses.All(pair => pair.Value.Count == 2)) || isClosedParametricSurface;
            var isInnerVoid = unresolvedBody.ShellRepresentation?.InnerShellIds.Contains(shell.Id) == true;
            shellStates.Add(new ShellState(shell.Id, isClosed, isInnerVoid));
            if (!isClosed)
            {
                diagnostics.Add(new KernelDiagnostic(
                    KernelDiagnosticCode.ValidationFailed,
                    KernelDiagnosticSeverity.Warning,
                    $"step-orientation-global-unknown: shell {shell.Id.Value} is open; local adjacency was resolved but no outward/material-side claim was made.",
                    "Importer.StepOrientation.GlobalOrientationUnknown"));
            }
        }

        var localCandidate = CloneWithOrientations(unresolvedBody, localOrientation, faceOrientationReport: null);
        // Only the sign of each closed component's volume is used. A coarse mesh decides it when the
        // volume dwarfs what its chord error can move (the chord times the area, four times over);
        // anything closer is measured on the display mesh, as before.
        var decidingComponents = components
            .Where(component => !component.HasPropagationConflict && shellStates.Single(state => state.ShellId == component.ShellId).IsClosed)
            .Select(component => component.Id)
            .ToHashSet();
        // The coarse chord scales with the part: a volume-to-area ratio is at most about half the smallest extent,
        // so a chord of a fortieth of it can still decide. When that chord is no coarser than the display mesh's,
        // the display mesh is measured directly instead of twice.
        var signOptions = SignOptionsFor(localCandidate);
        var tessellationSucceeded = false;
        var signedVolumeByComponent = new Dictionary<int, double>();
        if (signOptions is not null)
        {
            (tessellationSucceeded, signedVolumeByComponent) = MeasureComponents(localCandidate, componentByFace, signOptions, out var area);
            var volumes = signedVolumeByComponent;
            if (tessellationSucceeded && decidingComponents.Any(component =>
                    !volumes.TryGetValue(component, out var volume)
                    || System.Math.Abs(volume) <= 4d * signOptions.ChordTolerance * area.GetValueOrDefault(component)))
                tessellationSucceeded = false;
        }
        if (!tessellationSucceeded)
            (tessellationSucceeded, signedVolumeByComponent) = MeasureComponents(localCandidate, componentByFace, null, out _);

        var globalFlipByComponent = new Dictionary<int, bool>();
        foreach (var component in components)
        {
            var shell = shellStates.Single(state => state.ShellId == component.ShellId);
            if (!shell.IsClosed || component.HasPropagationConflict)
            {
                globalFlipByComponent[component.Id] = false;
                continue;
            }
            if (!tessellationSucceeded || !signedVolumeByComponent.TryGetValue(component.Id, out var signed) || System.Math.Abs(signed) <= VolumeTolerance)
            {
                diagnostics.Add(new KernelDiagnostic(
                    KernelDiagnosticCode.ValidationFailed,
                    KernelDiagnosticSeverity.Warning,
                    $"step-orientation-shell-ambiguous: shell {shell.ShellId.Value}; component seed {component.Faces[0].Value}; signed volume unavailable or near zero; source evidence was not promoted to authority.",
                    "Importer.StepOrientation.ShellOrientationAmbiguous"));
                globalFlipByComponent[component.Id] = false;
                continue;
            }

            var wantsPositive = !shell.IsInnerVoid;
            globalFlipByComponent[component.Id] = wantsPositive ? signed < 0d : signed > 0d;
        }

        var finalOrientation = localOrientation.ToDictionary(
            pair => pair.Key,
            pair => globalFlipByComponent.GetValueOrDefault(componentByFace[pair.Key]) ? !pair.Value : pair.Value);

        var orientedFaces = new List<OrientedFacePatch>();
        foreach (var face in unresolvedBody.Topology.Faces.OrderBy(face => face.Id.Value))
        {
            var evidence = sourceEvidence.TryGetValue(face.Id, out var found)
                ? found
                : new SourceFaceOrientationEvidence(null, null, null);
            var shellId = faceToShell[face.Id];
            var shell = shellStates.Single(state => state.ShellId == shellId);
            var component = components.Single(state => state.Id == componentByFace[face.Id]);
            var hasVolume = signedVolumeByComponent.TryGetValue(component.Id, out var signed) && System.Math.Abs(signed) > VolumeTolerance;
            var qualification = component.HasPropagationConflict
                ? FaceOrientationQualification.Ambiguous
                : !shell.IsClosed
                ? FaceOrientationQualification.DerivedLocallyConsistentGlobalUnknown
                : hasVolume
                    ? FaceOrientationQualification.DerivedQualified
                    : FaceOrientationQualification.Ambiguous;
            var orientation = new ResolvedFaceOrientation(finalOrientation[face.Id]);
            var comparison = evidence.StepSameSense is null
                ? SourceFaceOrientationComparison.SourceMissing
                : qualification == FaceOrientationQualification.Ambiguous
                    ? SourceFaceOrientationComparison.NotComparable
                    : evidence.StepSameSense.Value == orientation.IsAlignedWithSurface
                        ? SourceFaceOrientationComparison.Agrees
                        : SourceFaceOrientationComparison.Disagrees;
            var summary = shell.IsClosed
                ? $"local adjacency consistent; candidate signed volume={(hasVolume ? signed.ToString("G17") : "unavailable")}; shell role={(shell.IsInnerVoid ? "inner-void" : "outer")}; global flip={globalFlipByComponent.GetValueOrDefault(component.Id)}"
                : "local adjacency consistent; open shell; global orientation unknown";
            summary += $"; support-boundaries={string.Join(',', face.LoopIds.Select(loopId => $"{loopId.Value}:{supportAlignedLoopBoundary?.GetValueOrDefault(loopId)}"))}";
            orientedFaces.Add(new OrientedFacePatch(face.Id, shellId, orientation, qualification, evidence, comparison, summary));

            if (comparison == SourceFaceOrientationComparison.Disagrees)
            {
                diagnostics.Add(new KernelDiagnostic(
                    KernelDiagnosticCode.ValidationFailed,
                    KernelDiagnosticSeverity.Warning,
                    $"step-orientation-source-mismatch: face {face.Id.Value}; STEP face #{evidence.SourceEntityId}; source same_sense={evidence.StepSameSense}; derived aligned={orientation.IsAlignedWithSurface}; shell {shellId.Value}; {summary}; action=derived orientation used.",
                    "Importer.StepOrientation.SourceOrientationMismatch"));
            }
        }

        var shellResolutions = shellStates.Select(shell =>
        {
            var shellComponents = components.Where(component => component.ShellId == shell.ShellId).ToArray();
            var qualified = shell.IsClosed && shellComponents.All(component => !component.HasPropagationConflict
                && signedVolumeByComponent.TryGetValue(component.Id, out var signed)
                && System.Math.Abs(signed) > VolumeTolerance);
            var qualification = shellComponents.Any(component => component.HasPropagationConflict)
                ? FaceOrientationQualification.Ambiguous
                : !shell.IsClosed
                ? FaceOrientationQualification.DerivedLocallyConsistentGlobalUnknown
                : qualified ? FaceOrientationQualification.DerivedQualified : FaceOrientationQualification.Ambiguous;
            var volume = shellComponents.Sum(component => signedVolumeByComponent.GetValueOrDefault(component.Id));
            return new ShellOrientationResolution(
                shell.ShellId,
                qualification,
                shell.IsClosed,
                shell.IsInnerVoid,
                tessellationSucceeded ? volume : null,
                tessellationSucceeded
                    ? shellComponents.Sum(component => globalFlipByComponent.GetValueOrDefault(component.Id)
                        ? -signedVolumeByComponent.GetValueOrDefault(component.Id)
                        : signedVolumeByComponent.GetValueOrDefault(component.Id))
                    : null,
                shellComponents.Any(component => globalFlipByComponent.GetValueOrDefault(component.Id)),
                $"components={shellComponents.Length}; local adjacency consistent; role={(shell.IsInnerVoid ? "inner-void" : "outer")}");
        }).ToArray();
        var report = new FaceOrientationReport(orientedFaces, shellResolutions);
        var resolved = CloneWithOrientations(unresolvedBody, finalOrientation, report);
        return KernelResult<BrepBody>.Success(resolved, diagnostics);
    }

    private static Dictionary<EdgeId, List<EdgeUse>> CollectEdgeUses(BrepBody body, IReadOnlyCollection<FaceId> faces)
    {
        var result = new Dictionary<EdgeId, List<EdgeUse>>();
        foreach (var faceId in faces)
        foreach (var loopId in body.Topology.GetFace(faceId).LoopIds)
        foreach (var coedgeId in body.GetCoedgeIds(loopId))
        {
            var coedge = body.Topology.GetCoedge(coedgeId);
            if (!result.TryGetValue(coedge.EdgeId, out var uses)) result[coedge.EdgeId] = uses = [];
            uses.Add(new EdgeUse(faceId, loopId, coedge.EdgeId, coedge.IsReversed));
        }
        return result;
    }

    private static BrepBody CloneWithOrientations(
        BrepBody body,
        IReadOnlyDictionary<FaceId, bool> orientations,
        FaceOrientationReport? faceOrientationReport)
    {
        var bindings = new BrepBindingModel();
        foreach (var edge in body.Bindings.EdgeBindings) bindings.AddEdgeBinding(edge);
        foreach (var pcurve in body.Bindings.PcurveBindings) bindings.AddPcurveBinding(pcurve);
        foreach (var role in body.Bindings.FaceBoundaryRoleBindings) bindings.AddFaceBoundaryRoleBinding(role);
        foreach (var vertexLoop in body.Bindings.VertexLoopParameterBindings) bindings.AddVertexLoopParameterBinding(vertexLoop);
        foreach (var face in body.Bindings.FaceBindings)
        {
            var orientation = orientations.TryGetValue(face.FaceId, out var aligned)
                ? new ResolvedFaceOrientation(aligned)
                : face.Orientation;
            bindings.AddFaceBinding(face with { Orientation = orientation });
        }
        var points = body.Topology.Vertices
            .Where(vertex => body.TryGetVertexPoint(vertex.Id, out _))
            .ToDictionary(vertex => vertex.Id, vertex => { body.TryGetVertexPoint(vertex.Id, out var point); return point; });
        return new BrepBody(body.Topology, body.Geometry, bindings, points, body.SafeBooleanComposition, body.ShellRepresentation, faceOrientationReport);
    }

    /// <summary>A mesh for the volume sign only: coarse in angle, a chord of a fortieth of the part's smallest
    /// vertex extent up to 0.5 mm, and enough segments to honour it; null when that is no coarser than the
    /// display mesh.</summary>
    private static DisplayTessellationOptions? SignOptionsFor(BrepBody body)
    {
        double minX = double.PositiveInfinity, minY = double.PositiveInfinity, minZ = double.PositiveInfinity;
        double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity, maxZ = double.NegativeInfinity;
        foreach (var vertex in body.Topology.Vertices)
        {
            if (!body.TryGetVertexPoint(vertex.Id, out var p)) continue;
            minX = System.Math.Min(minX, p.X); minY = System.Math.Min(minY, p.Y); minZ = System.Math.Min(minZ, p.Z);
            maxX = System.Math.Max(maxX, p.X); maxY = System.Math.Max(maxY, p.Y); maxZ = System.Math.Max(maxZ, p.Z);
        }
        var extent = System.Math.Min(maxX - minX, System.Math.Min(maxY - minY, maxZ - minZ));
        var chord = double.IsFinite(extent) ? System.Math.Min(0.5d, extent / 40d) : 0d;
        return chord <= DisplayTessellationOptions.Default.ChordTolerance ? null : new(
            AngularToleranceRadians: double.Pi / 4d,
            ChordTolerance: chord,
            MinimumSegments: 4,
            MaximumSegments: 256);
    }

    private static (bool Succeeded, Dictionary<int, double> Volumes) MeasureComponents(
        BrepBody candidate, IReadOnlyDictionary<FaceId, int> componentByFace, DisplayTessellationOptions? options,
        out Dictionary<int, double> area)
    {
        var volumes = new Dictionary<int, double>();
        area = new Dictionary<int, double>();
        var tessellation = BrepDisplayTessellator.Tessellate(candidate, options);
        if (!tessellation.IsSuccess) return (false, volumes);
        foreach (var patch in tessellation.Value.FacePatches)
        {
            if (!componentByFace.TryGetValue(patch.FaceId, out var component)) continue;
            var (signed, patchArea) = SignedVolumeAndArea(patch);
            volumes[component] = volumes.GetValueOrDefault(component) + signed;
            area[component] = area.GetValueOrDefault(component) + patchArea;
        }
        return (true, volumes);
    }

    private static (double SignedVolume, double Area) SignedVolumeAndArea(DisplayFaceMeshPatch patch)
    {
        var result = 0d;
        var area = 0d;
        for (var i = 0; i < patch.TriangleIndices.Count; i += 3)
        {
            var a = patch.Positions[patch.TriangleIndices[i]];
            var b = patch.Positions[patch.TriangleIndices[i + 1]];
            var c = patch.Positions[patch.TriangleIndices[i + 2]];
            result += (a.X * ((b.Y * c.Z) - (b.Z * c.Y))
                + a.Y * ((b.Z * c.X) - (b.X * c.Z))
                + a.Z * ((b.X * c.Y) - (b.Y * c.X))) / 6d;
            area += (b - a).Cross(c - a).Length / 2d;
        }
        return (result, area);
    }

    private sealed record EdgeUse(FaceId FaceId, LoopId LoopId, EdgeId EdgeId, bool IsReversed);
    private sealed record Relation(FaceId FaceId, bool MustDiffer, string Evidence);
    private sealed record ComponentState(int Id, ShellId ShellId, IReadOnlyList<FaceId> Faces, bool HasPropagationConflict);
    private sealed record ShellState(ShellId ShellId, bool IsClosed, bool IsInnerVoid);
}
