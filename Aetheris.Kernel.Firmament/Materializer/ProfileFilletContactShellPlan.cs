using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Firmament.Materializer;

/// <summary>Exact local boundary supplied by one analytic fillet component to its parent shell.</summary>
public sealed record ProfileFilletContactBoundary(
    string StableId,
    string ComponentId,
    string SourceStableId,
    ProfileFilletContactBoundaryKind Kind,
    CurveGeometry Curve,
    ParameterInterval Trim,
    bool TraversesWithCurveParameter,
    Point3D Start,
    Point3D End,
    string RegularityEvidence,
    IReadOnlyList<string> Provenance);

public enum ProfileFilletContactBoundaryKind { CapContact, SideContact, PreviousInterface, NextInterface }

/// <summary>
/// Immutable, source-order contract used before a closed Fillet shell allocates
/// any topology.  It deliberately has no face or endpoint-termination ids.
/// </summary>
public sealed record ProfileFilletContactShellPlan(
    ProfileBoundaryChamferTarget Target,
    IReadOnlyList<ProfileFilletComponentContactContract> OrderedComponents,
    IReadOnlyList<ProfileFilletContactBoundary> OrderedInterfaces,
    IReadOnlyList<ProfileFilletContactBoundary> OrderedCapContacts,
    IReadOnlyDictionary<string, IReadOnlyList<ProfileFilletContactBoundary>> SideContactsBySource,
    IReadOnlyList<string> Provenance)
{
    /// <summary>
    /// Parent-side ownership is deliberately separate from the component contact
    /// list.  A sharp component can therefore contribute more than one boundary
    /// to one surviving source side without asking an emitter to rediscover it.
    /// </summary>
    public IReadOnlyList<ProfileFilletSideContactChain> SideContactChains { get; init; } = [];
    public IReadOnlyList<ProfileFilletContactEdgeIncidenceContract> ContactEdgeIncidence { get; init; } = [];
    public IReadOnlyList<ProfileFilletContactVertexIncidence> ContactVertexIncidence { get; init; } = [];
    public IReadOnlyList<ProfileFilletSourceSideTrimPlan> SourceSideTrims { get; init; } = [];
}

public sealed record ProfileFilletComponentContactContract(
    string ComponentId,
    string SourceStableId,
    ProfileEdgeFinishSurfaceFamily SurfaceFamily,
    ProfileFilletContactBoundary CapContact,
    IReadOnlyList<ProfileFilletContactBoundary> SideContacts,
    ProfileFilletContactBoundary PreviousInterface,
    ProfileFilletContactBoundary NextInterface,
    IReadOnlyList<string> SemanticDescendants,
    IReadOnlyList<string> Provenance);

/// <summary>Semantic role of a parent-side boundary contributed by a fillet component.</summary>
public enum ProfileFilletSideContactRole
{
    RollSideContact,
    JunctionSupportContact,
    JunctionSideContact,
    RetainedSharpContact,
    TransitionContact
}

/// <summary>
/// One item in an ordered parent-side chain.  Edge and vertex contacts are
/// distinct so a collapsed reflex notch cannot be smuggled through as a
/// zero-length curve.
/// </summary>
public abstract record ProfileFilletSideContactElement(
    string StableId,
    string ComponentId,
    string SourceSideId,
    ProfileFilletSideContactRole Role,
    string ExpectedOppositeFaceOwner,
    int Position,
    string? PredecessorId,
    string? SuccessorId,
    IReadOnlyList<string> Provenance);

public sealed record ProfileFilletSideContactEdge(
    string StableId,
    string ComponentId,
    string SourceSideId,
    ProfileFilletSideContactRole Role,
    CurveGeometry Curve,
    ParameterInterval Trim,
    string StartVertexId,
    string EndVertexId,
    bool TraversesWithCurveParameter,
    string ExpectedOppositeFaceOwner,
    int Position,
    string? PredecessorId,
    string? SuccessorId,
    IReadOnlyList<string> Provenance)
    : ProfileFilletSideContactElement(StableId, ComponentId, SourceSideId, Role,
        ExpectedOppositeFaceOwner, Position, PredecessorId, SuccessorId, Provenance);

public sealed record ProfileFilletSideContactVertex(
    string StableId,
    string ComponentId,
    string SourceSideId,
    ProfileFilletSideContactRole Role,
    string VertexId,
    string ExpectedOppositeFaceOwner,
    int Position,
    string? PredecessorId,
    string? SuccessorId,
    IReadOnlyList<string> Provenance)
    : ProfileFilletSideContactElement(StableId, ComponentId, SourceSideId, Role,
        ExpectedOppositeFaceOwner, Position, PredecessorId, SuccessorId, Provenance);

/// <summary>Source-order upper boundary of one parent-owned source-side fragment group.</summary>
public sealed record ProfileFilletSideContactChain(
    string SourceSideId,
    string StartVertexId,
    string EndVertexId,
    bool TraversesWithSourceOrder,
    IReadOnlyList<ProfileFilletSideContactElement> OrderedContacts,
    IReadOnlyList<string> Provenance);

/// <summary>One planned use of a contact edge by a face loop.</summary>
public sealed record ProfileFilletContactFaceUse(string FaceOwner, bool TraversesWithCurveParameter);

/// <summary>
/// Allocated before B-rep emission.  Both intended face uses refer to this one
/// identity; independent emitters are never allowed to allocate coincident
/// support edges.
/// </summary>
public sealed record ProfileFilletContactEdgeIncidenceContract(
    string EdgeId,
    CurveGeometry Curve,
    ParameterInterval Trim,
    string StartVertexId,
    string EndVertexId,
    ProfileFilletContactFaceUse FaceUseA,
    ProfileFilletContactFaceUse FaceUseB,
    ProfileFilletSideContactRole Role,
    IReadOnlyList<string> Provenance);

/// <summary>Explicit non-welded vertex incidence, including point-only contacts.</summary>
public sealed record ProfileFilletContactVertexIncidence(
    string VertexId,
    Point3D Point,
    IReadOnlyList<string> IncidentEdgeIds,
    IReadOnlyList<string> IncidentFaceOwners,
    string SourceSemanticIdentity,
    IReadOnlyList<string> Provenance);

/// <summary>
/// A source segment can materialize as several planar/analytic side fragments
/// while retaining the one source-side semantic owner.
/// </summary>
public sealed record ProfileFilletSideFaceFragmentPlan(
    string StableId,
    string SourceSideId,
    int FragmentIndex,
    string SupportSurface,
    IReadOnlyList<string> OrderedBoundaryEdgeIds,
    IReadOnlyList<string> SemanticDescendants,
    IReadOnlyList<string> Provenance);

public sealed record ProfileFilletSourceSideTrimPlan(
    string SourceSideId,
    IReadOnlyList<ProfileFilletSideFaceFragmentPlan> Fragments,
    IReadOnlyList<string> SharedContactEdgeIds,
    IReadOnlyList<string> Provenance);

public sealed record ProfileFilletContactGraphValidationResult(bool Succeeded, IReadOnlyList<string> Diagnostics);

public sealed record ProfileFilletSideContactExtraction(
    IReadOnlyList<ProfileFilletSideContactChain> Chains,
    IReadOnlyList<ProfileFilletContactEdgeIncidenceContract> EdgeIncidence,
    IReadOnlyList<ProfileFilletContactVertexIncidence> VertexIncidence);

public sealed record ProfileFilletContactShellPlanResult(bool Succeeded, ProfileFilletContactShellPlan? Plan, IReadOnlyList<string> Diagnostics);

/// <summary>
/// Validates topology ownership while names are still planned identities.  This
/// is intentionally stricter than the eventual manifold gate: the latter can
/// say only that an emitted edge is bad, whereas this identifies the missing
/// side/junction owner that caused it.
/// </summary>
public static class ProfileFilletContactGraphValidator
{
    public static ProfileFilletContactGraphValidationResult Validate(ProfileFilletContactShellPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var diagnostics = new List<string>();
        var contacts = plan.SideContactChains.SelectMany(chain => chain.OrderedContacts).ToArray();
        var contactEdges = contacts.OfType<ProfileFilletSideContactEdge>().ToArray();
        var edgeContracts = plan.ContactEdgeIncidence
            .GroupBy(contract => contract.EdgeId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        foreach (var chain in plan.SideContactChains)
        {
            if (chain.OrderedContacts.Count == 0)
            {
                diagnostics.Add($"ProfileFilletSideContactChainOpen:source={chain.SourceSideId}:reason=empty");
                continue;
            }

            for (var index = 0; index < chain.OrderedContacts.Count; index++)
            {
                var contact = chain.OrderedContacts[index];
                var predecessor = index == 0 ? null : chain.OrderedContacts[index - 1].StableId;
                var successor = index == chain.OrderedContacts.Count - 1 ? null : chain.OrderedContacts[index + 1].StableId;
                if (contact.Position != index || contact.PredecessorId != predecessor || contact.SuccessorId != successor)
                    diagnostics.Add($"ProfileFilletSideContactChainOutOfOrder:source={chain.SourceSideId}:contact={contact.StableId}");
            }

            var first = chain.OrderedContacts[0];
            var last = chain.OrderedContacts[^1];
            if (first is ProfileFilletSideContactEdge firstEdge && firstEdge.StartVertexId != chain.StartVertexId)
                diagnostics.Add($"ProfileFilletSideContactChainOpen:source={chain.SourceSideId}:contact={first.StableId}:at=start");
            if (last is ProfileFilletSideContactEdge lastEdge && lastEdge.EndVertexId != chain.EndVertexId)
                diagnostics.Add($"ProfileFilletSideContactChainOpen:source={chain.SourceSideId}:contact={last.StableId}:at=end");
            if (first is ProfileFilletSideContactVertex firstVertex && firstVertex.VertexId != chain.StartVertexId)
                diagnostics.Add($"ProfileFilletSideContactChainOpen:source={chain.SourceSideId}:contact={first.StableId}:at=start");
            if (last is ProfileFilletSideContactVertex lastVertex && lastVertex.VertexId != chain.EndVertexId)
                diagnostics.Add($"ProfileFilletSideContactChainOpen:source={chain.SourceSideId}:contact={last.StableId}:at=end");

            for (var index = 0; index < chain.OrderedContacts.Count - 1; index++)
            {
                var left = EndVertex(chain.OrderedContacts[index]);
                var right = StartVertex(chain.OrderedContacts[index + 1]);
                if (left != right)
                    diagnostics.Add($"ProfileFilletSideContactChainOpen:source={chain.SourceSideId}:between={chain.OrderedContacts[index].StableId}->{chain.OrderedContacts[index + 1].StableId}");
            }
        }

        foreach (var edge in contactEdges)
        {
            if (edge.StartVertexId == edge.EndVertexId)
                diagnostics.Add($"ProfileFilletDuplicateSupportEdge:edge={edge.StableId}:reason=zero-length-edge-contact");
            if (!edgeContracts.TryGetValue(edge.StableId, out var contract))
                diagnostics.Add($"ProfileFilletContactEdgeMissingSecondFace:edge={edge.StableId}:reason=no-incidence-contract");
            else if (contract.StartVertexId != edge.StartVertexId || contract.EndVertexId != edge.EndVertexId)
                diagnostics.Add($"ProfileFilletSideContactChainOutOfOrder:source={edge.SourceSideId}:contact={edge.StableId}:reason=contract-endpoints");
        }

        foreach (var contract in plan.ContactEdgeIncidence)
        {
            if (contract.StartVertexId == contract.EndVertexId)
                diagnostics.Add($"ProfileFilletDuplicateSupportEdge:edge={contract.EdgeId}:reason=zero-length-edge-contract");
            if (contract.FaceUseA.FaceOwner == contract.FaceUseB.FaceOwner)
                diagnostics.Add($"ProfileFilletContactEdgeOverSubscribed:edge={contract.EdgeId}:reason=non-distinct-face-owners");
            if (contract.FaceUseA.TraversesWithCurveParameter == contract.FaceUseB.TraversesWithCurveParameter)
                diagnostics.Add($"ProfileFilletContactOrientationConflict:edge={contract.EdgeId}");
        }

        foreach (var duplicate in plan.ContactEdgeIncidence.GroupBy(contract => contract.EdgeId, StringComparer.Ordinal).Where(group => group.Count() != 1))
            diagnostics.Add($"ProfileFilletContactEdgeOverSubscribed:edge={duplicate.Key}:reason=duplicate-edge-id");

        foreach (var duplicate in contactEdges
                     .Where(edge => edge.Role == ProfileFilletSideContactRole.JunctionSupportContact)
                     .GroupBy(edge => (edge.SourceSideId, A: string.CompareOrdinal(edge.StartVertexId, edge.EndVertexId) < 0 ? edge.StartVertexId : edge.EndVertexId, B: string.CompareOrdinal(edge.StartVertexId, edge.EndVertexId) < 0 ? edge.EndVertexId : edge.StartVertexId))
                     .Where(group => group.Count() > 1))
            diagnostics.Add($"ProfileFilletDuplicateSupportEdge:source={duplicate.Key.SourceSideId}:vertices={duplicate.Key.A}->{duplicate.Key.B}");

        var knownEdges = plan.ContactEdgeIncidence.Select(contract => contract.EdgeId).ToHashSet(StringComparer.Ordinal);
        foreach (var vertex in plan.ContactVertexIncidence)
            foreach (var edgeId in vertex.IncidentEdgeIds)
                if (!knownEdges.Contains(edgeId))
                    diagnostics.Add($"ProfileFilletContactEdgeMissingSecondFace:edge={edgeId}:vertex={vertex.VertexId}:reason=vertex-references-unknown-edge");

        return new(diagnostics.Count == 0, diagnostics);
    }

    private static string StartVertex(ProfileFilletSideContactElement contact) => contact switch
    {
        ProfileFilletSideContactEdge edge => edge.StartVertexId,
        ProfileFilletSideContactVertex vertex => vertex.VertexId,
        _ => throw new NotSupportedException($"Unknown contact type {contact.GetType().Name}.")
    };

    private static string EndVertex(ProfileFilletSideContactElement contact) => contact switch
    {
        ProfileFilletSideContactEdge edge => edge.EndVertexId,
        ProfileFilletSideContactVertex vertex => vertex.VertexId,
        _ => throw new NotSupportedException($"Unknown contact type {contact.GetType().Name}.")
    };
}

/// <summary>Extracts the reusable M1 roll-side contact before any face is emitted.</summary>
public static class ProfileFilletSideContactExtractor
{
    public static (ProfileFilletSideContactChain Chain, ProfileFilletContactEdgeIncidenceContract Incidence, IReadOnlyList<ProfileFilletContactVertexIncidence> Vertices)
        ExtractStraightRoll(StraightFilletRollComponent roll)
    {
        ArgumentNullException.ThrowIfNull(roll);
        var startVertex = $"{roll.StableId}:side:start";
        var endVertex = $"{roll.StableId}:side:end";
        var edgeId = $"{roll.StableId}:side-contact";
        var length = (roll.SideContactEnd - roll.SideContactStart).Length;
        if (length <= 1e-8) throw new InvalidOperationException($"ProfileFilletSideContactZeroLength:{roll.StableId}");
        var curve = CurveGeometry.FromLine(new Line3Curve(roll.SideContactStart, Direction3D.Create(roll.SideContactEnd - roll.SideContactStart)));
        var sourceFace = $"source-side:{roll.SourceStableId}:fragment:0";
        var rollFace = $"fillet-component:{roll.StableId}";
        var edge = new ProfileFilletSideContactEdge(edgeId, roll.StableId, roll.SourceStableId,
            ProfileFilletSideContactRole.RollSideContact, curve, new ParameterInterval(0d, length), startVertex, endVertex, true,
            rollFace, 0, null, null, ["StraightFilletRollComponent", "ExactLine", "ParentSide"]);
        var chain = new ProfileFilletSideContactChain(roll.SourceStableId, startVertex, endVertex, true, [edge], ["M1", "SourceOrder"]);
        var incidence = new ProfileFilletContactEdgeIncidenceContract(edgeId, curve, edge.Trim, startVertex, endVertex,
            new ProfileFilletContactFaceUse(sourceFace, true), new ProfileFilletContactFaceUse(rollFace, false), edge.Role,
            ["PreallocatedSharedContactEdge", "CylinderSideContact"]);
        return (chain, incidence,
        [
            new(startVertex, roll.SideContactStart, [edgeId], [sourceFace, rollFace], roll.SourceStableId, ["RollSideStart"]),
            new(endVertex, roll.SideContactEnd, [edgeId], [sourceFace, rollFace], roll.SourceStableId, ["RollSideEnd"])
        ]);
    }

    /// <summary>
    /// Extracts the two real M2 source-side chains from the already-proven
    /// roll/sphere/support topology.  The support endpoint is the retained
    /// vertical-depth vertex, not a source-offset guess.
    /// </summary>
    public static ProfileFilletSideContactExtraction ExtractConvexSharp(ProfileFilletShellPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.Junction is not ProfileConvexSphericalJunctionPlan junction || plan.Components is not [StraightFilletRollComponent rollA, ConvexSharpFilletJunctionComponent component, StraightFilletRollComponent rollB])
            throw new ArgumentException("ProfileFilletConvexSharpComponentsRequired", nameof(plan));

        var sideA = rollA.SideContactEnd;
        var sideB = rollB.SideContactStart;
        // BuildSphericalJunction defines sideA = vertex + nb*r + axial*r and
        // sideB = vertex + na*r + axial*r.  Their sum minus the sphere centre
        // is therefore exactly the support/depth vertex.
        var depth = sideA + (sideB - junction.Center);
        var depthId = $"{component.StableId}:vertical-depth";
        var aExternalId = $"{rollA.StableId}:side:external";
        var aJunctionId = $"{rollA.StableId}:side:junction";
        var bJunctionId = $"{rollB.StableId}:side:junction";
        var bExternalId = $"{rollB.StableId}:side:external";
        var supportFace = $"junction-support:{component.StableId}";
        var rollAEdge = Edge($"{rollA.StableId}:side-contact", rollA, aExternalId, aJunctionId, 0, null, $"{component.StableId}:side-a-support", ProfileFilletSideContactRole.RollSideContact, true);
        var supportA = Edge($"{component.StableId}:side-a-support", component.StableId, rollA.SourceStableId, sideA, depth, aJunctionId, depthId, 1, rollAEdge.StableId, null, ProfileFilletSideContactRole.JunctionSupportContact, supportFace, true);
        var supportB = Edge($"{component.StableId}:side-b-support", component.StableId, rollB.SourceStableId, depth, sideB, depthId, bJunctionId, 0, null, $"{rollB.StableId}:side-contact", ProfileFilletSideContactRole.JunctionSupportContact, supportFace, true);
        var rollBEdge = Edge($"{rollB.StableId}:side-contact", rollB, bJunctionId, bExternalId, 1, supportB.StableId, null, ProfileFilletSideContactRole.RollSideContact, true);
        var chainA = new ProfileFilletSideContactChain(rollA.SourceStableId, aExternalId, depthId, true, [rollAEdge, supportA], ["M2", "ConvexSphere", "SourceOrder"]);
        var chainB = new ProfileFilletSideContactChain(rollB.SourceStableId, depthId, bExternalId, true, [supportB, rollBEdge], ["M2", "ConvexSphere", "SourceOrder"]);
        var sourceA = SourceFace(rollA); var sourceB = SourceFace(rollB);
        return new([chainA, chainB],
        [
            Incidence(rollAEdge, sourceA, RollFace(rollA)),
            Incidence(supportA, sourceA, supportFace),
            Incidence(supportB, sourceB, supportFace),
            Incidence(rollBEdge, sourceB, RollFace(rollB))
        ],
        [
            Vertex(aExternalId, rollA.SideContactStart, [rollAEdge.StableId], rollA.SourceStableId),
            Vertex(aJunctionId, sideA, [rollAEdge.StableId, supportA.StableId], component.StableId),
            Vertex(depthId, depth, [supportA.StableId, supportB.StableId], component.StableId),
            Vertex(bJunctionId, sideB, [supportB.StableId, rollBEdge.StableId], component.StableId),
            Vertex(bExternalId, rollB.SideContactEnd, [rollBEdge.StableId], rollB.SourceStableId)
        ]);
    }

    /// <summary>
    /// Extracts M3 ExactRolling.  The shared notch is represented twice as a
    /// chain endpoint but once as a planned vertex; no support edge is created.
    /// </summary>
    public static ProfileFilletSideContactExtraction ExtractExactRollingReflex(ProfileFilletShellPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.Junction is not ProfileReflexFilletJunctionPlan junction || plan.Components is not [StraightFilletRollComponent rollA, ReflexSharpExactRollingJunctionComponent component, StraightFilletRollComponent rollB])
            throw new ArgumentException("ProfileFilletExactRollingComponentsRequired", nameof(plan));

        var notchId = $"{component.StableId}:notch";
        var aExternalId = $"{rollA.StableId}:side:external";
        var bExternalId = $"{rollB.StableId}:side:external";
        var componentFace = $"fillet-component:{component.StableId}";
        var rollAEdge = Edge($"{rollA.StableId}:side-contact", rollA, aExternalId, notchId, 0, null, $"{component.StableId}:notch:a", ProfileFilletSideContactRole.RollSideContact, true);
        var notchA = new ProfileFilletSideContactVertex($"{component.StableId}:notch:a", component.StableId, rollA.SourceStableId,
            ProfileFilletSideContactRole.JunctionSideContact, notchId, componentFace, 1, rollAEdge.StableId, null, ["M3", "PointContact"]);
        var notchB = new ProfileFilletSideContactVertex($"{component.StableId}:notch:b", component.StableId, rollB.SourceStableId,
            ProfileFilletSideContactRole.JunctionSideContact, notchId, componentFace, 0, null, $"{rollB.StableId}:side-contact", ["M3", "PointContact"]);
        var rollBEdge = Edge($"{rollB.StableId}:side-contact", rollB, notchId, bExternalId, 1, notchB.StableId, null, ProfileFilletSideContactRole.RollSideContact, true);
        var chainA = new ProfileFilletSideContactChain(rollA.SourceStableId, aExternalId, notchId, true, [rollAEdge, notchA], ["M3", "ExactRolling", "PointNotEdge"]);
        var chainB = new ProfileFilletSideContactChain(rollB.SourceStableId, notchId, bExternalId, true, [notchB, rollBEdge], ["M3", "ExactRolling", "PointNotEdge"]);
        return new([chainA, chainB],
        [Incidence(rollAEdge, SourceFace(rollA), RollFace(rollA)), Incidence(rollBEdge, SourceFace(rollB), RollFace(rollB))],
        [
            Vertex(aExternalId, rollA.SideContactStart, [rollAEdge.StableId], rollA.SourceStableId),
            Vertex(notchId, junction.VerticalNotchContact, [rollAEdge.StableId, rollBEdge.StableId], component.StableId),
            Vertex(bExternalId, rollB.SideContactEnd, [rollBEdge.StableId], rollB.SourceStableId)
        ]);
    }

    private static ProfileFilletSideContactEdge Edge(string edgeId, StraightFilletRollComponent roll, string startVertex, string endVertex,
        int position, string? predecessor, string? successor, ProfileFilletSideContactRole role, bool traverses) =>
        Edge(edgeId, roll.StableId, roll.SourceStableId, roll.SideContactStart, roll.SideContactEnd, startVertex, endVertex,
            position, predecessor, successor, role, RollFace(roll), traverses);

    private static ProfileFilletSideContactEdge Edge(string edgeId, string componentId, string sourceSideId, Point3D start, Point3D end,
        string startVertex, string endVertex, int position, string? predecessor, string? successor, ProfileFilletSideContactRole role,
        string oppositeFace, bool traverses)
    {
        var length = (end - start).Length;
        if (length <= 1e-8) throw new InvalidOperationException($"ProfileFilletSideContactZeroLength:{edgeId}");
        return new(edgeId, componentId, sourceSideId, role,
            CurveGeometry.FromLine(new Line3Curve(start, Direction3D.Create(end - start))), new ParameterInterval(0d, length),
            startVertex, endVertex, traverses, oppositeFace, position, predecessor, successor, ["ExactLine", "PreEmissionContact"]);
    }

    private static ProfileFilletContactEdgeIncidenceContract Incidence(ProfileFilletSideContactEdge edge, string sourceFace, string oppositeFace) =>
        new(edge.StableId, edge.Curve, edge.Trim, edge.StartVertexId, edge.EndVertexId,
            new(sourceFace, edge.TraversesWithCurveParameter), new(oppositeFace, !edge.TraversesWithCurveParameter), edge.Role,
            ["PreallocatedSharedContactEdge"]);

    private static ProfileFilletContactVertexIncidence Vertex(string id, Point3D point, IReadOnlyList<string> edges, string source) =>
        new(id, point, edges, [], source, ["PreEmissionVertexIncidence"]);

    private static string SourceFace(StraightFilletRollComponent roll) => $"source-side:{roll.SourceStableId}:fragment:0";
    private static string RollFace(StraightFilletRollComponent roll) => $"fillet-component:{roll.StableId}";
}

/// <summary>
/// Contact-only planner for the rounded, source-tangent portion of a whole
/// Profile.  Sharp vertices are intentionally rejected here until their M2/M3
/// components supply the displaced cap/side boundaries; this prevents a future
/// emitter from silently substituting a naïve source offset at those vertices.
/// </summary>
public static class ProfileFilletContactShellPlanner
{
    private const double Tolerance = 1e-8;

    public static ProfileFilletContactShellPlanResult TryPlan(
        ResolvedProfile2D profile, ProfileBoundaryChamferTarget target, ProfileEdgeFinishMixedShellPlan mixed)
    {
        ProfileFilletContactShellPlanResult Fail(string diagnostic) => new(false, null, [diagnostic]);
        if (mixed.FinishKind != ProfileEdgeFinishKind.Fillet) return Fail("ProfileFilletContactShellFilletPlanRequired");
        var loop = profile.Loops.SingleOrDefault(x => x.Name == target.LoopId);
        if (loop is null || target.ChainKind != ProfileBoundaryChamferChainKind.ClosedLoop) return Fail("ProfileFilletContactShellClosedLoopRequired");
        if (loop.Segments.Count != mixed.OrderedPatches.Count) return Fail("ProfileFilletContactShellProfileMismatch");
        var area = SignedArea(loop);
        if (Math.Abs(area) <= Tolerance) return Fail("ProfileFilletContactShellProfileDegenerate");

        // The extracted M2/M3 components own displaced contacts at sharp
        // line/line vertices.  Do not let a rounded-source planner invent them.
        for (var i = 0; i < loop.Segments.Count; i++)
        {
            var previous = loop.Segments[(i + loop.Segments.Count - 1) % loop.Segments.Count].Geometry;
            var current = loop.Segments[i].Geometry;
            if (previous is LineArcLineSegment2D && current is LineArcLineSegment2D)
                return Fail($"ProfileFilletContactSharpJunctionComponentRequired:vertex={loop.Name}.{loop.Segments[i].Name}.Start");
        }

        var frame = profile.EffectiveConstructionPlane;
        var cap = profile.LocalEndDepth ?? 1d;
        var transition = cap - mixed.FinishSize;
        var components = new List<ProfileFilletComponentContactContract>(loop.Segments.Count);
        var interfaces = new List<ProfileFilletContactBoundary>(loop.Segments.Count);
        var caps = new List<ProfileFilletContactBoundary>(loop.Segments.Count);
        var sides = new Dictionary<string, IReadOnlyList<ProfileFilletContactBoundary>>(StringComparer.Ordinal);
        var sideChains = new List<ProfileFilletSideContactChain>(loop.Segments.Count);
        var incidence = new List<ProfileFilletContactEdgeIncidenceContract>(loop.Segments.Count);
        var vertices = new List<ProfileFilletContactVertexIncidence>(loop.Segments.Count * 2);
        var sideTrims = new List<ProfileFilletSourceSideTrimPlan>(loop.Segments.Count);

        for (var i = 0; i < loop.Segments.Count; i++)
        {
            var segment = loop.Segments[i];
            var patch = mixed.OrderedPatches[i];
            var source = segment.Provenance.StableId;
            var componentId = patch.StableId;
            var (sourceStart, sourceEnd) = Ends(segment.Geometry);
            var start = frame.ToWorld(sourceStart, transition);
            var end = frame.ToWorld(sourceEnd, transition);
            var capCurve = CapCurve(segment.Geometry, area, mixed.FinishSize, cap, frame, out var capStart, out var capEnd);
            var capContact = new ProfileFilletContactBoundary($"{componentId}:cap", componentId, source, ProfileFilletContactBoundaryKind.CapContact,
                capCurve, Trim(segment.Geometry), Oriented(segment.Geometry), capStart, capEnd, patch.Regularity.ToString(), ["SourceOrder", "ParentCap"]);
            var side = new ProfileFilletContactBoundary($"{componentId}:side", componentId, source, ProfileFilletContactBoundaryKind.SideContact,
                Curve(segment.Geometry, transition, frame, start, end), Trim(segment.Geometry), Oriented(segment.Geometry), start, end, patch.Regularity.ToString(), ["SourceOrder", "ParentSide"]);
            var previous = Interface(componentId, source, ProfileFilletContactBoundaryKind.PreviousInterface, start, capStart, patch, mixed.FinishSize);
            var next = Interface(componentId, source, ProfileFilletContactBoundaryKind.NextInterface, capEnd, end, patch, mixed.FinishSize);
            components.Add(new(componentId, source, patch.SurfaceFamily, capContact, [side], previous, next, patch.SemanticDescendants, [patch.PlannerKind, "ContactContract"]));
            caps.Add(capContact); interfaces.Add(previous); interfaces.Add(next); sides.Add(source, [side]);
            var startVertexId = $"{side.StableId}:start";
            var endVertexId = $"{side.StableId}:end";
            var sourceFace = $"source-side:{source}:fragment:0";
            var componentFace = $"fillet-component:{componentId}";
            var sideEdge = new ProfileFilletSideContactEdge(side.StableId, componentId, source,
                ProfileFilletSideContactRole.RollSideContact, side.Curve, side.Trim, startVertexId, endVertexId,
                side.TraversesWithCurveParameter, componentFace, 0, null, null,
                ["RoundedSourceContact", "ParentSide", .. side.Provenance]);
            sideChains.Add(new(source, startVertexId, endVertexId, side.TraversesWithCurveParameter, [sideEdge], ["SourceOrder", "SingleContact"]));
            incidence.Add(new(side.StableId, side.Curve, side.Trim, startVertexId, endVertexId,
                new(sourceFace, side.TraversesWithCurveParameter), new(componentFace, !side.TraversesWithCurveParameter), sideEdge.Role,
                ["PreallocatedSharedContactEdge", "RoundedSourceContact"]));
            vertices.Add(new(startVertexId, side.Start, [side.StableId], [sourceFace, componentFace], source, ["SideContactStart"]));
            vertices.Add(new(endVertexId, side.End, [side.StableId], [sourceFace, componentFace], source, ["SideContactEnd"]));
            sideTrims.Add(new(source,
                [new ProfileFilletSideFaceFragmentPlan($"{source}:fragment:0", source, 0, "SourceSideSupportSurface", [side.StableId],
                    [$"SideFaceFragment({source},0)"], ["RoundedSourceContact"])],
                [side.StableId], ["ParentOwnedSide"]));
        }

        var plan = new ProfileFilletContactShellPlan(target, components, interfaces, caps, sides,
            ["ResolvedProfile2D", "ProfileEdgeFinishMixedShellPlan", "ContactBeforeTopology"])
        {
            SideContactChains = sideChains,
            ContactEdgeIncidence = incidence,
            ContactVertexIncidence = vertices,
            SourceSideTrims = sideTrims
        };
        var validation = ProfileFilletContactGraphValidator.Validate(plan);
        return validation.Succeeded
            ? new(true, plan, [])
            : new(false, null, validation.Diagnostics);
    }

    private static ProfileFilletContactBoundary Interface(string componentId, string source, ProfileFilletContactBoundaryKind kind,
        Point3D start, Point3D end, AnalyticEdgeFinishPatch patch, double radius)
    {
        var centre = start + (end - start) * .5d;
        var radial = start - centre;
        var normal = radial.Cross(end - centre);
        if (normal.Length <= Tolerance) normal = new Vector3D(0d, 0d, 1d);
        return new($"{componentId}:{kind}", componentId, source, kind,
            CurveGeometry.FromCircle(new Circle3Curve(centre, Direction3D.Create(normal), radius, Direction3D.Create(radial))),
            new ParameterInterval(0d, Math.PI / 2d), true, start, end, patch.Regularity.ToString(), ["ExactRollingMeridian", "SourceTangency"]);
    }

    private static CurveGeometry CapCurve(LineArcProfileCurve2D curve, double area, double distance, double depth, ConstructionPlane frame, out Point3D start, out Point3D end)
    {
        LineArcProfileCurve2D? inset = curve switch
        {
            LineArcLineSegment2D line => OffsetLine(line, area, distance),
            LineArcCircularArc2D arc => OffsetArc(arc, area, distance),
            _ => throw new NotSupportedException()
        };
        if (inset is null) throw new InvalidOperationException("ProfileFilletContactSphereLimitRequiresExplicitApexComponent");
        var ends = Ends(inset); start = frame.ToWorld(ends.Start, depth); end = frame.ToWorld(ends.End, depth);
        return Curve(inset, depth, frame, start, end);
    }

    private static LineArcLineSegment2D OffsetLine(LineArcLineSegment2D line, double area, double distance)
    {
        var dx = line.End.X - line.Start.X; var dy = line.End.Y - line.Start.Y; var length = Math.Sqrt(dx * dx + dy * dy);
        var nx = area > 0d ? -dy / length : dy / length; var ny = area > 0d ? dx / length : -dx / length;
        return new((line.Start.X + nx * distance, line.Start.Y + ny * distance), (line.End.X + nx * distance, line.End.Y + ny * distance));
    }
    private static LineArcCircularArc2D? OffsetArc(LineArcCircularArc2D arc, double area, double distance)
    {
        var convex = Math.Sign(arc.SweepAngleRadians) * Math.Sign(area) >= 0d;
        var radius = convex ? arc.Radius - distance : arc.Radius + distance;
        return radius <= Tolerance ? null : new(arc.Center, radius, arc.StartAngleRadians, arc.SweepAngleRadians);
    }
    private static ((double X, double Y) Start, (double X, double Y) End) Ends(LineArcProfileCurve2D curve) => curve switch
    {
        LineArcLineSegment2D line => (line.Start, line.End),
        LineArcCircularArc2D arc => ((arc.Center.X + arc.Radius * Math.Cos(arc.StartAngleRadians), arc.Center.Y + arc.Radius * Math.Sin(arc.StartAngleRadians)),
            (arc.Center.X + arc.Radius * Math.Cos(arc.StartAngleRadians + arc.SweepAngleRadians), arc.Center.Y + arc.Radius * Math.Sin(arc.StartAngleRadians + arc.SweepAngleRadians))),
        _ => throw new NotSupportedException()
    };
    private static CurveGeometry Curve(LineArcProfileCurve2D curve, double depth, ConstructionPlane frame, Point3D start, Point3D end) => curve switch
    {
        LineArcLineSegment2D => CurveGeometry.FromLine(new Line3Curve(start, Direction3D.Create(end - start))),
        LineArcCircularArc2D arc => CurveGeometry.FromCircle(new Circle3Curve(frame.ToWorld(arc.Center, depth), frame.AxisZ, arc.Radius, frame.AxisX)),
        _ => throw new NotSupportedException()
    };
    private static ParameterInterval Trim(LineArcProfileCurve2D curve) => curve switch
    {
        LineArcLineSegment2D line => new(0d, Math.Sqrt(Math.Pow(line.End.X - line.Start.X, 2d) + Math.Pow(line.End.Y - line.Start.Y, 2d))),
        LineArcCircularArc2D arc => new(Math.Min(arc.StartAngleRadians, arc.StartAngleRadians + arc.SweepAngleRadians), Math.Max(arc.StartAngleRadians, arc.StartAngleRadians + arc.SweepAngleRadians)),
        _ => throw new NotSupportedException()
    };
    private static bool Oriented(LineArcProfileCurve2D curve) => curve is not LineArcCircularArc2D arc || arc.SweepAngleRadians >= 0d;
    private static double SignedArea(ResolvedProfileLoop2D loop) => loop.Segments.Sum(x => x.Geometry switch
    {
        LineArcLineSegment2D line => line.Start.X * line.End.Y - line.End.X * line.Start.Y,
        LineArcCircularArc2D arc => 2d * (arc.Center.X * arc.Radius * (Math.Sin(arc.StartAngleRadians + arc.SweepAngleRadians) - Math.Sin(arc.StartAngleRadians)) - arc.Center.Y * arc.Radius * (Math.Cos(arc.StartAngleRadians + arc.SweepAngleRadians) - Math.Cos(arc.StartAngleRadians)) + arc.Radius * arc.Radius * arc.SweepAngleRadians),
        _ => 0d
    }) * .5d;
}
