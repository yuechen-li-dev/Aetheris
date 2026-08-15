using System.Diagnostics;
using System.Numerics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Semantics;

namespace Aetheris.Kernel.Firmament.Assembly;

public sealed class AssemblyM0Compiler
{
    public const string MissingRole = "assembly-mate-missing-role";
    public const string DuplicateRole = "assembly-mate-duplicate-role";
    public const string CapabilityMismatch = "assembly-role-capability-mismatch";
    public const string InvalidParticipant = "assembly-mate-invalid-participant";
    public const string OutsideScope = "assembly-participant-outside-scope";
    public const string IncompatibleDimensions = "assembly-interface-incompatible-dimensions";
    public const string Underconstrained = "assembly-placement-underconstrained";
    public const string Overconstrained = "assembly-placement-overconstrained";
    public const string UnresolvedTransform = "assembly-placement-unresolved";
    public const string TolerancePathMissing = "assembly-tolerance-path-missing";
    public const string TolerancePathAmbiguous = "assembly-tolerance-path-ambiguous";
    public const string ToleranceAssertionFailure = "assembly-tolerance-assertion-failure";

    public AssemblyCompilationResult Compile(AssemblySource source, double parseMilliseconds = 0)
    {
        var total = Stopwatch.StartNew();
        var diagnostics = new List<AssemblyDiagnostic>();
        var bindWatch = Stopwatch.StartNew();
        var instances = BindInstances(source, diagnostics);
        bindWatch.Stop();
        var byPath = instances.ToDictionary(x => x.Path.ToString(), StringComparer.Ordinal);
        var interfaces = source.Interfaces.ToDictionary(x => x.Name, StringComparer.Ordinal);

        var mateWatch = Stopwatch.StartNew();
        var mates = BindMates(source, interfaces, byPath, diagnostics);
        var panelMateEvidence = ValidatePanelEdgeMates(mates, interfaces, instances, diagnostics);
        mateWatch.Stop();

        var placementWatch = Stopwatch.StartNew();
        var constraints = LowerConstraints(mates, interfaces, instances, diagnostics);
        mates = mates.Select(m => m with { ConstraintIds = constraints.Where(c => c.MateStableId == m.StableId).Select(c => c.StableId).ToArray() }).ToArray();
        var placements = ResolvePlacements(source, instances, mates, interfaces, constraints, diagnostics);
        instances = instances.Select(instance => instance with
        {
            ResolvedTransform = placements.First(x => x.InstanceStableId == instance.StableId).Transform
        }).ToArray();
        placementWatch.Stop();

        var graphWatch = Stopwatch.StartNew();
        var relations = BindDimensionalRelations(source, instances, diagnostics)
            .Concat(LowerAssemblyDefinitionRelations(source, instances))
            .Concat(LowerInterfaceDimensionalRelations(mates, interfaces, instances, diagnostics))
            .OrderBy(relation => relation.StableId, StringComparer.Ordinal).ToArray();
        graphWatch.Stop();

        var toleranceWatch = Stopwatch.StartNew();
        var fits = AnalyzeFits(mates, interfaces, instances, diagnostics);
        var stackups = AnalyzeStackups(source, relations, instances, diagnostics);
        toleranceWatch.Stop();
        total.Stop();

        var perf = new AssemblyPerformanceIr(parseMilliseconds, bindWatch.Elapsed.TotalMilliseconds,
            mateWatch.Elapsed.TotalMilliseconds, placementWatch.Elapsed.TotalMilliseconds,
            graphWatch.Elapsed.TotalMilliseconds, toleranceWatch.Elapsed.TotalMilliseconds);
        var assemblyDefinitions = source.Root.Flatten().Select(member => member.SolvedAssemblyDefinition).Where(definition => definition is not null)
            .Cast<AssemblyDefinitionIr>().DistinctBy(definition => definition.StableId).OrderBy(definition => definition.StableId, StringComparer.Ordinal).ToArray();
        var datums = instances.SelectMany(instance => Flatten([instance.SemanticRoot])
                .Where(value => value.Type.Name == "AssemblyDatumFrame" && value.TryBinding<ExactDatumFrameBinding>(out _))
                .Select(value => new AssemblyDatumIr(value.StableIdentity, SemanticPath(instance, value), "DatumFrame",
                    value.TryBinding<ExactDatumFrameBinding>(out var frame) ? frame.FrameStableId : value.StableIdentity)))
            .OrderBy(datum => datum.SemanticPath, StringComparer.Ordinal).ToArray();
        var datumSolutions = mates.SelectMany(mate => constraints.Where(constraint => constraint.MateStableId == mate.StableId && constraint.Kind == PlacementConstraintKind.FrameCoincident)
                .Select(constraint => new DatumMateSolutionIr(mate.StableId, constraint.FirstSemanticValueId, constraint.SecondSemanticValueId,
                    constraint.Orientation, 6, placements.Any(placement => placement.ConstraintIds.Contains(constraint.StableId) && placement.Status == PlacementStatus.Overconstrained) ? "conflicting" : "resolved",
                    placements.FirstOrDefault(placement => placement.ConstraintIds.Contains(constraint.StableId) && placement.Authority == PlacementAuthority.MateDerived)?.Transform)))
            .OrderBy(solution => solution.MateStableId, StringComparer.Ordinal).ToArray();
        var ir = new AssemblyIr("aetheris/assembly-ir/m0", $"assembly:{source.Name}", source.Name,
            instances.Single(x => x.ParentStableId is null).StableId, instances, source.Interfaces, mates,
            constraints, placements, relations, stackups, fits, diagnostics, assemblyDefinitions, panelMateEvidence, datums, datumSolutions);
        return new(ir, diagnostics, perf);

        static string SemanticPath(AssemblyInstanceIr instance, SemanticValue value)
        {
            var prefix = instance.SemanticRoot.StableIdentity + ":";
            return value.StableIdentity.StartsWith(prefix, StringComparison.Ordinal)
                ? instance.Path + "." + value.StableIdentity[prefix.Length..]
                : instance.Path + "." + (value.ExposedName ?? value.StableIdentity);
        }
    }

    private static IReadOnlyList<AssemblyInstanceIr> BindInstances(AssemblySource source, List<AssemblyDiagnostic> diagnostics)
    {
        var pending = new List<(AssemblyMemberSource member, AssemblyPath path, string? parent)>();
        pending.Add((source.Root, new AssemblyPath([source.Root.Name]), null));
        var flat = new List<(AssemblyMemberSource member, AssemblyPath path, string? parent, string id, SemanticValue semantic)>();
        while (pending.Count > 0)
        {
            var item = pending[0]; pending.RemoveAt(0);
            var id = "assembly-instance:" + item.path;
            var semantic = InstanceScope(item.member, item.path, source.SourceIdentity);
            flat.Add((item.member, item.path, item.parent, id, semantic));
            foreach (var child in item.member.Children)
                pending.Add((child, item.path.Append(child.Name), id));
        }
        if (flat.Select(x => x.path.ToString()).Distinct(StringComparer.Ordinal).Count() != flat.Count)
            diagnostics.Add(new("assembly-instance-path-collision", "Assembly contains duplicate deterministic instance paths."));
        return flat.Select(x => new AssemblyInstanceIr(x.id, x.path, x.member.Kind, x.member.DefinitionIdentity, x.parent,
            flat.Where(c => c.parent == x.id).Select(c => c.id).Order(StringComparer.Ordinal).ToArray(), x.semantic,
            x.member.ExplicitTransform, null, x.member.Provenance ?? [], x.member.PlacementAuthority, x.member.IsEncapsulatedDefinition)).ToArray();
    }

    private static SemanticValue InstanceScope(AssemblyMemberSource member, AssemblyPath path, string sourceIdentity)
    {
        SemanticValue Clone(SemanticValue value, string semanticPath, string? exposedName) => new(
            $"assembly-semantic:{path}:{semanticPath}", value.Type, value.Capabilities.Values, value.Bindings,
            value.ExposedMembers.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => Clone(x.Value, semanticPath + "." + x.Key, x.Key)),
            [.. value.Provenance, new("assembly-instance", path.ToString(), value.StableIdentity, SemanticSourceSpan.Generated(sourceIdentity))],
            value.AuthoredSourceSpan, SemanticSourceSpan.Generated(sourceIdentity), exposedName);
        return new SemanticValue($"assembly-semantic:{path}", new(member.Kind.ToString()),
            exposedMembers: member.ExposedSemantics.OrderBy(x => x.ExposedName, StringComparer.Ordinal).Select(x => Clone(x, x.ExposedName ?? x.StableIdentity, x.ExposedName)),
            provenance: [new("assembly-instance", path.ToString(), member.DefinitionIdentity, SemanticSourceSpan.Generated(sourceIdentity))],
            generatedSourceSpan: SemanticSourceSpan.Generated(sourceIdentity));
    }

    private static IReadOnlyList<MateIr> BindMates(AssemblySource source, IReadOnlyDictionary<string, InterfaceDefinition> interfaces,
        IReadOnlyDictionary<string, AssemblyInstanceIr> byPath, List<AssemblyDiagnostic> diagnostics)
    {
        var result = new List<MateIr>();
        foreach (var mate in source.Mates.OrderBy(x => x.Name, StringComparer.Ordinal))
        {
            if (!interfaces.TryGetValue(mate.InterfaceName, out var definition))
            { diagnostics.Add(new("assembly-interface-unknown", $"Mate '{mate.Name}' references unknown Interface '{mate.InterfaceName}'.")); continue; }
            var duplicate = mate.Roles.GroupBy(x => x.Role, StringComparer.Ordinal).FirstOrDefault(x => x.Count() > 1);
            if (duplicate is not null) diagnostics.Add(new(DuplicateRole, $"Mate '{mate.Name}' assigns Role '{duplicate.Key}' more than once."));
            var endpoints = new List<MateEndpointIr>();
            foreach (var role in definition.Roles)
            {
                var assignment = mate.Roles.FirstOrDefault(x => x.Role == role.Name);
                if (assignment is null) { diagnostics.Add(new(MissingRole, $"Mate '{mate.Name}' is missing required Role '{role.Name}'.")); continue; }
                if (!TryResolve(assignment.Participant, byPath.Values, out var reference))
                {
                    var boundary = EncapsulationBoundary(assignment.Participant, byPath.Values);
                    diagnostics.Add(boundary is null
                        ? new(OutsideScope, $"Mate '{mate.Name}' Role '{role.Name}' participant '{assignment.Participant}' is not reachable in the Assembly tree.")
                        : new("assembly-internal-member-hidden", $"'{assignment.Participant}' crosses the private boundary of Assembly '{boundary.Path}'. Expose a semantic member from the Assembly if parent assemblies must depend on it."));
                    continue;
                }
                var missing = role.RequiredCapabilities.Where(c => !HasCapability(reference!.Value, c)).ToArray();
                if (missing.Length > 0)
                    diagnostics.Add(new(CapabilityMismatch, $"Mate '{mate.Name}' Role '{role.Name}' participant '{assignment.Participant}' lacks: {string.Join(", ", missing)}."));
                endpoints.Add(new(role.Name, assignment.Participant, reference!.Value.StableIdentity, role.RequiredCapabilities));
            }
            result.Add(new($"mate:{source.Name}:{mate.Name}", mate.Name, definition.StableId, endpoints, [],
                endpoints.Count == definition.Roles.Count ? "valid" : "invalid"));
        }
        return result;
    }

    private static bool HasCapability(SemanticValue value, string capability) =>
        value.Capabilities.Values.Any(x => string.Equals(x.Name, capability, StringComparison.Ordinal));

    private static IReadOnlyList<PanelMateEvidenceIr> ValidatePanelEdgeMates(
        IReadOnlyList<MateIr> mates,IReadOnlyDictionary<string,InterfaceDefinition> interfaces,
        IReadOnlyList<AssemblyInstanceIr> instances,List<AssemblyDiagnostic> diagnostics)
    {
        var values=Flatten(instances.Select(instance=>instance.SemanticRoot)).ToDictionary(value=>value.StableIdentity,StringComparer.Ordinal);
        var evidence=new List<PanelMateEvidenceIr>();var used=new HashSet<string>(StringComparer.Ordinal);
        foreach(var mate in mates.OrderBy(item=>item.StableId,StringComparer.Ordinal))
        {
            var definition=interfaces.Values.Single(item=>item.StableId==mate.InterfaceStableId);
            if(definition.Roles.Count!=2||definition.Roles.Any(role=>!role.RequiredCapabilities.Contains("BoundaryEdgeCapable",StringComparer.Ordinal)))continue;
            if(mate.Roles.Count!=2)continue;
            var first=values.GetValueOrDefault(mate.Roles[0].ParticipantSemanticValueId);var second=values.GetValueOrDefault(mate.Roles[1].ParticipantSemanticValueId);
            if(first is null||second is null||!first.TryBinding<ExactCurveBinding>(out var a)||!second.TryBinding<ExactCurveBinding>(out var b))
            {diagnostics.Add(new("assembly-panel-mate-missing-exact-edge-binding",$"Panel Mate '{mate.Name}' requires two exact directed curve bindings."));continue;}
            foreach(var edge in new[]{first.StableIdentity,second.StableIdentity})if(!used.Add(edge))diagnostics.Add(new("assembly-panel-mate-edge-already-mated",$"Panel edge '{edge}' is already used by a one-to-one Mate."));
            var opposite=definition.EdgeCorrespondence=="OppositeDirections";
            var endpoint=Math.Max(Distance(Evaluate(a,0),Evaluate(b,opposite?1:0)),Distance(Evaluate(a,1),Evaluate(b,opposite?0:1)));
            var residual=0d;for(var i=0;i<=16;i++){var t=i/16d;residual=Math.Max(residual,Distance(Evaluate(a,t),Evaluate(b,opposite?1-t:t)));}
            if(endpoint>definition.GapToleranceMm)diagnostics.Add(new("assembly-panel-mate-endpoint-mismatch",$"Panel Mate '{mate.Name}' endpoint residual {endpoint:G6} mm exceeds {definition.GapToleranceMm:G6} mm."));
            if(residual>definition.GapToleranceMm)
            {diagnostics.Add(new("assembly-panel-mate-edge-shape-mismatch",$"Panel Mate '{mate.Name}' edge residual {residual:G6} mm exceeds {definition.GapToleranceMm:G6} mm."));diagnostics.Add(new("assembly-panel-mate-g0-failure",$"Panel Mate '{mate.Name}' failed G0 continuity."));}
            if(definition.Continuity=="G1")diagnostics.Add(new("assembly-panel-mate-g1-unsupported",$"Panel Mate '{mate.Name}' requests G1; Panel M0 verifies G0 only."));
            var valid=endpoint<=definition.GapToleranceMm&&residual<=definition.GapToleranceMm&&definition.Continuity!="G1";
            evidence.Add(new(mate.StableId,first.StableIdentity,second.StableIdentity,definition.Continuity??"G0",definition.EdgeCorrespondence,endpoint,residual,valid?"valid":"invalid"));
        }
        return evidence;

        static Aetheris.Kernel.Core.Math.Point3D Evaluate(ExactCurveBinding binding,double normalized)
        {var parameter=binding.ParameterStart+Math.Clamp(normalized,0,1)*(binding.ParameterEnd-binding.ParameterStart);return binding.Curve.Kind switch{CurveGeometryKind.Line3=>binding.Curve.Line3!.Value.Evaluate(parameter),CurveGeometryKind.Circle3=>binding.Curve.Circle3!.Value.Evaluate(parameter),CurveGeometryKind.BSpline3=>binding.Curve.BSpline3!.Value.Evaluate(parameter),_=>throw new NotSupportedException($"Unsupported Panel edge family {binding.Curve.Kind}.")};}
        static double Distance(Aetheris.Kernel.Core.Math.Point3D a,Aetheris.Kernel.Core.Math.Point3D b)=>(a-b).Length;
    }

    private static IReadOnlyList<PlacementConstraintIr> LowerConstraints(IReadOnlyList<MateIr> mates,
        IReadOnlyDictionary<string, InterfaceDefinition> interfaces, IReadOnlyList<AssemblyInstanceIr> instances,
        List<AssemblyDiagnostic> diagnostics)
    {
        var result = new List<PlacementConstraintIr>();
        foreach (var mate in mates)
        {
            var definition = interfaces.Values.Single(x => x.StableId == mate.InterfaceStableId);
            foreach (var (requirement, index) in definition.Requirements.Select((x, i) => (x, i)))
            {
                var first = mate.Roles.FirstOrDefault(x => x.Role == requirement.FirstRole);
                var second = mate.Roles.FirstOrDefault(x => x.Role == requirement.SecondRole);
                if (first is null || second is null) continue;
                var firstId = ResolveRelativeSemantic(first.ParticipantSemanticValueId, requirement.FirstMember, instances);
                var secondId = ResolveRelativeSemantic(second.ParticipantSemanticValueId, requirement.SecondMember, instances);
                if (firstId is null || secondId is null)
                { diagnostics.Add(new(InvalidParticipant, $"Interface '{definition.Name}' requirement '{requirement.Kind}' cannot resolve exposed members '{requirement.FirstMember}'/'{requirement.SecondMember}'.")); continue; }
                result.Add(new($"constraint:{mate.StableId}:{index:D2}", requirement.Kind, mate.StableId, firstId, secondId, requirement.OffsetMm, 0, "admitted", requirement.Orientation));
            }
        }
        return result;
    }

    private static string? ResolveRelativeSemantic(string rootId, string member, IReadOnlyList<AssemblyInstanceIr> instances)
    {
        var root = Flatten(instances.Select(x => x.SemanticRoot)).FirstOrDefault(x => x.StableIdentity == rootId);
        if (root is null) return null;
        if (string.IsNullOrWhiteSpace(member) || member == ".") return root.StableIdentity;
        var current = root;
        foreach (var segment in member.Split('.', StringSplitOptions.RemoveEmptyEntries))
            if (!current.ExposedMembers.TryGetValue(segment, out current!)) return null;
        return current.StableIdentity;
    }

    private static IReadOnlyList<PlacementResultIr> ResolvePlacements(AssemblySource source, IReadOnlyList<AssemblyInstanceIr> instances,
        IReadOnlyList<MateIr> mates, IReadOnlyDictionary<string, InterfaceDefinition> interfaces,
        IReadOnlyList<PlacementConstraintIr> constraints, List<AssemblyDiagnostic> diagnostics)
    {
        var anchor = instances.Where(x => source.Anchor.ToString().StartsWith(x.Path + ".", StringComparison.Ordinal) || source.Anchor.ToString() == x.Path.ToString())
            .OrderByDescending(x => x.Path.Segments.Count).FirstOrDefault();
        if (anchor is null) diagnostics.Add(new("assembly-anchor-invalid", $"Anchor '{source.Anchor}' is outside the Assembly tree."));
        var known = new Dictionary<string, AssemblyTransform>(StringComparer.Ordinal);
        var overconstrained = new HashSet<string>(StringComparer.Ordinal);
        var root = instances.Single(instance => instance.ParentStableId is null);
        known[root.StableId] = AssemblyTransform.Identity;
        if (anchor is not null) known[anchor.StableId] = AssemblyTransform.Identity;
        var explicitPending = instances.Where(instance => instance.LocalTransform is not null).OrderBy(instance => instance.Path.Segments.Count).ToList();
        var explicitProgress = true;
        while (explicitProgress && explicitPending.Count > 0)
        {
            explicitProgress = false;
            foreach (var instance in explicitPending.ToArray())
            {
                var parent = instance.ParentStableId is null ? null : instances.Single(candidate => candidate.StableId == instance.ParentStableId);
                AssemblyTransform? parentWorld = null;
                if (parent is not null && !known.TryGetValue(parent.StableId, out parentWorld)) continue;
                var local = ToMatrix(instance.LocalTransform!);
                var world = parent is null ? local : local * ToMatrix(parentWorld!);
                known[instance.StableId] = FromMatrix(world);
                explicitPending.Remove(instance);
                explicitProgress = true;
            }
        }
        var progress = true;
        while (progress)
        {
            progress = false;
            foreach (var mate in mates.OrderBy(x => x.StableId, StringComparer.Ordinal))
            {
                var mateConstraints = constraints.Where(x => x.MateStableId == mate.StableId).ToArray();
                var owners = mateConstraints.SelectMany(x => new[] { OwnerInstance(x.FirstSemanticValueId, instances), OwnerInstance(x.SecondSemanticValueId, instances) })
                    .Where(x => x is not null).DistinctBy(x => x!.StableId).Cast<AssemblyInstanceIr>().ToArray();
                if (owners.Length != 2 || mateConstraints.Length == 0) continue;
                var firstKnown = known.ContainsKey(owners[0].StableId); var secondKnown = known.ContainsKey(owners[1].StableId);
                if (firstKnown == secondKnown)
                {
                    if (!firstKnown) continue;
                    foreach (var moving in owners)
                    {
                        var target = owners.Single(x => x.StableId != moving.StableId);
                        var oriented = Orient(mateConstraints, moving, instances);
                        var candidate = CandidateTransform(oriented, instances, known[target.StableId]);
                        if (TransformDistance(candidate, known[moving.StableId]) > 1e-7 && overconstrained.Add(moving.StableId))
                            diagnostics.Add(new(Overconstrained, $"Instance '{moving.Path}' receives a conflicting placement from Mate '{mate.Name}'; transform residual exceeds 1e-7."));
                    }
                    continue;
                }
                var movingInstance = firstKnown ? owners[1] : owners[0];
                var targetInstance = firstKnown ? owners[0] : owners[1];
                var orientedConstraints = Orient(mateConstraints, movingInstance, instances);
                var frameCandidates = orientedConstraints.Where(x => x.Kind == PlacementConstraintKind.FrameCoincident)
                    .Select(x => CandidateTransform([x], instances, known[targetInstance.StableId])).ToArray();
                if (frameCandidates.Length > 1 && frameCandidates.Skip(1).Any(x => TransformDistance(frameCandidates[0], x) > 1e-7))
                {
                    overconstrained.Add(movingInstance.StableId);
                    diagnostics.Add(new(Overconstrained, $"Instance '{movingInstance.Path}' receives conflicting DatumFrame constraints from Mate '{mate.Name}'."));
                    continue;
                }
                var axisCandidates = orientedConstraints.Where(x => x.Kind == PlacementConstraintKind.AxisCoincident)
                    .Select(x => CandidateTransform([x], instances, known[targetInstance.StableId])).ToArray();
                if (axisCandidates.Length > 1 && axisCandidates.Skip(1).Any(x => TransformDistance(axisCandidates[0], x) > 1e-7))
                {
                    overconstrained.Add(movingInstance.StableId);
                    diagnostics.Add(new(Overconstrained, $"Instance '{movingInstance.Path}' receives incompatible constraints from Mate '{mate.Name}'; transform residual exceeds 1e-7."));
                    continue;
                }
                known[movingInstance.StableId] = CandidateTransform(orientedConstraints, instances, known[targetInstance.StableId]);
                progress = true;
            }
        }

        // A parent Mate may have resolved a subassembly occurrence after the
        // first explicit-local pass. Compose its already-solved definition
        // children now; no internal Mate is evaluated in the parent scope.
        explicitProgress = true;
        while (explicitProgress && explicitPending.Count > 0)
        {
            explicitProgress = false;
            foreach (var instance in explicitPending.ToArray())
            {
                var parent = instance.ParentStableId is null ? null : instances.Single(candidate => candidate.StableId == instance.ParentStableId);
                AssemblyTransform? parentWorld = null;
                if (parent is not null && !known.TryGetValue(parent.StableId, out parentWorld)) continue;
                var world = parent is null ? ToMatrix(instance.LocalTransform!) : ToMatrix(instance.LocalTransform!) * ToMatrix(parentWorld!);
                known[instance.StableId] = FromMatrix(world); explicitPending.Remove(instance); explicitProgress = true;
            }
        }

        var results = new List<PlacementResultIr>();
        foreach (var instance in instances)
        {
            if ((anchor is not null && instance.StableId == anchor.StableId) || instance.ParentStableId is null)
            { results.Add(new(instance.StableId, PlacementStatus.Anchored, AssemblyTransform.Identity, [], [], [], instance.PlacementAuthority)); continue; }
            if (instance.LocalTransform is not null && known.TryGetValue(instance.StableId, out var explicitTransform))
            { results.Add(new(instance.StableId, PlacementStatus.Resolved, explicitTransform, [], [], [], instance.PlacementAuthority)); continue; }
            var participantIds = Flatten([instance.SemanticRoot]).Select(x => x.StableIdentity).ToHashSet(StringComparer.Ordinal);
            var relevant = constraints.Where(x => participantIds.Contains(x.FirstSemanticValueId) || participantIds.Contains(x.SecondSemanticValueId)).ToArray();
            if (overconstrained.Contains(instance.StableId))
            { results.Add(new(instance.StableId, PlacementStatus.Overconstrained, null, [], [], relevant.Select(x => x.StableId).ToArray())); continue; }
            if (relevant.Length == 0 || !known.TryGetValue(instance.StableId, out var transform))
            { results.Add(new(instance.StableId, PlacementStatus.Unresolved, null, ["X", "Y", "Z"], ["X", "Y", "Z"], relevant.Select(x => x.StableId).ToArray())); continue; }
            var hasFrame = relevant.Any(x => x.Kind == PlacementConstraintKind.FrameCoincident);
            var hasAxis = relevant.Any(x => x.Kind is PlacementConstraintKind.AxisCoincident or PlacementConstraintKind.AxisAligned);
            var hasPlane = relevant.Any(x => x.Kind == PlacementConstraintKind.PlaneCoincident);
            var hasPoint = relevant.Any(x => x.Kind == PlacementConstraintKind.PointCoincident);
            var freeT = hasFrame ? Array.Empty<string>() : hasAxis ? (hasPlane || hasPoint ? Array.Empty<string>() : ["along-axis"]) : (hasPoint ? Array.Empty<string>() : ["X", "Y", "Z"]);
            string[] freeR = hasFrame ? [] : hasAxis ? ["about-axis"] : ["X", "Y", "Z"];
            var involvedInterfaces = mates.Where(m => relevant.Any(c => c.MateStableId == m.StableId)).Select(m => interfaces.Values.Single(x => x.StableId == m.InterfaceStableId)).ToArray();
            string[] admitted = involvedInterfaces.Length == 0 ? [] : (involvedInterfaces.Select(x => x.AdmittedFreeMotions ?? []).Aggregate((IEnumerable<string>?)null,
                (common, next) => common is null ? next : common.Intersect(next, StringComparer.Ordinal)) ?? []).ToArray();
            var unadmittedT = freeT.Where(x => !admitted.Contains("translation:" + x, StringComparer.Ordinal)).ToArray();
            var unadmittedR = freeR.Where(x => !admitted.Contains("rotation:" + x, StringComparer.Ordinal)).ToArray();
            var status = unadmittedT.Length == 0 && unadmittedR.Length == 0 ? PlacementStatus.Resolved : PlacementStatus.Underconstrained;
            if (status == PlacementStatus.Underconstrained)
                diagnostics.Add(new(Underconstrained, $"Instance '{instance.Path}' retains translations [{string.Join(",", unadmittedT)}] and rotations [{string.Join(",", unadmittedR)}].", AssemblyDiagnosticSeverity.Warning));
            results.Add(new(instance.StableId, status, transform, unadmittedT, unadmittedR, relevant.Select(x => x.StableId).ToArray(), PlacementAuthority.MateDerived));
        }
        return results;
    }

    private static PlacementConstraintIr[] Orient(IEnumerable<PlacementConstraintIr> constraints, AssemblyInstanceIr moving, IReadOnlyList<AssemblyInstanceIr> instances) =>
        constraints.Select(x => OwnerInstance(x.FirstSemanticValueId, instances)?.StableId == moving.StableId
            ? x : x with { FirstSemanticValueId = x.SecondSemanticValueId, SecondSemanticValueId = x.FirstSemanticValueId }).ToArray();

    private static AssemblyInstanceIr? OwnerInstance(string semanticId, IReadOnlyList<AssemblyInstanceIr> instances) =>
        instances.Where(x => semanticId == x.SemanticRoot.StableIdentity || semanticId.StartsWith(x.SemanticRoot.StableIdentity + ":", StringComparison.Ordinal))
            .OrderByDescending(x => x.Path.Segments.Count).FirstOrDefault();

    private static double TransformDistance(AssemblyTransform a, AssemblyTransform b)
    {
        return Math.Sqrt(Enumerable.Range(0, 16).Sum(i => Math.Pow(a.Matrix[i] - b.Matrix[i], 2)));
    }

    private static AssemblyTransform CandidateTransform(IReadOnlyList<PlacementConstraintIr> constraints, IReadOnlyList<AssemblyInstanceIr> instances, AssemblyTransform? targetWorld = null)
    {
        var values = Flatten(instances.Select(x => x.SemanticRoot)).ToDictionary(x => x.StableIdentity, StringComparer.Ordinal);
        var frameConstraint = constraints.FirstOrDefault(x => x.Kind == PlacementConstraintKind.FrameCoincident);
        if (frameConstraint is not null
            && values[frameConstraint.FirstSemanticValueId].TryBinding<ExactDatumFrameBinding>(out var sourceFrame)
            && values[frameConstraint.SecondSemanticValueId].TryBinding<ExactDatumFrameBinding>(out var targetFrame))
        {
            var source = FrameMatrix(sourceFrame, DatumOrientationRelation.SameDirection);
            var targetFrameMatrix = FrameMatrix(targetFrame, frameConstraint.Orientation);
            if (!Matrix4x4.Invert(source, out var inverse)) return targetWorld ?? AssemblyTransform.Identity;
            var transform = inverse * targetFrameMatrix;
            if (targetWorld is not null) transform *= ToMatrix(targetWorld);
            return FromMatrix(transform);
        }
        var axis = constraints.FirstOrDefault(x => x.Kind == PlacementConstraintKind.AxisCoincident);
        if (axis is null || !values[axis.FirstSemanticValueId].TryBinding<ExactAxisBinding>(out var a) || !values[axis.SecondSemanticValueId].TryBinding<ExactAxisBinding>(out var b))
            return targetWorld ?? AssemblyTransform.Identity;
        var from = Vector3.Normalize(new((float)a.DirectionX, (float)a.DirectionY, (float)a.DirectionZ));
        var to = Vector3.Normalize(new((float)b.DirectionX, (float)b.DirectionY, (float)b.DirectionZ));
        var dot = Math.Clamp(Vector3.Dot(from, to), -1, 1);
        var cross = Vector3.Cross(from, to);
        Matrix4x4 rotation;
        if (cross.LengthSquared() < 1e-12f) rotation = dot >= 0 ? Matrix4x4.Identity : Matrix4x4.CreateFromAxisAngle(Vector3.UnitX, MathF.PI);
        else rotation = Matrix4x4.CreateFromAxisAngle(Vector3.Normalize(cross), MathF.Acos(dot));
        var origin = Vector3.Transform(new((float)a.OriginX, (float)a.OriginY, (float)a.OriginZ), rotation);
        var target = new Vector3((float)b.OriginX, (float)b.OriginY, (float)b.OriginZ);
        rotation.Translation = target - origin;
        var plane = constraints.FirstOrDefault(x => x.Kind == PlacementConstraintKind.PlaneCoincident);
        if (plane is not null && values[plane.FirstSemanticValueId].TryBinding<ExactPlaneBinding>(out var sourcePlane)
            && values[plane.SecondSemanticValueId].TryBinding<ExactPlaneBinding>(out var targetPlane))
        {
            var sourcePlaneWorld = Vector3.Transform(new((float)sourcePlane.OriginX, (float)sourcePlane.OriginY, (float)sourcePlane.OriginZ), rotation);
            var targetPlanePoint = new Vector3((float)targetPlane.OriginX, (float)targetPlane.OriginY, (float)targetPlane.OriginZ);
            var targetAxis = Vector3.Normalize(to);
            rotation.Translation += targetAxis * Vector3.Dot(targetPlanePoint - sourcePlaneWorld, targetAxis);
        }
        if (targetWorld is not null) rotation *= ToMatrix(targetWorld);
        return new([rotation.M11, rotation.M12, rotation.M13, rotation.M14, rotation.M21, rotation.M22, rotation.M23, rotation.M24,
            rotation.M31, rotation.M32, rotation.M33, rotation.M34, rotation.M41, rotation.M42, rotation.M43, rotation.M44]);
    }

    private static Matrix4x4 FrameMatrix(ExactDatumFrameBinding frame, DatumOrientationRelation orientation)
    {
        var x = Vector3.Normalize(new((float)frame.XAxisX, (float)frame.XAxisY, (float)frame.XAxisZ));
        var y = Vector3.Normalize(new((float)frame.YAxisX, (float)frame.YAxisY, (float)frame.YAxisZ));
        var z = Vector3.Normalize(new((float)frame.ZAxisX, (float)frame.ZAxisY, (float)frame.ZAxisZ));
        if (orientation == DatumOrientationRelation.OpposedDirection) { y = -y; z = -z; }
        return new(x.X, x.Y, x.Z, 0, y.X, y.Y, y.Z, 0, z.X, z.Y, z.Z, 0,
            (float)frame.OriginX, (float)frame.OriginY, (float)frame.OriginZ, 1);
    }

    private static Matrix4x4 ToMatrix(AssemblyTransform transform) => new(
        (float)transform.Matrix[0], (float)transform.Matrix[1], (float)transform.Matrix[2], (float)transform.Matrix[3],
        (float)transform.Matrix[4], (float)transform.Matrix[5], (float)transform.Matrix[6], (float)transform.Matrix[7],
        (float)transform.Matrix[8], (float)transform.Matrix[9], (float)transform.Matrix[10], (float)transform.Matrix[11],
        (float)transform.Matrix[12], (float)transform.Matrix[13], (float)transform.Matrix[14], (float)transform.Matrix[15]);

    private static AssemblyTransform FromMatrix(Matrix4x4 matrix) => new([
        matrix.M11, matrix.M12, matrix.M13, matrix.M14,
        matrix.M21, matrix.M22, matrix.M23, matrix.M24,
        matrix.M31, matrix.M32, matrix.M33, matrix.M34,
        matrix.M41, matrix.M42, matrix.M43, matrix.M44]);

    private static IReadOnlyList<DimensionalRelationIr> BindDimensionalRelations(AssemblySource source,
        IReadOnlyList<AssemblyInstanceIr> instances, List<AssemblyDiagnostic> diagnostics)
    {
        var result = new List<DimensionalRelationIr>();
        foreach (var relation in source.DimensionalRelations)
        {
            if (!TryResolve(relation.From, instances, out var from) || !TryResolve(relation.To, instances, out var to))
            { diagnostics.Add(new(TolerancePathMissing, $"Dimensional relation '{relation.Name}' references an endpoint outside the Assembly tree.")); continue; }
            var origin = OwnerPath(from!.Value.StableIdentity, instances);
            var mateId = relation.Provenance.StartsWith("Mate:", StringComparison.Ordinal) ? "mate-reference:" + relation.Provenance[5..] : null;
            result.Add(new($"dimension-relation:{source.Name}:{relation.Name}", from.Value.StableIdentity, to!.Value.StableIdentity,
                relation.Nominal, relation.LowerTolerance, relation.UpperTolerance, relation.Unit, 1, origin, relation.Provenance, mateId));
        }
        return result;
    }

    private static IReadOnlyList<DimensionalRelationIr> LowerAssemblyDefinitionRelations(AssemblySource source, IReadOnlyList<AssemblyInstanceIr> instances)
    {
        var definitions = source.Root.Flatten().Where(member => member.SolvedAssemblyDefinition is not null)
            .ToDictionary(member => member.Name, member => member.SolvedAssemblyDefinition!, StringComparer.Ordinal);
        var result = new List<DimensionalRelationIr>();
        foreach (var instance in instances.Where(item => item.IsEncapsulatedDefinition))
        {
            if (!definitions.TryGetValue(instance.Path.Segments.Last(), out var definition)) continue;
            foreach (var relation in definition.PublicDimensionalRelations)
            {
                if (!instance.SemanticRoot.ExposedMembers.TryGetValue(relation.FromSemanticValueId, out var from)
                    || !instance.SemanticRoot.ExposedMembers.TryGetValue(relation.ToSemanticValueId, out var to)) continue;
                result.Add(relation with
                {
                    StableId = relation.StableId + ":occurrence:" + instance.StableId,
                    FromSemanticValueId = from.StableIdentity,
                    ToSemanticValueId = to.StableIdentity,
                    OriginInstancePath = instance.Path.ToString(),
                    AssemblyDefinitionStableId = definition.StableId,
                    SourceProvenance = [.. relation.SourceProvenance ?? [], .. instance.Provenance]
                });
            }
        }
        return result;
    }

    private static IReadOnlyList<InterfaceFitResultIr> AnalyzeFits(IReadOnlyList<MateIr> mates,
        IReadOnlyDictionary<string, InterfaceDefinition> interfaces, IReadOnlyList<AssemblyInstanceIr> instances,
        List<AssemblyDiagnostic> diagnostics)
    {
        var values = Flatten(instances.Select(x => x.SemanticRoot)).ToDictionary(x => x.StableIdentity, StringComparer.Ordinal);
        var result = new List<InterfaceFitResultIr>();
        foreach (var mate in mates)
        {
            var definition = interfaces.Values.Single(x => x.StableId == mate.InterfaceStableId);
            if (definition.Fit is not { } fit) continue;
            var shaft = mate.Roles.FirstOrDefault(x => x.Role == fit.ShaftRole);
            var bore = mate.Roles.FirstOrDefault(x => x.Role == fit.BoreRole);
            if (shaft is null || bore is null) continue;
            var shaftId = ResolveRelativeSemantic(shaft.ParticipantSemanticValueId, fit.ShaftDimension, instances);
            var boreId = ResolveRelativeSemantic(bore.ParticipantSemanticValueId, fit.BoreDimension, instances);
            if (shaftId is null || boreId is null || !values[shaftId].TryBinding<TolerancedDimensionBinding>(out var sd) || !values[boreId].TryBinding<TolerancedDimensionBinding>(out var bd))
            { diagnostics.Add(new(CapabilityMismatch, $"Mate '{mate.Name}' fit requires toleranced dimensions '{fit.ShaftDimension}' and '{fit.BoreDimension}'.")); continue; }
            if (sd.Unit != bd.Unit) { diagnostics.Add(new("assembly-tolerance-unit-mismatch", $"Mate '{mate.Name}' fit dimensions use '{sd.Unit}' and '{bd.Unit}'.")); continue; }
            var nominal = fit.ClearanceScale * (bd.Nominal - sd.Nominal);
            var min = fit.ClearanceScale * (bd.Minimum - sd.Maximum);
            var max = fit.ClearanceScale * (bd.Maximum - sd.Minimum);
            var contributions = Array.Empty<FitContributionIr>();
            if (fit.Variation is { } variation)
            {
                var bend = 2d * variation.EngagementLengthMm * Math.Tan(variation.BendAngleToleranceDegrees * Math.PI / 180d);
                contributions =
                [
                    new("linear dimension tolerance", $"{shaft.ParticipantPath}/{bore.ParticipantPath}", variation.LinearToleranceMm),
                    new("sheet thickness tolerance", $"{shaft.ParticipantPath}/{bore.ParticipantPath}.Thickness", 2d * variation.ThicknessToleranceMm),
                    new("bend location tolerance", $"{shaft.ParticipantPath}/{bore.ParticipantPath}.Bends", 2d * variation.BendLocationToleranceMm),
                    new("bend angle envelope", $"{shaft.ParticipantPath}/{bore.ParticipantPath}.Bends", bend),
                    new("two coated fit surfaces", $"{shaft.ParticipantPath}/{bore.ParticipantPath}.Coating", 2d * (variation.CoatingThicknessMm + variation.CoatingThicknessToleranceMm))
                ];
                var reduction = contributions.Sum(item => item.WorstCaseClearanceReductionMm);
                var symmetric = reduction - 2d * (variation.CoatingThicknessMm + variation.CoatingThicknessToleranceMm);
                min = nominal - reduction;
                max = nominal + symmetric - 2d * Math.Max(0, variation.CoatingThicknessMm - variation.CoatingThicknessToleranceMm);
            }
            var compatible = min >= 0;
            if (!compatible) diagnostics.Add(new(IncompatibleDimensions, $"Mate '{mate.Name}' fit ranges from {min:G6} to {max:G6} {sd.Unit} (nominal {nominal:G6}).", AssemblyDiagnosticSeverity.Warning));
            result.Add(new(mate.StableId, nominal, min, max, sd.Unit, compatible,
                Classify(nominal, nominal), Classify(min, max), Math.Max(0, -min),
                contributions.OrderByDescending(item => item.WorstCaseClearanceReductionMm).ThenBy(item => item.Source, StringComparer.Ordinal).ToArray(),
                fit.Variation is null ? "toleranced semantic dimensions" : "worst-case analytic interval over authored dimensions, thickness, bend and coating allowances"));
        }
        return result;

        static FitClassification Classify(double minimum, double maximum)
        {
            const double engineeringNoiseFloor = 1e-6;
            return minimum > engineeringNoiseFloor ? FitClassification.GuaranteedClearance
                : maximum < -engineeringNoiseFloor ? FitClassification.GuaranteedInterference
                : minimum < -engineeringNoiseFloor ? FitClassification.PossibleInterference
                : FitClassification.PossibleContact;
        }
    }

    private static IReadOnlyList<DimensionalRelationIr> LowerInterfaceDimensionalRelations(
        IReadOnlyList<MateIr> mates,
        IReadOnlyDictionary<string, InterfaceDefinition> interfaces,
        IReadOnlyList<AssemblyInstanceIr> instances,
        List<AssemblyDiagnostic> diagnostics)
    {
        var values = Flatten(instances.Select(instance => instance.SemanticRoot)).ToDictionary(value => value.StableIdentity, StringComparer.Ordinal);
        var result = new List<DimensionalRelationIr>();
        foreach (var mate in mates.OrderBy(item => item.StableId, StringComparer.Ordinal))
        {
            var definition = interfaces.Values.Single(item => item.StableId == mate.InterfaceStableId);
            if (definition.Fit is not { } fit) continue;
            var shaft = mate.Roles.FirstOrDefault(role => role.Role == fit.ShaftRole);
            var bore = mate.Roles.FirstOrDefault(role => role.Role == fit.BoreRole);
            if (shaft is null || bore is null) continue;
            var shaftId = ResolveRelativeSemantic(shaft.ParticipantSemanticValueId, fit.ShaftDimension, instances);
            var boreId = ResolveRelativeSemantic(bore.ParticipantSemanticValueId, fit.BoreDimension, instances);
            if (shaftId is null || boreId is null
                || !values[shaftId].TryBinding<TolerancedDimensionBinding>(out var shaftDimension)
                || !values[boreId].TryBinding<TolerancedDimensionBinding>(out var boreDimension)
                || !string.Equals(shaftDimension.Unit, boreDimension.Unit, StringComparison.Ordinal)) continue;
            result.Add(new(
                $"dimension-relation:{mate.StableId}:fit-clearance",
                shaftId,
                boreId,
                fit.ClearanceScale * (boreDimension.Nominal - shaftDimension.Nominal),
                fit.ClearanceScale * (boreDimension.LowerTolerance - shaftDimension.UpperTolerance),
                fit.ClearanceScale * (boreDimension.UpperTolerance - shaftDimension.LowerTolerance),
                shaftDimension.Unit,
                1,
                OwnerPath(shaftId, instances),
                $"Interface:{definition.Name}.Fit",
                mate.StableId,
                definition.StableId,
                values[shaftId].Provenance.Concat(values[boreId].Provenance)
                    .DistinctBy(item => (item.Stage, item.Identity, item.Evidence)).ToArray()));
        }
        return result;
    }

    private static IReadOnlyList<ToleranceStackupResultIr> AnalyzeStackups(AssemblySource source,
        IReadOnlyList<DimensionalRelationIr> relations, IReadOnlyList<AssemblyInstanceIr> instances,
        List<AssemblyDiagnostic> diagnostics)
    {
        var result = new List<ToleranceStackupResultIr>();
        foreach (var assertion in source.StackupAsserts)
        {
            if (!TryResolve(assertion.From, instances, out var from) || !TryResolve(assertion.To, instances, out var to))
            { diagnostics.Add(new(TolerancePathMissing, $"Assert ToleranceStackup '{assertion.Name}' endpoints cannot be resolved.")); continue; }
            var paths = FindPaths(from!.Value.StableIdentity, to!.Value.StableIdentity, relations, 2);
            if (paths.Count == 0) { diagnostics.Add(new(TolerancePathMissing, $"Assert ToleranceStackup '{assertion.Name}' has no dimensional path.")); continue; }
            if (paths.Count > 1) { diagnostics.Add(new(TolerancePathAmbiguous, $"Assert ToleranceStackup '{assertion.Name}' has multiple valid dimensional paths.")); continue; }
            var contributions = paths[0].Select(step => new StackupContributionIr(step.edge.StableId, step.sign,
                step.sign * step.edge.Nominal,
                step.sign > 0 ? step.edge.LowerTolerance : -step.edge.UpperTolerance,
                step.sign > 0 ? step.edge.UpperTolerance : -step.edge.LowerTolerance,
                step.edge.Unit, step.edge.OriginInstancePath, step.edge.Provenance, step.edge.MateStableId, step.edge.InterfaceStableId, step.edge.SourceProvenance,
                step.edge.ExpandedContributors)).ToArray();
            if (contributions.Any(x => x.Unit != assertion.Unit))
            { diagnostics.Add(new("assembly-tolerance-unit-mismatch", $"Assert ToleranceStackup '{assertion.Name}' mixes units.")); continue; }
            var nominal = contributions.Sum(x => x.Nominal);
            var min = nominal + contributions.Sum(x => x.LowerTolerance);
            var max = nominal + contributions.Sum(x => x.UpperTolerance);
            var passed = min >= assertion.RequiredMinimum;
            if (!passed) diagnostics.Add(new(ToleranceAssertionFailure,
                $"Assert ToleranceStackup '{assertion.Name}' failed: worst-case minimum {min:G6} {assertion.Unit} < required {assertion.RequiredMinimum:G6} {assertion.Unit}."));
            result.Add(new(assertion.Name, from.Value.StableIdentity, to.Value.StableIdentity, nominal, min, max,
                assertion.RequiredMinimum, assertion.Unit, passed, passed ? "passed" : "failed", contributions));
        }
        return result;
    }

    private static List<List<(DimensionalRelationIr edge, int sign)>> FindPaths(string start, string end,
        IReadOnlyList<DimensionalRelationIr> edges, int limit)
    {
        var result = new List<List<(DimensionalRelationIr, int)>>();
        void Visit(string node, HashSet<string> visited, List<(DimensionalRelationIr, int)> path)
        {
            if (result.Count >= limit) return;
            if (node == end) { result.Add([.. path]); return; }
            foreach (var edge in edges.OrderBy(x => x.StableId, StringComparer.Ordinal))
            {
                var next = edge.FromSemanticValueId == node ? edge.ToSemanticValueId : edge.ToSemanticValueId == node ? edge.FromSemanticValueId : null;
                if (next is null || visited.Contains(next)) continue;
                visited.Add(next); path.Add((edge, edge.FromSemanticValueId == node ? 1 : -1));
                Visit(next, visited, path); path.RemoveAt(path.Count - 1); visited.Remove(next);
            }
        }
        Visit(start, new([start], StringComparer.Ordinal), []);
        return result;
    }

    internal static bool TryResolve(AssemblyPath path, IEnumerable<AssemblyInstanceIr> instances, out SemanticReference? reference)
    {
        reference = null;
        var all = instances.ToArray();
        if (EncapsulationBoundary(path, all) is not null) return false;
        var instance = all.Where(x => path.Segments.Count >= x.Path.Segments.Count && path.Segments.Take(x.Path.Segments.Count).SequenceEqual(x.Path.Segments))
            .OrderByDescending(x => x.Path.Segments.Count).FirstOrDefault();
        if (instance is null) return false;
        var current = instance.SemanticRoot;
        var resolved = new List<SemanticPathSegment>();
        foreach (var segment in path.Segments.Skip(instance.Path.Segments.Count))
        {
            if (!current.ExposedMembers.TryGetValue(segment, out current!)) return false;
            resolved.Add(new(segment, SemanticSourceSpan.Generated(path.ToString())));
        }
        reference = new(current, resolved, SemanticSourceSpan.Generated(path.ToString()));
        return true;
    }

    private static AssemblyInstanceIr? EncapsulationBoundary(AssemblyPath path, IEnumerable<AssemblyInstanceIr> instances)
    {
        var all = instances.ToArray();
        return all.Where(instance => instance.IsEncapsulatedDefinition
                && path.Segments.Count > instance.Path.Segments.Count
                && path.Segments.Take(instance.Path.Segments.Count).SequenceEqual(instance.Path.Segments)
                // A direct next segment is a public semantic name.  Crossing into
                // an actual child occurrence is the prohibited traversal.
                && all.Any(child => child.ParentStableId == instance.StableId
                    && child.Path.Segments.Last() == path.Segments[instance.Path.Segments.Count]))
            .OrderByDescending(instance => instance.Path.Segments.Count).FirstOrDefault();
    }

    private static IEnumerable<SemanticValue> Flatten(IEnumerable<SemanticValue> roots)
    {
        foreach (var root in roots)
        {
            yield return root;
            foreach (var child in Flatten(root.ExposedMembers.Values)) yield return child;
        }
    }

    private static string OwnerPath(string semanticId, IReadOnlyList<AssemblyInstanceIr> instances) =>
        instances.Where(x => semanticId == x.SemanticRoot.StableIdentity || semanticId.StartsWith(x.SemanticRoot.StableIdentity + ":", StringComparison.Ordinal))
            .OrderByDescending(x => x.Path.Segments.Count).Select(x => x.Path.ToString()).FirstOrDefault() ?? "unknown";
}
