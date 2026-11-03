using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Tests.Step242;

/// <summary>Trim-curve checks against NIST parts: what was the validator's or recovery's fault, not the data's.</summary>
public sealed class Step242PcurveAuditTests
{
    private static BrepPcurveEvidence ValidateWithSourceAccuracy(BrepBody body)
    {
        var accuracy = body.ImportQualification?.SourceDistanceAccuracyMillimetres ?? 0d;
        var topology = accuracy > 0
            ? new BrepPcurveTopologyEvidence(body.Topology.Vertices.ToDictionary(v => v.Id, _ => accuracy), new HashSet<(CoedgeId, CoedgeId)>())
            : null;
        return BrepPcurveValidator.Validate(body, 1e-5, sourceTopology: topology);
    }

    /// <summary>FTC-08 and STC-10 have edges whose curve runs against their vertices (same_sense .F.).
    /// The validator matched vertices to trim ends ignoring that: 1,688 and 1,493 false errors.</summary>
    [Theory]
    [InlineData("testdata/step242/nist/FTC/nist_ftc_08_asme1_ap242-e2.stp")]
    [InlineData("testdata/step242/nist/STC/nist_stc_10_asme1_ap242-e2.stp")]
    public void Validator_HonoursEdgeSense(string path)
    {
        var evidence = ValidateWithSourceAccuracy(Step242Corpus.Body(path));
        Assert.True(evidence.IsValid, string.Join("\n", evidence.Diagnostics.Take(5)));
    }

    /// <summary>Sphere corner pcurves meet at the pole and cone pcurves at the apex with any U gap; that
    /// point is one model point, and the loop is closed there.</summary>
    [Theory]
    [InlineData("testdata/step242/nist/CTC/nist_ctc_04_asme1_ap242-e1.stp")]
    [InlineData("testdata/step242/nist/STC/nist_stc_07_asme1_ap242-e3.stp")]
    [InlineData("testdata/step242/nist/STC/nist_stc_08_asme1_ap242-e3.stp")]
    public void Validator_AcceptsLoopsClosingAtSingularPoints(string path)
    {
        var evidence = ValidateWithSourceAccuracy(Step242Corpus.Body(path));
        Assert.True(evidence.IsValid, string.Join("\n", evidence.Diagnostics.Take(5)));
    }

    /// <summary>FTC-11's vertices sit up to 3.75 µm off their curves; the file declares 5 µm accuracy.</summary>
    [Fact]
    public void Validator_AllowsVerticesWithinDeclaredSourceAccuracy()
    {
        var body = Step242Corpus.Body("testdata/step242/nist/FTC/nist_ftc_11_asme1_ap242-e2.stp");
        Assert.Equal(0.005, body.ImportQualification!.SourceDistanceAccuracyMillimetres!.Value, 9);
        Assert.True(ValidateWithSourceAccuracy(body).IsValid);
        Assert.False(BrepPcurveValidator.Validate(body, 1e-5).IsValid); // without the source's tolerance it is not
    }

    /// <summary>Circles through a sphere pole and B-spline curves into a cone apex lie exactly on their
    /// surfaces; recovery took atan2 noise for U there and failed them.</summary>
    [Theory]
    [InlineData("testdata/step242/nist/CTC/nist_ctc_02_asme1_ap242-e2.stp", "Sphere pcurve refinement")]
    [InlineData("testdata/step242/nist/FTC/nist_ftc_08_asme1_ap242-e2.stp", "Sphere pcurve refinement")]
    [InlineData("testdata/step242/nist/CTC/nist_ctc_04_asme1_ap242-e1.stp", "Cone pcurve refinement")]
    public void Recovery_UsesTheLimitAtPolesAndApexes(string path, string failure)
    {
        var report = Step242Corpus.Body(path).PcurveRecoveryReport;
        Assert.NotNull(report);
        Assert.DoesNotContain(report.Diagnostics, d => d.Message.StartsWith(failure, StringComparison.Ordinal));
    }

    /// <summary>The fitted spline's last knot must be the interval end exactly.</summary>
    [Fact]
    public void Recovery_DoesNotRejectItsOwnTrimByOneUlp()
    {
        foreach (var path in new[] { "testdata/step242/nist/STC/nist_stc_07_asme1_ap242-e3.stp", "testdata/step242/nist/FTC/nist_ftc_07_asme1_ap242-e2.stp" })
            Assert.DoesNotContain(Step242Corpus.Body(path).PcurveRecoveryReport!.Diagnostics, d => d.Message.Contains("requires a finite in-domain trim", StringComparison.Ordinal));
    }
}

/// <summary>Tolerant edges: an imported edge whose curve lies off its surface within the file's declared
/// accuracy keeps its pcurve at its measured gap; nothing is repaired, and exports declare the gap.</summary>
public sealed class Step242TolerantEdgeTests
{
    private static Aetheris.Kernel.Core.Brep.CoedgePcurveBinding[] Tolerant(Aetheris.Kernel.Core.Brep.BrepBody body) => body.Bindings.PcurveBindings
        .Where(b => b.Qualification?.RecoveryMethod.StartsWith(BrepPcurveRecovery.SourceTolerantMethod, StringComparison.Ordinal) == true).ToArray();

    [Theory]
    [InlineData("testdata/step242/nist/CTC/nist_ctc_01_asme1_ap242-e1.stp", 28)]
    [InlineData("testdata/step242/nist/FTC/nist_ftc_07_asme1_ap242-e2.stp", 52)]
    public void GapsWithinDeclaredAccuracy_Qualify_AtTheirMeasuredGap(string path, int expected)
    {
        var body = Step242Corpus.Body(path);
        var tolerant = Tolerant(body);
        Assert.Equal(expected, tolerant.Length);
        var accuracy = body.ImportQualification!.SourceDistanceAccuracyMillimetres!.Value;
        Assert.All(tolerant, b =>
        {
            Assert.True(b.Qualification!.MaximumLiftDeviationMillimetres <= accuracy);
            Assert.True(b.Qualification.QualificationToleranceMillimetres < accuracy, "recorded at the measured gap, not the cap");
        });
        Assert.Equal(Aetheris.Kernel.Core.Import.ImportQualificationStatus.Qualified, body.ImportQualification.GeometryStatus);
    }

    /// <summary>FTC-11: three edges sit within the declared 5 µm and are kept; three sit 14 µm off and still fail.</summary>
    [Fact]
    public void GapsBeyondDeclaredAccuracy_StillFail()
    {
        var body = Step242Corpus.Body("testdata/step242/nist/FTC/nist_ftc_11_asme1_ap242-e2.stp");
        Assert.Equal(3, Tolerant(body).Length);
        Assert.False(body.PcurveRecoveryReport!.IsSuccess);
        Assert.Equal(3, body.Topology.Coedges.Count(c => !body.Bindings.TryGetPcurveBinding(c.Id, out _)));
    }

    [Fact]
    public void Export_DeclaresAtLeastTheWorstTolerantGap()
    {
        var body = Step242Corpus.Body("testdata/step242/nist/FTC/nist_ftc_07_asme1_ap242-e2.stp");
        var worst = Tolerant(body).Max(b => b.Qualification!.QualificationToleranceMillimetres);
        var step = Step242Exporter.ExportBody(body).Value;
        var declared = double.Parse(System.Text.RegularExpressions.Regex.Match(step, @"UNCERTAINTY_MEASURE_WITH_UNIT\(LENGTH_MEASURE\(([0-9.Ee-]+)\)").Groups[1].Value,
            System.Globalization.CultureInfo.InvariantCulture);
        Assert.True(declared >= worst);
        // A body without tolerant edges keeps its exact form and declares nothing.
        Assert.DoesNotContain("UNCERTAINTY_MEASURE_WITH_UNIT", Step242Exporter.ExportBody(Step242Corpus.Body("testdata/step242/nist/FTC/nist_ftc_08_asme1_ap242-e2.stp")).Value);
    }
}
