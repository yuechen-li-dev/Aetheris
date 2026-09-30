using Aetheris.Kernel.Firmament.Assembly;

namespace Aetheris.Kernel.Firmament.Tests.Assembly;

public sealed class AssemblyAuthoringFoundationTests
{
    [Fact]
    public void ReusableAssembliesSolveNamedFramesOnceInTheirLocalScope()
    {
        const string source = """
            Subassembly Unit {
             <Assembly Unit>
              <Part Pin = PinPart>
               Semantic Mount { DatumFrame Frame = [0,0,0] x [1,0,0] y [0,1,0] z [0,0,1]; }
               Placement { From: Mount.Frame; To: Offset.Frame; }
              </Part>
             </Assembly>
             FrameTransform Offset { From: World; TranslateLocal: [5mm,0mm,0mm]; }
             Expose { DatumFrame Mount = Pin.Mount.Frame; }
             Anchor: Unit;
            }
            Assembly Demo {
             <Assembly Demo>
              <Assembly First = Unit> Placement { From: Origin; To: World; TranslateLocal: [10mm,0mm,0mm]; } </Assembly>
              <Assembly Second = Unit> Placement { From: Origin; To: World; TranslateLocal: [20mm,0mm,0mm]; } </Assembly>
             </Assembly>
             Anchor: Demo;
            }
            """;
        var parsed = new AssemblyM0Parser().Parse(source);
        Assert.True(parsed.IsSuccess, string.Join("\n", parsed.Diagnostics));
        var result = new AssemblyM0Compiler().Compile(parsed.Source!);
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics));
        Assert.Single(result.Ir!.AssemblyDefinitions!);
        Assert.Equal(15, result.Ir.Instances.Single(i => i.Path.ToString() == "Demo.First.Pin").ResolvedTransform!.Matrix[12], 6);
        Assert.Equal(25, result.Ir.Instances.Single(i => i.Path.ToString() == "Demo.Second.Pin").ResolvedTransform!.Matrix[12], 6);
    }
    [Fact]
    public void FixedSeatingRequiresFrameMembersRatherThanCompositePorts()
    {
        var path = FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/AssemblyInterfaces/AuthoringFoundation/published-ports.firmament");
        var source = File.ReadAllText(path).Replace("Placement { From: Bottom.Frame; To: PublishedPorts.Base.Top.Frame; }", "");
        source = source.Insert(source.LastIndexOf('}'), "Interface<Fixed> Seat { A: PublishedPorts.Base.Top; B: PublishedPorts.Child.Bottom.Frame; Gap: 0mm; }");
        var result = new AssemblyM1Pipeline().Compile(source, path);
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == "assembly-interface-seating-member-type");
    }

    [Fact]
    public void UndeclaredMateRolesAreRejected()
    {
        var source = Layout.Insert(Layout.LastIndexOf('}'), "Interface Mount { Role Moving requires DatumFrameCapable; Role Target requires DatumFrameCapable; Lower FrameCoincident Moving Target; } Mate Bad: Mount { Moving: Layout.Base.Mount.Frame; Target: Layout.Base.Mount.Frame; Extra: Layout.Base.Mount.Frame; }");
        var parsed = new AssemblyM0Parser().Parse(source);
        Assert.True(parsed.IsSuccess, string.Join("\n", parsed.Diagnostics));
        var result = new AssemblyM0Compiler().Compile(parsed.Source!);
        Assert.Contains(result.Diagnostics, d => d.Code == "assembly-mate-unknown-role");
    }
    [Theory]
    [InlineData("Gap: 2deg", "assembly-interface-invalid-seating")]
    [InlineData("Orientation: 123", "assembly-interface-invalid-seating")]
    [InlineData("Orientation: SameDirection; Orientation: OpposedDirection", "assembly-interface-invalid-seating")]
    [InlineData("Gap: 0mm; Clocking: NaNdeg", "assembly-interface-invalid-seating")]
    public void SeatingRejectsMalformedFields(string fields, string code)
    {
        var parsed = new AssemblyM0Parser().Parse(Layout.Insert(Layout.LastIndexOf('}'),
            $"Interface<Fixed> Bad {{ A: Layout.Base.Mount.Frame; B: Layout.Child; {fields}; }}"));
        Assert.Contains(parsed.Diagnostics, d => d.Code == code);
    }

    [Theory]
    [InlineData("DatumFrame Bad = Missing.Frame;", "assembly-semantic-unresolved-member")]
    [InlineData("UnknownMember Bad = 7mm;", "assembly-semantic-unresolved-member")]
    [InlineData("DatumFrame Frame = [0,0,0] x [1,0,0] y [0,1,0] z [0,0,1];", "assembly-semantic-duplicate-member")]
    public void PublishedPortsRejectUnresolvedAndDuplicateMembers(string extra, string code)
    {
        var path = FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/AssemblyInterfaces/AuthoringFoundation/published-ports.firmament");
        var source = File.ReadAllText(path).Replace("Semantic Top {", "Semantic Top { " + extra);
        var result = new AssemblyM1Pipeline().Compile(source, path);
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == code);
    }

    [Fact]
    public void LayoutRejectsAnotherPlacementAuthority()
    {
        var source = Layout.Insert(Layout.LastIndexOf('}'), "Interface<Fixed> Seat { A: Layout.Base.Mount.Frame; B: Layout.Child; }");
        var parsed = new AssemblyM0Parser().Parse(source);
        var result = new AssemblyM0Compiler().Compile(parsed.Source!);
        Assert.Contains(result.Diagnostics, d => d.Code == "assembly-placement-authority-conflict");
    }

    [Fact]
    public void ReversedFixedAnchorStillRejectsTwoPlacementAuthorities()
    {
        var source = Layout.Replace("Placement { From: Origin; To: Tilt.Frame; TranslateLocal: [0mm,0mm,3mm]; }", "")
            .Replace("Anchor: Layout;", "Anchor: Layout.Child;");
        source = source.Insert(source.LastIndexOf('}'), "Interface<Fixed> Seat { A: Layout.Base.Mount.Frame; B: Layout.Child; }");
        var parsed = new AssemblyM0Parser().Parse(source);
        var result = new AssemblyM0Compiler().Compile(parsed.Source!);
        Assert.Contains(result.Diagnostics, d => d.Code == "assembly-placement-authority-conflict");
    }

    [Fact]
    public void LayoutDependencyCycleFailsInsteadOfLeavingUnderconstrainedInstances()
    {
        var source = Layout.Replace("To: World", "To: Layout.Child.Mount.Frame")
            .Replace("<Part Child = ChildPart>", "<Part Child = ChildPart> Semantic Mount { DatumFrame Frame = [0,0,0] x [1,0,0] y [0,1,0] z [0,0,1]; }");
        var parsed = new AssemblyM0Parser().Parse(source);
        var result = new AssemblyM0Compiler().Compile(parsed.Source!);
        Assert.Contains(result.Diagnostics, d => d.Code == "assembly-layout-unresolved");
    }
    [Fact]
    public void FixedSeatingIsOneRelationAndZeroPosePreservesGapAndClocking()
    {
        var path = FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/AssemblyInterfaces/AuthoringFoundation/published-ports.firmament");
        var source = File.ReadAllText(path).Replace("Placement { From: Bottom.Frame; To: PublishedPorts.Base.Top.Frame; }", "");
        var index=source.LastIndexOf('}');
        source=source.Insert(index,"Interface<Fixed> Seat { A: PublishedPorts.Base.Top.Frame; B: PublishedPorts.Child.Bottom.Frame; Gap: 2mm; Clocking: 90deg; Orientation: OpposedDirection; }");
        var compiled=new AssemblyM1Pipeline().Compile(source,path);
        Assert.True(compiled.IsSuccess,string.Join("\n",compiled.Diagnostics));
        var child=compiled.Ir!.Instances.Single(i=>i.Path.ToString()=="PublishedPorts.Child");
        Assert.Equal(16,child.ResolvedTransform!.Matrix[14],6);
        Assert.Equal(1,child.ResolvedTransform.Matrix[1],6);
        Assert.Equal(-1,child.ResolvedTransform.Matrix[10],6);
        Assert.Single(compiled.Ir.Joints!);
        var posed=AssemblyKinematics.Evaluate(compiled.Ir,new Dictionary<string,double>());
        Assert.True(posed.IsSuccess,string.Join("\n",posed.Diagnostics));
        Assert.Equal(child.ResolvedTransform.Matrix,posed.Instances.Single(i=>i.Path.ToString()=="PublishedPorts.Child").ResolvedTransform!.Matrix);
    }

    [Fact]
    public void AssemblyPatternsUseCheckedSetsAndPreserveKeyedIdentity()
    {
        const string source="""
            Record Site { X: Length }
            Static Sites: Set<Site> { Left => Site { X: -5mm } Right => Site { X: 5mm } }
            Assembly Patterned {
              <Assembly Patterned>
                Pattern Poles Over Sites {
                  site => <Part Pole = PolePart>
                    Placement { From: Origin; To: World; TranslateLocal: [site.X,0mm,0mm]; }
                  </Part>
                }
              </Assembly>
              Anchor: Patterned;
            }
            """;
        AssemblyCompilationResult Compile(string text)
        {
            var parsed=new AssemblyM0Parser().Parse(text);
            Assert.True(parsed.IsSuccess,string.Join("\n",parsed.Diagnostics));
            var result=new AssemblyM0Compiler().Compile(parsed.Source!);
            Assert.True(result.IsSuccess,string.Join("\n",result.Diagnostics));
            return result;
        }
        var first=Compile(source);
        var reordered=Compile(source.Replace("Left => Site { X: -5mm } Right => Site { X: 5mm }","Right => Site { X: 5mm } Left => Site { X: -5mm }"));
        foreach (var key in new[]{"Left","Right"})
        {
            var a=first.Ir!.Instances.Single(i=>i.Path.ToString()==$"Patterned.Poles.{key}.Pole");
            var b=reordered.Ir!.Instances.Single(i=>i.Path.ToString()==a.Path.ToString());
            Assert.Equal(a.StableId,b.StableId);
            Assert.Equal(a.ResolvedTransform!.Matrix,b.ResolvedTransform!.Matrix);
            Assert.Contains(a.Provenance,p=>p.Stage=="assembly-pattern" && p.Evidence.Contains("Key:"+key));
        }
        Assert.Single(first.Ir!.Patterns!);
        var invalid=new AssemblyM0Parser().Parse(source.Replace("X: -5mm","X: 90deg"));
        Assert.False(invalid.IsSuccess);
        Assert.Contains(invalid.Diagnostics,d=>d.Message.Contains("field-type-mismatch"));
    }
    [Fact]
    public void TemplatePublishesSpecializedPortsAndSharesExactDefinition()
    {
        var path = FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/AssemblyInterfaces/AuthoringFoundation/published-ports.firmament");
        var compiled = new AssemblyM1Pipeline().CompileFile(path);
        Assert.True(compiled.IsSuccess,string.Join("\n",compiled.Diagnostics));
        var child = compiled.Ir!.Instances.Single(i => i.Path.ToString() == "PublishedPorts.Child");
        Assert.Equal(14,child.ResolvedTransform!.Matrix[14],6);
        Assert.Single(compiled.Geometry!.DefinitionBodies);
        Assert.True(child.SemanticRoot.ExposedMembers.ContainsKey("Top"));
    }
    private const string Layout = """
        Assembly Layout {
          <Assembly Layout>
            <Part Base = BasePart>
              Placement { From: Origin; To: World; TranslateLocal: [10mm,20mm,30mm]; RotateLocal: { Axis: Z; Angle: 90deg } }
              Semantic Mount { DatumFrame Frame = [2,0,0] x [1,0,0] y [0,1,0] z [0,0,1]; }
            </Part>
            <Part Child = ChildPart>
              Placement { From: Origin; To: Tilt.Frame; TranslateLocal: [0mm,0mm,3mm]; }
            </Part>
          </Assembly>
          FrameTransform Tilt { From: Layout.Base.Mount.Frame; TranslateLocal: [1mm,0mm,0mm]; RotateLocal: { Axis: X; Angle: 90deg } }
          Anchor: Layout;
        }
        """;

    [Fact]
    public void LayoutComposesLocalFramesThroughOrdinaryConstraints()
    {
        var parsed = new AssemblyM0Parser().Parse(Layout);
        Assert.True(parsed.IsSuccess, string.Join("\n",parsed.Diagnostics));
        var compiled = new AssemblyM0Compiler().Compile(parsed.Source!);
        Assert.True(compiled.IsSuccess,string.Join("\n",compiled.Diagnostics));
        var child = compiled.Ir!.Instances.Single(i => i.Path.ToString() == "Layout.Child");
        Assert.Equal(PlacementAuthority.AuthoredFrame,child.PlacementAuthority);
        Assert.Equal(13,child.ResolvedTransform!.Matrix[12],6);
        Assert.Equal(23,child.ResolvedTransform.Matrix[13],6);
        Assert.Equal(30,child.ResolvedTransform.Matrix[14],6);
        Assert.Empty(compiled.Ir.Joints!); // Layout is not a mechanical Interface.
    }

    [Theory]
    [InlineData("TranslateLocal: [10mm,20mm,30mm]", "TranslateLocal: [10,20mm,30mm]", "assembly-frame-invalid-unit")]
    [InlineData("To: Tilt.Frame", "To: Missing.Frame", "assembly-frame-unresolved")]
    [InlineData("From: Layout.Base.Mount.Frame", "From: Tilt.Frame", "assembly-frame-cycle")]
    [InlineData("To: Tilt.Frame", "To: Tilt.Frame; Normal: [0,0,1]; Up: [0,0,2]", "assembly-frame-invalid-basis")]
    [InlineData("To: Tilt.Frame", "To: Tilt.Frame; Unknown: 7mm", "assembly-frame-invalid-fields")]
    public void InvalidFrameIntentFailsClosed(string before,string after,string code)
    {
        var parsed = new AssemblyM0Parser().Parse(Layout.Replace(before,after));
        var diagnostics = parsed.Diagnostics.Concat(parsed.Source is null ? [] : new AssemblyM0Compiler().Compile(parsed.Source).Diagnostics);
        Assert.Contains(diagnostics,d => d.Code == code);
    }
}
