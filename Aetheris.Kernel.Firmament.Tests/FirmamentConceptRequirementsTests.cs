using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentConceptRequirementsTests
{
    private static string Fixture(string name) => File.ReadAllText(FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/Concepts/" + name + ".firmament"));

    [Fact]
    public void PredicateBundleAdmitsTemplateAndPreservesLiteralGeometry()
    {
        var source = Fixture("requirements");
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.True(parsed.IsSuccess, string.Join("\n", parsed.Diagnostics));
        Assert.Equal("Product", parsed.Document!.ModelName);
        var expansion = Assert.Single(parsed.Document.TemplateInstantiations!);
        Assert.StartsWith("Passed:", expansion.RequireResults!["dimensions"]);
        var build = FirmamentBuildAndExport.CompileSource(source);
        var literal = FirmamentBuildAndExport.CompileSource("Model Product { units: mm Box Body { size: [40mm, 20mm, 5mm] } }");
        Assert.True(build.IsSuccess, string.Join("\n", build.Diagnostics.Select(d => d.Message)));
        Assert.True(literal.IsSuccess);
        Assert.Equal(literal.Value.StepText, build.Value.StepText);
        Assert.Equal(6, Step242Importer.ImportBody(build.Value.StepText).Value.Topology.Faces.Count());
    }

    [Fact]
    public void FailedBundleNamesTheMemberAndRetainsTheAuthoredRequireLocation()
    {
        var source = Fixture("requirements").Replace("width: 40mm, height: 20mm", "width: 10mm, height: 20mm");
        Reject(source, "firmament-concept-requirement-failed");
        var diagnostic = Assert.Single(FirmamentV2Parser.Parse(source).SyntaxDiagnostics, d => d.Code == "firmament-concept-requirement-failed");
        Assert.Contains("PlateRequirements.aspect", diagnostic.Message);
        Assert.Contains("Require dimensions", source.Substring(diagnostic.SourceSpan!.Start, diagnostic.SourceSpan.Length));
    }

    [Fact]
    public void GeometryExpansionCannotInventAnOriginalRequirementOffset()
    {
        var source = "Square2 unused { center: [0mm,0mm]; size: 10mm }\n" + Fixture("requirements")
            .Replace("width: 40mm, height: 20mm", "width: 10mm, height: 20mm");
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.False(parsed.IsSuccess);
        var diagnostic = Assert.Single(parsed.SyntaxDiagnostics, d => d.Code == "firmament-concept-requirement-failed");
        Assert.Contains("Require dimensions", source.Substring(diagnostic.SourceSpan!.Start, diagnostic.SourceSpan.Length));
        Assert.Contains("enclosing", diagnostic.LocationNote);
    }

    [Theory]
    [InlineData("true")]
    [InlineData("!(2mm > 3mm) && (1mm + 2mm == 3mm)")]
    [InlineData("Positive(value: 4mm)")]
    [InlineData("Positive(4mm) || false")]
    [InlineData("length > 2mm")]
    public void NativeRequireActuallyChecksTheSameBooleanLanguage(string expression)
    {
        var source = $$"""
            Concept Positive(value: Length) => value > 0mm
            Model Part {
              units: mm
              Static length: Length = 4mm tol 0.1mm
              Require check => {{expression}}
              Box Body { size: [length, 3mm, 2mm] }
            }
            """;
        var build = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(build.IsSuccess, string.Join("\n", build.Diagnostics.Select(d => d.Message)));
    }

    [Theory]
    [InlineData("false", "requirement-failed")]
    [InlineData("Positive(-1mm)", "requirement-failed")]
    [InlineData("Positive(1deg)", "argument-type-mismatch")]
    [InlineData("Positive(other: 1mm)", "argument-invalid")]
    [InlineData("Positive(1mm, 2mm)", "argument-invalid")]
    [InlineData("Unknown(1mm)", "name-unresolved")]
    [InlineData("1mm > 1deg", "expression-type-mismatch")]
    [InlineData("1mm / 0 > 1mm", "expression-type-mismatch")]
    [InlineData("true || Unknown(1mm)", "name-unresolved")]
    [InlineData("1mm", "requirement-non-bool")]
    public void InvalidRequirementCannotBecomeAnAcceptedBox(string expression, string suffix) =>
        Reject($$"""
            Concept Positive(value: Length) => value > 0mm
            Model Part { units: mm Require check => {{expression}}; Box Body { size: [4mm, 3mm, 2mm] } }
            """, "firmament-concept-" + suffix);

    [Theory]
    [InlineData("Concept Positive(value: Length) => Positive(value)", "predicate-cycle")]
    [InlineData("Concept Positive(value: Length) => value", "requirement-non-bool")]
    [InlineData("Concept Positive(value: Length) { one: true; one: false }", "bundle-member-invalid")]
    [InlineData("Concept Positive(value: Length) { Box Body { size: [1mm,1mm,1mm] } }", "bundle-member-invalid")]
    public void MalformedConceptsFailBeforeMaterialization(string declaration, string suffix) =>
        Reject(declaration + "\nModel Part { units: mm Require check => Positive(1mm); Box Body { size: [4mm,3mm,2mm] } }", "firmament-concept-" + suffix);

    [Fact]
    public void UnappliedPredicatesAreTypeCheckedButNeedNotBeTrue()
    {
        const string model = "\nModel Part { units: mm Box Body { size: [4mm,3mm,2mm] } }";
        Reject("Concept Broken(value: Length) => value > 0deg" + model, "firmament-concept-expression-type-mismatch");
        Reject("Concept Broken => Missing" + model, "firmament-concept-name-unresolved");
        Assert.True(FirmamentBuildAndExport.CompileSource("Concept AlwaysFalse => false" + model).IsSuccess);
    }

    [Fact]
    public void ZeroArgumentPredicatesAndBundlesComposeWithoutConjunctions()
    {
        const string source = """
            Concept First => true
            Concept Pair { first: First; second: true }
            Concept Bundle { pair: Pair; another: First }
            Model Part { units: mm Require check => Bundle; Box Body { size: [4mm,3mm,2mm] } }
            """;
        Assert.True(FirmamentBuildAndExport.CompileSource(source).IsSuccess);
        Reject(source.Replace("Concept First => true", "Concept First => false"), "firmament-concept-requirement-failed");
    }

    [Theory]
    [InlineData("1.5 + 0.5")]
    [InlineData("4 / 2")]
    public void IntegralFloatResultsDoNotBecomeIntArguments(string expression) =>
        Reject($"Concept Integer(value: Int) => value > 0\nModel Part {{ units: mm Require check => Integer({expression}); Box Body {{ size: [4mm,3mm,2mm] }} }}", "firmament-concept-argument-type-mismatch");

    [Fact]
    public void RecordFieldsAndTypedAllowancesAreNominalInputsToTheRequirement()
    {
        var source = Fixture("requirements").Replace("Model Product = Plate<width: 40mm, height: 20mm>", """
            Record SizeSpec { width: Length; height: Length }
            Static size: SizeSpec = SizeSpec { width: 40mm tol 0.1mm; height: 20mm }
            Model Product = Plate<width: size.width, height: size.height>
            """);
        var build = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(build.IsSuccess, string.Join("\n", build.Diagnostics.Select(d => d.Message)));
        var baseline = FirmamentBuildAndExport.CompileSource(Fixture("requirements"));
        Assert.Equal(baseline.Value.StepText, build.Value.StepText);
    }

    [Theory]
    [InlineData("Concept Contract { Bounds: Box3 }", true)]
    [InlineData("Concept Contract { Missing: Length }", false)]
    [InlineData("Concept Contract { Bounds: Length }", false)]
    [InlineData("Concept Contract { Bounds: Box3; Missing: Length }", false)]
    [InlineData("Concept BoundsContract { Bounds: Box3 } Concept PlaneContract { TopPlane: Plane } Concept Contract : BoundsContract && PlaneContract { }", true)]
    public void InlineStructuralContractsAndCompositionAreNotVacuouslySatisfied(string contract, bool valid)
    {
        var source = contract + "\nTemplate<type T satisfies Contract> Model Part { units: mm Box Body { size: [4mm,3mm,2mm] } }\nModel Product = Part<T: Box>";
        var parsed = FirmamentV2Parser.Parse(source);
        var build = FirmamentBuildAndExport.CompileSource(source);
        Assert.Equal(valid, parsed.IsSuccess);
        Assert.Equal(valid, build.IsSuccess);
        if (!valid) Assert.Contains(parsed.Diagnostics, d => d.StartsWith("firmament-template-type-argument-does-not-satisfy-", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Concept A : B {} Concept B : A {}", "contract-cycle")]
    [InlineData("Concept A { x: Length; x: Angle }", "contract-member-conflict")]
    [InlineData("Concept A { x: Length } Concept B { x: Angle } Concept C : A && B {}", "contract-member-conflict")]
    [InlineData("Concept A : Missing {}", "contract-unresolved")]
    public void StructuralCompositionErrorsHaveNoGeometry(string declarations, string suffix) =>
        Reject(declarations + "\nModel Part { units: mm Box Body { size: [4mm,3mm,2mm] } }", "firmament-concept-" + suffix);

    [Theory]
    [InlineData("Bounds: Box3 { Size: [4mm,3mm,2mm] }", true)]
    [InlineData("Bounds: 4mm", false)]
    [InlineData("Other: 4mm", false)]
    public void AClaimedContractMustBeProvidedByTheActualConceptStruct(string members, bool valid)
    {
        var source = $$"""
            Concept Bounded { Bounds: Box3 }
            Concept Struct Design: Bounded { {{members}} }
            Template<type T satisfies Bounded> Model Part { units: mm Box Body { size: [4mm,3mm,2mm] } }
            Model Product = Part<T: Design>
            """;
        Assert.Equal(valid, FirmamentV2Parser.Parse(source).IsSuccess);
        Assert.Equal(valid, FirmamentBuildAndExport.CompileSource(source).IsSuccess);
    }

    [Fact]
    public void AnUnusedConformanceClaimStillCannotLie()
    {
        const string source = """
            Concept Bounded { Bounds: Box3 }
            Concept Struct Design: Bounded { Bounds: 4mm }
            Model Part { units: mm Box Body { size: [4mm,3mm,2mm] } }
            """;
        Reject(source, "firmament-concept-structural-conformance-failed");
        var diagnostic = Assert.Single(FirmamentV2Parser.Parse(source).SyntaxDiagnostics, d => d.Code == "firmament-concept-structural-conformance-failed");
        Assert.Contains("Concept Struct Design", source.Substring(diagnostic.SourceSpan!.Start, diagnostic.SourceSpan.Length));
    }

    [Fact]
    public void ExplicitCoordinatesCannotSilentlyDiscardRequestedRotation()
    {
        var source = File.ReadAllText(FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Regression/Concepts/explicit-rotation.firmament"));
        Reject(source, "firmament-boundary2-rotation-unsupported");
        var admitted = FirmamentBuildAndExport.CompileSource(source.Replace("rotation: 90deg", "rotation: 0deg"));
        Assert.True(admitted.IsSuccess);
        Assert.Equal(5, Step242Importer.ImportBody(admitted.Value.StepText).Value.Topology.Faces.Count());
    }

    [Fact]
    public void RequireDoesNotDisappearFromInsideAPrimitiveFieldBlock() =>
        Reject("Model Part { units: mm Box Body { size: [4mm,3mm,2mm]; Require check => true } }", "firmament-concept-requirement-scope-unsupported");

    [Fact]
    public void NativeRequirementKeepsItsExistingStaticInspectionReceipt()
    {
        var parsed = FirmamentV2Parser.Parse("Model Part { units: mm Require dimensions => 4mm > 1mm; Box Body { size: [4mm,3mm,2mm] } }");
        Assert.True(parsed.IsSuccess);
        var requirement = Assert.Single(parsed.Document!.StaticAuthoring!.Requires);
        Assert.Equal("dimensions", requirement.Name);
        Assert.True(requirement.Value);
    }

    [Fact]
    public void CompilerDiagnosticProjectsToTheLanguageServiceAtTheAuthoredRequirement()
    {
        var source = Fixture("requirements").Replace("width: 40mm, height: 20mm", "width: 10mm, height: 20mm");
        var analysis = FirmamentLanguageAnalysisService.Analyze(source, "plate.firmament", "draft");
        var diagnostic = Assert.Single(analysis.Diagnostics, d => d.Code == "firmament-concept-requirement-failed");
        Assert.Equal("error", diagnostic.Severity);
        Assert.Contains("Require dimensions", source.Substring(diagnostic.Start, diagnostic.Length));
    }

    public static TheoryData<string> Shapes => new()
    {
        "Rect2 boundary { center: [0mm,0mm]; size: [40mm,20mm] }",
        "Square2 boundary { center: [0mm,0mm]; size: 20mm }",
        "Circle2 boundary { center: [0mm,0mm]; radius: 12mm }",
        "Circle2 boundary { center: [0mm,0mm]; diameter: 24mm }",
        "Ellipse2 boundary { center: [0mm,0mm]; axisLengths: [40mm,20mm] }",
        "Slot2 boundary { center: [0mm,0mm]; length: 40mm; width: 12mm }",
        "RoundedRect2 boundary { center: [0mm,0mm]; size: [40mm,20mm]; radius: 4mm }",
        "SmoothRoundedRect2 boundary { center: [0mm,0mm]; size: [40mm,20mm]; cornerExtent: 4mm }",
        "Triangle2<Equilateral> boundary { center: [0mm,0mm]; side: 20mm }",
        "Triangle2<Isosceles> boundary { center: [0mm,0mm]; base: 30mm; height: 20mm }",
        "Triangle2<Right> boundary { origin: [0mm,0mm]; legs: [30mm,20mm] }",
        "Triangle2<Explicit> boundary { a: [0mm,0mm]; b: [30mm,0mm]; c: [0mm,20mm] }",
        "Polygon2<Rhombus> boundary { center: [0mm,0mm]; diagonals: [40mm,20mm] }",
        "Polygon2<Parallelogram> boundary { center: [0mm,0mm]; base: 30mm; height: 20mm; shear: 8mm }",
        "Polygon2<Trapezoid> boundary { center: [0mm,0mm]; bottomWidth: 30mm; topWidth: 20mm; height: 15mm }",
        "RegularPolygon2<6> boundary { center: [0mm,0mm]; circumradius: 20mm }"
    };

    [Theory]
    [MemberData(nameof(Shapes))]
    public void ConceptShapePrefixHasTheSameGeometryAsThePlainShape(string shape)
    {
        var plain = Shape(shape);
        var prefixed = Shape("Concept " + shape);
        var baseline = FirmamentBuildAndExport.CompileSource(plain);
        var compatibility = FirmamentBuildAndExport.CompileSource(prefixed);
        Assert.True(baseline.IsSuccess, string.Join("\n", baseline.Diagnostics.Select(d => d.Message)));
        Assert.True(compatibility.IsSuccess, string.Join("\n", compatibility.Diagnostics.Select(d => d.Message)));
        Assert.Equal(baseline.Value.StepText, compatibility.Value.StepText);
    }

    [Theory]
    [InlineData("Concept Circle2 boundary { center: [0mm,0mm]; radius: 12mm; raduis: 8mm }", "canonical-field-unknown")]
    [InlineData("Concept Slot2 boundary on XZ { center: [0mm,0mm]; length: 40mm; width: 12mm }", "placement-unsupported")]
    [InlineData("Concept Rect2 boundary { center: [0mm,0mm]; size: [40mm,20mm]; heigth: 20mm }", "canonical-field-unknown")]
    public void ConceptShapeCannotBypassUnknownFieldsOrPlacement(string shape, string suffix)
    {
        var source = Shape(shape);
        Assert.False(FirmamentBuildAndExport.CompileSource(source).IsSuccess);
        Assert.Contains(FirmamentV2Parser.Parse(source).Diagnostics, d => d.Contains(suffix, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Concept Rect2 unused { center: Missing; size: [40mm,20mm]; heigth: 10mm }", "canonical-field-unknown")]
    [InlineData("Concept Circle2 unused { center: Missing; radius: 10mm; raduis: 8mm }", "canonical-field-unknown")]
    [InlineData("Concept Rect2 unused { center: [0mm,0mm]; size: Missing }", "rect2-invalid-size")]
    [InlineData("Concept Circle2 unused { center: Missing; radius: 10mm }", "profile-layout-unresolved-circle")]
    [InlineData("Concept Point2 unused { position: [0mm,0mm]; heigth: 10mm }", "canonical-field-unknown")]
    public void UnconsumedGuideCannotHideInvalidAuthoringIntent(string declaration, string code)
    {
        var source = $"Model Part {{ units: mm {declaration} Box Body {{ size: [4mm,3mm,2mm] }} }}";
        Assert.False(FirmamentBuildAndExport.CompileSource(source).IsSuccess);
        Assert.Contains(FirmamentV2Parser.Parse(source).Diagnostics, d => d.Contains(code, StringComparison.Ordinal));
    }

    [Fact]
    public void ExplicitPolygonUsesTheSameNamedPointSetWithEitherSpelling()
    {
        const string source = """
            Model GuidePlate {
              units: mm
              Static vertices: Set<Point2> { A => Point2(0mm,0mm) B => Point2(30mm,0mm) C => Point2(20mm,20mm) D => Point2(0mm,15mm) }
              Polygon2<Explicit> boundary { vertices: vertices }
              Profile outline { Loop Outer { boundary |> TraceLoop } }
              Extrude Body { profile: outline; from: 0mm; to: 5mm }
            }
            """;
        var plain = FirmamentBuildAndExport.CompileSource(source);
        var prefixed = FirmamentBuildAndExport.CompileSource(source.Replace("Polygon2<Explicit>", "Concept Polygon2<Explicit>"));
        Assert.True(plain.IsSuccess, string.Join("\n", plain.Diagnostics.Select(d => d.Message)));
        Assert.True(prefixed.IsSuccess, string.Join("\n", prefixed.Diagnostics.Select(d => d.Message)));
        Assert.Equal(plain.Value.StepText, prefixed.Value.StepText);
    }

    private static string Shape(string declaration) => $$"""
        Model GuidePlate {
          units: mm
          Concept Struct layout on XY { {{declaration}} }
          Profile outline using layout { Loop Outer { boundary |> TraceLoop } }
          Extrude Body { profile: outline; from: 0mm; to: 5mm }
        }
        """;

    private static void Reject(string source, string code)
    {
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.Diagnostics, d => d.StartsWith(code, StringComparison.Ordinal));
        Assert.False(FirmamentBuildAndExport.CompileSource(source).IsSuccess);
    }
}
