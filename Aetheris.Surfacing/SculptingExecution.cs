using System.Security.Cryptography;
using System.Text;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Surfacing;

public static class SculptedHousingFactory
{
    public const string CrownRegion = "HousingCrown";
    public const string TransitionZone = "CrownTransitionZone";
    public const string BottomMountingInterface = "BottomMountingInterface";
    public const string MountingHolePattern = "MountingHolePattern";
    public const string OuterFootprintBoundary = "OuterFootprintBoundary";
    public const string SideWallsLower = "SideWallsLower";
    public const string CrownBoundarySouth = "CrownBoundarySouth";
    public const string CrownBoundaryEast = "CrownBoundaryEast";
    public const string CrownBoundaryNorth = "CrownBoundaryNorth";
    public const string CrownBoundaryWest = "CrownBoundaryWest";
    public const string HousingSideEast = "HousingSideEast";
    public const string HousingSideWest = "HousingSideWest";

    public static SculptResult CreateBase(string stateName, double width, double depth, double height, IReadOnlyList<HousingHole> holes)
    {
        var construction = new HousingConstruction(width, depth, height, width, depth, 0d, holes);
        var built = SculptedHousingBrepBuilder.Build(construction);
        if (built.Body is null) return SculptResult.Failure(built.Diagnostics);
        var canonical = $"BodyState0|{width:R}|{depth:R}|{height:R}|{string.Join(';', holes.OrderBy(x => x.StableId, StringComparer.Ordinal).Select(HoleFingerprint))}";
        var stateId = BodyStateId.Derive(canonical);
        var inventory = Inventory(construction);
        var evidence = ValidateBody(built.Body, height);
        if (evidence.Any(x => !x.Satisfied)) return SculptResult.Failure(
            [new("sculpt-base-invalid", "Initial body did not satisfy the body-state validity contract: " + string.Join("; ", evidence.Where(x => !x.Satisfied).Select(x => $"{x.Check}: {x.Detail}")))], evidence);
        var associations = PersistentAssociations(built.Body, construction);
        return new(true, new(stateId, null, "housing-body", stateName, built.Body, construction, inventory, null, evidence,
            associations, SemanticPmi(associations, construction), AssemblyInterfaces(associations), ConstructionAuthority: ConstructionState.FromHousing(construction)), null, evidence, []);
    }

    internal static IReadOnlyDictionary<string, SculptSemanticEntity> Inventory(HousingConstruction c)
    {
        var inventory = new Dictionary<string, SculptSemanticEntity>(StringComparer.Ordinal)
        {
            [CrownRegion] = new(CrownRegion, SculptEntityKind.Region, $"rect:{c.CrownWidth:R}x{c.CrownDepth:R}@z={c.FinalHeight:R}", "Bounded top/crown support."),
            [TransitionZone] = new(TransitionZone, SculptEntityKind.Region, $"transition:z={c.BaseHeight:R}..{c.FinalHeight:R}", "Bounded crown reconnection zone."),
            [BottomMountingInterface] = new(BottomMountingInterface, SculptEntityKind.Interface, $"plane:z=0;rect={c.Width:R}x{c.Depth:R};holes={HolePattern(c.Holes)}", "Protected planar mounting interface."),
            [MountingHolePattern] = new(MountingHolePattern, SculptEntityKind.Pattern, HolePattern(c.Holes), "Protected mounting-hole axes, centers, and diameters."),
            [OuterFootprintBoundary] = new(OuterFootprintBoundary, SculptEntityKind.Region, $"rect:{c.Width:R}x{c.Depth:R}@z=0", "Protected lower outer footprint boundary."),
            [SideWallsLower] = new(SideWallsLower, SculptEntityKind.Region, $"vertical:rect={c.Width:R}x{c.Depth:R};z=0..{c.BaseHeight:R}", "Protected lower side-wall region."),
            [CrownBoundarySouth] = new(CrownBoundarySouth, SculptEntityKind.Region, $"south:{c.CrownWidth:R}@y={-c.CrownDepth / 2d:R};z={c.BaseHeight:R}", "South replacement boundary."),
            [CrownBoundaryEast] = new(CrownBoundaryEast, SculptEntityKind.Region, $"east:{c.CrownDepth:R}@x={c.CrownWidth / 2d:R};z={c.BaseHeight:R}", "East replacement boundary."),
            [CrownBoundaryNorth] = new(CrownBoundaryNorth, SculptEntityKind.Region, $"north:{c.CrownWidth:R}@y={c.CrownDepth / 2d:R};z={c.BaseHeight:R}", "North replacement boundary."),
            [CrownBoundaryWest] = new(CrownBoundaryWest, SculptEntityKind.Region, $"west:{c.CrownDepth:R}@x={-c.CrownWidth / 2d:R};z={c.BaseHeight:R}", "West replacement boundary."),
            [HousingSideEast] = new(HousingSideEast, SculptEntityKind.Region, $"plane:x={c.Width / 2d:R};y={-c.Depth / 2d:R}..{c.Depth / 2d:R};z=0..{c.BaseHeight:R}", "Semantic east housing support region."),
            [HousingSideWest] = new(HousingSideWest, SculptEntityKind.Region, $"plane:x={-c.Width / 2d:R};y={-c.Depth / 2d:R}..{c.Depth / 2d:R};z=0..{c.BaseHeight:R}", "Semantic west housing support region."),
        };
        foreach (var hole in c.Holes) inventory[hole.StableId] = new(hole.StableId, SculptEntityKind.Region, HoleFingerprint(hole), "Stable mounting-hole feature.");
        return inventory;
    }

    internal static IReadOnlyList<SculptValidationEvidence> ValidateBody(BrepBody body, double localityTolerance)
    {
        var preflight = BrepExportPreflight.Validate(body);
        var mass = BrepMassProperties.Evaluate(body);
        var pcurves = BrepPcurveValidator.Validate(body, 1e-5d, requireEveryCoedge: true);
        return
        [
            new("ClosedManifold", preflight.IsValid && mass.IsEnclosed, LocalityEvidenceLevel.CertifiedBounded, null, 1e-6, $"BRep preflight errors={preflight.ErrorCount}; independent edge-incidence enclosure={mass.IsEnclosed}. {string.Join(" | ", preflight.Diagnostics.Where(item => item.Severity == BrepExportPreflightSeverity.Error).Select(item => $"{item.Code}[edge={item.EdgeId}]:{item.Message}"))}"),
            new("OrientationConsistency", mass.IsOrientationConsistent, LocalityEvidenceLevel.CertifiedBounded, null, 1e-9, $"Independent boundary verifier orientationConsistent={mass.IsOrientationConsistent}."),
            new("NoSelfIntersection", preflight.IsValid, LocalityEvidenceLevel.CertifiedBounded, null, 1e-6, "Bounded analytic construction validation plus BRep export preflight; this is not a general-purpose global intersection proof."),
            new("PcurveConsistency", pcurves.IsValid, LocalityEvidenceLevel.CertifiedBounded, pcurves.MaximumReconstructionDeviation, 1e-5d,
                $"Independent reconstruction: edges={pcurves.EdgeCount}, pcurves={pcurves.PcurveCount}, domainValid={pcurves.DomainValid}, orientationConsistent={pcurves.OrientationConsistent}. {string.Join(" | ", pcurves.Diagnostics)}"),
        ];
    }

    internal static IReadOnlyList<PersistentGeometryAssociation> PersistentAssociations(BrepBody body, HousingConstruction construction)
    {
        var bottomCandidates = body.Topology.Faces.Where(face => body.TryGetFaceSurfaceGeometry(face.Id, out var support)
            && support?.Plane is { } plane && Math.Abs(plane.Origin.Z) <= 1e-8d && plane.Normal.ToVector().Z < -1d + 1e-8d).ToArray();
        var maximumLoopCount = bottomCandidates.Select(face => face.LoopIds.Count).DefaultIfEmpty().Max();
        var bottom = bottomCandidates.Where(face => face.LoopIds.Count == maximumLoopCount).Select(face => face.Id.Value).ToArray();
        var cylinders = body.Topology.Faces.Where(face => body.TryGetFaceSurfaceGeometry(face.Id, out var support) && support?.Cylinder is not null)
            .Select(face => face.Id.Value).Order().ToArray();
        return
        [
            new(BottomMountingInterface, PersistentAssociationState.Preserved, bottom, "Explicit protected bottom-plane association."),
            new(MountingHolePattern, PersistentAssociationState.Preserved, cylinders, "Explicit protected cylindrical-face association."),
        ];
    }

    public static PersistentAssociationRemapResult RemapPersistentAssociations(BodyState input, BrepBody outputBody, GeometricDelta delta)
    {
        var remapped = new List<PersistentGeometryAssociation>();
        var diagnostics = new List<SculptDiagnostic>();
        foreach (var association in input.GeometryAssociations ?? [])
        {
            var correspondence = delta.Correspondence.SingleOrDefault(item => item.InputEntity == association.SemanticTarget);
            if (correspondence is null || correspondence.Change != GeometricChangeKind.Preserved)
            {
                diagnostics.Add(new("surf-association-target-removed",
                    $"Association target '{association.SemanticTarget}' has no explicit Preserved correspondence in {delta.OutputState.Value}; semantic rebinding was not guessed.", association.SemanticTarget));
                continue;
            }
            var missing = association.FaceIds.Where(id => !outputBody.Topology.TryGetFace(new(id), out _)).ToArray();
            if (missing.Length > 0)
            {
                diagnostics.Add(new("surf-association-current-geometry-missing",
                    $"Preserved target '{association.SemanticTarget}' references absent current faces: {string.Join(", ", missing)}.", association.SemanticTarget));
                continue;
            }
            remapped.Add(association with
            {
                State = PersistentAssociationState.Preserved,
                Evidence = $"Explicit GeometricDelta Preserved correspondence into {delta.OutputState.Value}; retained current face IDs {string.Join(", ", association.FaceIds)}."
            });
        }
        return new(diagnostics.Count == 0, remapped, diagnostics);
    }

    internal static IReadOnlyList<Step242SemanticPmi> SemanticPmi(IReadOnlyList<PersistentGeometryAssociation> associations, HousingConstruction construction)
    {
        var bottom = associations.Single(item => item.SemanticTarget == BottomMountingInterface).FaceIds;
        var holes = associations.Single(item => item.SemanticTarget == MountingHolePattern).FaceIds;
        var diameter = construction.Holes[0].Diameter;
        return
        [
            new Step242SemanticPmiDatum("DatumA", "plane", "A", BottomMountingInterface) { GeometricFaceIds = bottom },
            new Step242SemanticPmiHole(MountingHolePattern, diameter, construction.BaseHeight, "through", .05d, -.05d, construction.Holes.Count) { GeometricFaceIds = holes },
            new Step242SemanticPmiGeometricTolerance(MountingHolePattern, "HolePatternPosition", "position", .1d, ["A"], construction.Holes.Count) { GeometricFaceIds = holes },
        ];
    }

    internal static IReadOnlyList<SculptAssemblyInterface> AssemblyInterfaces(IReadOnlyList<PersistentGeometryAssociation> associations)
    {
        var bottom = associations.Single(item => item.SemanticTarget == BottomMountingInterface);
        return [new(BottomMountingInterface, bottom.SemanticTarget, bottom.FaceIds, "Protected assembly mounting interface on the bottom planar face.")];
    }

    private static string HolePattern(IEnumerable<HousingHole> holes) => string.Join(';', holes.OrderBy(x => x.StableId, StringComparer.Ordinal).Select(HoleFingerprint));
    private static string HoleFingerprint(HousingHole h) => $"{h.StableId}@{h.CenterX:R},{h.CenterY:R}:d={h.Diameter:R}";
}

public static class OffsetRegionSculptor
{
    private const double LocalityTolerance = 1e-6;

    public static SculptResult Apply(BodyState input, string outputName, OffsetRegionOperation operation)
    {
        ArgumentNullException.ThrowIfNull(input); ArgumentNullException.ThrowIfNull(operation);
        var diagnostics = new List<SculptDiagnostic>(); var evidence = new List<SculptValidationEvidence>();
        if (!input.SemanticInventory.ContainsKey(operation.TargetRegion)) diagnostics.Add(new("sculpt-target-unresolved", $"Target region '{operation.TargetRegion}' does not exist in the input state.", operation.TargetRegion));
        if (!operation.MayModify.Contains(operation.TargetRegion, StringComparer.Ordinal)) diagnostics.Add(new("sculpt-target-not-authorized", "Target region is not present in MayModify.", operation.TargetRegion));
        if (operation.BoundaryContinuity != "G0") diagnostics.Add(new("sculpt-boundary-continuity-unsupported", "SURF-X0 supports G0 positional continuity only."));
        foreach (var contract in operation.Preserves)
        {
            if (!input.SemanticInventory.ContainsKey(contract.EntityId)) diagnostics.Add(new("sculpt-preserve-unresolved", $"Preserved entity '{contract.EntityId}' does not exist.", contract.EntityId));
            if (operation.MayModify.Contains(contract.EntityId, StringComparer.Ordinal)) diagnostics.Add(new("sculpt-breaks-preserved-interface", $"'{contract.EntityId}' cannot be both modified and preserved.", contract.EntityId));
        }
        var actualDomain = new SpatialInfluenceEnvelope(-input.Construction.Width / 2d, -input.Construction.Depth / 2d, input.Construction.BaseHeight,
            input.Construction.Width / 2d, input.Construction.Depth / 2d, input.Construction.BaseHeight + operation.Offset);
        if (!operation.InfluenceEnvelope.Contains(actualDomain, LocalityTolerance)) diagnostics.Add(new("sculpt-outside-authorized-region", "The declared influence envelope does not contain the analytic transition and crown change domain."));
        if (operation.InfluenceEnvelope.MinZ < input.Construction.BaseHeight - LocalityTolerance)
            diagnostics.Add(new("sculpt-breaks-preserved-interface", "The influence envelope reaches below the authorized top boundary and could affect protected lower geometry."));
        if (!double.IsFinite(operation.Offset) || input.Construction.BaseHeight + operation.Offset <= 0d)
            diagnostics.Add(new("sculpt-self-intersection", "The requested offset crosses or collapses the bottom mounting plane."));
        if (operation.RegionWidth <= 0d || operation.RegionDepth <= 0d || operation.RegionWidth > input.Construction.Width || operation.RegionDepth > input.Construction.Depth)
            diagnostics.Add(new("sculpt-target-domain-invalid", "OffsetRegion dimensions must be positive and contained by the input footprint."));
        if (diagnostics.Count > 0) return SculptResult.Failure(diagnostics, evidence);

        var outputConstruction = input.Construction with { CrownWidth = operation.RegionWidth, CrownDepth = operation.RegionDepth, CrownOffset = operation.Offset };
        var built = SculptedHousingBrepBuilder.Build(outputConstruction);
        if (built.Body is null) return SculptResult.Failure(built.Diagnostics, evidence);
        var inventory = SculptedHousingFactory.Inventory(outputConstruction);
        foreach (var contract in operation.Preserves)
        {
            var before = input.SemanticInventory[contract.EntityId]; var after = inventory[contract.EntityId];
            var satisfied = string.Equals(before.StableId, after.StableId, StringComparison.Ordinal) && string.Equals(before.GeometryFingerprint, after.GeometryFingerprint, StringComparison.Ordinal);
            evidence.Add(new($"Preserve:{contract.EntityId}", satisfied, LocalityEvidenceLevel.ExactSemantic, satisfied ? 0d : null, LocalityTolerance,
                satisfied ? $"Stable identity and {contract.Mode} fingerprint are identical." : "Protected identity or contract fingerprint changed."));
            if (!satisfied) diagnostics.Add(new("sculpt-preservation-failed", $"Preservation contract failed for '{contract.EntityId}'.", contract.EntityId));
        }
        var locality = SculptLocalityVerifier.CompareOutsideTopEnvelope(input.Body, built.Body, input.Construction.BaseHeight, LocalityTolerance);
        evidence.Add(locality);
        if (!locality.Satisfied) diagnostics.Add(new("sculpt-outside-authorized-region", locality.Detail));
        evidence.AddRange(SculptedHousingFactory.ValidateBody(built.Body, LocalityTolerance));
        foreach (var requirement in operation.Requirements)
        {
            var check = requirement.ToString();
            if (!evidence.Any(x => x.Check == check && x.Satisfied)) diagnostics.Add(new("sculpt-postcondition-failed", $"Required postcondition '{check}' was not proven."));
        }
        if (diagnostics.Count > 0 || evidence.Any(x => !x.Satisfied)) return SculptResult.Failure(diagnostics, evidence);

        var outputId = BodyStateId.Derive($"{input.StateId.Value}|OffsetRegion|{operation.Canonical}");
        var correspondence = new List<GeometricDeltaEntry>
        {
            new(SculptedHousingFactory.BottomMountingInterface, GeometricChangeKind.Preserved, [SculptedHousingFactory.BottomMountingInterface], "Exact semantic identity and planar trim fingerprint."),
            new(SculptedHousingFactory.MountingHolePattern, GeometricChangeKind.Preserved, [SculptedHousingFactory.MountingHolePattern], "Exact axes, centers, and diameters; upper cylindrical extent is introduced inside the authorized region."),
            new(SculptedHousingFactory.OuterFootprintBoundary, GeometricChangeKind.Preserved, [SculptedHousingFactory.OuterFootprintBoundary], "Exact lower boundary coordinates."),
            new(SculptedHousingFactory.CrownRegion, GeometricChangeKind.Replaced, [$"{SculptedHousingFactory.CrownRegion}@{outputId.Value}"], "Planar top support offset and bounded to the authored region."),
            new("<none>", GeometricChangeKind.Introduced, [SculptedHousingFactory.TransitionZone], "Four analytic planar G0 transition faces."),
        };
        var delta = new GeometricDelta(input.StateId, outputId, [operation.TargetRegion, .. operation.Preserves.Select(x => x.EntityId)], operation.Preserves.Select(x => x.EntityId).ToArray(),
            [SculptedHousingFactory.CrownRegion], [], [SculptedHousingFactory.TransitionZone], operation.MayModify, operation.InfluenceEnvelope, correspondence);
        var associationRemap = SculptedHousingFactory.RemapPersistentAssociations(input, built.Body, delta);
        if (!associationRemap.IsSuccess) return SculptResult.Failure(associationRemap.Diagnostics, evidence);
        var outputAssociations = associationRemap.Associations;
        var authority = ConstructionAuthorityEvolution.Append(input, outputName, operation, outputId, delta, evidence);
        var output = new BodyState(outputId, input.StateId, input.BodyStableId, outputName, built.Body, outputConstruction, inventory, delta, evidence,
            outputAssociations, SculptedHousingFactory.SemanticPmi(outputAssociations, outputConstruction), SculptedHousingFactory.AssemblyInterfaces(outputAssociations), ConstructionAuthority: authority);
        return new(true, output, delta, evidence, []);
    }
}

public static class ReplaceRegionSculptor
{
    public static SculptResult Apply(BodyState input, string outputName, ReplaceRegionOperation operation)
        => ApplyCore(input, outputName, operation, null);

    internal static SculptResult ApplyWithCertifiedPolynomialBounds(BodyState input, string outputName, ReplaceRegionOperation operation, SpatialInfluenceEnvelope certifiedBounds)
        => ApplyCore(input, outputName, operation, certifiedBounds);

    private static SculptResult ApplyCore(BodyState input, string outputName, ReplaceRegionOperation operation, SpatialInfluenceEnvelope? certifiedBounds)
    {
        ArgumentNullException.ThrowIfNull(input); ArgumentNullException.ThrowIfNull(operation);
        var diagnostics = operation.ReplacementPatch.Validate().ToList();
        var evidence = new List<SculptValidationEvidence>();
        if (!input.SemanticInventory.ContainsKey(operation.TargetRegion))
        {
            var replacement = input.Delta?.Correspondence.FirstOrDefault(x => x.InputEntity == operation.TargetRegion && x.Change == GeometricChangeKind.Replaced);
            diagnostics.Add(replacement is null
                ? new("sculpt-target-unresolved", $"Target region '{operation.TargetRegion}' does not exist in the input state.", operation.TargetRegion)
                : new("surf-selector-target-replaced", $"'{operation.TargetRegion}' was replaced in {input.StateId.Value} by {string.Join(", ", replacement.OutputEntities)}; select a current-state entity.", operation.TargetRegion));
        }
        if (!operation.MayModify.Contains(operation.TargetRegion, StringComparer.Ordinal))
            diagnostics.Add(new("sculpt-target-not-authorized", "ReplaceRegion target is not present in MayModify.", operation.TargetRegion));
        foreach (var contract in operation.Preserves)
        {
            if (!input.SemanticInventory.ContainsKey(contract.EntityId)) diagnostics.Add(new("sculpt-preserve-unresolved", $"Preserved entity '{contract.EntityId}' does not exist.", contract.EntityId));
            if (operation.MayModify.Contains(contract.EntityId, StringComparer.Ordinal)) diagnostics.Add(new("sculpt-breaks-preserved-interface", $"'{contract.EntityId}' cannot be both modified and preserved.", contract.EntityId));
        }
        var expected = new Dictionary<PatchBoundarySide, string>
        {
            [PatchBoundarySide.South] = SculptedHousingFactory.CrownBoundarySouth,
            [PatchBoundarySide.East] = SculptedHousingFactory.CrownBoundaryEast,
            [PatchBoundarySide.North] = SculptedHousingFactory.CrownBoundaryNorth,
            [PatchBoundarySide.West] = SculptedHousingFactory.CrownBoundaryWest,
        };
        foreach (var boundary in operation.ReplacementPatch.BoundaryLoop.Boundaries)
            if (!string.Equals(boundary.ExistingBoundary, expected[boundary.PatchSide], StringComparison.Ordinal))
                diagnostics.Add(new("surf-boundary-correspondence-invalid", $"{boundary.PatchSide} must correspond to '{expected[boundary.PatchSide]}', not '{boundary.ExistingBoundary}'.", boundary.StableId));
        if (diagnostics.Count > 0) return SculptResult.Failure(diagnostics, evidence);

        var domain = operation.ReplacementPatch.ParameterDomain;
        var corners = new[]
        {
            operation.ReplacementPatch.Evaluate(domain.UMin, domain.VMin), operation.ReplacementPatch.Evaluate(domain.UMax, domain.VMin),
            operation.ReplacementPatch.Evaluate(domain.UMax, domain.VMax), operation.ReplacementPatch.Evaluate(domain.UMin, domain.VMax),
        };
        var width = Math.Abs(corners[1].X - corners[0].X); var depth = Math.Abs(corners[3].Y - corners[0].Y);
        var rectangular = width > operation.GeometricTolerance && depth > operation.GeometricTolerance
            && corners.All(x => Math.Abs(x.Z - input.Construction.BaseHeight) <= operation.GeometricTolerance)
            && Math.Abs(corners[0].Y - corners[1].Y) <= operation.GeometricTolerance
            && Math.Abs(corners[1].X - corners[2].X) <= operation.GeometricTolerance
            && Math.Abs(corners[2].Y - corners[3].Y) <= operation.GeometricTolerance
            && Math.Abs(corners[3].X - corners[0].X) <= operation.GeometricTolerance;
        if (!rectangular) diagnostics.Add(new("surf-boundary-mismatch", "Patch corners must form the declared axis-aligned rectangular boundary on the existing crown plane."));
        if (width > input.Construction.Width || depth > input.Construction.Depth) diagnostics.Add(new("surf-patch-outside-target", "Replacement patch boundary exceeds the housing top region."));

        if (operation.ReplacementPatch is BSplineSurfacePatch trimSplinePatch
            && Math.Abs(width - input.Construction.Width) <= operation.GeometricTolerance
            && Math.Abs(depth - input.Construction.Depth) <= operation.GeometricTolerance)
        {
            var intersections = DeriveOuterTrimIntersections(trimSplinePatch, input.Construction, operation);
            var derived = intersections.Count == 4 && intersections.All(result => result.IsSuccess && result.SelectedBranch is not null);
            evidence.Add(new("DerivedTrimIntersections", derived, LocalityEvidenceLevel.CertifiedBounded,
                intersections.Sum(result => result.Branches.Count), 4d,
                derived ? "Four outer trim edges were derived by qualified Plane/non-rational-B-spline intersections and deterministic branch selection."
                    : string.Join(" | ", intersections.SelectMany(result => result.Diagnostics))));
            if (!derived) diagnostics.Add(new("surf-intersection-ambiguous", "The whole-top replacement could not derive all four outer trim branches."));
        }

        var sampled = Sample(operation.ReplacementPatch, 17);
        var maximumZ = sampled.Max(x => x.Z);
        var envelopePoints = operation.ReplacementPatch is BSplineSurfacePatch splinePatch && certifiedBounds is null
            ? sampled.Concat(splinePatch.Spline.ControlPoints.SelectMany(x => x)).ToArray() : sampled.ToArray();
        var actual = certifiedBounds ?? new SpatialInfluenceEnvelope(envelopePoints.Min(x => x.X), envelopePoints.Min(x => x.Y), Math.Min(input.Construction.BaseHeight, envelopePoints.Min(x => x.Z)),
            envelopePoints.Max(x => x.X), envelopePoints.Max(x => x.Y), Math.Max(input.Construction.BaseHeight, envelopePoints.Max(x => x.Z)));
        if (certifiedBounds is { } certified)
        {
            var observed = new SpatialInfluenceEnvelope(sampled.Min(point => point.X), sampled.Min(point => point.Y), sampled.Min(point => point.Z),
                sampled.Max(point => point.X), sampled.Max(point => point.Y), sampled.Max(point => point.Z));
            if (!certified.Contains(observed, operation.GeometricTolerance)) diagnostics.Add(new("surf-certified-bounds-invalid", "The supplied exact patch-bounds certificate does not contain deterministic surface samples."));
        }
        if (!operation.InfluenceEnvelope.Contains(actual, operation.GeometricTolerance)) diagnostics.Add(new("sculpt-outside-authorized-region", "The declared influence envelope does not contain the replacement patch."));
        if (actual.MinZ < input.Construction.BaseHeight - operation.GeometricTolerance) diagnostics.Add(new("surf-patch-self-intersection", "The replacement patch enters the preserved housing volume below the original crown plane."));

        foreach (var boundary in operation.ReplacementPatch.BoundaryLoop.Boundaries.OrderBy(x => x.PatchSide))
        {
            var (g0, angle) = MeasureBoundary(operation.ReplacementPatch, boundary.PatchSide, input.Construction.BaseHeight, 33);
            var g0Ok = g0 <= operation.GeometricTolerance;
            evidence.Add(new($"Boundary:{boundary.StableId}:G0", g0Ok, LocalityEvidenceLevel.CertifiedBounded, g0, operation.GeometricTolerance, $"Maximum sampled positional error over 33 deterministic parameters is {g0:R} mm."));
            if (!g0Ok) diagnostics.Add(new("surf-boundary-g0-violation", $"Boundary '{boundary.StableId}' has G0 error {g0:R} mm, exceeding {operation.GeometricTolerance:R} mm.", boundary.StableId));
            if (boundary.Continuity is PatchBoundaryContinuity.G1 or PatchBoundaryContinuity.G2)
            {
                var g1Ok = angle <= operation.G1AngularToleranceDegrees;
                evidence.Add(new($"Boundary:{boundary.StableId}:G1", g1Ok, LocalityEvidenceLevel.CertifiedBounded, angle, operation.G1AngularToleranceDegrees, $"Maximum sampled tangent-plane angular error over 33 deterministic parameters is {angle:R} degrees."));
                if (!g1Ok) diagnostics.Add(new("surf-boundary-g1-violation", $"Boundary '{boundary.StableId}' has tangent-plane error {angle:R} degrees, exceeding {operation.G1AngularToleranceDegrees:R} degrees.", boundary.StableId));
            }
            if (boundary.Continuity == PatchBoundaryContinuity.G2)
            {
                var curvature = MeasurePlanarBoundarySecondDifference(operation.ReplacementPatch, boundary.PatchSide);
                var g2Ok = curvature <= operation.G2CurvatureTolerance;
                evidence.Add(new($"Boundary:{boundary.StableId}:G2", g2Ok, LocalityEvidenceLevel.ExactAnalytic, curvature, operation.G2CurvatureTolerance,
                    $"Exact clamped B-spline boundary control-net second-difference evidence is {curvature:R}; zero proves transverse normal-curvature equality to the planar shoulder."));
                if (!g2Ok) diagnostics.Add(new("surf-boundary-g2-violation", $"Boundary '{boundary.StableId}' has planar normal-curvature control residual {curvature:R}, exceeding {operation.G2CurvatureTolerance:R}.", boundary.StableId));
            }
        }
        if (diagnostics.Count > 0) return SculptResult.Failure(diagnostics, evidence);

        var construction = input.Construction with { CrownWidth = width, CrownDepth = depth, CrownOffset = maximumZ - input.Construction.BaseHeight, ReplacementPatch = operation.ReplacementPatch };
        var built = SculptedHousingBrepBuilder.Build(construction);
        if (built.Body is null) return SculptResult.Failure(built.Diagnostics, evidence);
        var inventory = SculptedHousingFactory.Inventory(construction).ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        inventory.Remove(SculptedHousingFactory.CrownRegion);
        inventory[operation.ReplacementPatch.PatchId] = new(operation.ReplacementPatch.PatchId, SculptEntityKind.Surface,
            $"{operation.ReplacementPatch.ExportClass}:{operation.ReplacementPatch.DegreeU}x{operation.ReplacementPatch.DegreeV}:{operation.ReplacementPatch.ControlCountU}x{operation.ReplacementPatch.ControlCountV}", "Accepted bounded replacement patch.");
        foreach (var contract in operation.Preserves)
        {
            var before = input.SemanticInventory[contract.EntityId]; var after = inventory[contract.EntityId];
            var satisfied = before.StableId == after.StableId && before.GeometryFingerprint == after.GeometryFingerprint;
            evidence.Add(new($"Preserve:{contract.EntityId}", satisfied, LocalityEvidenceLevel.ExactSemantic, satisfied ? 0d : null, operation.GeometricTolerance,
                satisfied ? $"Stable identity and {contract.Mode} fingerprint are identical." : "Protected identity or geometry fingerprint changed."));
            if (!satisfied) diagnostics.Add(new("sculpt-preservation-failed", $"Preservation contract failed for '{contract.EntityId}'.", contract.EntityId));
        }
        var locality = SculptLocalityVerifier.CompareOutsideTopEnvelope(input.Body, built.Body, input.Construction.BaseHeight, operation.GeometricTolerance);
        evidence.Add(locality); if (!locality.Satisfied) diagnostics.Add(new("sculpt-outside-authorized-region", locality.Detail));
        var bodyEvidence = SculptedHousingFactory.ValidateBody(built.Body, operation.GeometricTolerance);
        evidence.AddRange(bodyEvidence);
        foreach (var failed in bodyEvidence.Where(item => !item.Satisfied))
            diagnostics.Add(new("surf-body-invalid", $"{failed.Check}: {failed.Detail}"));
        var shared = VerifySharedBoundaryTopology(built.Body);
        evidence.Add(shared); if (!shared.Satisfied) diagnostics.Add(new("surf-boundary-not-shared", shared.Detail));
        foreach (var requirement in operation.Requirements)
            if (!evidence.Any(x => x.Check == requirement.ToString() && x.Satisfied)) diagnostics.Add(new("sculpt-postcondition-failed", $"Required postcondition '{requirement}' was not proven."));
        if (diagnostics.Count > 0 || evidence.Any(x => !x.Satisfied)) return SculptResult.Failure(diagnostics, evidence);

        var outputId = BodyStateId.Derive($"{input.StateId.Value}|ReplaceRegion|{operation.Canonical}");
        var correspondence = operation.Preserves.Select(x => new GeometricDeltaEntry(x.EntityId, GeometricChangeKind.Preserved, [x.EntityId], "Exact semantic identity and geometry fingerprint."))
            .Append(new(SculptedHousingFactory.CrownRegion, GeometricChangeKind.Replaced, [operation.ReplacementPatch.PatchId], "Explicit outer-loop boundary correspondence; declared G0/G1/G2 contracts verified."))
            .Concat(operation.ReplacementPatch.BoundaryLoop.Boundaries.Select(x => new GeometricDeltaEntry(x.ExistingBoundary, GeometricChangeKind.Preserved, [x.StableId], $"Shared edge correspondence with {x.Continuity}."))).ToArray();
        var delta = new GeometricDelta(input.StateId, outputId, [operation.TargetRegion, .. operation.Preserves.Select(x => x.EntityId)], operation.Preserves.Select(x => x.EntityId).ToArray(),
            [operation.TargetRegion], [], [operation.ReplacementPatch.PatchId], operation.MayModify, operation.InfluenceEnvelope, correspondence);
        var associationRemap = SculptedHousingFactory.RemapPersistentAssociations(input, built.Body, delta);
        if (!associationRemap.IsSuccess) return SculptResult.Failure(associationRemap.Diagnostics, evidence);
        var outputAssociations = associationRemap.Associations;
        var authority = ConstructionAuthorityEvolution.Append(input, outputName, operation, outputId, delta, evidence);
        return new(true, new(outputId, input.StateId, input.BodyStableId, outputName, built.Body, construction, inventory, delta, evidence,
            outputAssociations, SculptedHousingFactory.SemanticPmi(outputAssociations, construction), SculptedHousingFactory.AssemblyInterfaces(outputAssociations), ConstructionAuthority: authority), delta, evidence, []);
    }

    private static IReadOnlyList<Point3D> Sample(BoundedSurfacePatch patch, int count)
    {
        var result = new List<Point3D>(count * count); var d = patch.ParameterDomain;
        for (var i = 0; i < count; i++) for (var j = 0; j < count; j++)
            result.Add(patch.Evaluate(d.UMin + (d.UMax - d.UMin) * i / (count - 1d), d.VMin + (d.VMax - d.VMin) * j / (count - 1d)));
        return result;
    }

    private static IReadOnlyList<SurfaceIntersectionResult> DeriveOuterTrimIntersections(BSplineSurfacePatch patch, HousingConstruction construction, ReplaceRegionOperation operation)
    {
        var z = construction.BaseHeight; var x = construction.Width / 2d; var y = construction.Depth / 2d;
        var sideDomain = new SurfaceParameterDomain(-construction.Width, construction.Width, -construction.Depth, construction.Depth);
        var sides = new[]
        {
            ("South", new PlaneSurface(new(0, -y, z), Direction3D.Create(new Vector3D(0, -1, 0)), Direction3D.Create(new Vector3D(1, 0, 0))), new SurfaceParameterPoint(0, 0)),
            ("East", new PlaneSurface(new(x, 0, z), Direction3D.Create(new Vector3D(1, 0, 0)), Direction3D.Create(new Vector3D(0, 1, 0))), new SurfaceParameterPoint(0, 0)),
            ("North", new PlaneSurface(new(0, y, z), Direction3D.Create(new Vector3D(0, 1, 0)), Direction3D.Create(new Vector3D(1, 0, 0))), new SurfaceParameterPoint(0, 1)),
            ("West", new PlaneSurface(new(-x, 0, z), Direction3D.Create(new Vector3D(-1, 0, 0)), Direction3D.Create(new Vector3D(0, 1, 0))), new SurfaceParameterPoint(0, 1)),
        };
        return sides.Select(side => BoundedSurfaceIntersector.Intersect(new SurfaceIntersectionRequest(
            $"Preserved{side.Item1}Plane", SurfaceGeometry.FromPlane(side.Item2), sideDomain,
            patch.PatchId, patch.Support, patch.ParameterDomain, operation.GeometricTolerance, operation.GeometricTolerance, side.Item3))).ToArray();
    }

    private static (double G0, double G1Degrees) MeasureBoundary(BoundedSurfacePatch patch, PatchBoundarySide side, double planeZ, int count)
    {
        var domain = patch.ParameterDomain; var g0 = 0d; var g1 = 0d;
        var corners = new[] { patch.Evaluate(domain.UMin, domain.VMin), patch.Evaluate(domain.UMax, domain.VMin), patch.Evaluate(domain.UMax, domain.VMax), patch.Evaluate(domain.UMin, domain.VMax) };
        var du = (domain.UMax - domain.UMin) * 1e-5; var dv = (domain.VMax - domain.VMin) * 1e-5;
        for (var i = 0; i < count; i++)
        {
            var t = i / (count - 1d); var u = domain.UMin + (domain.UMax - domain.UMin) * t; var v = domain.VMin + (domain.VMax - domain.VMin) * t;
            (u, v) = side switch { PatchBoundarySide.South => (u, domain.VMin), PatchBoundarySide.East => (domain.UMax, v), PatchBoundarySide.North => (u, domain.VMax), _ => (domain.UMin, v) };
            var p = patch.Evaluate(u, v);
            var (a, b) = side switch { PatchBoundarySide.South => (corners[0], corners[1]), PatchBoundarySide.East => (corners[1], corners[2]), PatchBoundarySide.North => (corners[3], corners[2]), _ => (corners[0], corners[3]) };
            a = new(a.X, a.Y, planeZ); b = new(b.X, b.Y, planeZ);
            var segment = b - a; var parameter = Math.Clamp((p - a).Dot(segment) / segment.Dot(segment), 0d, 1d); var closest = a + segment * parameter;
            g0 = Math.Max(g0, (p - closest).Length);
            var ua = Math.Max(domain.UMin, u - du); var ub = Math.Min(domain.UMax, u + du); var va = Math.Max(domain.VMin, v - dv); var vb = Math.Min(domain.VMax, v + dv);
            var tu = patch.Evaluate(ub, v) - patch.Evaluate(ua, v); var tv = patch.Evaluate(u, vb) - patch.Evaluate(u, va);
            var normal = tu.Cross(tv); if (normal.Length <= 1e-14) { g1 = 180d; continue; }
            var cosine = Math.Clamp(Math.Abs(normal.Z) / normal.Length, 0d, 1d); g1 = Math.Max(g1, Math.Acos(cosine) * 180d / Math.PI);
        }
        return (g0, g1);
    }

    private static double MeasurePlanarBoundarySecondDifference(BoundedSurfacePatch patch, PatchBoundarySide side)
    {
        if (patch is not BSplineSurfacePatch spline) return double.PositiveInfinity;
        var points = spline.Spline.ControlPoints; var uCount = points.Count; var vCount = points[0].Count;
        IEnumerable<Vector3D> differences = side switch
        {
            PatchBoundarySide.South => Enumerable.Range(0, uCount).Select(i => (points[i][2] - points[i][1]) - (points[i][1] - points[i][0])),
            PatchBoundarySide.North => Enumerable.Range(0, uCount).Select(i => (points[i][vCount - 3] - points[i][vCount - 2]) - (points[i][vCount - 2] - points[i][vCount - 1])),
            PatchBoundarySide.West => Enumerable.Range(0, vCount).Select(j => (points[2][j] - points[1][j]) - (points[1][j] - points[0][j])),
            _ => Enumerable.Range(0, vCount).Select(j => (points[uCount - 3][j] - points[uCount - 2][j]) - (points[uCount - 2][j] - points[uCount - 1][j]))
        };
        return differences.Max(vector => vector.Length);
    }

    private static SculptValidationEvidence VerifySharedBoundaryTopology(BrepBody body)
    {
        var counts = body.Topology.Coedges.GroupBy(x => x.EdgeId).ToDictionary(x => x.Key, x => x.Count());
        var invalid = counts.Where(x => x.Value != 2).ToArray();
        return new("SharedBoundaryTopology", invalid.Length == 0, LocalityEvidenceLevel.CertifiedBounded, invalid.Length, 0d,
            invalid.Length == 0 ? "Every replacement, trim, and preserved-neighbor edge has exactly two coedge uses; vertices are shared topology objects." : $"{invalid.Length} edges do not have exactly two coedge uses.");
    }
}

public static class SafeHoleSculptor
{
    public static SculptResult Apply(BodyState input, string outputName, SafeHoleOperation operation)
    {
        var diagnostics = new List<SculptDiagnostic>(); var evidence = new List<SculptValidationEvidence>();
        if (!input.SemanticInventory.ContainsKey(operation.TargetRegion))
            diagnostics.Add(new("sculpt-target-unresolved", $"Hole target '{operation.TargetRegion}' does not exist in the current BodyState.", operation.TargetRegion));
        if (input.SemanticInventory.ContainsKey(operation.Hole.StableId)) diagnostics.Add(new("sculpt-hole-duplicate", $"Hole '{operation.Hole.StableId}' already exists.", operation.Hole.StableId));
        foreach (var contract in operation.Preserves) if (!input.SemanticInventory.ContainsKey(contract.EntityId)) diagnostics.Add(new("sculpt-preserve-unresolved", $"Preserved entity '{contract.EntityId}' does not exist in the current BodyState.", contract.EntityId));
        var radius = operation.Hole.Diameter / 2d;
        var actual = new SpatialInfluenceEnvelope(operation.Hole.CenterX - radius, operation.Hole.CenterY - radius, 0d, operation.Hole.CenterX + radius, operation.Hole.CenterY + radius, input.Construction.BaseHeight);
        if (!operation.InfluenceEnvelope.Contains(actual, 1e-6)) diagnostics.Add(new("sculpt-outside-authorized-region", "HoleFeature influence envelope does not contain the full through-hole cylinder."));
        if (diagnostics.Count > 0) return SculptResult.Failure(diagnostics);

        var construction = input.Construction with { Holes = [.. input.Construction.Holes, operation.Hole] };
        var retainedAdd = input.ConstructionAuthority?.Operations.LastOrDefault()?.Payload as AddSectionChainOperation;
        BrepBody? realizedBody; IReadOnlyList<SculptDiagnostic> buildDiagnostics;
        if (retainedAdd is not null)
        {
            var retained = SectionChainHousingBrepBuilder.BuildAddEast(construction, retainedAdd.Chain);
            realizedBody = retained.Body; buildDiagnostics = retained.Diagnostics;
        }
        else
        {
            var built = SculptedHousingBrepBuilder.Build(construction);
            realizedBody = built.Body; buildDiagnostics = built.Diagnostics;
        }
        if (realizedBody is null) return SculptResult.Failure(buildDiagnostics);
        var inventory = SculptedHousingFactory.Inventory(construction).ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        if (construction.ReplacementPatch is { } patch)
        {
            inventory.Remove(SculptedHousingFactory.CrownRegion);
            inventory[patch.PatchId] = new(patch.PatchId, SculptEntityKind.Surface, $"{patch.ExportClass}:{patch.DegreeU}x{patch.DegreeV}:{patch.ControlCountU}x{patch.ControlCountV}", "Accepted bounded replacement patch.");
        }
        if (retainedAdd is not null)
        {
            inventory.Remove(retainedAdd.Attachment.SupportRegion);
            var successor = $"{retainedAdd.Chain.StableId}.AttachedSurface";
            inventory[successor] = input.SemanticInventory[successor];
        }
        foreach (var contract in operation.Preserves)
        {
            var before = input.SemanticInventory[contract.EntityId]; var after = inventory[contract.EntityId]; var satisfied = before.GeometryFingerprint == after.GeometryFingerprint;
            evidence.Add(new($"Preserve:{contract.EntityId}", satisfied, LocalityEvidenceLevel.ExactSemantic, satisfied ? 0d : null, 1e-6, satisfied ? "Current-state semantic identity and geometry fingerprint are unchanged." : "Preserved geometry changed."));
            if (!satisfied) diagnostics.Add(new("sculpt-preservation-failed", $"Preservation contract failed for '{contract.EntityId}'.", contract.EntityId));
        }
        evidence.AddRange(SculptedHousingFactory.ValidateBody(realizedBody, 1e-6));
        if (diagnostics.Count > 0 || evidence.Any(x => !x.Satisfied)) return SculptResult.Failure(diagnostics, evidence);
        var outputId = BodyStateId.Derive($"{input.StateId.Value}|HoleFeature|{operation.Canonical}");
        var correspondence = operation.Preserves.Select(x => new GeometricDeltaEntry(x.EntityId, GeometricChangeKind.Preserved, [x.EntityId], "Resolved and verified against the current BodyState."))
            .Append(new("<none>", GeometricChangeKind.Introduced, [operation.Hole.StableId], "Exact cylindrical through-hole on the current planar frame.")).ToArray();
        var delta = new GeometricDelta(input.StateId, outputId, [operation.TargetRegion], operation.Preserves.Select(x => x.EntityId).ToArray(), [], [], [operation.Hole.StableId], [operation.TargetRegion], operation.InfluenceEnvelope, correspondence);
        var outputAssociations = SculptedHousingFactory.PersistentAssociations(realizedBody, construction);
        var authority = ConstructionAuthorityEvolution.Append(input, outputName, operation, outputId, delta, evidence);
        return new(true, new(outputId, input.StateId, input.BodyStableId, outputName, realizedBody, construction, inventory, delta, evidence,
            outputAssociations, SculptedHousingFactory.SemanticPmi(outputAssociations, construction), SculptedHousingFactory.AssemblyInterfaces(outputAssociations), ConstructionAuthority: authority), delta, evidence, []);
    }
}

internal static class SculptLocalityVerifier
{
    public static SculptValidationEvidence CompareOutsideTopEnvelope(BrepBody input, BrepBody output, double boundaryZ, double tolerance)
    {
        var deviations = new List<double>(); var details = new List<string>();
        var inputPoints = PointsBelow(input, boundaryZ, tolerance); var outputPoints = PointsBelow(output, boundaryZ, tolerance);
        var pointsMatch = CompareSets(inputPoints, outputPoints, PointDistance, tolerance, deviations);
        details.Add($"lowerVertices={inputPoints.Count}/{outputPoints.Count}");
        var inputPlanes = LowerPlanes(input, boundaryZ, tolerance); var outputPlanes = LowerPlanes(output, boundaryZ, tolerance);
        var planesMatch = CompareSets(inputPlanes, outputPlanes, VectorDistance, tolerance, deviations);
        details.Add($"lowerPlanes={inputPlanes.Count}/{outputPlanes.Count}");
        var inputCylinders = Cylinders(input); var outputCylinders = Cylinders(output);
        var cylindersMatch = CompareSets(inputCylinders, outputCylinders, VectorDistance, tolerance, deviations);
        details.Add($"cylinderSupports={inputCylinders.Count}/{outputCylinders.Count}");
        var inputCircles = CirclesBelow(input, boundaryZ, tolerance); var outputCircles = CirclesBelow(output, boundaryZ, tolerance);
        var circlesMatch = CompareSets(inputCircles, outputCircles, VectorDistance, tolerance, deviations);
        details.Add($"lowerCircularTrims={inputCircles.Count}/{outputCircles.Count}");
        var max = deviations.Count == 0 ? 0d : deviations.Max(); var satisfied = pointsMatch && planesMatch && cylindersMatch && circlesMatch && max <= tolerance;
        return new("AuthorizedLocality", satisfied, LocalityEvidenceLevel.ExactAnalytic, max, tolerance,
            $"Independent realized-BRep comparison below z={boundaryZ:R}: {string.Join(", ", details)}; analytic supports and trims matched={satisfied}.");
    }

    private static IReadOnlyList<Point3D> PointsBelow(BrepBody body, double boundary, double tolerance) => body.Topology.Vertices
        .Select(x => body.TryGetVertexPoint(x.Id, out var point) ? (Point3D?)point : null).Where(x => x is not null && x.Value.Z < boundary - tolerance).Select(x => x!.Value).ToArray();
    private static IReadOnlyList<double[]> LowerPlanes(BrepBody body, double boundary, double tolerance)
    {
        var result = new List<double[]>();
        foreach (var face in body.Topology.Faces)
        {
            if (!body.TryGetFaceSurfaceGeometry(face.Id, out var geometry) || geometry?.Plane is not { } plane) continue;
            var points = face.LoopIds.SelectMany(id => body.Topology.GetLoop(id).CoedgeIds).Select(id => body.Topology.GetEdge(body.Topology.GetCoedge(id).EdgeId)).SelectMany(edge => new[] { edge.StartVertexId, edge.EndVertexId }).Distinct().Select(id => body.TryGetVertexPoint(id, out var p) ? (Point3D?)p : null).Where(x => x is not null).Select(x => x!.Value).ToArray();
            if (points.Length == 0 || points.Min(x => x.Z) >= boundary - tolerance || points.Max(x => x.Z) > boundary + tolerance) continue;
            var n = Canonical(plane.Normal.ToVector()); var d = n.X * plane.Origin.X + n.Y * plane.Origin.Y + n.Z * plane.Origin.Z;
            result.Add([n.X, n.Y, n.Z, d]);
        }
        return result;
    }
    private static IReadOnlyList<double[]> Cylinders(BrepBody body) => body.Topology.Faces.Select(face => body.TryGetFaceSurfaceGeometry(face.Id, out var geometry) ? geometry?.Cylinder : null).Where(x => x is not null).Select(x =>
    {
        var c = x!.Value; var axis = Canonical(c.Axis.ToVector()); var origin = new Vector3D(c.Origin.X, c.Origin.Y, c.Origin.Z); var perpendicular = origin - axis * origin.Dot(axis);
        return new[] { axis.X, axis.Y, axis.Z, perpendicular.X, perpendicular.Y, perpendicular.Z, c.Radius };
    }).ToArray();
    private static IReadOnlyList<double[]> CirclesBelow(BrepBody body, double boundary, double tolerance) => body.Bindings.EdgeBindings.Select(binding => body.Geometry.TryGetCurve(binding.CurveGeometryId, out var curve) ? curve?.Circle3 : null).Where(x => x is not null && x.Value.Center.Z < boundary - tolerance).Select(x =>
    { var c = x!.Value; var n = Canonical(c.Normal.ToVector()); return new[] { c.Center.X, c.Center.Y, c.Center.Z, n.X, n.Y, n.Z, c.Radius }; }).ToArray();

    private static bool CompareSets<T>(IReadOnlyList<T> left, IReadOnlyList<T> right, Func<T, T, double> distance, double tolerance, ICollection<double> deviations)
    {
        if (left.Count != right.Count) return false; var remaining = right.ToList();
        foreach (var item in left) { var match = remaining.Select((candidate, index) => (index, delta: distance(item, candidate))).OrderBy(x => x.delta).FirstOrDefault(); deviations.Add(match.delta); if (match.delta > tolerance) return false; remaining.RemoveAt(match.index); }
        return remaining.Count == 0;
    }
    private static double PointDistance(Point3D a, Point3D b) => (a - b).Length;
    private static double VectorDistance(double[] a, double[] b) => a.Length != b.Length ? double.PositiveInfinity : a.Zip(b, (x, y) => Math.Abs(x - y)).Max();
    private static Vector3D Canonical(Vector3D value)
    {
        var n = value / value.Length; var sign = Math.Abs(n.X) > 1e-12 ? Math.Sign(n.X) : Math.Abs(n.Y) > 1e-12 ? Math.Sign(n.Y) : Math.Sign(n.Z); return sign < 0 ? -n : n;
    }
}

public sealed record StepSurfaceInventory(int Plane, int Cylinder, int Cone, int Sphere, int Torus, int NonRationalBSpline, int Other, int RationalNurbs);
public sealed record SculptStepResult(bool IsSuccess, string? Step, StepSurfaceInventory Inventory, IReadOnlyList<SculptDiagnostic> Diagnostics);

public static class SculptStepExporter
{
    public static SculptStepResult Export(BodyState state, string productName)
    {
        var semanticPmi = new List<Step242SemanticPmi>(state.SemanticPmi ?? []);
        foreach (var assemblyInterface in state.AssemblyInterfaces ?? [])
            semanticPmi.Add(new Step242SemanticPmiNote($"assembly-interface:{assemblyInterface.StableId}", assemblyInterface.SemanticTarget, assemblyInterface.Description)
            { GeometricFaceIds = assemblyInterface.FaceIds });
        var export = Step242Exporter.ExportBody(state.Body, semanticPmi, new Step242ExportOptions { BrepExportPreflightMode = BrepExportPreflightMode.Enforce, ProductName = productName });
        if (!export.IsSuccess) return new(false, null, Empty(), export.Diagnostics.Select(x => new SculptDiagnostic("surf-step-export-failed", x.Message)).ToArray());
        var text = export.Value;
        var inventory = new StepSurfaceInventory(Count(text, "=PLANE("), Count(text, "=CYLINDRICAL_SURFACE("), Count(text, "=CONICAL_SURFACE("), Count(text, "=SPHERICAL_SURFACE("), Count(text, "=TOROIDAL_SURFACE("), Count(text, "=B_SPLINE_SURFACE_WITH_KNOTS("), 0, Count(text, "RATIONAL_B_SPLINE_SURFACE"));
        if (inventory.RationalNurbs != 0)
            return new(false, null, inventory, [new("surf-surface-export-normalization-failed", "STEP product boundary contains a rational NURBS surface; export was blocked.")]);
        return new(true, text, inventory, []);
    }
    private static int Count(string text, string marker) => (text.Length - text.Replace(marker, string.Empty, StringComparison.Ordinal).Length) / marker.Length;
    private static StepSurfaceInventory Empty() => new(0, 0, 0, 0, 0, 0, 0, 0);
}

public sealed record InternalRationalSurfaceCandidate(int DegreeU, int DegreeV, IReadOnlyList<IReadOnlyList<Point3D>> ControlPoints, IReadOnlyList<IReadOnlyList<double>> Weights);
public sealed record SurfaceNormalizationResult(bool IsSuccess, string EmittedFamily, SurfaceGeometry? Surface, string Diagnostic);
public static class RationalSurfaceNormalizer
{
    public static SurfaceNormalizationResult Normalize(InternalRationalSurfaceCandidate candidate)
    {
        var weights = candidate.Weights.SelectMany(x => x).ToArray();
        var controlCountV = candidate.ControlPoints.Count == 0 ? 0 : candidate.ControlPoints[0].Count;
        if (weights.Length == 0 || candidate.Weights.Count != candidate.ControlPoints.Count || candidate.Weights.Any(x => x.Count != controlCountV) || weights.Any(x => !double.IsFinite(x) || x <= 0d)) return new(false, "None", null, "surf-surface-export-normalization-failed: invalid rational weights");
        if (weights.Max() - weights.Min() > 1e-12d) return new(false, "None", null, "surf-surface-export-normalization-failed: analytic recovery failed and rationality is not removable");
        if (candidate.DegreeU == 1 && candidate.DegreeV == 1 && candidate.ControlPoints.Count == 2 && candidate.ControlPoints.All(x => x.Count == 2))
        {
            var p = candidate.ControlPoints; var u = p[1][0] - p[0][0]; var v = p[0][1] - p[0][0]; var normal = u.Cross(v);
            if (normal.Length > 1e-12d && Math.Abs((p[1][1] - p[0][0]).Dot(normal / normal.Length)) <= 1e-12d)
                return new(true, "Plane", SurfaceGeometry.FromPlane(new PlaneSurface(p[0][0], Direction3D.Create(normal), Direction3D.Create(u))), "Equal weights removed; exact analytic plane recovered.");
        }
        try
        {
            var (multU, knotsU) = OpenUniformKnots(candidate.ControlPoints.Count, candidate.DegreeU); var (multV, knotsV) = OpenUniformKnots(controlCountV, candidate.DegreeV);
            var surface = new BSplineSurfaceWithKnots(candidate.DegreeU, candidate.DegreeV, candidate.ControlPoints, "UNSPECIFIED", false, false, false, multU, multV, knotsU, knotsV, "UNSPECIFIED");
            return new(true, "NonRationalBSpline", SurfaceGeometry.FromBSplineSurfaceWithKnots(surface), "All weights are equal; common rational scale removed exactly and a non-rational B-spline was produced.");
        }
        catch (ArgumentException exception) { return new(false, "None", null, $"surf-surface-export-normalization-failed: equal-weight non-rational conversion rejected: {exception.Message}"); }
    }

    private static (IReadOnlyList<int> Multiplicities, IReadOnlyList<double> Values) OpenUniformKnots(int controlCount, int degree)
    {
        if (controlCount < degree + 1) throw new ArgumentException("Control count must be at least degree plus one.");
        var internalCount = controlCount - degree - 1; var values = Enumerable.Range(0, internalCount + 2).Select(i => i / (double)(internalCount + 1)).ToArray();
        var multiplicities = Enumerable.Range(0, values.Length).Select(i => i == 0 || i == values.Length - 1 ? degree + 1 : 1).ToArray(); return (multiplicities, values);
    }
}
