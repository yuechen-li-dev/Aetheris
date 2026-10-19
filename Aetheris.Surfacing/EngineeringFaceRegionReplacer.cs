using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Brep;

namespace Aetheris.Surfacing;

/// <summary>Current-state association authority around the shared bounded graft.</summary>
public static class EngineeringFaceRegionReplacer
{
    public static SculptResult Apply(BodyState input, string name, ReplaceRegionOperation operation)
    {
        SculptResult Fail(string code, string message) => SculptResult.Failure([new(code, message, operation.TargetRegion)]);
        if (input.HasImportedEngineeringSource)
            return Fail("surf-face-import-metadata-unqualified", "The source-level graft is qualified for native engineering sources. Imported product metadata requires a complete successor projection; use the existing bounded imported-face API for its qualified contract.");
        if (operation.Requirements.Contains(SculptRequirement.NoSelfIntersection))
            return Fail("surf-face-global-intersection-unqualified", "This bounded graft checks trims, pcurves and enclosure; it cannot prove global NoSelfIntersection.");
        if (input.RealizedStepText is { } step)
        {
            var pmi = Step242SemanticPmiInspector.Inspect(step);
            if (!pmi.Success || pmi.Items.Count != 0)
                return Fail("surf-face-pmi-projection-unsupported", "Retained STEP PMI has no complete editable projection. The exact predecessor and its PMI remain unchanged; this edit cannot discard them.");
        }
        var associations = input.GeometryAssociations ?? [];
        var target = associations.SingleOrDefault(a => a.SemanticTarget == operation.TargetRegion && a.State != PersistentAssociationState.Removed);
        if (target is null || target.FaceIds.Count != 1)
            return Fail("surf-face-target-unresolved", "ReplaceRegion requires one current face association. Inspect the BodyState for explicit FaceN handles or select its replacement patch name.");
        var faceId = target.FaceIds[0];
        if ((input.SemanticPmi ?? []).Any(p => p.GeometricFaceIds.Contains(faceId)) || (input.AssemblyInterfaces ?? []).Any(a => a.FaceIds.Contains(faceId)))
            return Fail("surf-face-associated-target-unsupported", "The selected face carries PMI or an assembly interface whose successor meaning is not qualified. Unaffected associations remain retained.");
        if (!operation.MayModify.Contains(operation.TargetRegion, StringComparer.Ordinal))
            return Fail("sculpt-target-not-authorized", "The current face target must be listed in mayModify.");
        foreach (var contract in operation.Preserves)
        {
            var preserved = associations.SingleOrDefault(a => a.SemanticTarget == contract.EntityId);
            if (preserved is null || preserved.FaceIds.Contains(faceId))
                return Fail("sculpt-preserve-conflict", $"Cannot preserve '{contract.EntityId}': it is unresolved or includes the replaced face.");
        }
        if (operation.ReplacementPatch is not BSplineSurfacePatch patch)
            return Fail("surf-face-patch-unsupported", "The exact face graft requires a non-rational BSpline SurfacePatch.");
        if (input.ConstructionAuthority is null)
            return Fail("surf-face-replay-authority-missing", "Exact face editing requires a replayable source body.");
        if (patch.BoundaryLoop.Boundaries.Any(b => b.Continuity != PatchBoundaryContinuity.G0))
            return Fail("surf-face-continuity-unsupported", "The bounded exact-face graft currently qualifies G0 boundaries only.");
        var face = input.Body.Topology.GetFace(new(faceId));
        var edges = face.LoopIds.Count == 1 ? input.Body.GetCoedgeIds(face.LoopIds[0]).Select(input.Body.Topology.GetCoedge).Select(c => c.EdgeId).ToArray() : [];
        if (edges.Length != 4 || patch.BoundaryLoop.Boundaries.Count != 4)
            return Fail("surf-face-boundary-unsupported", "This exact-edit adapter requires one four-line outer loop and no holes.");
        var rows = patch.Spline.ControlPoints;
        var spline = patch.Spline; var domain = patch.ParameterDomain;
        if (spline.IsRational || spline.DegreeU != 3 || spline.DegreeV != 3 || rows.Count != 4 || rows.Any(r => r.Count != 4)
            || spline.KnotMultiplicitiesU.Count != 2 || spline.KnotMultiplicitiesV.Count != 2
            || spline.KnotMultiplicitiesU.Any(m => m != 4) || spline.KnotMultiplicitiesV.Any(m => m != 4)
            || domain.UMin != spline.DomainStartU || domain.UMax != spline.DomainEndU || domain.VMin != spline.DomainStartV || domain.VMax != spline.DomainEndV
            || !input.Body.TryGetFaceSurfaceGeometry(new(faceId), out var original) || original?.Plane is not { } sourcePlane)
            return Fail("surf-face-patch-family-unsupported", "This adapter admits a full-domain non-rational bicubic Bezier graph over one planar four-line face.");
        input.Body.Bindings.TryGetFaceBinding(new(faceId), out var sourceBinding);
        var normal = sourcePlane.Normal.ToVector() * (sourceBinding.Orientation.IsAlignedWithSurface ? 1 : -1);
        var u = rows[^1][0] - rows[0][0]; var v = rows[0][^1] - rows[0][0];
        var alignment = !patch.ReversedOrientation;
        if (u.Cross(v).Dot(normal) * (alignment ? 1 : -1) <= 0)
            return Fail("surf-face-orientation-invalid", "Patch parameter orientation must retain the selected face's outward material side.");
        for (var i = 0; i < 4; i++) for (var j = 0; j < 4; j++)
        {
            var expected = rows[0][0] + u * (i / 3d) + v * (j / 3d);
            var displacement = rows[i][j] - expected; var height = displacement.Dot(normal);
            if ((displacement - normal * height).Length > operation.GeometricTolerance || height < -operation.GeometricTolerance)
                return Fail("surf-face-graph-unsupported", "Control points must retain the face's affine parameter grid and displace outward along its normal; broader deformation belongs to MeshState.");
        }
        foreach (var boundary in patch.BoundaryLoop.Boundaries)
        {
            var edge = edges.SingleOrDefault(e => boundary.ExistingBoundary == "Edge" + e.Value);
            if (!edges.Contains(edge) || !input.Body.TryGetEdgeCurveGeometry(edge, out var curve) || curve?.Line3 is not { } line)
                return Fail("surf-face-boundary-unresolved", "Each patch side must explicitly name its current straight EdgeN handle.");
            var controls = boundary.PatchSide switch
            {
                PatchBoundarySide.South => rows.Select(r => r[0]).ToArray(),
                PatchBoundarySide.North => rows.Select(r => r[^1]).ToArray(),
                PatchBoundarySide.West => rows[0].ToArray(),
                _ => rows[^1].ToArray()
            };
            var topologyEdge = input.Body.Topology.GetEdge(edge);
            input.Body.TryGetVertexPoint(topologyEdge.StartVertexId, out var a); input.Body.TryGetVertexPoint(topologyEdge.EndVertexId, out var b);
            var tol = operation.GeometricTolerance;
            if (controls.Any(p => (p - line.Origin).Cross(line.Direction.ToVector()).Length > tol) ||
                !(((controls[0] - a).Length <= tol && (controls[^1] - b).Length <= tol) || ((controls[0] - b).Length <= tol && (controls[^1] - a).Length <= tol)))
                return Fail("surf-face-boundary-mismatch", $"Patch side {boundary.PatchSide} does not retain its named edge endpoints/support.");
        }
        if (patch.BoundaryLoop.Boundaries.Select(b => b.ExistingBoundary).Distinct().Count() != 4)
            return Fail("surf-face-boundary-duplicate", "Each outer edge must be bound once.");
        var points = patch.Spline.ControlPoints.SelectMany(r => r).ToArray();
        var bounds = new SpatialInfluenceEnvelope(points.Min(p => p.X), points.Min(p => p.Y), points.Min(p => p.Z), points.Max(p => p.X), points.Max(p => p.Y), points.Max(p => p.Z));
        if (!operation.InfluenceEnvelope.Contains(bounds, operation.GeometricTolerance))
            return Fail("sculpt-envelope-exceeded", "The non-rational patch control hull exceeds the authorized influenceEnvelope.");
        var graft = BoundedFaceGraft.Apply(input.Body, new(faceId), patch, operation.GeometricTolerance, new(alignment));
        if (graft.Body is null) return SculptResult.Failure(graft.Diagnostics);
        var affected = associations.Where(a => a.FaceIds.Contains(faceId)).Select(a => a.SemanticTarget).ToHashSet(StringComparer.Ordinal);
        var inventory = input.SemanticInventory.Where(p => !affected.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        if (inventory.ContainsKey(patch.PatchId)) return Fail("surf-face-successor-duplicate", "The replacement patch identity already belongs to another current entity.");
        inventory[patch.PatchId] = new(patch.PatchId, SculptEntityKind.Surface, patch.GeometryFingerprint, "Explicit bounded face successor.");
        var id = BodyStateId.Derive(input.StateId.Value + "|ExactFaceGraft|" + operation.Canonical);
        inventory[input.BodyStableId] = new(input.BodyStableId, SculptEntityKind.Body, id.Value, "Exact body after bounded face graft.");
        var correspondence = associations.Select(a => new GeometricDeltaEntry(a.SemanticTarget,
            affected.Contains(a.SemanticTarget) ? GeometricChangeKind.Replaced : GeometricChangeKind.Preserved,
            affected.Contains(a.SemanticTarget) ? [patch.PatchId] : [a.SemanticTarget], "Retained topology; explicit face-support succession.")).ToArray();
        var delta = new GeometricDelta(input.StateId, id, operation.Reads,
            associations.Where(a => !affected.Contains(a.SemanticTarget)).Select(a => a.SemanticTarget).ToArray(), affected.ToArray(), [], [patch.PatchId], operation.MayModify,
            operation.InfluenceEnvelope, correspondence);
        var remapped = associations.Where(a => !affected.Contains(a.SemanticTarget)).Append(new(patch.PatchId, PersistentAssociationState.ReplacedBy, [faceId], "Retained face identity; explicitly replaced support.")).ToArray();
        var evidence = new SculptValidationEvidence[] { new("ExactFaceGraft", true, LocalityEvidenceLevel.CertifiedBounded,
            graft.Pcurves!.MaximumReconstructionDeviation, operation.GeometricTolerance, "Topology/edges/vertices and all neighboring supports retained. Pcurves and export preflight passed; global self-intersection is unqualified.") };
        var output = input with
        {
            StateId = id,
            PredecessorStateId = input.StateId,
            AuthoredName = name,
            Body = graft.Body,
            SemanticInventory = inventory,
            GeometryAssociations = remapped,
            Delta = delta,
            ValidationEvidence = evidence,
            RealizedStepText = null,
            ExactFacePatches = [.. input.ExactFacePatches.Where(p => !affected.Contains(p.PatchId)), SurfacePatchMetadata.From(patch, 1)],
            ConstructionAuthority = ConstructionAuthorityEvolution.Append(input, name, operation, id, delta, evidence)
        };
        return new(true, output, delta, evidence, []);
    }
}
