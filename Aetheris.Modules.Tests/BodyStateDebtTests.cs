using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Firmament;
using Aetheris.Surfacing;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;
using Xunit;

namespace Aetheris.Modules.Tests;

public sealed class BodyStateDebtTests
{
    [Theory]
    [InlineData("Primitives/cone", "Body")]
    [InlineData("Primitives/sphere", "Body")]
    [InlineData("Primitives/torus", "Body")]
    [InlineData("Primitives/rounded-box", "Base")]
    [InlineData("Gears/spur-basic", "DriveGear")]
    [InlineData("Features/Sweep/circular-arc-sweep", "Wire")]
    [InlineData("Revolve/stepped-shaft", "Shaft")]
    [InlineData("Revolve/arbitrary-axis", "TiltedRing")]
    public void EngineeringAdoptionUsesTheExistingCompilerAcrossMaterialFamilies(string relative, string bodyName)
    {
        var path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Fixture("box"))!, "../../Canonical", relative + ".firmament"));
        var source = File.ReadAllText(path);
        var reference = FirmamentBuildAndExport.CompileSource(source, Path.GetDirectoryName(path));
        Assert.True(reference.IsSuccess, string.Join(";", reference.Diagnostics.Select(d => d.Message)));
        // The wrapper is a Sculpting product. Keep the reference's explicit
        // Mechanical selector for the reference build, not for its new owner.
        var adoptionSource = FirmamentFrontendSchemas.Select(source).Source;
        var model = Assert.Single(FirmamentSyntaxParser.Parse(adoptionSource).Document.Statements.OfType<DeclarationSyntax>(), d => d.Type.Name.Text == "Model");
        var at = model.Body!.ContentSpan.Start + model.Body.ContentSpan.Length;
        var stateSource = "schema Sculpting\n" + adoptionSource.Insert(at, "\nBodyState Snapshot { input: " + bodyName + " }\noutput: Snapshot\n");
        var result = SculptingAuthoring.Compile(stateSource, path);
        Assert.True(result.IsSuccess, string.Join(";", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal(reference.Value.StepText, SculptStepExporter.Export(result.OutputState!, result.ModelName).Step);
        var saved = ConstructionStateSerializer.Serialize(result.OutputState!.ConstructionAuthority!);
        var loaded = ConstructionStateSerializer.Deserialize(saved.Json!);
        Assert.True(loaded.IsSuccess, string.Join(";", loaded.Diagnostics.Select(d => d.Message)));
        var replay = ConstructionStateReplayer.Replay(loaded.ConstructionState!);
        Assert.True(replay.IsSuccess, string.Join(";", replay.Diagnostics.Select(d => d.Message)));
        Assert.Equal(reference.Value.StepText, SculptStepExporter.Export(replay.OutputState!, result.ModelName).Step);
    }

    [Theory]
    [InlineData("box")]
    [InlineData("cylinder")]
    [InlineData("counterbore-chamfer")]
    [InlineData("profile")]
    [InlineData("explicit-pmi")]
    public void EngineeringBodiesRetainExactProductAndReplay(string name)
    {
        var path = Fixture(name);
        var reference = FirmamentBuildAndExport.CompileSource(File.ReadAllText(Fixture(name + ".reference")));
        Assert.True(reference.IsSuccess, string.Join(";", reference.Diagnostics.Select(d => d.Message)));
        var result = SculptingAuthoring.CompileFile(path);
        Assert.True(result.IsSuccess, string.Join(";", result.Diagnostics.Select(d => d.Message)));
        var state = result.OutputState!;
        Assert.Null(state.Construction);
        Assert.DoesNotContain(SculptedHousingFactory.CrownRegion, state.SemanticInventory.Keys);
        Assert.Equal(reference.Value.StepText, SculptStepExporter.Export(state, result.ModelName).Step);
        var renamedExport = SculptStepExporter.Export(state, result.ModelName + "_renamed");
        Assert.False(renamedExport.IsSuccess);
        Assert.Null(renamedExport.Step);
        Assert.Contains(renamedExport.Diagnostics, d => d.Code == "bodystate-product-name-mismatch");
        var serialized = ConstructionStateSerializer.Serialize(state.ConstructionAuthority!);
        Assert.True(serialized.IsSuccess);
        var deserialized = ConstructionStateSerializer.Deserialize(serialized.Json!);
        Assert.True(deserialized.IsSuccess);
        var replay = ConstructionStateReplayer.Replay(deserialized.ConstructionState!, "Replayed");
        Assert.True(replay.IsSuccess, string.Join(";", replay.Diagnostics.Select(d => d.Message)));
        Assert.Equal("Replayed", replay.OutputState!.AuthoredName);
        Assert.Equal(state.StateId, replay.OutputState.StateId);
        Assert.Equal(state.BodyStableId, replay.OutputState.BodyStableId);
        Assert.Equal(reference.Value.StepText, SculptStepExporter.Export(replay.OutputState, result.ModelName).Step);
        Assert.Equal(BrepMassProperties.Evaluate(state.Body).AbsoluteVolume, BrepMassProperties.Evaluate(replay.OutputState.Body).AbsoluteVolume);
    }

    [Fact]
    public void PlainHousingCanBeEditedWithoutInventedHolesOrHolePmi()
    {
        var result = SculptingAuthoring.CompileFile(Fixture("plain-offset"));
        Assert.True(result.IsSuccess, string.Join(";", result.Diagnostics.Select(d => d.Message)));
        var state = result.OutputState!;
        Assert.Empty(state.Construction!.Holes);
        Assert.DoesNotContain(SculptedHousingFactory.MountingHolePattern, state.SemanticInventory.Keys);
        Assert.DoesNotContain(state.SemanticPmi!, p => p is Aetheris.Kernel.Core.Step242.Step242SemanticPmiHole);
        Assert.True(BrepMassProperties.Evaluate(state.Body).AbsoluteVolume > 6000);
        var replay = ConstructionStateReplayer.Replay(state.ConstructionAuthority!);
        Assert.True(replay.IsSuccess, string.Join(";", replay.Diagnostics.Select(d => d.Message)));
        Assert.Equal(state.StateId, replay.OutputState!.StateId);
        Assert.Equal(SculptStepExporter.Export(state, result.ModelName).Step, SculptStepExporter.Export(replay.OutputState, result.ModelName).Step);
    }

    [Fact]
    public void UnsupportedHousingEditCannotReplaceAnEngineeringCylinder()
    {
        var result = SculptingAuthoring.CompileFile(Fixture("cylinder"));
        Assert.True(result.IsSuccess, string.Join(";", result.Diagnostics.Select(d => d.Message)));
        var input = result.OutputState!;
        var operation = new OffsetRegionOperation("Raise", "HousingCrown", 3, 4, 4, ["HousingCrown"], new(-10,-10,0,10,10,30), [], []);
        var failed = OffsetRegionSculptor.Apply(input, "Bad", operation);
        Assert.False(failed.IsSuccess);
        Assert.Null(failed.OutputState);
        Assert.Contains(failed.Diagnostics, d => d.Code == "bodystate-operation-capability-unsupported");
        var authority = input.ConstructionAuthority!.Append(new("Raise", operation.OperationKind, 1, input.StateId,
            BodyStateId.Derive("bad"), "Bad", operation, [], [], operation.InfluenceEnvelope, [],
            new(input.StateId, BodyStateId.Derive("bad"), [], [], [], [], [], [], operation.InfluenceEnvelope, []), []));
        var replay = ConstructionStateReplayer.Replay(authority);
        Assert.False(replay.IsSuccess);
        Assert.Equal(input.StateId, replay.AuthoritativePredecessor!.StateId);
        Assert.Null(replay.OutputState);
        Assert.Equal(input.RealizedStepText, replay.AuthoritativePredecessor.RealizedStepText);
    }

    [Fact]
    public void RepeatedHolesRetainAttachedGeometryAndAllPriorFeatures()
    {
        var result = SculptingAuthoring.CompileFile(Fixture("second-hole-after-add"));
        Assert.True(result.IsSuccess, string.Join(";", result.Diagnostics.Select(d => d.Message)));
        var state = result.OutputState!;
        Assert.Equal(4, state.Construction!.Holes.Count);
        Assert.Contains("GripChain.AttachedSurface", state.SemanticInventory.Keys);
        Assert.Contains("ServicePort", state.SemanticInventory.Keys);
        Assert.Contains("SecondPort", state.SemanticInventory.Keys);
        Assert.Equal(3, state.ConstructionAuthority!.Operations.Count);
        var replay = ConstructionStateReplayer.Replay(state.ConstructionAuthority);
        Assert.True(replay.IsSuccess, string.Join(";", replay.Diagnostics.Select(d => d.Message)));
        Assert.Equal(state.StateId, replay.OutputState!.StateId);
        Assert.Equal(SculptStepExporter.Export(state, result.ModelName).Step, SculptStepExporter.Export(replay.OutputState, result.ModelName).Step);
        var previous = result.States["PortAdded"];
        var beforeMass = BrepMassProperties.Evaluate(previous.Body);
        var afterMass = BrepMassProperties.Evaluate(state.Body);
        // The generic trimmed-face mass evaluator is a tessellated sanity estimate.
        // Qualify exact geometry against the independently ordered construction instead.
        var reference = SculptingAuthoring.CompileFile(Fixture("second-hole-after-add.reference"));
        Assert.True(reference.IsSuccess, string.Join(";", reference.Diagnostics.Select(d => d.Message)));
        Assert.Equal(SculptStepExporter.Export(reference.OutputState!, result.ModelName).Step, SculptStepExporter.Export(state, result.ModelName).Step);
        Assert.Equal(4, state.Body.Geometry.Surfaces.Count(s => s.Value.Cylinder is not null));
        var holes = state.SemanticPmi!.OfType<Aetheris.Kernel.Core.Step242.Step242SemanticPmiHole>().OrderBy(p => p.Diameter).ToArray();
        Assert.Equal(new[] { 3d, 4d }, holes.Select(p => p.Diameter));
        Assert.All(holes, p => { Assert.Equal(2, p.Quantity); Assert.Equal(2, p.GeometricFaceIds.Count); });
        Assert.True(beforeMass.IsEnclosed && afterMass.IsEnclosed);
        Assert.True(afterMass.AbsoluteVolume > 80 * 40 * 20);
    }

    [Theory]
    [InlineData("unsupported-cylinder-edit", "bodystate-operation-capability-unsupported")]
    [InlineData("history-drop", "bodystate-history-capability-unsupported")]
    public void UnsupportedHistoryFailsBeforePublishingAnOutput(string name, string code)
    {
        var result = SculptingAuthoring.CompileFile(Fixture(name));
        Assert.False(result.IsSuccess);
        Assert.Null(result.OutputState);
        Assert.DoesNotContain("Bad", result.States.Keys);
        Assert.Contains(result.Diagnostics, d => d.Code == code && d.SourceSpan is not null);
    }

    [Theory]
    [InlineData("input: Stock", "input: Missing", "bodystate-input-unresolved")]
    [InlineData("BodyState Base { input: Stock }", "Struct Empty {} BodyState Base { input: Empty }", "bodystate-input-unresolved")]
    [InlineData("size: [30mm, 20mm, 10mm]", "size: [30mm, 20mm, 10mm] heigth: 2mm", "firmament-v2-canonical-field-unknown")]
    [InlineData("input: Stock", "input: Stock size: [1mm, 1mm, 1mm]", "firmament-v2-canonical-field-unknown")]
    public void InvalidIntentRejectsWithOriginalLocation(string before, string after, string code)
    {
        var path = Fixture("box");
        var result = SculptingAuthoring.Compile(File.ReadAllText(path).Replace(before, after), path);
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code.StartsWith(code, StringComparison.Ordinal) || d.Message.Contains(code, StringComparison.Ordinal));
        Assert.All(result.Diagnostics, d => { Assert.Equal(path, d.SourceFile); Assert.NotNull(d.SourceSpan); });
    }

    [Fact]
    public void DuplicateOrBlankHoleIdentitiesCannotCollapseSemanticInventory()
    {
        foreach (var holes in new HousingHole[][]
        {
            [new("same", -5, 0, 2), new("same", 5, 0, 2)],
            [new("", 0, 0, 2)]
        })
        {
            var result = SculptedHousingFactory.CreateBase("Base", 30, 20, 10, holes);
            Assert.False(result.IsSuccess);
            Assert.Null(result.OutputState);
            Assert.Contains(result.Diagnostics, d => d.Code is "sculpt-hole-duplicate" or "sculpt-hole-identity-required");
        }
    }

    [Fact]
    public void ReplacementControlNetIsPartOfStateIdentityAndDurableReplay()
    {
        var path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Fixture("box"))!, "../../Canonical/Sculpting/surf-x1-freeform-housing.firmament"));
        var compiled = SculptingAuthoring.CompileFile(path);
        Assert.True(compiled.IsSuccess, string.Join(";", compiled.Diagnostics.Select(d => d.Message)));
        var original = compiled.States["FreeformCrown"];
        var payload = Assert.IsType<ReplaceRegionOperation>(Assert.Single(original.ConstructionAuthority!.Operations).Payload);
        var patch = Assert.IsType<BSplineSurfacePatch>(payload.ReplacementPatch);
        Assert.NotEqual(patch.GeometryFingerprint, new BSplineSurfacePatch(patch.PatchId, patch.Spline,
            new(.1, .9, .1, .9), patch.BoundaryLoop).GeometryFingerprint);
        Assert.NotEqual(patch.GeometryFingerprint, new BSplineSurfacePatch(patch.PatchId, patch.Spline,
            patch.ParameterDomain, patch.BoundaryLoop, reversedOrientation: true).GeometryFingerprint);
        var points = patch.Spline.ControlPoints.Select(row => row.ToArray()).ToArray();
        points[2][2] = points[2][2] + new Vector3D(0, 0, 1);
        var spline = new BSplineSurfaceWithKnots(patch.Spline.DegreeU, patch.Spline.DegreeV, points, patch.Spline.SurfaceForm,
            patch.Spline.UClosed, patch.Spline.VClosed, patch.Spline.SelfIntersect, patch.Spline.KnotMultiplicitiesU,
            patch.Spline.KnotMultiplicitiesV, patch.Spline.KnotValuesU, patch.Spline.KnotValuesV, patch.Spline.KnotSpec);
        var changedPatch = new BSplineSurfacePatch(patch.PatchId, spline, patch.ParameterDomain, patch.BoundaryLoop);
        var changed = ReplaceRegionSculptor.Apply(compiled.States["Base"], "Changed", payload with { ReplacementPatch = changedPatch });
        Assert.True(changed.IsSuccess, string.Join(";", changed.Diagnostics.Select(d => d.Message)));
        Assert.NotEqual(original.StateId, changed.OutputState!.StateId);
        Assert.NotEqual(original.SemanticInventory[patch.PatchId].GeometryFingerprint, changed.OutputState.SemanticInventory[patch.PatchId].GeometryFingerprint);
        Assert.NotEqual(SculptStepExporter.Export(original, "Shape").Step, SculptStepExporter.Export(changed.OutputState, "Shape").Step);
        var saved = ConstructionStateSerializer.Serialize(changed.OutputState.ConstructionAuthority!);
        Assert.True(saved.IsSuccess, string.Join(";", saved.Diagnostics.Select(d => d.Message)));
        var loaded = ConstructionStateSerializer.Deserialize(saved.Json!);
        Assert.True(loaded.IsSuccess, string.Join(";", loaded.Diagnostics.Select(d => d.Message)));
        var replay = ConstructionStateReplayer.Replay(loaded.ConstructionState!);
        Assert.True(replay.IsSuccess, string.Join(";", replay.Diagnostics.Select(d => d.Message)));
        Assert.Equal(changed.OutputState.StateId, replay.OutputState!.StateId);
        Assert.Equal(SculptStepExporter.Export(changed.OutputState, "Shape").Step, SculptStepExporter.Export(replay.OutputState, "Shape").Step);

        var imported = Step242Importer.ImportBody(SculptStepExporter.Export(original, "Shape").Step!);
        Assert.True(imported.IsSuccess);
        var adopted = ImportedFaceRegionReplacer.AdoptImportedBody(original, imported.Value, "Imported");
        Assert.Null(adopted.ConstructionAuthority);
        var refusal = OffsetRegionSculptor.Apply(adopted, "Bad", new("Bad", "HousingCrown", 1, 10, 10, [], new(-50,-40,20,50,40,30), [], []));
        Assert.False(refusal.IsSuccess);
        Assert.Contains(refusal.Diagnostics, d => d.Code == "bodystate-imported-reconstruction-unsupported");
    }

    [Fact]
    public void SectionFramesAndSplineKnotsParticipateInConstructionIdentity()
    {
        var chain = SectionChainTemplates.TwoProfileRuled();
        var first = chain.Sections[0];
        var rotated = first with { Frame = SectionFrame.Create(first.Frame.Origin, new(0, 1, 0), new(-1, 0, 0)) };
        Assert.NotEqual(SectionChainCanonical.Fingerprint(chain), SectionChainCanonical.Fingerprint(chain with { Sections = [rotated, .. chain.Sections.Skip(1)] }));
        var span = first.Profile.Spans[0];
        var curve = new SectionProfileCurve.PolynomialBSpline(1, [new(0,0), new(1,0), new(2,0)], [2,1,2], [0,.5,1]);
        var a = chain with { Sections = [first with { Profile = first.Profile with { Spans = [span with { Curve = curve }] } }, .. chain.Sections.Skip(1)] };
        var b = a with { Sections = [a.Sections[0] with { Profile = a.Sections[0].Profile with { Spans = [span with { Curve = curve with { KnotValues = [0,.25,1] } }] } }, .. a.Sections.Skip(1)] };
        Assert.NotEqual(SectionChainCanonical.Fingerprint(a), SectionChainCanonical.Fingerprint(b));
    }

    private static string Fixture(string name)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Aetheris.slnx"))) root = root.Parent;
        return Path.Combine(root!.FullName, "fixtures/Regression/BodyStateDebt", name + ".firmament");
    }
}
