using System.Globalization;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

/// <summary>
/// Source pcurves on the handcrafted cone (a cone of base radius 0.5 and height 1 on a disk). STEP measures a cone's
/// v from its placement circle, not its apex; and some writers keep a pcurve's trace but not its parameter speed.
/// </summary>
public sealed class Step242PcurveConventionTests
{
    private const string Cone = "testdata/step242/handcrafted/baseline/cone.step";
    private static readonly double SeamLength = double.Sqrt(0.25 + 1d);
    private static readonly double AxialPerSlant = 1d / SeamLength;

    /// <summary>The cone with pcurves added: the circle on the disk and the cone, the seam twice on the cone.</summary>
    private static string WithPcurves(string diskCircle, double seamSpeed, double seamU = 0d)
    {
        var text = Step242Corpus.Text(Cone);
        string N(double value) => value.ToString("0.0##############", CultureInfo.InvariantCulture);
        // The circle's 3D parameter is the cone's u (both frames share x = (1,0,0) and y = (0,0,1)); v = 0 on it.
        var added = $"""
            #900 = REPRESENTATION_CONTEXT('2D','parameter space');
            #901 = SURFACE_CURVE('',#24,(#902,#910),.PCURVE_S1.);
            #902 = PCURVE('',#29,#903);
            #903 = DEFINITIONAL_REPRESENTATION('',(#904),#900);
            {diskCircle}
            #910 = PCURVE('',#47,#911);
            #911 = DEFINITIONAL_REPRESENTATION('',(#912),#900);
            #912 = LINE('',#913,#914);
            #913 = CARTESIAN_POINT('',(0.,0.));
            #914 = VECTOR('',#915,1.);
            #915 = DIRECTION('',(1.,0.));
            #920 = SEAM_CURVE('',#42,(#921,#930),.PCURVE_S1.);
            #921 = PCURVE('',#47,#922);
            #922 = DEFINITIONAL_REPRESENTATION('',(#923),#900);
            #923 = LINE('',#924,#925);
            #924 = CARTESIAN_POINT('',({N(seamU)},0.));
            #925 = VECTOR('',#926,{N(seamSpeed)});
            #926 = DIRECTION('',(0.,-1.));
            #930 = PCURVE('',#47,#931);
            #931 = DEFINITIONAL_REPRESENTATION('',(#932),#900);
            #932 = LINE('',#933,#934);
            #933 = CARTESIAN_POINT('',({N(seamU + double.Tau)},0.));
            #934 = VECTOR('',#935,{N(seamSpeed)});
            #935 = DIRECTION('',(0.,-1.));

            """;
        text = text.Replace("#21 = EDGE_CURVE('',#22,#22,#24,.T.);", "#21 = EDGE_CURVE('',#22,#22,#901,.T.);", StringComparison.Ordinal)
                   .Replace("#39 = EDGE_CURVE('',#22,#40,#42,.T.);", "#39 = EDGE_CURVE('',#22,#40,#920,.T.);", StringComparison.Ordinal);
        return text.Replace("ENDSEC;\nEND-ISO", added + "ENDSEC;\nEND-ISO", StringComparison.Ordinal)
                   .Replace("ENDSEC;\r\nEND-ISO", added + "ENDSEC;\r\nEND-ISO", StringComparison.Ordinal);
    }

    private const string ExactDiskCircle = """
        #904 = CIRCLE('',#905,0.5);
        #905 = AXIS2_PLACEMENT_2D('',#906,#907);
        #906 = CARTESIAN_POINT('',(0.,0.));
        #907 = DIRECTION('',(1.,0.));
        """;

    /// <summary>The circle as three rational 120° arcs with knots in radians: its trace, not its speed.</summary>
    private const string RationalDiskCircle = """
        #904 = ( BOUNDED_CURVE() B_SPLINE_CURVE(2,(#940,#941,#942,#943,#944,#945,#946),.UNSPECIFIED.,.T.,.F.) B_SPLINE_CURVE_WITH_KNOTS((1,2,2,2,2,1),(-2.094395102393,0.,2.094395102393,4.188790204786,6.28318530718,8.377580409573),.UNSPECIFIED.) CURVE() GEOMETRIC_REPRESENTATION_ITEM() RATIONAL_B_SPLINE_CURVE((1.,0.5,1.,0.5,1.,0.5,1.)) REPRESENTATION_ITEM('') );
        #940 = CARTESIAN_POINT('',(0.5,0.));
        #941 = CARTESIAN_POINT('',(0.5,0.866025403784));
        #942 = CARTESIAN_POINT('',(-0.25,0.433012701892));
        #943 = CARTESIAN_POINT('',(-1.,0.));
        #944 = CARTESIAN_POINT('',(-0.25,-0.433012701892));
        #945 = CARTESIAN_POINT('',(0.5,-0.866025403784));
        #946 = CARTESIAN_POINT('',(0.5,0.));
        """;

    private static BrepBody Import(string text)
    {
        var imported = Step242Importer.ImportBody(text);
        Assert.True(imported.IsSuccess, string.Join("\n", imported.Diagnostics.Select(d => $"{d.Source}: {d.Message}")));
        return imported.Value;
    }

    [Fact]
    public void ConePcurves_MeasureVFromThePlacementCircle()
    {
        var body = Import(WithPcurves(ExactDiskCircle, AxialPerSlant));
        Assert.Equal(4, body.Bindings.PcurveBindings.Count()); // the circle on each face, the seam twice
        Assert.True(BrepPcurveValidator.Validate(body, 1e-7).IsValid);
        AssertConeVolume(body);
    }

    [Fact]
    public void ConePcurves_ExportWithVFromThePlacementCircle()
    {
        var exported = Step242Exporter.ExportBody(Import(WithPcurves(ExactDiskCircle, AxialPerSlant)));
        Assert.True(exported.IsSuccess, string.Join("\n", exported.Diagnostics.Select(d => d.Message)));
        // The circle's pcurve on the cone is the line v = 0 in STEP's terms, not v = 1 (the apex distance).
        var points = Regex.Matches(exported.Value, @"CARTESIAN_POINT\((?:'[^']*'|\$),\(([^,()]+),([^,()]+)\)\)")
            .Select(m => double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture)).ToArray();
        Assert.NotEmpty(points);
        Assert.DoesNotContain(points, v => double.Abs(v - 1d) < 1e-9);
        var reimported = Import(exported.Value);
        Assert.True(BrepPcurveValidator.Validate(reimported, 1e-7).IsValid);
    }

    [Fact]
    public void PcurvesTracingTheirEdgeAtAnotherSpeed_AreRebuiltAtTheEdgesSpeed()
    {
        // The disk circle as rational arcs, and the seam advancing v by its slant length rather than its axial length.
        var imported = Step242Importer.ImportBody(WithPcurves(RationalDiskCircle, 1d));
        Assert.True(imported.IsSuccess, string.Join("\n", imported.Diagnostics.Select(d => $"{d.Source}: {d.Message}")));
        Assert.Contains(imported.Diagnostics, d => d.Source == "Importer.Pcurve.Reparameterized" && d.Message.StartsWith("step-import-pcurve-reparameterized: 3 ", StringComparison.Ordinal));
        Assert.True(BrepPcurveValidator.Validate(imported.Value, 1e-7).IsValid);
        AssertConeVolume(imported.Value);
    }

    [Fact]
    public void APcurveOffItsEdge_IsSetAsideAndRebuiltFromTheEdge_AndSaysSo()
    {
        // The seam's pcurves run along u = 0.1 and 0.1 + 2pi: a generator, but not the seam's. The seam and the cone
        // agree, so the pcurves are rebuilt from the seam; the import discloses it. The circle's pcurve on the cone
        // is right but no longer closes the loop against the seam's, so it is rebuilt too: three.
        var imported = Step242Importer.ImportBody(WithPcurves(ExactDiskCircle, AxialPerSlant, seamU: 0.1));
        Assert.True(imported.IsSuccess, string.Join("\n", imported.Diagnostics.Select(d => $"{d.Source}: {d.Message}")));
        var qualification = imported.Value.ImportQualification!;
        Assert.True(qualification.Repairs.Any(r => r.Code == "step-import-source-pcurves-rebuilt" && r.Message.StartsWith("3 ", StringComparison.Ordinal)),
            string.Join("\n", qualification.Repairs.Select(r => r.Code + ": " + r.Message)));
        Assert.Empty(qualification.Tolerances);
        AssertConeVolume(imported.Value);
    }

    /// <summary>The disk's plane moved along its normal: its edge circle is then off it by that much.</summary>
    private static string WithDiskOffset(double millimetres) => WithPcurves(ExactDiskCircle, AxialPerSlant)
        .Replace("#31 = CARTESIAN_POINT('',(0.,0.,0.));",
            $"#31 = CARTESIAN_POINT('',(0.,{(-millimetres).ToString("0.0########", CultureInfo.InvariantCulture)},0.));", StringComparison.Ordinal);

    [Fact]
    public void AGapUnderTheTolerantBudget_IsMeasuredAndDisclosed_NotClosed()
    {
        // 5 µm, beyond the declared 1e-7 mm accuracy but under the 10 µm tolerant budget.
        var imported = Step242Importer.ImportBody(WithDiskOffset(0.005));
        Assert.True(imported.IsSuccess, string.Join("\n", imported.Diagnostics.Select(d => $"{d.Source}: {d.Message}")));
        var qualification = imported.Value.ImportQualification!;
        var tolerance = Assert.Single(qualification.Tolerances);
        Assert.Equal("tolerant-edges", tolerance.Code);
        Assert.Contains("0.005 mm", tolerance.Message);
        Assert.Contains(qualification.Repairs, r => r.Code == "step-import-source-pcurves-rebuilt");
        // Nothing moved: the disk's plane is still 5 µm off its edge.
        var gap = imported.Value.Bindings.PcurveBindings.Single(b => b.Qualification is { MaximumLiftDeviationMillimetres: > 1e-6 })
            .Qualification!.MaximumLiftDeviationMillimetres;
        Assert.Equal(0.005, gap, 9);
    }

    [Fact]
    public void AGapOverTheTolerantBudget_IsRefused()
    {
        var imported = Step242Importer.ImportBody(WithDiskOffset(0.05));
        Assert.False(imported.IsSuccess);
        Assert.Contains(imported.Diagnostics, d => d.Source == "Importer.Pcurve.Consistency");
    }

    private static void AssertConeVolume(BrepBody body)
    {
        var mass = BrepMassProperties.Evaluate(body);
        var expected = double.Pi * 0.25 / 3d;
        Assert.True(double.Abs(mass.AbsoluteVolume - expected) <= double.Max(1e-6, mass.ErrorBound ?? 0d), $"volume {mass.AbsoluteVolume} against {expected}");
    }
}
