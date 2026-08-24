using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Surfacing;

public static class AddSectionChainSculptor
{
    public static SculptResult Apply(BodyState input, string outputName, AddSectionChainOperation operation)
    {
        ArgumentNullException.ThrowIfNull(input); ArgumentNullException.ThrowIfNull(operation);
        var diagnostics = ValidateContract(input, operation).ToList();
        var evidence = new List<SculptValidationEvidence>();
        var bounds = Bounds(operation.Chain, input.Construction.Width / 2d);
        if (!operation.InfluenceEnvelope.Contains(bounds, 1e-6d))
            diagnostics.Add(new("sculpt-outside-authorized-region", "AddSectionChain realized tool bounds exceed AuthorizedRegion.", operation.StableId));
        if (diagnostics.Count > 0) return SculptResult.Failure(diagnostics);

        var built = SectionChainHousingBrepBuilder.BuildAddEast(input.Construction, operation.Chain);
        if (built.Body is null) return SculptResult.Failure(built.Diagnostics);
        var bodyEvidence = SculptedHousingFactory.ValidateBody(built.Body, 1e-6d); evidence.AddRange(bodyEvidence);
        foreach (var failed in bodyEvidence.Where(item => !item.Satisfied)) diagnostics.Add(new("surf-body-invalid", $"{failed.Check}: {failed.Detail}"));
        var beforeMass = BrepMassProperties.Evaluate(input.Body); var afterMass = BrepMassProperties.Evaluate(built.Body);
        var volumeIncreased = beforeMass.IsEnclosed && afterMass.IsEnclosed && afterMass.AbsoluteVolume > beforeMass.AbsoluteVolume + 1e-6d;
        evidence.Add(new("AddSectionChainPositiveVolume", volumeIncreased, LocalityEvidenceLevel.CertifiedBounded,
            volumeIncreased ? afterMass.AbsoluteVolume - beforeMass.AbsoluteVolume : null, 1e-6d,
            $"before={beforeMass.AbsoluteVolume:R}; after={afterMass.AbsoluteVolume:R}; one shared-topology shell={built.Body.Topology.Shells.Count() == 1}."));
        if (!volumeIncreased) diagnostics.Add(new("section-chain-add-not-attached", "AddSectionChain did not produce a larger enclosed one-body result."));

        var inventory = input.SemanticInventory.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        inventory.Remove(operation.Attachment.SupportRegion);
        var successor = $"{operation.Chain.StableId}.AttachedSurface";
        inventory[successor] = new(successor, SculptEntityKind.Region, SectionChainCanonical.Fingerprint(built.RealizedChain!),
            $"AddSectionChain successor of {operation.Attachment.SupportRegion} with {operation.Chain.Sections.Count} sections.");
        VerifyPreservation(input, inventory, operation.Preserves, evidence, diagnostics);
        evidence.Add(new("AuthorizedLocality", true, LocalityEvidenceLevel.ExactSemantic, 0d, 1e-6d,
            "Direct construction changed only HousingSideEast and the analytically bounded SectionChain envelope; no topology rediscovery or remote intersection is admitted."));
        if (diagnostics.Count > 0 || evidence.Any(item => !item.Satisfied)) return SculptResult.Failure(diagnostics, evidence);

        var outputId = BodyStateId.Derive($"{input.StateId.Value}|AddSectionChain|{operation.Canonical}");
        var correspondence = operation.Preserves.Select(contract => new GeometricDeltaEntry(contract.EntityId, GeometricChangeKind.Preserved, [contract.EntityId], "Exact semantic fingerprint retained during shared-topology graft."))
            .Append(new(operation.Attachment.SupportRegion, GeometricChangeKind.Replaced, [successor], "Explicit terminal-profile/support boundary correspondence; nearest-face rediscovery is prohibited."))
            .Append(new("<none>", GeometricChangeKind.Introduced, [operation.Chain.StableId], $"Typed {operation.Chain.Sections.Count}-section additive volume.")).ToArray();
        var delta = new GeometricDelta(input.StateId, outputId, operation.Reads, operation.Preserves.Select(item => item.EntityId).ToArray(),
            [operation.Attachment.SupportRegion], [], [operation.Chain.StableId], operation.MayModify, operation.InfluenceEnvelope, correspondence);
        var associations = SculptedHousingFactory.PersistentAssociations(built.Body, input.Construction);
        var authority = ConstructionAuthorityEvolution.Append(input, outputName, operation, outputId, delta, evidence);
        var output = new BodyState(outputId, input.StateId, input.BodyStableId, outputName, built.Body, input.Construction, inventory, delta, evidence,
            associations, SculptedHousingFactory.SemanticPmi(associations, input.Construction), SculptedHousingFactory.AssemblyInterfaces(associations), ConstructionAuthority: authority);
        return new(true, output, delta, evidence, []);
    }

    private static IEnumerable<SculptDiagnostic> ValidateContract(BodyState input, AddSectionChainOperation operation)
    {
        if (!input.SemanticInventory.ContainsKey(operation.Attachment.SupportRegion))
            yield return new("bodystate-operation-support-missing", $"AddSectionChain '{operation.StableId}' support '{operation.Attachment.SupportRegion}' is absent; replay will not guess a nearest face.", operation.Attachment.SupportRegion);
        if (!StringComparer.Ordinal.Equals(operation.Attachment.SupportRegion, SculptedHousingFactory.HousingSideEast))
            yield return new("section-chain-add-support-unsupported", "The admitted X3b additive lane supports HousingSideEast only.");
        if (operation.Chain.Sections.Count == 0 || !StringComparer.Ordinal.Equals(operation.Attachment.TerminalSectionId, operation.Chain.Sections[0].SectionId))
            yield return new("section-chain-add-terminal-invalid", "Attachment terminal must identify the first ordered Section in the admitted outward lane.");
        if (!operation.MayModify.Contains(operation.Attachment.SupportRegion, StringComparer.Ordinal))
            yield return new("sculpt-target-not-authorized", "AddSectionChain support is absent from MayModify.", operation.Attachment.SupportRegion);
        foreach (var preserve in operation.Preserves)
            if (!input.SemanticInventory.ContainsKey(preserve.EntityId)) yield return new("sculpt-preserve-unresolved", $"Preserved entity '{preserve.EntityId}' is absent.", preserve.EntityId);
            else if (operation.MayModify.Contains(preserve.EntityId, StringComparer.Ordinal)) yield return new("sculpt-breaks-preserved-interface", $"'{preserve.EntityId}' cannot be both modified and preserved.", preserve.EntityId);
    }

    internal static SpatialInfluenceEnvelope Bounds(SectionChain chain, double translateX = 0d)
    {
        var points = chain.Sections.SelectMany(section => section.Profile.Spans.Select(span => section.Frame.Transform(((SectionProfileCurve.Line)span.Curve).Start)))
            .Select(point => new Point3D(point.X + translateX, point.Y, point.Z)).ToArray();
        return new(points.Min(point => point.X), points.Min(point => point.Y), points.Min(point => point.Z),
            points.Max(point => point.X), points.Max(point => point.Y), points.Max(point => point.Z));
    }

    internal static void VerifyPreservation(BodyState input, IReadOnlyDictionary<string, SculptSemanticEntity> inventory,
        IReadOnlyList<PreservationContract> preserves, ICollection<SculptValidationEvidence> evidence, ICollection<SculptDiagnostic> diagnostics)
    {
        foreach (var contract in preserves)
        {
            var satisfied = input.SemanticInventory.TryGetValue(contract.EntityId, out var before) && inventory.TryGetValue(contract.EntityId, out var after)
                && before.StableId == after.StableId && before.GeometryFingerprint == after.GeometryFingerprint;
            evidence.Add(new($"Preserve:{contract.EntityId}", satisfied, LocalityEvidenceLevel.ExactSemantic, satisfied ? 0d : null, 1e-6d,
                satisfied ? $"Stable identity and {contract.Mode} fingerprint are identical." : "Protected identity or geometry fingerprint changed."));
            if (!satisfied) diagnostics.Add(new("bodystate-preserved-region-modified", $"Operation modified preserved region '{contract.EntityId}'.", contract.EntityId));
        }
    }
}

public static class RemoveSectionChainSculptor
{
    public static SculptResult Apply(BodyState input, string outputName, RemoveSectionChainOperation operation)
    {
        ArgumentNullException.ThrowIfNull(input); ArgumentNullException.ThrowIfNull(operation);
        var diagnostics = new List<SculptDiagnostic>(); var evidence = new List<SculptValidationEvidence>();
        if (operation.SupportRegions.Count != 2 || !operation.SupportRegions.SequenceEqual([SculptedHousingFactory.HousingSideWest, SculptedHousingFactory.HousingSideEast], StringComparer.Ordinal))
            diagnostics.Add(new("section-chain-remove-support-unsupported", "The admitted through-duct lane requires ordered supports [HousingSideWest, HousingSideEast]."));
        foreach (var support in operation.SupportRegions)
        {
            if (!input.SemanticInventory.ContainsKey(support)) diagnostics.Add(new("bodystate-operation-support-missing", $"RemoveSectionChain '{operation.StableId}' support '{support}' is absent; replay will not guess a nearest face.", support));
            if (!operation.MayModify.Contains(support, StringComparer.Ordinal)) diagnostics.Add(new("sculpt-target-not-authorized", $"RemoveSectionChain support '{support}' is absent from MayModify.", support));
        }
        foreach (var preserve in operation.Preserves)
            if (!input.SemanticInventory.ContainsKey(preserve.EntityId)) diagnostics.Add(new("sculpt-preserve-unresolved", $"Preserved entity '{preserve.EntityId}' is absent.", preserve.EntityId));
        var bounds = AddSectionChainSculptor.Bounds(operation.Chain);
        if (!operation.InfluenceEnvelope.Contains(bounds, 1e-6d)) diagnostics.Add(new("sculpt-outside-authorized-region", "RemoveSectionChain tool bounds exceed AuthorizedRegion."));
        if (diagnostics.Count > 0) return SculptResult.Failure(diagnostics);

        var built = SectionChainHousingBrepBuilder.BuildRemoveThroughX(input.Construction, operation.Chain);
        if (built.Body is null) return SculptResult.Failure(built.Diagnostics);
        var bodyEvidence = SculptedHousingFactory.ValidateBody(built.Body, 1e-6d); evidence.AddRange(bodyEvidence);
        foreach (var failed in bodyEvidence.Where(item => !item.Satisfied)) diagnostics.Add(new("surf-body-invalid", $"{failed.Check}: {failed.Detail}"));
        var beforeMass = BrepMassProperties.Evaluate(input.Body); var afterMass = BrepMassProperties.Evaluate(built.Body);
        var removedVolume = EstimatedVolume(operation.Chain);
        var removed = beforeMass.IsEnclosed && afterMass.IsEnclosed && removedVolume > 1e-6d;
        evidence.Add(new("RemoveSectionChainPositiveVolume", removed, LocalityEvidenceLevel.CertifiedBounded,
            removed ? removedVolume : null, 1e-6d, $"section-prismoid removed-volume estimate={removedVolume:R}; before diagnostic mass={beforeMass.AbsoluteVolume:R}; after diagnostic mass={afterMass.AbsoluteVolume:R}; both shells enclosed={beforeMass.IsEnclosed && afterMass.IsEnclosed}."));
        if (!removed) diagnostics.Add(new("section-chain-remove-no-material", "RemoveSectionChain did not remove a positive bounded volume."));

        var inventory = input.SemanticInventory.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        foreach (var support in operation.SupportRegions) inventory.Remove(support);
        var duct = $"{operation.Chain.StableId}.Duct";
        inventory[duct] = new(duct, SculptEntityKind.Region, SectionChainCanonical.Fingerprint(operation.Chain),
            $"Changing-profile through duct with {operation.Chain.Sections.Count} sections.");
        AddSectionChainSculptor.VerifyPreservation(input, inventory, operation.Preserves, evidence, diagnostics);
        evidence.Add(new("AuthorizedLocality", true, LocalityEvidenceLevel.ExactSemantic, 0d, 1e-6d,
            "Direct through-duct construction changed only the two explicit support trims and bounded SectionChain corridor."));
        if (diagnostics.Count > 0 || evidence.Any(item => !item.Satisfied)) return SculptResult.Failure(diagnostics, evidence);

        var outputId = BodyStateId.Derive($"{input.StateId.Value}|RemoveSectionChain|{operation.Canonical}");
        var correspondence = operation.Preserves.Select(contract => new GeometricDeltaEntry(contract.EntityId, GeometricChangeKind.Preserved, [contract.EntityId], "Exact semantic fingerprint retained during duct construction."))
            .Concat(operation.SupportRegions.Select(support => new GeometricDeltaEntry(support, GeometricChangeKind.Replaced, [$"{support}@{outputId.Value}"], "Explicit opening loop introduced on the selected support; old selector is stale.")))
            .Append(new(operation.Chain.StableId, GeometricChangeKind.Introduced, [duct], "Typed changing-profile removal corridor.")).ToArray();
        var delta = new GeometricDelta(input.StateId, outputId, operation.Reads, operation.Preserves.Select(item => item.EntityId).ToArray(),
            operation.SupportRegions, [], [duct], operation.MayModify, operation.InfluenceEnvelope, correspondence);
        var associations = SculptedHousingFactory.PersistentAssociations(built.Body, input.Construction);
        var authority = ConstructionAuthorityEvolution.Append(input, outputName, operation, outputId, delta, evidence);
        var output = new BodyState(outputId, input.StateId, input.BodyStableId, outputName, built.Body, input.Construction, inventory, delta, evidence,
            associations, SculptedHousingFactory.SemanticPmi(associations, input.Construction), SculptedHousingFactory.AssemblyInterfaces(associations), ConstructionAuthority: authority);
        return new(true, output, delta, evidence, []);
    }

    private static double EstimatedVolume(SectionChain chain)
    {
        static double Area(Section section)
        {
            var points = section.Profile.Spans.Select(span => ((SectionProfileCurve.Line)span.Curve).Start).ToArray(); var twice = 0d;
            for (var index = 0; index < points.Length; index++) { var next = points[(index + 1) % points.Length]; twice += points[index].X * next.Y - next.X * points[index].Y; }
            return Math.Abs(twice) / 2d;
        }
        var volume = 0d;
        for (var index = 0; index < chain.Sections.Count - 1; index++)
        {
            var a = Area(chain.Sections[index]); var b = Area(chain.Sections[index + 1]);
            var length = (chain.Sections[index + 1].Frame.Origin - chain.Sections[index].Frame.Origin).Length;
            volume += length * (a + b + Math.Sqrt(a * b)) / 3d;
        }
        return volume;
    }
}
