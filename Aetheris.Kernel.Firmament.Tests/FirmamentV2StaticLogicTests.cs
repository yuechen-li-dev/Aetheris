using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentV2StaticLogicTests
{
    [Fact]
    public void EnumAndMatch_ResolveTypedValuesAndEraseExecutableLogic()
    {
        var parse = FirmamentV2Parser.Parse(Source());

        Assert.True(parse.IsSuccess, string.Join(Environment.NewLine, parse.Diagnostics));
        var ir = Assert.IsType<ConceptIrDocument>(parse.Document!.ConceptIr);
        var definition = Assert.Single(ir.Enums!);
        Assert.Equal("BracketVariant", definition.Name);
        Assert.Equal(["Compact", "Standard", "HeavyDuty"], definition.Variants);
        var concept = Assert.Single(ir.Structs);
        var variant = Assert.IsType<ConceptIrEnumValue>(concept.Members["Variant"]);
        Assert.Equal(("BracketVariant", "Standard", 1), (variant.EnumType, variant.Variant, variant.Ordinal));
        Assert.Equal([80d, 50d, 25d], Assert.IsType<ConceptIrBox3Value>(concept.Members["Bounds"]).Size);
        Assert.Equal(1.5d, Assert.IsType<ConceptIrLengthValue>(concept.Members["ChamferDistance"]).Value);
        Assert.Equal(5d, Assert.IsType<ConceptIrLengthValue>(concept.Members["WallThickness"]).Value);
        Assert.Equal(2, Assert.IsType<ConceptIrPointSetValue>(concept.Members["MountPoints"]).Points.Count);
        Assert.Equal(4, ir.StaticSelections!.Count);
        Assert.All(ir.StaticSelections, selection => Assert.Equal(selection.ScrutineeValue, selection.SelectedArm));
        Assert.Equal("ErasedBeforeFeatureAir", ir.ErasureStatus);
        Assert.DoesNotContain(ir.ResolvedValues, value => value.GetType().Name.Contains("Match", StringComparison.Ordinal));
        Assert.Equal(1.5d, Assert.Single(parse.Document.ModifyBlocks!).EdgeFinishes!.Single().Distance);
        Assert.Equal("BracketConcept.ChamferDistance", Assert.Single(parse.Document.ModifyBlocks!).EdgeFinishes!.Single().Provenance!["Distance"]);
    }

    [Fact]
    public void Match_NonExhaustive_ReportsEveryMissingVariant()
    {
        var parse = FirmamentV2Parser.Parse(Source().Replace("        HeavyDuty => 2mm\n", string.Empty, StringComparison.Ordinal));
        Assert.False(parse.IsSuccess);
        Assert.Contains(parse.Diagnostics, d => d == ConceptIrResolver.NonExhaustiveMatch + ":Missing arm: HeavyDuty");
    }

    [Fact]
    public void Match_DuplicateArm_IsRejectedAsUnreachable()
    {
        var parse = FirmamentV2Parser.Parse(Source().Replace("        HeavyDuty => 2mm", "        Standard => 2mm", StringComparison.Ordinal));
        Assert.False(parse.IsSuccess);
        Assert.Contains(parse.Diagnostics, d => d == ConceptIrResolver.DuplicateMatchArm + ":Standard:unreachable");
    }

    [Fact]
    public void Match_IncompatibleDimensionKind_ReportsArmAndTypes()
    {
        var parse = FirmamentV2Parser.Parse(Source().Replace("        Standard => 1.5mm", "        Standard => 90deg", StringComparison.Ordinal));
        Assert.False(parse.IsSuccess);
        Assert.Contains(parse.Diagnostics, d => d.Contains(ConceptIrResolver.MatchArmTypeMismatch + ":BracketConcept.ChamferDistance:Standard:expected-Length:actual-Angle", StringComparison.Ordinal));
    }

    [Fact]
    public void EnumAndBoolean_InvalidDomainsAreDiagnosed()
    {
        var unknownType = FirmamentV2Parser.Parse(Source().Replace("Variant: BracketVariant = Standard", "Variant: MissingVariant = Standard", StringComparison.Ordinal));
        Assert.Contains(unknownType.Diagnostics, d => d == ConceptIrResolver.UnknownEnumType + ":MissingVariant");

        var unknownVariant = FirmamentV2Parser.Parse(Source().Replace("Variant: BracketVariant = Standard", "Variant: BracketVariant = Unknown", StringComparison.Ordinal));
        Assert.Contains(unknownVariant.Diagnostics, d => d == ConceptIrResolver.UnknownEnumVariant + ":BracketVariant.Unknown");

        var invalidBool = FirmamentV2Parser.Parse(Source().Replace("        false => 3mm", "        Standard => 3mm", StringComparison.Ordinal));
        Assert.Contains(invalidBool.Diagnostics, d => d == ConceptIrResolver.InvalidBooleanArm + ":Standard");
    }

    [Fact]
    public void EnumDuplicate_InvalidScrutinee_AndSelectedFailureAreDiagnosed()
    {
        var duplicate = FirmamentV2Parser.Parse(Source().Replace("    HeavyDuty\n", "    HeavyDuty\n    HeavyDuty\n", StringComparison.Ordinal));
        Assert.Contains(duplicate.Diagnostics, d => d == ConceptIrResolver.DuplicateEnumVariant + ":BracketVariant.HeavyDuty");

        var invalidScrutinee = FirmamentV2Parser.Parse(Source().Replace("WallThickness: Match UseHeavyWall", "WallThickness: Match ChamferDistance", StringComparison.Ordinal));
        Assert.Contains(invalidScrutinee.Diagnostics, d => d.StartsWith(ConceptIrResolver.InvalidMatchScrutinee + ":BracketConcept.ChamferDistance:Length", StringComparison.Ordinal));

        var selectedFailure = FirmamentV2Parser.Parse(Source().Replace("        Standard => 1.5mm", "        Standard => MissingValue", StringComparison.Ordinal));
        Assert.Contains(selectedFailure.Diagnostics, d => d == ConceptIrResolver.SelectedBranchEvaluationFailure + ":BracketConcept.ChamferDistance:Standard");
    }

    [Fact]
    public void MatchDependencyCycle_ReportsConcreteChain()
    {
        var cyclic = Source().Replace("Variant: BracketVariant = Standard", "Variant: BracketVariant = SelectedVariant\n    SelectedVariant: Match Variant { Compact => Compact Standard => Standard HeavyDuty => HeavyDuty }", StringComparison.Ordinal);
        var parse = FirmamentV2Parser.Parse(cyclic);
        Assert.False(parse.IsSuccess);
        Assert.Contains(parse.Diagnostics, d => d.StartsWith(ConceptIrResolver.CircularDependency + ":Variant -> SelectedVariant -> Variant", StringComparison.Ordinal));
    }

    internal static string Source(string variant = "Standard") => $$"""
        Enum BracketVariant {
            Compact
            Standard
            HeavyDuty
        }
        Concept Struct BracketConcept {
            Variant: BracketVariant = {{variant}}
            UseHeavyWall: bool = true
            Bounds: Match Variant {
                Compact => Box3 { Size: [60mm, 40mm, 20mm] }
                Standard => Box3 { Size: [80mm, 50mm, 25mm] }
                HeavyDuty => Box3 { Size: [100mm, 60mm, 30mm] }
            }
            MountPoints: Match Variant {
                Compact => Grid {
                    Within: Bounds.Face(+Z).Inset(8mm)
                    Columns: 2
                    Rows: 1
                }
                Standard => Grid {
                    Within: Bounds.Face(+Z).Inset(10mm)
                    Columns: 2
                    Rows: 1
                }
                HeavyDuty => Grid {
                    Within: Bounds.Face(+Z).Inset(12mm)
                    Columns: 2
                    Rows: 2
                }
            }
            ChamferDistance: Match Variant {
                Compact => 1mm
                Standard => 1.5mm
                HeavyDuty => 2mm
            }
            WallThickness: Match UseHeavyWall {
                true => 5mm
                false => 3mm
            }
        }
        Struct Bracket {
            Box Base { Bounds: BracketConcept.Bounds }
            Modify Base {
                EdgeFinish TopBreak {
                    Face: +Z
                    Target: Boundary
                    Kind: Chamfer
                    Distance: BracketConcept.ChamferDistance
                }
            }
        }
        """;
}
