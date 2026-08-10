using System.Diagnostics;
using System.Numerics;
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
        var relations = BindDimensionalRelations(source, instances, diagnostics);
        graphWatch.Stop();

        var toleranceWatch = Stopwatch.StartNew();
        var fits = AnalyzeFits(mates, interfaces, instances, diagnostics);
        var stackups = AnalyzeStackups(source, relations, instances, diagnostics);
        toleranceWatch.Stop();
        total.Stop();

        var perf = new AssemblyPerformanceIr(parseMilliseconds, bindWatch.Elapsed.TotalMilliseconds,
            mateWatch.Elapsed.TotalMilliseconds, placementWatch.Elapsed.TotalMilliseconds,
            graphWatch.Elapsed.TotalMilliseconds, toleranceWatch.Elapsed.TotalMilliseconds);
        var ir = new AssemblyIr("aetheris/assembly-ir/m0", $"assembly:{source.Name}", source.Name,
            instances.Single(x => x.ParentStableId is null).StableId, instances, source.Interfaces, mates,
            constraints, placements, relations, stackups, fits, diagnostics);
        return new(ir, diagnostics, perf);
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
            null, null, x.member.Provenance ?? [])).ToArray();
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
                { diagnostics.Add(new(OutsideScope, $"Mate '{mate.Name}' Role '{role.Name}' participant '{assignment.Participant}' is not reachable in the Assembly tree.")); continue; }
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
                result.Add(new($"constraint:{mate.StableId}:{index:D2}", requirement.Kind, mate.StableId, firstId, secondId, requirement.OffsetMm, 0, "admitted"));
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
        if (anchor is not null) known[anchor.StableId] = AssemblyTransform.Identity;
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

        var results = new List<PlacementResultIr>();
        foreach (var instance in instances)
        {
            if (anchor is not null && instance.StableId == anchor.StableId)
            { results.Add(new(instance.StableId, PlacementStatus.Anchored, AssemblyTransform.Identity, [], [], [])); continue; }
            var participantIds = Flatten([instance.SemanticRoot]).Select(x => x.StableIdentity).ToHashSet(StringComparer.Ordinal);
            var relevant = constraints.Where(x => participantIds.Contains(x.FirstSemanticValueId) || participantIds.Contains(x.SecondSemanticValueId)).ToArray();
            if (overconstrained.Contains(instance.StableId))
            { results.Add(new(instance.StableId, PlacementStatus.Overconstrained, null, [], [], relevant.Select(x => x.StableId).ToArray())); continue; }
            if (relevant.Length == 0 || !known.TryGetValue(instance.StableId, out var transform))
            { results.Add(new(instance.StableId, PlacementStatus.Unresolved, null, ["X", "Y", "Z"], ["X", "Y", "Z"], relevant.Select(x => x.StableId).ToArray())); continue; }
            var hasAxis = relevant.Any(x => x.Kind is PlacementConstraintKind.AxisCoincident or PlacementConstraintKind.AxisAligned);
            var hasPlane = relevant.Any(x => x.Kind == PlacementConstraintKind.PlaneCoincident);
            var hasPoint = relevant.Any(x => x.Kind == PlacementConstraintKind.PointCoincident);
            var freeT = hasAxis ? (hasPlane || hasPoint ? Array.Empty<string>() : ["along-axis"]) : (hasPoint ? Array.Empty<string>() : ["X", "Y", "Z"]);
            string[] freeR = hasAxis ? ["about-axis"] : ["X", "Y", "Z"];
            var involvedInterfaces = mates.Where(m => relevant.Any(c => c.MateStableId == m.StableId)).Select(m => interfaces.Values.Single(x => x.StableId == m.InterfaceStableId)).ToArray();
            string[] admitted = involvedInterfaces.Length == 0 ? [] : (involvedInterfaces.Select(x => x.AdmittedFreeMotions ?? []).Aggregate((IEnumerable<string>?)null,
                (common, next) => common is null ? next : common.Intersect(next, StringComparer.Ordinal)) ?? []).ToArray();
            var unadmittedT = freeT.Where(x => !admitted.Contains("translation:" + x, StringComparer.Ordinal)).ToArray();
            var unadmittedR = freeR.Where(x => !admitted.Contains("rotation:" + x, StringComparer.Ordinal)).ToArray();
            var status = unadmittedT.Length == 0 && unadmittedR.Length == 0 ? PlacementStatus.Resolved : PlacementStatus.Underconstrained;
            if (status == PlacementStatus.Underconstrained)
                diagnostics.Add(new(Underconstrained, $"Instance '{instance.Path}' retains translations [{string.Join(",", unadmittedT)}] and rotations [{string.Join(",", unadmittedR)}].", AssemblyDiagnosticSeverity.Warning));
            results.Add(new(instance.StableId, status, transform, unadmittedT, unadmittedR, relevant.Select(x => x.StableId).ToArray()));
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

    private static Matrix4x4 ToMatrix(AssemblyTransform transform) => new(
        (float)transform.Matrix[0], (float)transform.Matrix[1], (float)transform.Matrix[2], (float)transform.Matrix[3],
        (float)transform.Matrix[4], (float)transform.Matrix[5], (float)transform.Matrix[6], (float)transform.Matrix[7],
        (float)transform.Matrix[8], (float)transform.Matrix[9], (float)transform.Matrix[10], (float)transform.Matrix[11],
        (float)transform.Matrix[12], (float)transform.Matrix[13], (float)transform.Matrix[14], (float)transform.Matrix[15]);

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
            var nominal = bd.Nominal - sd.Nominal;
            var min = bd.Minimum - sd.Maximum;
            var max = bd.Maximum - sd.Minimum;
            var compatible = min >= 0;
            if (!compatible) diagnostics.Add(new(IncompatibleDimensions, $"Mate '{mate.Name}' fit ranges from {min:G6} to {max:G6} {sd.Unit} (nominal {nominal:G6}).", AssemblyDiagnosticSeverity.Warning));
            result.Add(new(mate.StableId, nominal, min, max, sd.Unit, compatible));
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
                step.edge.Unit, step.edge.OriginInstancePath, step.edge.Provenance, step.edge.MateStableId, step.edge.InterfaceStableId)).ToArray();
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
