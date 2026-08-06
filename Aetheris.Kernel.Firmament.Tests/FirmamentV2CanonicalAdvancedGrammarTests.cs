using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentV2CanonicalAdvancedGrammarTests
{
    [Fact]
    public void CanonicalStaticRecordsArraysTemplatePatternAndRequire_ExpandToProductionHoles()
    {
        var parse = FirmamentV2Parser.Parse("""
            Model GeneratedMounts {
                Units: mm
                Record MountSpec { Center: Point2 Diameter: Length }
                Static Mounts: MountSpec[] = [
                    MountSpec { Center: Point2(-20mm, 0mm) Diameter: 8mm }
                    MountSpec { Center: Point2(20mm, 0mm) Diameter: 8mm }
                ]
                Require ValidDiameter => 8mm > 0mm
                Template MountHole(MountSpec spec) {
                    Hole<Shaft> Mount {
                        On: +Z
                        Center: spec.Center
                        Diameter: spec.Diameter
                        End: ThroughAll
                    }
                }
                Box Base { Size: [80mm, 40mm, 10mm] }
                Modify Base {
                    Pattern MountPattern Over Mounts { MountHole(Current) }
                }
            }
            """);

        Assert.True(parse.IsSuccess, string.Join(Environment.NewLine, parse.Diagnostics));
        Assert.Equal(2, parse.Document!.ModifyBlocks!.Single().SemanticHoles.Count);
        var staticAuthoring = Assert.IsType<FirmamentV2StaticAuthoringDocument>(parse.Document.StaticAuthoring);
        Assert.Equal("MountSpec", Assert.Single(staticAuthoring.RecordTypes).Name);
        Assert.Equal(2, Assert.Single(staticAuthoring.Arrays).Elements.Count);
        Assert.Equal(["MountPattern[0]", "MountPattern[1]"], Assert.Single(staticAuthoring.Patterns).GeneratedIds);
    }

    [Fact]
    public void CanonicalStaticRequire_FailsBeforeMaterialLowering()
    {
        var parse = FirmamentV2Parser.Parse("""
            Model InvalidRequire {
                Units: mm
                Require Dimensions => 0mm > 1mm
                Box Base { Size: [10mm, 10mm, 10mm] }
            }
            """);

        Assert.False(parse.IsSuccess);
        Assert.Contains(parse.Diagnostics, diagnostic => diagnostic == "firmament-v2-static-require-failed:Dimensions:0mm > 1mm");
    }

    [Fact]
    public void CanonicalStaticPoint2Value_DrivesTypedProfileGuide()
    {
        var parse = FirmamentV2Parser.Parse("""
            Model StaticProfile {
                Units: mm
                Record GuideSpec { Center: Point2 }
                Static Guides: GuideSpec[] = [ GuideSpec { Center: Point2(0mm, 0mm) } ]
                Concept Struct Layout On XY {
                    Rect2 Stock { Center: Guides[0].Center; Size: [20mm, 10mm] }
                }
                Profile Plate Using Layout { Loop Outer {
                    Segment South { Trace: Stock.Bottom; From: Stock.BottomLeft; To: Stock.BottomRight }
                    Segment East { Trace: Stock.Right; From: Stock.BottomRight; To: Stock.TopRight }
                    Segment North { Trace: Stock.Top; From: Stock.TopRight; To: Stock.TopLeft }
                    Segment West { Trace: Stock.Left; From: Stock.TopLeft; To: Stock.BottomLeft }
                } }
                Struct Plate { Extrude Plate { Profile: Plate; From: -2mm; To: 2mm } }
            }
            """);

        Assert.True(parse.IsSuccess, string.Join(Environment.NewLine, parse.Diagnostics));
        Assert.Equal("Plate", Assert.Single(parse.Document!.Profiles!).Name);
        Assert.NotNull(parse.Document.StaticAuthoring);
    }

    [Fact]
    public void CanonicalTemplate_EmitsSlotsForPatternAndProfileForDirectInvocation()
    {
        var slots = FirmamentV2Parser.Parse("""
            Model GeneratedSlot {
                Units: mm
                Record SlotSpec { Center: Point2 Direction: Vector2 Length: Length Width: Length }
                Static Vents: SlotSpec[] = [ SlotSpec { Center: Point2(0mm, 0mm) Direction: Vector2(1, 0) Length: 80mm Width: 40mm } ]
                Template Vent(SlotSpec spec) { Slot<Capsule> Vent { Center: spec.Center Direction: spec.Direction Length: spec.Length Width: spec.Width Extent: ThroughAll Role: ThroughSlot } }
                Concept Struct Layout On XY { Rect2 Stock { Center: Point2(0mm, 0mm); Size: [200mm, 100mm] } }
                Profile Plate Using Layout { Loop Outer {
                    Segment South { Trace: Stock.Bottom; From: Stock.BottomLeft; To: Stock.BottomRight }
                    Segment East { Trace: Stock.Right; From: Stock.BottomRight; To: Stock.TopRight }
                    Segment North { Trace: Stock.Top; From: Stock.TopRight; To: Stock.TopLeft }
                    Segment West { Trace: Stock.Left; From: Stock.TopLeft; To: Stock.BottomLeft }
                } }
                Struct Plate { Compose Body { Base Stock { Profile: Plate; From: -10mm; To: 10mm; Role: Stock } Pattern SlotPattern Over Vents { Vent(Current) } } }
            }
            """);
        Assert.True(slots.IsSuccess, string.Join(Environment.NewLine, slots.Diagnostics));

        var profile = FirmamentV2Parser.Parse("""
            Model GeneratedProfile {
                Units: mm
                Record ProfileSpec { Center: Point2 }
                Static Specs: ProfileSpec[] = [ ProfileSpec { Center: Point2(0mm, 0mm) } ]
                Template PlateProfile(ProfileSpec spec) { Profile Plate Using Layout { Loop Outer {
                    Segment South { Trace: Stock.Bottom; From: Stock.BottomLeft; To: Stock.BottomRight }
                    Segment East { Trace: Stock.Right; From: Stock.BottomRight; To: Stock.TopRight }
                    Segment North { Trace: Stock.Top; From: Stock.TopRight; To: Stock.TopLeft }
                    Segment West { Trace: Stock.Left; From: Stock.TopLeft; To: Stock.BottomLeft }
                } } }
                Concept Struct Layout On XY { Rect2 Stock { Center: Specs[0].Center; Size: [20mm, 10mm] } }
                PlateProfile(Specs[0])
                Struct PlateBody { Extrude Plate { Profile: Plate; From: -2mm; To: 2mm } }
            }
            """);
        Assert.True(profile.IsSuccess, string.Join(Environment.NewLine, profile.Diagnostics));
        Assert.Equal("Plate", Assert.Single(profile.Document!.Profiles!).Name);
    }

    [Theory]
    [InlineData("record-array-pattern-slots.firmament", "CYLINDRICAL_SURFACE")]
    [InlineData("record-array-template-profile.firmament", "MANIFOLD_SOLID_BREP")]
    public void CanonicalStaticTemplateOutput_BuildsThroughTheProductionMaterializer(string fixtureName, string expectedStepEntity)
    {
        var source = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../fixtures/FirmamentV2/Canonical/valid", fixtureName));
        var output = Path.Combine(Path.GetTempPath(), "aetheris-" + Guid.NewGuid().ToString("N") + ".step");
        try
        {
            var build = FirmamentBuildAndExport.Run(source, output);

            Assert.True(build.IsSuccess, string.Join(Environment.NewLine, build.Diagnostics.Select(diagnostic => diagnostic.Message)));
            Assert.Contains(expectedStepEntity, build.Value.Export.StepText, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(output)) File.Delete(output);
        }
    }

    [Fact]
    public void CanonicalSymbolTable_BindsSelectionsAcrossProfileComposeAndSlotFamilies()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../fixtures/FirmamentV2/Canonical/valid/semantic-slot-capsule.firmament"));
        var parse = FirmamentV2Parser.Parse(File.ReadAllText(path));

        Assert.True(parse.IsSuccess, string.Join(Environment.NewLine, parse.Diagnostics));
        var symbols = Assert.IsType<FirmamentV2CanonicalSymbolTable>(parse.Document!.SymbolTable);
        Assert.Equal(FirmamentV2CanonicalSymbolKind.Profile, symbols.Resolve("Plate")!.Kind);
        Assert.Equal(FirmamentV2CanonicalSymbolKind.Compose, symbols.Resolve("SlotBody")!.Kind);
        Assert.Equal(FirmamentV2CanonicalSymbolKind.Slot, symbols.Resolve("Relief")!.Kind);
        Assert.Contains(symbols.Bindings, binding => binding.OwnerCanonicalId == "Selection:ReliefEntry" && binding.TargetCanonicalId == "Slot:Relief");
        Assert.Contains("firmament-v2-unified-canonical-symbols-bound", parse.Diagnostics);
    }

    [Fact]
    public void CanonicalSymbolTable_RejectsCrossFamilyNameCollisions()
    {
        var parse = FirmamentV2Parser.Parse("""
            Model Collision {
                Units: mm
                Concept Struct Layout On XY { Rect2 Stock { Center: [0mm, 0mm]; Size: [20mm, 10mm] } }
                Profile Plate Using Layout { Loop Outer {
                    Segment South { Trace: Stock.Bottom; From: Stock.BottomLeft; To: Stock.BottomRight }
                    Segment East { Trace: Stock.Right; From: Stock.BottomRight; To: Stock.TopRight }
                    Segment North { Trace: Stock.Top; From: Stock.TopRight; To: Stock.TopLeft }
                    Segment West { Trace: Stock.Left; From: Stock.TopLeft; To: Stock.BottomLeft }
                } }
                Struct PlateBody { Compose Plate { Base Stock { Profile: Plate; From: -2mm; To: 2mm; Role: Stock } } }
            }
            """);

        Assert.False(parse.IsSuccess);
        Assert.Contains("firmament-v2-symbol-duplicate:Plate:Profile:Compose", parse.Diagnostics);
    }

    [Theory]
    [InlineData("semantic-slot-capsule.firmament", "ReliefEntry")]
    [InlineData("semantic-slot-rounded-rectangle.firmament", "OpeningEntry")]
    [InlineData("semantic-selection-chamfer.firmament", "OuterTopBoundary")]
    public void CanonicalRoot_AdmitsSemanticSlotsAndSelections_WithoutACompatibilityDocumentRoot(string fixtureName, string selectionName)
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../fixtures/FirmamentV2/Canonical/valid", fixtureName));
        var parse = FirmamentV2Parser.Parse(File.ReadAllText(path));

        Assert.True(parse.IsSuccess, string.Join(Environment.NewLine, parse.Diagnostics));
        var selection = Assert.Single(parse.Document!.Selections!);
        Assert.Equal(selectionName, selection.Name);
        Assert.Contains("firmament-v2-unified-canonical-advanced-parsed", parse.Diagnostics);
    }

    [Fact]
    public void CanonicalSelection_ReportsSpecificBindingFailures()
    {
        var source = """
            Model InvalidSelection {
                Units: mm
                Concept Struct Layout On XY { Rect2 Stock { Center: [0mm, 0mm]; Size: [20mm, 10mm] } }
                Profile Plate Using Layout { Loop Outer {
                    Segment South { Trace: Stock.Bottom; From: Stock.BottomLeft; To: Stock.BottomRight }
                    Segment East { Trace: Stock.Right; From: Stock.BottomRight; To: Stock.TopRight }
                    Segment North { Trace: Stock.Top; From: Stock.TopRight; To: Stock.TopLeft }
                    Segment West { Trace: Stock.Left; From: Stock.TopLeft; To: Stock.BottomLeft }
                } }
                Struct PlateBody { Extrude Plate { Profile: Plate; From: -2mm; To: 2mm } }
                Selection Bad { Target: VertexSet Source: Missing.ProfileLoop(Outer) Require: ClosedLoop }
            }
            """;

        var parse = FirmamentV2Parser.Parse(source);

        Assert.False(parse.IsSuccess);
        Assert.Contains(parse.Diagnostics, diagnostic => diagnostic == FirmamentV2Parser.SelectionResultKindInvalid + ":Bad:VertexSet");
        Assert.Contains(parse.Diagnostics, diagnostic => diagnostic == FirmamentV2Parser.SelectionUnknownSource + ":Bad:Missing");
    }

    [Fact]
    public void CanonicalRoot_AdmitsConceptConstructionPlaneAndBlindHole_ThroughTheV2Route()
    {
        var parse = FirmamentV2Parser.Parse("""
            Model SideHolePart {
                Units: mm
                Concept Struct SideLayout {
                    Datum: Plane { Origin: [-50mm, 0mm, 0mm]; Normal: [1, 0, 0]; Up: [0, 0, 1] }
                }
                Construction Plane PositiveXWorkplane { Trace: SideLayout.Datum }
                Struct SideHoleBracket {
                    Box Base { Size: [100mm, 60mm, 12mm] }
                    Modify Base {
                        Hole<Shaft> SideMount {
                            From: PositiveXWorkplane
                            Center: Point2(10mm, 6mm)
                            Diameter: 8mm
                            End: ThroughAll
                        }
                    }
                }
            }
            """);

        Assert.True(parse.IsSuccess, string.Join(Environment.NewLine, parse.Diagnostics));
        Assert.Equal(FirmamentV2ParseDisposition.RecognizedValid, parse.Disposition);
        Assert.Equal("SideHolePart", parse.Document!.ModelName);
        Assert.Single(parse.Document.ModifyBlocks!.Single().SemanticHoles);
        Assert.Contains("firmament-v2-unified-canonical-advanced-parsed", parse.Diagnostics);
    }

    [Fact]
    public void CanonicalRoot_AdmitsProfileAndComposeDeclarations_IntoTheNormalizedDocument()
    {
        var profile = FirmamentV2Parser.Parse("""
            Model ProfilePart {
                Units: mm
                Concept Struct Layout On XY {
                    Rect2 Stock { Center: [0mm, 0mm]; Size: [20mm, 10mm] }
                }
                Profile Plate Using Layout {
                    Segment South { Trace: Stock.Bottom; From: Stock.BottomLeft; To: Stock.BottomRight }
                    Segment East { Trace: Stock.Right; From: Stock.BottomRight; To: Stock.TopRight }
                    Segment North { Trace: Stock.Top; From: Stock.TopRight; To: Stock.TopLeft }
                    Segment West { Trace: Stock.Left; From: Stock.TopLeft; To: Stock.BottomLeft }
                }
                Struct PlateBody { Extrude Plate { Profile: Plate; From: -2mm; To: 2mm } }
            }
            """);
        Assert.True(profile.IsSuccess, string.Join(Environment.NewLine, profile.Diagnostics));
        Assert.Equal("Plate", Assert.Single(profile.Document!.Profiles!).Name);

        var compose = FirmamentV2Parser.Parse("""
            Model ComposePart {
                Units: mm
                Concept Struct Layout On XY { Rect2 Stock { Center: [0mm, 0mm]; Size: [20mm, 10mm] } }
                Profile Plate Using Layout { Loop Outer {
                    Segment South { Trace: Stock.Bottom; From: Stock.BottomLeft; To: Stock.BottomRight }
                    Segment East { Trace: Stock.Right; From: Stock.BottomRight; To: Stock.TopRight }
                    Segment North { Trace: Stock.Top; From: Stock.TopRight; To: Stock.TopLeft }
                    Segment West { Trace: Stock.Left; From: Stock.TopLeft; To: Stock.BottomLeft }
                } }
                Struct PlateBody { Compose Body { Base Stock { Profile: Plate; From: -2mm; To: 2mm; Role: Stock } } }
            }
            """);
        Assert.True(compose.IsSuccess, string.Join(Environment.NewLine, compose.Diagnostics));
        Assert.Equal("Body", Assert.Single(compose.Document!.Composes!).Name);
    }

    [Fact]
    public void CanonicalMultiRectProfileComposeFixture_IsDiscoverableAndBuilds()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../fixtures/FirmamentV2/Canonical/valid/profile-compose-l-bracket.firmament"));
        var parse = FirmamentV2Parser.Parse(File.ReadAllText(path));
        var output = Path.Combine(Path.GetTempPath(), "aetheris-l-profile-" + Guid.NewGuid().ToString("N") + ".step");
        try
        {
            Assert.True(parse.IsSuccess, string.Join(Environment.NewLine, parse.Diagnostics));
            Assert.Equal("BracketBody", Assert.Single(parse.Document!.Composes!).Name);
            var build = FirmamentBuildAndExport.Run(path, output);
            Assert.True(build.IsSuccess, string.Join(Environment.NewLine, build.Diagnostics.Select(diagnostic => diagnostic.Message)));
        }
        finally
        {
            if (File.Exists(output)) File.Delete(output);
        }
    }

    [Fact]
    public void ProfileComposePolygonEdgeFinish_ReportsHostAdmissionInsteadOfMissingSemanticSource()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../fixtures/FirmamentV2/Canonical/invalid/profile-compose-l-bracket-edgefinish-unsupported.firmament"));
        var output = Path.Combine(Path.GetTempPath(), "aetheris-l-profile-finish-" + Guid.NewGuid().ToString("N") + ".step");
        try
        {
            var parse = FirmamentV2Parser.Parse(File.ReadAllText(path));
            Assert.False(parse.IsSuccess);
            Assert.Contains(FirmamentV2Parser.EdgeFinishProfileComposeBoundaryUnsupported, parse.Diagnostics);
            var build = FirmamentBuildAndExport.Run(path, output);
            Assert.False(build.IsSuccess);
            var diagnostic = Assert.Single(build.Diagnostics);
            Assert.Contains(FirmamentBuildAndExport.EdgeFinishProfileComposeBoundaryUnsupported, diagnostic.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("SemanticSourceNotFound", diagnostic.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(output)) File.Delete(output);
        }
    }

    [Fact]
    public void ProfileCoordinateEndpoint_ReportsNamedPointDiagnosticBeforeLoopValidation()
    {
        var parse = FirmamentV2Parser.Parse("""
            Model RawEndpoint {
                Units: mm
                Concept Struct Layout On XY { Rect2 Stock { Center: [0mm, 0mm]; Size: [20mm, 10mm] } }
                Profile Plate Using Layout { Loop Outer {
                    Segment South { Trace: Stock.Bottom; From: [0mm, -5mm]; To: Stock.BottomRight }
                    Segment East { Trace: Stock.Right; From: Stock.BottomRight; To: Stock.TopRight }
                    Segment North { Trace: Stock.Top; From: Stock.TopRight; To: Stock.TopLeft }
                    Segment West { Trace: Stock.Left; From: Stock.TopLeft; To: Stock.BottomLeft }
                } }
                Struct Body { Extrude Plate { Profile: Plate; From: 0mm; To: 1mm } }
            }
            """);

        Assert.False(parse.IsSuccess);
        Assert.Contains(ProfileAuthoringParser.SegmentEndpointMustReferenceNamedPoint + ":South:From", parse.Diagnostics);
    }
}
