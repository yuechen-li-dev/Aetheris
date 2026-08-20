using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class X1aSemanticClosureWitnessTests
{
    [Fact]
    public void ShippedModuleResolution_BindsQualifiedStaticWithAndTemplate()
    {
        var source = File.ReadAllText(Fixture("Canonical/Integration/standard-products/mounting-plate-library-use.firmament"));
        var resolution = FirmamentStandardLibraryResolver.Resolve(source, out var diagnostics);
        Assert.NotNull(resolution);
        Assert.Empty(diagnostics);
        Assert.Equal(["Standard.Products.Mechanical.MountingPlate"], resolution!.Declarations);
        var parse = FirmamentV2Parser.Parse(resolution.Source);
        Assert.True(parse.IsSuccess, string.Join(Environment.NewLine, parse.Diagnostics));
        var policy = Assert.Single(parse.Document!.TemplateInstantiations!).RecordArguments!["P"];
        Assert.Equal("120mm", policy.Members["Width"]);
        Assert.Contains("derivedFrom:StandardMountingPlate", policy.Provenance, StringComparison.Ordinal);
    }

    [Fact]
    public void StaticWithDefaults_AreResolvedByBinderMetadata()
    {
        const string source = """
            Model Defaults {
                Units: mm
                Record Policy { Width: Length Material: String }
                Static Base: Policy = Policy { Width: 100mm Material: "catalog.material" }
                Static Wide = Base with { Width: 140mm }
                Template<P: Policy = Wide> Struct Product { Box Body { Size: [P.Width, 20mm, 5mm] } }
            }
            """;
        var module = FirmamentTemplateHostBridge.InspectModule(source, out var diagnostics);
        Assert.Empty(diagnostics);
        var wide = Assert.Single(module.StaticRecords, item => item.Name == "Wide");
        Assert.Equal("140mm", wide.Fields["Width"]);
        Assert.Equal("\"catalog.material\"", wide.Fields["Material"]);
        Assert.Contains("derivedFrom:Base", wide.Provenance, StringComparison.Ordinal);
    }

    [Fact]
    public void VariableFlangePattern_RetainsCanonicalPatternInventory()
    {
        var source = StandardProductTemplateLibrary.GetTemplateSource("FlangedAdapterTemplate", "FlangedAdapterPolicy");
        var expansion = FirmamentTemplateHostBridge.Expand(source, "FlangedAdapterTemplate", "Flange8",
            new Dictionary<string, FirmamentHostArgument>
            {
                ["P"] = new("", "FlangedAdapterPolicy", new Dictionary<string, string>
                {
                    ["OuterDiameter"] = "80mm", ["BodyThickness"] = "12mm", ["BoreDiameter"] = "30mm",
                    ["BoltHoleDiameter"] = "6.6mm", ["BoltCount"] = "8",
                    ["Material"] = "\"Standard.Materials.Aluminum.6061_T6\"",
                })
            }, out var diagnostics);
        Assert.NotNull(expansion);
        Assert.Empty(diagnostics);
        var parse = FirmamentV2Parser.Parse(expansion!.ExpandedSource);
        Assert.True(parse.IsSuccess, string.Join(Environment.NewLine, parse.Diagnostics));
        var pattern = Assert.Single(parse.Document!.StaticAuthoring!.Patterns);
        Assert.Equal("BoltPattern", pattern.Name);
        Assert.Equal("FlangeBolt", pattern.Template);
        Assert.Equal(8, pattern.GeneratedCount);
        Assert.Equal("8", expansion.RecordArguments["P"]["BoltCount"]);
        var compiled = FirmamentBuildAndExport.CompileSource(expansion.ExpandedSource);
        Assert.True(compiled.IsSuccess, string.Join(Environment.NewLine, compiled.Diagnostics.Select(item => item.Message)));
        Assert.Equal(9, compiled.Value.Features!.Count);
        var report = Assert.Single(compiled.Value.Patterns!);
        Assert.Equal(8, report.Count);
        Assert.Equal("FlangeBolt", report.Generator);
    }

    [Fact]
    public void TemplatePmi_IsLiftedAndSurvivesAp242Reinspection()
    {
        var source = StandardProductTemplateLibrary.GetTemplateSource("MountingPlateTemplate", "MountingPlatePolicy", includeDefaultInstance: true);
        var result = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics.Select(item => item.Message)));
        var inspection = Aetheris.Kernel.Core.Step242.Step242SemanticPmiInspector.Inspect(result.Value.StepText);
        Assert.True(inspection.Success, string.Join(Environment.NewLine, inspection.Diagnostics));
        Assert.Equal(1, inspection.DatumCount);
        Assert.Equal(1, inspection.DimensionCount);
        Assert.NotEmpty(inspection.Items.Single(item => item.Kind == "Diameter").GeometricFaceEntityIds);
    }

    [Fact]
    public void NestedTemplatePmi_RebindsThroughBothSpecializationsAndAp242()
    {
        const string source = """
            Model NestedPmi {
                Units: mm
                Concept MountingPlateContract {
                    Bounds: Box3
                    TopPlane: Plane
                    MountPoints: Point3[]
                }
                Template<> Struct Inner {
                    Concept Struct Design: MountingPlateContract {
                        Bounds: Box3 { Size: [40mm, 30mm, 8mm] }
                        TopPlane: Bounds.Face(+Z)
                        MountPoints: Grid {
                            Within: Bounds.Face(+Z).Inset(10mm)
                            Columns: 1
                            Rows: 1
                        }
                    }
                    Box Base { Bounds: Design.Bounds }
                    Modify Base {
                        Hole<Shaft> InnerHole {
                            On: Base.Top
                            Center: Design.MountPoints[0]
                            Diameter: 6mm
                            End: ThroughAll
                        }
                    }
                    Expose {
                        Bounds: Design.Bounds
                        TopPlane: Base.Top
                        MountPoints: Design.MountPoints
                    }
                    Pmi {
                        Datum A { Target: face(+Z) }
                        HoleDiameter InnerHoleDiameter {
                            Target: InnerHole
                            Value: 6mm
                            Tolerance: PlusMinus(0.05mm, 0.02mm)
                            DatumRefs: [A]
                        }
                    }
                }
                Template<> Struct Outer { Struct Generated = Inner<> }
                Struct Product = Outer<>
            }
            """;
        var parse = FirmamentV2Parser.Parse(source);
        Assert.True(parse.IsSuccess, string.Join(Environment.NewLine, parse.Diagnostics));
        Assert.Equal(["Outer", "Inner"], parse.Document!.ConceptIr!.TemplateInstantiations!.Select(item => item.Template));
        Assert.Equal(2, parse.Document.Pmi?.Count ?? parse.Document.PmiBlock?.Records.Count ?? 0);
        var result = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics.Select(item => item.Message)));
        var inspection = Aetheris.Kernel.Core.Step242.Step242SemanticPmiInspector.Inspect(result.Value.StepText);
        Assert.True(inspection.Success, string.Join(Environment.NewLine, inspection.Diagnostics));
        Assert.Equal(1, inspection.DatumCount);
        Assert.Equal(1, inspection.DimensionCount);
    }

    private static string Fixture(string relative) => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "../../../../fixtures", relative.Replace('/', Path.DirectorySeparatorChar)));
}
