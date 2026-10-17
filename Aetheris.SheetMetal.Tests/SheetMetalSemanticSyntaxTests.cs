using Aetheris.Kernel.Core.Step242;
using Xunit;

namespace Aetheris.SheetMetal.Tests;

public sealed class SheetMetalSemanticSyntaxTests
{
    [Theory]
    [InlineData("l-bracket-with-hole")]
    [InlineData("l-bracket-multiple-holes")]
    [InlineData("sensor-bracket")]
    [InlineData("u-channel")]
    public void SharedProfileOnImplicitBasePreservesLegacyGeometryAndIdentities(string name)
    {
        var original = SheetMetalFirmament.CompileFile(Path.Combine(Repo, "fixtures/Compatibility/FirmamentV2/SheetMetal", name + ".firmament"));
        var modern = SheetMetalFirmament.CompileFile(Path.Combine(Repo, "fixtures/Canonical/SheetMetal", name + ".firmament"));
        Assert.Equal(Step(original), Step(modern));
        Assert.True(modern.Spec!.LegacySyntax);
        Assert.Equal(original.Part!.Regions.Select(r => r.StableId), modern.Part!.Regions.Select(r => r.StableId));
        Assert.Equal(original.Part.Bends.Select(b => b.StableId), modern.Part.Bends.Select(b => b.StableId));
        Assert.Equal(original.Part.Features.Select(f => f.StableId), modern.Part.Features.Select(f => f.StableId));
        Assert.Equal(original.FlatPattern!.DeterministicHash, modern.FlatPattern!.DeterministicHash);
    }

    private static string Repo
    {
        get
        {
            var directory=new DirectoryInfo(AppContext.BaseDirectory);
            while(directory is not null && !File.Exists(Path.Combine(directory.FullName,"Aetheris.slnx")))directory=directory.Parent;
            return directory!.FullName;
        }
    }
    private static string Source(string name)=>File.ReadAllText(Path.Combine(Repo,"fixtures/Regression/SheetMetalSemantics",name+".firmament"));
    private static SheetMetalAuthoringResult Compile(string name)=>SheetMetalFirmament.Compile(Source(name),name+".firmament");
    private static string Step(SheetMetalAuthoringResult result)
    {
        Assert.True(result.IsSuccess,string.Join('\n',result.Diagnostics.Select(d=>d.Message)));
        var step=Step242Exporter.ExportBody(result.Part!.FormedBody!,result.Spec!.Manufacturing.ToStep242SemanticPmi(result.Part));
        Assert.True(step.IsSuccess);return step.Value;
    }

    [Theory]
    [InlineData("layout-unknown-field","firmament-v2-canonical-field-unknown")]
    [InlineData("layout-unknown-feature-field","firmament-v2-canonical-field-unknown")]
    [InlineData("layout-modern-xz","firmament-v2-placement-unsupported")]
    [InlineData("layout-unknown-statement","firmament-v2-canonical-declaration-unknown")]
    [InlineData("layout-false-require","firmament-concept-requirement-failed")]
    [InlineData("layout-contract-missing","firmament-concept-missing-member")]
    [InlineData("layout-contract-semicolon","firmament-concept-missing-member")]
    [InlineData("layout-extra-unused-pattern","sheetmetal-layout-owner-ambiguous")]
    [InlineData("manufacturing-unknown-pmi","sheetmetal-manufacturing-pmi-invalid")]
    [InlineData("manufacturing-unknown-field","firmament-v2-canonical-field-unknown")]
    [InlineData("manufacturing-negative-tolerance","sheetmetal-manufacturing-pmi-invalid")]
    [InlineData("manufacturing-zero-quantity","sheetmetal-manufacturing-pmi-invalid")]
    [InlineData("manufacturing-unknown-kind","sheetmetal-manufacturing-pmi-invalid")]
    [InlineData("manufacturing-second-pmi","sheetmetal-manufacturing-pmi-invalid")]
    public void IgnoredIntentCannotProduceGeometry(string specimen,string code)
    {
        var result=Compile(specimen);
        Assert.False(result.IsSuccess);Assert.Null(result.Part);
        var diagnostic=Assert.Single(result.Diagnostics.Where(d=>d.Code==code).Take(1));
        Assert.False(string.IsNullOrWhiteSpace(diagnostic.Message));
        Assert.Equal(specimen+".firmament",diagnostic.SourceFile);
        Assert.NotNull(diagnostic.SourceSpan);
    }

    [Theory]
    [InlineData("layout-point2")]
    [InlineData("layout-no-semicolons")]
    [InlineData("layout-suffix-capture")]
    public void ModernPointSpellingAndScopedLookupPreserveBaselineGeometry(string name)
    {
        var original=SheetMetalFirmament.CompileFile(Path.Combine(Repo,"fixtures/Canonical/SheetMetal/semantic-panel.firmament"));
        var modern=Compile(name);
        Assert.Equal(Step(original),Step(modern));
        Assert.Equal(original.FlatPattern!.MaterialArea,modern.FlatPattern!.MaterialArea);
        Assert.Equal(50,Assert.Single(modern.Spec!.SemanticLayout.Patterns).CenterX);
        Assert.Equal(original.Part!.Features.Select(f=>f.StableId),modern.Part!.Features.Select(f=>f.StableId));
    }

    [Fact]
    public void UnnamedPmiPreservesEveryExportedRequirement()
    {
        var original=SheetMetalFirmament.CompileFile(Path.Combine(Repo,"fixtures/InvestorPitch/sheet-metal-bracket.firmament"));
        var modern=Compile("manufacturing-unnamed-pmi");
        Assert.Equal(Step(original),Step(modern));
        var pmi=Step242SemanticPmiInspector.Inspect(Step(modern));
        Assert.Equal(13,pmi.Items.Count);Assert.Equal(2,pmi.GeometricToleranceCount);
        Assert.Contains(pmi.Items,i=>i.Kind=="Position" && i.DatumReferences.SequenceEqual(["A","B"]) && i.GeometricFaceEntityIds.Count==2);
    }

    [Theory]
    [InlineData("manufacturing-comment-in-string")]
    [InlineData("manufacturing-direct-hole-target")]
    [InlineData("manufacturing-typed-dimension")]
    public void FormerBindingGapsBuildThroughTheRealExporter(string name)
    {
        var result=Compile(name);var pmi=Step242SemanticPmiInspector.Inspect(Step(result));
        Assert.True(pmi.Success);Assert.Equal(13,pmi.Items.Count);
        if(name=="manufacturing-direct-hole-target")
        { Assert.Equal(5,result.Part!.Features.Count);Assert.Contains(pmi.Items,i=>i.Target=="Extra" && i.GeometricFaceEntityIds.Count>0); }
        if(name=="manufacturing-comment-in-string")Assert.Equal("CNC // process specification",result.Spec!.Manufacturing.BlankProcess);
        if(name=="manufacturing-typed-dimension")Assert.NotEmpty(result.Spec!.Manufacturing.Dimensions.Single(d=>d.Name=="BaseWidth").DimensionOrigins);
    }

    [Fact]
    public void TypedRecordRequirementsReachGeometryAndExplicitPmi()
    {
        var result=Compile("modern-layout-release");var pmi=Step242SemanticPmiInspector.Inspect(Step(result));
        Assert.Equal(100,result.Spec!.Base.Width);Assert.Equal(2,result.Part!.Features.Count);
        var drill=Assert.Single(pmi.Items,i=>i.Kind=="Diameter");
        Assert.Equal(6,drill.Value);Assert.Equal(0.1,drill.TolerancePlus);Assert.Equal(0.05,drill.ToleranceMinus);Assert.Equal(2,drill.GeometricFaceEntityIds.Count);
        Assert.NotEmpty(result.Spec.Manufacturing.Dimensions.Single(d=>d.Name=="drill").DimensionOrigins);
        Assert.Contains(pmi.Items,i=>i.Text=="Cut before forming // keep this text");
        Assert.Equal("5052-H32 // shop specification",result.Spec.Manufacturing.MaterialSpecification);
    }

    [Fact]
    public void KeyedPatternUsesSharedFeatureExpansionAndStableIdentities()
    {
        var original=Compile("keyed-features");var step=Step(original);
        Assert.Equal(new[]{"mounting.left","mounting.right"},original.Spec!.Cuts.Select(c=>c.Name));
        Assert.Equal(new[]{20d,80d},original.Spec.Cuts.Select(c=>c.X));
        var reordered=SheetMetalFirmament.Compile(Source("keyed-features").Replace("left => Point2(20mm, 30mm)\n    right => Point2(80mm, 30mm)","right => Point2(80mm, 30mm)\n    left => Point2(20mm, 30mm)",StringComparison.Ordinal));
        Assert.True(reordered.IsSuccess,string.Join('\n',reordered.Diagnostics.Select(d=>d.Message)));
        Assert.Equal(original.Spec.Cuts.OrderBy(c=>c.Name),reordered.Spec!.Cuts.OrderBy(c=>c.Name));
        Assert.Contains(Step242SemanticPmiInspector.Inspect(step).Items,i=>i.Target=="mounting.left" && i.Value==6 && i.GeometricFaceEntityIds.Count==1);
    }

    [Fact]
    public void LegacySlotAndMisleadingRectangleAliasKeepTheirExactCapsule()
    {
        var slot=Compile("layout-slot");var rectangle=Compile("layout-rectangle");
        Assert.Equal(Step(slot),Step(rectangle));
        Assert.Equal(7860.553063332699,slot.FlatPattern!.MaterialArea!.Value,8);
    }

    [Fact]
    public void KeyedCutRecipeUsesTheSharedExactProfileConsumer()
    {
        var result=Compile("keyed-profile-cuts");var pmi=Step242SemanticPmiInspector.Inspect(Step(result));
        Assert.Equal(new[]{"vents.left","vents.right"},result.Spec!.Cuts.Select(c=>c.Name));
        Assert.All(result.Spec.Cuts,c=>Assert.NotNull(c.Profile));
        Assert.Equal(6000-2*(36+9*Math.PI),result.FlatPattern!.MaterialArea!.Value,8);
        Assert.Contains(pmi.Items,i=>i.Target=="vents.left" && i.Kind=="Annotation" && i.GeometricFaceEntityIds.Count>0);
    }

    [Theory]
    [InlineData("count: 2","count: 2; Count: 3")]
    [InlineData("kind: Linear","kind: Linear; Kind: Thickness")]
    [InlineData("on: deck; at: deck.Center","on: deck; at: right")]
    public void DuplicateAliasesAndUnresolvedLayoutValuesReject(string before,string after)
    {
        var result=SheetMetalFirmament.Compile(Source("modern-layout-release").Replace(before,after,StringComparison.Ordinal));
        Assert.False(result.IsSuccess);Assert.Null(result.Part);Assert.NotEmpty(result.Diagnostics);
    }

    [Fact]
    public void ProfileDeltaModernFieldsAndTypedAnchorsPreserveMaterial()
    {
        var legacy=File.ReadAllText(Path.Combine(Repo,"fixtures/Canonical/SheetMetal/profiledelta-remove-area.firmament"));
        var old=SheetMetalFirmament.Compile(legacy);
        var modern=legacy.Replace("Anchor: CenteredAt 50mm;","anchor: CenteredAt(anchorCenter)",StringComparison.Ordinal);
        foreach(var field in new[]{"On","Side","Offset","Kind","To","Run","At"})modern=modern.Replace(field+":",char.ToLowerInvariant(field[0])+field[1..]+":",StringComparison.Ordinal);
        modern="Static anchorCenter: Length = 50mm\n"+modern;
        var result=SheetMetalFirmament.Compile(modern);
        Assert.Equal(Step(old),Step(result));Assert.Equal(old.FlatPattern!.MaterialArea,result.FlatPattern!.MaterialArea);
        var unknown=SheetMetalFirmament.Compile(modern.Replace("run: 15mm;","run: 15mm; ignored: 1mm;",StringComparison.Ordinal));
        Assert.False(unknown.IsSuccess);Assert.Null(unknown.Part);
        var bodyless=SheetMetalFirmament.Compile(legacy+"\nProfileDelta Empty\n");
        Assert.False(bodyless.IsSuccess);Assert.NotEmpty(bodyless.Diagnostics);
    }

    [Fact]
    public void ForwardLocalDatumShadowsAnAlreadyBoundGlobalDatum()
    {
        var source="""
        SheetMetal panel { thickness: 1mm; Base deck { profile: Rectangle { width: 100mm; height: 60mm } } Datum later { on: deck; at: Point2(55mm,30mm) } }
        Concept Struct layout {
            Datum first { on: deck; at: later }
            Datum later { on: deck; at: Point2(50mm,30mm) }
            Pattern holes { on: deck; feature: Circle { diameter: 6mm }; center: first; count: 1; pitch: Vector2(10mm,0mm) }
        }
        """;
        var result=SheetMetalFirmament.Compile(source);Assert.True(result.IsSuccess,string.Join('\n',result.Diagnostics.Select(d=>d.Message)));
        Assert.Equal(50,Assert.Single(result.Spec!.Cuts).X);
        var cyclic=SheetMetalFirmament.Compile(source.Replace("at: Point2(50mm,30mm)","at: first",StringComparison.Ordinal));
        Assert.False(cyclic.IsSuccess);Assert.Null(cyclic.Part);
    }

    [Fact]
    public void ImportsCannotBeIgnoredOrDiscoveredInsideStrings()
    {
        var original=Source("modern-layout-release");
        var unresolved=SheetMetalFirmament.Compile("include \"missing.firmament\"\n"+original);
        Assert.False(unresolved.IsSuccess);Assert.Contains(unresolved.Diagnostics,d=>d.Code=="sheetmetal-module-reference-unresolved");
        var quoted=SheetMetalFirmament.Compile(original.Replace("Laser cut # deburr","Use Profile.Modifications; // literal text",StringComparison.Ordinal));
        Assert.True(quoted.IsSuccess,string.Join('\n',quoted.Diagnostics.Select(d=>d.Message)));
        Assert.Equal("Use Profile.Modifications; // literal text",quoted.Spec!.Manufacturing.BlankProcess);
    }

    [Theory]
    [InlineData("heigth: 5mm")]
    [InlineData("Fillet ignored { radius: 2mm }")]
    public void ExtensionScopesCannotBypassStatementAndFieldAdmission(string extra)
    {
        var result=SheetMetalFirmament.Compile(Source("keyed-features")+"\nExtend SheetMetal keyedPanel { "+extra+" }");
        Assert.False(result.IsSuccess);Assert.Null(result.Part);Assert.NotEmpty(result.Diagnostics);
    }

    [Fact]
    public void OrdinarySharedConstructionRetainsItsOwnFieldAndFrameAdmission()
    {
        var result=Compile("shared-construction-layout");Step(result);
        Assert.Equal(100,result.Spec!.Base.Width);Assert.Equal(6000-9*Math.PI,result.FlatPattern!.MaterialArea!.Value,8);
        var unknown=SheetMetalFirmament.Compile(Source("shared-construction-layout").Replace("size: [width, 60mm]","size: [width, 60mm]; heigth: 3mm",StringComparison.Ordinal));
        Assert.False(unknown.IsSuccess);Assert.Null(unknown.Part);
        Assert.Contains(unknown.Diagnostics,d=>d.Code=="firmament-v2-canonical-field-unknown" && d.SourceSpan is not null);
    }

    [Fact]
    public void ProfileTemplateKeepsTheSharedToleranceEnvironment()
    {
        var source="""
        Static width: Length = 100mm tol 0.25mm
        Template<w: Length> Profile Blank {
            Rect2 boundary { center: [50mm,30mm]; size: [w,60mm] }
            Loop Outer { boundary |> TraceLoop }
        }
        Profile stock = Blank<w: width>
        SheetMetal templated {
            thickness: 1.5mm
            Base deck { profile: stock }
            Pmi { Dimension width { kind: Linear; target: deck; dimension: width } }
        }
        """;
        var result=SheetMetalFirmament.Compile(source);
        var pmi=Step242SemanticPmiInspector.Inspect(Step(result));
        Assert.Equal(100,result.Spec!.Base.Width);Assert.Equal(6000,result.FlatPattern!.MaterialArea!.Value,8);
        Assert.Contains(pmi.Items,i=>i.Value==100 && i.TolerancePlus==0.25);
    }

    [Fact]
    public void ModuleStatementsCloseWithoutDroppingLegacyOwnedCuts()
    {
        const string body="SheetMetal plate { thickness: 1mm; Base deck { profile: Rectangle { width: 100mm; height: 60mm } } ";
        const string hole="Hole mount { on: deck; center: Point2(20mm,30mm); diameter: 6mm }";
        var inside=SheetMetalFirmament.Compile(body+hole+" }");
        var module=SheetMetalFirmament.Compile(body+" }\n"+hole);
        Assert.Equal(Step(inside),Step(module));Assert.Single(module.Part!.Features);
        var ignored=SheetMetalFirmament.Compile(body+" }\nFillet ignored { radius: 2mm }");
        Assert.False(ignored.IsSuccess);Assert.Null(ignored.Part);
        Assert.Contains(ignored.Diagnostics,d=>d.Code=="sheetmetal-statement-unknown" && d.SourceSpan is not null);
    }

    [Fact]
    public void CanonicalTableFamilyRetainsItsDataOwnerAndProfileDelta()
    {
        var result=SheetMetalFirmament.CompileFile(Path.Combine(Repo,"fixtures/Canonical/SheetMetal/profile-delta-tab-family.firmament"));
        Step(result);
        Assert.Equal(32,Assert.Single(result.Spec!.SemanticLayout.ProfileDeltas!).Program.Span);
    }

    [Fact]
    public void SheetScopedTableSelectionFeedsTheSameScalarGeometryBinder()
    {
        var source="""
        Record Stock { width: Length }
        SheetMetal scoped {
            Static Table variants: Stock { width: [80mm,100mm] }
            Static selected = variants[1]
            thickness: 1.5mm
            Base deck { profile: Rectangle { width: selected.width; height: 60mm } }
        }
        """;
        var result=SheetMetalFirmament.Compile(source);Step(result);
        Assert.Equal(100,result.Spec!.Base.Width);Assert.Equal(6000,result.FlatPattern!.MaterialArea!.Value,8);
    }
}
